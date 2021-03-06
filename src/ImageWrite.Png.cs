using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace StbSharp.ImageWrite
{
    [SkipLocalsInit]
    public static class Png
    {
        public enum FilterType : byte
        {
            None = 0,
            Sub = 1,
            Up = 2,
            Average = 3,
            Paeth = 4,
            AverageFirst = 5,
            PaethFirst = 6
        }

        public static ReadOnlySpan<byte> FilterMapping => new byte[]
        {
            0, 1, 2, 3, 4
        };

        public static ReadOnlySpan<byte> FirstFilterMapping => new byte[]
        {
            0, 1, 0, 5, 6
        };

        public static ReadOnlySpan<byte> ColorTypeMap => new byte[]
        {
            255, 0, 4, 2, 6
        };

        public static ReadOnlySpan<byte> Signature => new byte[]
        {
            137, 80, 78, 71, 13, 10, 26, 10
        };

        public class ChunkStream : Stream
        {
            private ArrayPool<byte> _pool;
            private byte[] _buffer;
            private int _bufferPos;

            private uint _crc;
            private PngChunkType _chunkType;
            private bool _inChunk;

            public Stream BaseStream { get; private set; }

            public override bool CanRead => throw new NotSupportedException();
            public override bool CanSeek => throw new NotSupportedException();
            public override bool CanWrite => BaseStream.CanWrite;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public ChunkStream(Stream stream, ArrayPool<byte> pool)
            {
                BaseStream = stream ?? throw new ArgumentNullException(nameof(stream));
                _pool = pool ?? throw new ArgumentNullException(nameof(pool));

                _buffer = pool.Rent(1024 * 64);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (buffer.IsEmpty)
                    return;

                if (!_inChunk)
                {
                    BaseStream.Write(buffer);
                    return;
                }

                if (_chunkType != PngChunkType.IDAT)
                {
                    BaseStream.Write(buffer);
                    _crc = Crc32.Calculate(buffer, _crc);
                }
                else
                {
                    do
                    {
                        int available = _buffer.Length - _bufferPos;
                        if (available == 0)
                        {
                            // The buffer is full so flush.
                            WriteBufferedChunk();
                            continue;
                        }

                        // Copy to buffer to fill it up.
                        int toCopy = Math.Min(buffer.Length, available);
                        buffer.Slice(0, toCopy).CopyTo(_buffer.AsSpan(_bufferPos));

                        _bufferPos += toCopy;
                        buffer = buffer[toCopy..];

                    }
                    while (!buffer.IsEmpty);
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Write(buffer.AsSpan(offset, count));
            }

            public override void WriteByte(byte value)
            {
                Write(stackalloc byte[] { value });
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public void Begin(PngChunkType type, int? length)
            {
                if (_inChunk)
                    throw new InvalidOperationException("Already in chunk.");

                _chunkType = type;
                if (_chunkType != PngChunkType.IDAT)
                {
                    if (!length.HasValue)
                    {
                        throw new ArgumentException(
                            "Length may only be null if the chunk type is IDAT.", nameof(length));
                    }
                    WriteHeader(length.GetValueOrDefault());
                    _crc = GetInitialCrc(_chunkType);
                }

                _inChunk = true;
            }

            public void End()
            {
                if (!_inChunk)
                    throw new InvalidOperationException("Not in chunk.");

                if (_chunkType != PngChunkType.IDAT)
                    WriteFooter(_crc);
                else if (_bufferPos > 0)
                    WriteBufferedChunk();

                _inChunk = false;
            }

            public void WriteChunk(PngChunkType type, ReadOnlySpan<byte> data)
            {
                Begin(type, data.Length);
                Write(data);
                End();
            }

            private static uint GetInitialCrc(PngChunkType type)
            {
                Span<byte> tmp = stackalloc byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(tmp, (uint)type);
                return Crc32.Calculate(tmp, ~0u);
            }

            private void WriteHeader(int length)
            {
                Span<byte> header = stackalloc byte[8];
                BinaryPrimitives.WriteUInt32BigEndian(header[..], (uint)length);
                BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)_chunkType);
                BaseStream.Write(header);
            }

            private void WriteFooter(uint crc)
            {
                Span<byte> crcBytes = stackalloc byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ~crc);
                BaseStream.Write(crcBytes);
            }

            private void WriteBufferedChunk()
            {
                Span<byte> slice = _buffer.AsSpan(0, _bufferPos);
                WriteHeader(slice.Length);
                BaseStream.Write(slice);
                WriteFooter(Crc32.Calculate(slice, GetInitialCrc(_chunkType)));
                _bufferPos = 0;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_inChunk)
                        End();

                    if (_buffer != null)
                    {
                        _pool.Return(_buffer);
                        _buffer = null!;
                    }

                    BaseStream = null!;
                }
                base.Dispose(disposing);
            }
        }

        // TODO: add more color formats and a palette
        // http://www.libpng.org/pub/png/spec/1.2/PNG-Chunks.html

        public static void Write<TImage>(
            ImageBinWriter state,
            TImage image,
            CompressionLevel compressionLevel,
            int? forcedFilter = null,
            ZlibHelper.DeflateCompressorFactory? deflateCompressorFactory = null,
            ArrayPool<byte>? pool = null)
            where TImage : IPixelRowProvider
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ZlibHeader.ConvertLevel(compressionLevel); // acts as a parameter check
            image.ThrowIfCancelled();

            pool ??= ArrayPool<byte>.Shared;

            int w = image.Width;
            int h = image.Height;
            int n = image.Components;
            int pixelCount = w * h;
            int stride = w * n;
            double dHeight = h;

            using (ChunkStream encoder = new(state.Stream, pool))
            {
                encoder.Write(Signature);

                #region IHDR
                {
                    Span<byte> hdr = stackalloc byte[13];
                    Span<byte> dst = hdr;

                    BinaryPrimitives.WriteUInt32BigEndian(dst, (uint)w); // width
                    dst = dst[sizeof(uint)..];

                    BinaryPrimitives.WriteUInt32BigEndian(dst, (uint)h); // height
                    dst = dst[sizeof(uint)..];

                    dst[0] = 8; // bit depth
                    dst[1] = ColorTypeMap[n]; // color type
                    dst[2] = 0; // compression method
                    dst[3] = 0; // filter method
                    dst[4] = 0; // interlace method

                    encoder.WriteChunk(PngChunkType.IHDR, hdr);
                }
                #endregion

                #region IDAT

                byte[] previousRowArray = pool.Rent(stride);
                byte[] currentRowArray = pool.Rent(stride);
                byte[] resultArray = pool.Rent(1 + stride);
                try
                {
                    Span<byte> previousRow = previousRowArray.AsSpan(0, stride);
                    Span<byte> currentRow = currentRowArray.AsSpan(0, stride);
                    Span<byte> fullResultRow = resultArray.AsSpan(0, 1 + stride);
                    Span<byte> resultRow = fullResultRow[1..];

                    encoder.Begin(PngChunkType.IDAT, null);
                    using (Stream compressor = ZlibHelper.CreateCompressor(
                        encoder, leaveOpen: true, compressionLevel, deflateCompressorFactory))
                    {
                        for (int y = 0; y < h; y++)
                        {
                            image.GetByteRow(y, currentRow);

                            ReadOnlySpan<byte> filterMap = (y != 0) ? FilterMapping : FirstFilterMapping;
                            int filterType;

                            if (forcedFilter.HasValue)
                            {
                                filterType = forcedFilter.GetValueOrDefault();
                                EncodeLine(previousRow, currentRow, n, (FilterType)filterMap[filterType], resultRow);
                            }
                            else
                            {
                                int bestFilter = 0;
                                uint bestFilterValue = uint.MaxValue;
                                for (filterType = 0; filterType < 5; filterType++)
                                {
                                    EncodeLine(previousRow, currentRow, n, (FilterType)filterMap[filterType], resultRow);

                                    uint estimate = GetRowEstimate(resultRow);
                                    if (estimate < bestFilterValue)
                                    {
                                        bestFilterValue = estimate;
                                        bestFilter = filterType;
                                    }

                                    image.ThrowIfCancelled();
                                }

                                if (filterType != bestFilter)
                                {
                                    EncodeLine(previousRow, currentRow, n, (FilterType)filterMap[bestFilter], resultRow);
                                    filterType = bestFilter;
                                }
                            }

                            image.ThrowIfCancelled();

                            fullResultRow[0] = (byte)filterType;
                            compressor.Write(fullResultRow);

                            // Swap buffers. 
                            Span<byte> nextRow = previousRow;
                            previousRow = currentRow;
                            currentRow = nextRow;

                            // TODO: tidy progress up a notch so it's easier to reuse in other implementations
                            if (state.HasProgressListener)
                                state.ReportProgress(y / dHeight, null);
                        }
                    }
                    encoder.End();
                }
                finally
                {
                    pool.Return(previousRowArray);
                    pool.Return(currentRowArray);
                    pool.Return(resultArray);
                }

                #endregion

                encoder.WriteChunk(PngChunkType.IEND, data: default);
            }
        }

        public static uint GetRowEstimate(ReadOnlySpan<byte> row)
        {
            uint estimate = 0;

            if (Vector.IsHardwareAccelerated)
            {
                Vector<uint> estimateSum = Vector<uint>.Zero;

                while (row.Length >= Vector<byte>.Count)
                {
                    Vector<byte> source = new(row);

                    Vector.Widen(source, out Vector<ushort> shortLow, out Vector<ushort> shortHigh);

                    Vector.Widen(shortLow, out Vector<uint> intLow, out Vector<uint> intHigh);
                    estimateSum += intLow;
                    estimateSum += intHigh;

                    Vector.Widen(shortHigh, out intLow, out intHigh);
                    estimateSum += intLow;
                    estimateSum += intHigh;

                    row = row[Vector<byte>.Count..];
                }

                for (int i = 0; i < Vector<uint>.Count; i++)
                    estimate += estimateSum[i];
            }

            for (int i = 0; i < row.Length; i++)
                estimate += row[i];

            return estimate;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void EncodeLine(
            ReadOnlySpan<byte> previous,
            ReadOnlySpan<byte> current,
            int n,
            FilterType filterType,
            Span<byte> output)
        {
            if (filterType == 0)
            {
                current.CopyTo(output);
                return;
            }

            int i = n;
            switch (filterType)
            {
                case FilterType.Sub:
                case FilterType.AverageFirst:
                case FilterType.PaethFirst:
                    current.Slice(0, i).CopyTo(output);
                    break;

                case FilterType.Up:
                    for (int j = 0; j < i; j++)
                        output[j] = (byte)(current[j] - previous[j]);
                    break;

                case FilterType.Average:
                    for (int j = 0; j < i; j++)
                        output[j] = (byte)(current[j] - (previous[j] / 2));
                    break;

                case FilterType.Paeth:
                    for (int j = 0; j < i; j++)
                        output[j] = (byte)(current[j] - MathHelper.Paeth(0, previous[j], 0));
                    break;
            }

            switch (filterType)
            {
                case FilterType.Sub:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            Vector<byte> v_ncurrent = new(current[(i - n)..]);
                            Vector<byte> v_current = new(current[i..]);

                            Vector<byte> result = v_current - v_ncurrent;
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - current[i - n]);
                    break;

                case FilterType.Up:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            Vector<byte> v_current = new(current[i..]);
                            Vector<byte> v_previous = new(previous[i..]);

                            Vector<byte> result = v_current - v_previous;
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - previous[i]);
                    break;

                case FilterType.Average:
                    // We shouldn't use Vector<T> here as it doesn't intrinsify integer division
                    if (Sse2.IsSupported)
                    {
                        fixed (byte* currentPtr = current)
                        fixed (byte* previousPtr = previous)
                        fixed (byte* outputPtr = output)
                        {
                            for (; i + Vector128<byte>.Count <= output.Length; i += Vector128<byte>.Count)
                            {
                                Vector128<byte> v_ncurrent = Sse2.LoadVector128(currentPtr + i - n);
                                Vector128<byte> v_current = Sse2.LoadVector128(currentPtr + i);
                                Vector128<byte> v_previous = Sse2.LoadVector128(previousPtr + i);

                                VectorHelper.Widen(v_ncurrent, out Vector128<ushort> v_ncurrent1, out Vector128<ushort> v_ncurrent2);
                                VectorHelper.Widen(v_previous, out Vector128<ushort> v_previous1, out Vector128<ushort> v_previous2);

                                Vector128<ushort> div1 = Sse2.ShiftRightLogical(Sse2.Add(v_ncurrent1, v_previous1), 1);
                                Vector128<ushort> div2 = Sse2.ShiftRightLogical(Sse2.Add(v_ncurrent2, v_previous2), 1);

                                Vector128<byte> result = Sse2.Subtract(v_current, VectorHelper.Narrow(div1, div2));
                                Sse2.Store(outputPtr + i, result);
                            }
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - ((current[i - n] + previous[i]) / 2));
                    break;

                case FilterType.Paeth:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            Vector<byte> v_ncurrent = new(current[(i - n)..]);
                            Vector<byte> v_current = new(current[i..]);
                            Vector<byte> v_nprevious = new(previous[(i - n)..]);
                            Vector<byte> v_previous = new(previous[i..]);

                            Vector<byte> result = v_current - MathHelper.Paeth(v_ncurrent, v_previous, v_nprevious);
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                    {
                        output[i] = (byte)(
                            current[i] - MathHelper.Paeth(current[i - n], previous[i], previous[i - n]));
                    }
                    break;

                case FilterType.AverageFirst:
                    // We shouldn't use Vector<T> here as it doesn't intrinsify integer division
                    if (Sse2.IsSupported)
                    {
                        fixed (byte* currentPtr = current)
                        fixed (byte* outputPtr = output)
                        {
                            for (; i + Vector128<byte>.Count <= output.Length; i += Vector128<byte>.Count)
                            {
                                Vector128<byte> v_ncurrent = Sse2.LoadVector128(currentPtr + i - n);
                                Vector128<byte> v_current = Sse2.LoadVector128(currentPtr + i);

                                VectorHelper.Widen(v_ncurrent, out Vector128<ushort> v_ncurrent1, out Vector128<ushort> v_ncurrent2);
                                VectorHelper.Widen(v_current, out Vector128<ushort> v_current1, out Vector128<ushort> v_current2);

                                Vector128<ushort> div1 = Sse2.ShiftRightLogical(v_ncurrent1, 1);
                                Vector128<ushort> div2 = Sse2.ShiftRightLogical(v_ncurrent2, 1);

                                Vector128<byte> result = Sse2.Subtract(v_current, VectorHelper.Narrow(div1, div2));
                                Sse2.Store(outputPtr + i, result);
                            }
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - (current[i - n] / 2));
                    break;

                case FilterType.PaethFirst:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            Vector<byte> v_ncurrent = new(current[(i - n)..]);
                            Vector<byte> v_current = new(current[i..]);

                            Vector<byte> result = v_current - MathHelper.Paeth(v_ncurrent, Vector<byte>.Zero, Vector<byte>.Zero);
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - MathHelper.Paeth(current[i - n], 0, 0));
                    break;
            }
        }
    }
}