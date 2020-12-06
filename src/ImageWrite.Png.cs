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

                    } while (!buffer.IsEmpty);
                }
            }

            public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

            public override void WriteByte(byte value) => Write(stackalloc byte[] { value });

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
                        throw new ArgumentException(
                            "Length may not be null if the chunk type is not IDAT.", nameof(length));

                    WriteHeader(length.Value);
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
                var slice = _buffer.AsSpan(0, _bufferPos);
                WriteHeader(slice.Length);
                BaseStream.Write(slice);
                WriteFooter(Crc32.Calculate(slice, GetInitialCrc(_chunkType)));
                _bufferPos = 0;
            }

            protected override void Dispose(bool disposing)
            {
                if (_inChunk)
                    End();

                if (_buffer != null)
                {
                    _pool.Return(_buffer);
                    _buffer = null!;
                }

                BaseStream = null!;

                base.Dispose(disposing);
            }
        }

        // TODO: add more color formats and a palette
        // http://www.libpng.org/pub/png/spec/1.2/PNG-Chunks.html

        public static void Write(
            WriteState s, CompressionLevel compressionLevel, int? forcedFilter, ArrayPool<byte>? pool)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            ZlibHeader.ConvertLevel(compressionLevel); // acts as a parameter check

            pool ??= ArrayPool<byte>.Shared;

            var cancellation = s.CancellationToken;
            int w = s.Width;
            int h = s.Height;
            int n = s.Components;
            int pixelCount = w * h;
            int stride = w * n;

            double progressStep = 0;
            double progressStepCount = pixelCount / (1000 * Math.Log(pixelCount, 2));
            double progressStepSize = Math.Max(1, pixelCount / progressStepCount);

            cancellation.ThrowIfCancellationRequested();

            // TODO: use ArrayPool
            using (var encoder = new ChunkStream(s.Stream, pool))
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

                WriteProgressCallback? weightedProgress = null;
                if (s.ProgressCallback != null)
                    weightedProgress = (p, r) => s.ProgressCallback.Invoke(p * 0.49f + 0.5f, r);

                var previousRowArray = pool.Rent(stride);
                var currentRowArray = pool.Rent(stride);
                var resultArray = pool.Rent(1 + stride);
                try
                {
                    var previousRow = previousRowArray.AsSpan(0, stride);
                    var currentRow = currentRowArray.AsSpan(0, stride);
                    var fullResultRow = resultArray.AsSpan(0, 1 + stride);
                    var resultRow = fullResultRow[1..];

                    encoder.Begin(PngChunkType.IDAT, null);
                    using (var compressor = ZlibHelper.CreateCompressor(encoder, compressionLevel, leaveOpen: true))
                    {
                        for (int y = 0; y < h; y++)
                        {
                            cancellation.ThrowIfCancellationRequested();

                            s.GetByteRow(y, currentRow);

                            var filterMap = (y != 0) ? FilterMapping : FirstFilterMapping;
                            int filterType;

                            if (forcedFilter.HasValue)
                            {
                                filterType = forcedFilter.Value;
                                EncodeLine(previousRow, currentRow, n, filterMap[filterType], resultRow);
                            }
                            else
                            {
                                int bestFilter = 0;
                                uint bestFilterValue = uint.MaxValue;
                                for (filterType = 0; filterType < 5; filterType++)
                                {
                                    EncodeLine(previousRow, currentRow, n, filterMap[filterType], resultRow);

                                    uint estimate = GetRowEstimate(resultRow);
                                    if (estimate < bestFilterValue)
                                    {
                                        bestFilterValue = estimate;
                                        bestFilter = filterType;
                                    }

                                    cancellation.ThrowIfCancellationRequested();
                                }

                                if (filterType != bestFilter)
                                {
                                    EncodeLine(previousRow, currentRow, n, filterMap[bestFilter], resultRow);
                                    filterType = bestFilter;
                                }
                            }

                            cancellation.ThrowIfCancellationRequested();

                            fullResultRow[0] = (byte)filterType;
                            compressor.Write(fullResultRow);

                            // Swap buffers. 
                            var nextRow = previousRow;
                            previousRow = currentRow;
                            currentRow = nextRow;


                            // TODO: tidy this up a notch so it's easier to reuse in other implementations
                            var progress = s.ProgressCallback;
                            if (progress != null)
                            {
                                progressStep += w;
                                while (progressStep >= progressStepSize)
                                {
                                    progress.Invoke(y / (float)h * 0.5f, null);
                                    progressStep -= progressStepSize;
                                }
                            }
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

        [CLSCompliant(false)]
        public static uint GetRowEstimate(ReadOnlySpan<byte> row)
        {
            uint estimate = 0;

            if (Vector.IsHardwareAccelerated)
            {
                var estimateSum = Vector<uint>.Zero;

                while (row.Length >= Vector<byte>.Count)
                {
                    var source = new Vector<byte>(row);

                    Vector.Widen(source, out Vector<ushort> shortLow, out Vector<ushort> shortHigh);

                    Vector.Widen(shortLow, out Vector<uint> intLow, out Vector<uint> intHigh);
                    estimateSum += intLow;
                    estimateSum += intHigh;

                    Vector.Widen(shortHigh, out intLow, out intHigh);
                    estimateSum += intLow;
                    estimateSum += intHigh;

                    row = row[Vector<byte>.Count..];
                }

                for (int i = 0; i < Vector<int>.Count; i++)
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
            int filterType,
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
                case 1:
                case 5:
                case 6:
                    current.Slice(0, i).CopyTo(output);
                    break;

                case 2:
                    for (int j = 0; j < i; j++)
                        output[j] = (byte)(current[j] - previous[j]);
                    break;

                case 3:
                    for (int j = 0; j < i; j++)
                        output[j] = (byte)(current[j] - (previous[j] / 2));
                    break;

                case 4:
                    for (int j = 0; j < i; j++)
                        output[j] = (byte)(current[j] - MathHelper.Paeth(0, previous[j], 0));
                    break;
            }

            switch (filterType)
            {
                case 1:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            var v_ncurrent = new Vector<byte>(current[(i - n)..]);
                            var v_current = new Vector<byte>(current[i..]);

                            var result = v_current - v_ncurrent;
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - current[i - n]);
                    break;

                case 2:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            var v_current = new Vector<byte>(current[i..]);
                            var v_previous = new Vector<byte>(previous[i..]);

                            var result = v_current - v_previous;
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - previous[i]);
                    break;

                case 3:
                    // We shouldn't use Vector<T> here as it doesn't intrinsify integer division
                    if (Sse2.IsSupported)
                    {
                        fixed (byte* currentPtr = current)
                        fixed (byte* previousPtr = previous)
                        fixed (byte* outputPtr = output)
                        {
                            for (; i + Vector128<byte>.Count <= output.Length; i += Vector128<byte>.Count)
                            {
                                var v_ncurrent = Sse2.LoadVector128(currentPtr + i - n);
                                var v_current = Sse2.LoadVector128(currentPtr + i);
                                var v_previous = Sse2.LoadVector128(previousPtr + i);

                                VectorHelper.Widen(v_ncurrent, out var v_ncurrent1, out var v_ncurrent2);
                                VectorHelper.Widen(v_previous, out var v_previous1, out var v_previous2);

                                var div1 = Sse2.ShiftRightLogical(Sse2.Add(v_ncurrent1, v_previous1), 1);
                                var div2 = Sse2.ShiftRightLogical(Sse2.Add(v_ncurrent2, v_previous2), 1);

                                var result = Sse2.Subtract(v_current, VectorHelper.Narrow(div1, div2));
                                Sse2.Store(outputPtr + i, result);
                            }
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - ((current[i - n] + previous[i]) / 2));
                    break;

                case 4:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            var v_ncurrent = new Vector<byte>(current[(i - n)..]);
                            var v_current = new Vector<byte>(current[i..]);
                            var v_nprevious = new Vector<byte>(previous[(i - n)..]);
                            var v_previous = new Vector<byte>(previous[i..]);

                            var result = v_current - MathHelper.Paeth(v_ncurrent, v_previous, v_nprevious);
                            result.CopyTo(output[i..]);
                        }
                    }
                    for (; i < output.Length; i++)
                    {
                        output[i] = (byte)(
                            current[i] - MathHelper.Paeth(current[i - n], previous[i], previous[i - n]));
                    }
                    break;

                case 5:
                    // We shouldn't use Vector<T> here as it doesn't intrinsify integer division
                    if (Sse2.IsSupported)
                    {
                        fixed (byte* currentPtr = current)
                        fixed (byte* outputPtr = output)
                        {
                            for (; i + Vector128<byte>.Count <= output.Length; i += Vector128<byte>.Count)
                            {
                                var v_ncurrent = Sse2.LoadVector128(currentPtr + i - n);
                                var v_current = Sse2.LoadVector128(currentPtr + i);

                                VectorHelper.Widen(v_ncurrent, out var v_ncurrent1, out var v_ncurrent2);
                                VectorHelper.Widen(v_current, out var v_current1, out var v_current2);

                                var div1 = Sse2.ShiftRightLogical(v_ncurrent1, 1);
                                var div2 = Sse2.ShiftRightLogical(v_ncurrent2, 1);

                                var result = Sse2.Subtract(v_current, VectorHelper.Narrow(div1, div2));
                                Sse2.Store(outputPtr + i, result);
                            }
                        }
                    }
                    for (; i < output.Length; i++)
                        output[i] = (byte)(current[i] - (current[i - n] / 2));
                    break;

                case 6:
                    if (Vector.IsHardwareAccelerated)
                    {
                        for (; i + Vector<byte>.Count <= output.Length; i += Vector<byte>.Count)
                        {
                            var v_ncurrent = new Vector<byte>(current[(i - n)..]);
                            var v_current = new Vector<byte>(current[i..]);

                            var result = v_current - MathHelper.Paeth(v_ncurrent, Vector<byte>.Zero, Vector<byte>.Zero);
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