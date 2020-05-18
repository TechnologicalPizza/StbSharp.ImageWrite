using System;
using System.Buffers.Binary;
using System.IO.Compression;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Png
        {
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

            public static int WriteForceFilter = -1;

            // TODO: add more color formats and a palette
            // TODO: split IDAT chunk into multiple
            // http://www.libpng.org/pub/png/spec/1.2/PNG-Chunks.html

            public static void Write(WriteState s, CompressionLevel level)
            {
                ZlibHeader.ConvertLevel(level); // acts as a parameter check

                s.CancellationToken.ThrowIfCancellationRequested();

                int w = s.Width;
                int h = s.Height;
                int n = s.Components;
                int pixelCount = w * h;
                int stride = w * n;

                int forceFilter = WriteForceFilter;
                if (forceFilter >= 5)
                    forceFilter = -1;

                // TODO: remove this (most often huge) alloc
                //      create a Stream where you Write uncompressed pixel data
                //      and it automatically creates IDAT chunks
                //    look at comment in IDAT code region

                int filtLength = (stride + 1) * h;
                var filt = new byte[filtLength];

                double progressStep = 0;
                double progressStepCount = pixelCount / (1000 * Math.Log(pixelCount, 2));
                double progressStepSize = Math.Max(1, pixelCount / progressStepCount);

                // TODO: use ArrayPool
                var previousRowBuffer = new byte[stride];
                var rowBuffer = new byte[stride];
                var lineBuffer = new byte[stride];

                void SwapBuffers()
                {
                    var tmp = previousRowBuffer;
                    previousRowBuffer = rowBuffer;
                    rowBuffer = tmp;
                }

                for (int y = 0; y < h; ++y)
                {
                    s.GetByteRow(y, rowBuffer);

                    int filterType = 0;
                    if (forceFilter > (-1))
                    {
                        filterType = forceFilter;
                        EncodeLine(previousRowBuffer, rowBuffer, y, n, forceFilter, lineBuffer);
                    }
                    else
                    {
                        int bestFilter = 0;
                        int bestFilterValue = 0x7fffffff;
                        int estimate = 0;
                        for (filterType = 0; filterType < 5; filterType++)
                        {
                            EncodeLine(previousRowBuffer, rowBuffer, y, n, filterType, lineBuffer);

                            estimate = 0;
                            for (int i = 0; i < lineBuffer.Length; ++i)
                                estimate += lineBuffer[i];

                            if (estimate < bestFilterValue)
                            {
                                bestFilterValue = estimate;
                                bestFilter = filterType;
                            }

                            s.CancellationToken.ThrowIfCancellationRequested();
                        }

                        if (filterType != bestFilter)
                        {
                            EncodeLine(previousRowBuffer, rowBuffer, y, n, bestFilter, lineBuffer);
                            filterType = bestFilter;
                        }
                    }

                    s.CancellationToken.ThrowIfCancellationRequested();

                    SwapBuffers();

                    var filtSlice = filt.AsSpan(y * (stride + 1), stride + 1);
                    filtSlice[0] = (byte)filterType;
                    lineBuffer.CopyTo(filtSlice.Slice(1));

                    // TODO: tidy this up a notch so it's easier to reuse in other implementations
                    if (s.ProgressCallback != null)
                    {
                        progressStep += w;
                        while (progressStep >= progressStepSize)
                        {
                            s.ReportProgress(y / (float)h * 0.5f);
                            progressStep -= progressStepSize;
                        }
                    }
                }

                // TODO: redesign chunk encoding to write partial chunks instead of one large

                Action<float> weightedProgress = null;
                if (s.ProgressCallback != null)
                {
                    var ctx = s;
                    weightedProgress = (p) => ctx.ReportProgress(p * 0.49f + 0.5f);
                }

                var compressed = Zlib.DeflateCompress(
                    filt, level, s.CancellationToken, weightedProgress);

                using (compressed)
                {
                    // sizeof sig + (fields + CRCs)
                    Span<byte> tmp = stackalloc byte[Signature.Length + sizeof(uint) * (5 + 2) + 5];
                    int pos = 0;

                    Signature.CopyTo(tmp);
                    pos += Signature.Length;

                    #region IHDR chunk

                    var hdrChunk = new PngChunkHeader(13, "IHDR");
                    hdrChunk.WriteHeader(tmp, ref pos);

                    hdrChunk.WriteUInt32((uint)w, tmp, ref pos); // width
                    hdrChunk.WriteUInt32((uint)h, tmp, ref pos); // height

                    byte colorType = ColorTypeMap[n];
                    hdrChunk.WriteByte(8, tmp, ref pos); // bit depth
                    hdrChunk.WriteByte(colorType, tmp, ref pos); // color type
                    hdrChunk.WriteByte(0, tmp, ref pos); // compression method
                    hdrChunk.WriteByte(0, tmp, ref pos); // filter method
                    hdrChunk.WriteByte(0, tmp, ref pos); // interlace method

                    hdrChunk.WriteFooter(tmp, ref pos);

                    #endregion

                    // TODO: write multiple IDAT chunks instead of one large to lower memory usage,
                    //       this requires quite a lot of work as the encoding needs to be redesigned
                    //       as it currently encodes one line at the time, 
                    //       encoding needs to happen "on demand"
                    //       (instead of lines encode a specified amount of pixels)

                    #region IDAT chunk

                    // TODO: make IDAT chunk writing progressive

                    var datChunk = new PngChunkHeader(compressed.Length, "IDAT");
                    datChunk.WriteHeader(tmp, ref pos);

                    s.Write(tmp.Slice(0, pos));
                    pos = 0;

                    // TODO: fix this garbage alloc by creatign that "on demand" writer
                    var compressedSpan = compressed.Span.ToArray();

                    int written = 0;
                    while (written < compressed.Length)
                    {
                        int sliceLength = compressed.Length - written;
                        s.Write(compressedSpan.AsSpan(written, sliceLength));

                        written += sliceLength;
                        s.ReportProgress(written / (float)compressed.Length * 0.01f + 0.99f);
                    }

                    // TODO: create a stream wrapper that can 
                    // both write/compress multiple chunks and calculate the CRC simultaneously
                    datChunk.HashData(compressed.Span);
                    datChunk.WriteFooter(tmp, ref pos);

                    #endregion

                    #region IEND chunk

                    var endChunk = new PngChunkHeader(0, "IEND");
                    endChunk.WriteHeader(tmp, ref pos);
                    endChunk.WriteFooter(tmp, ref pos);

                    s.Write(tmp.Slice(0, pos));

                    #endregion
                }
            }

            #region PngChunk

            private struct PngChunkHeader
            {
                public readonly uint Length;
                public readonly uint Type;
                public uint Crc;

                public unsafe PngChunkHeader(int length, string type)
                {
                    if (length < 0)
                        throw new ArgumentOutOfRangeException(
                            nameof(length), "The value may not be negative.");
                    if (type == null)
                        throw new ArgumentNullException(nameof(type));
                    if (type.Length != 4)
                        throw new ArgumentException(
                            nameof(type), "The string must be exactly 4 characters long.");

                    Span<byte> typeBytes = stackalloc byte[sizeof(uint)];
                    for (int i = 0; i < type.Length; i++)
                    {
                        if (type[i] > byte.MaxValue)
                            throw new ArgumentException(
                                nameof(type), "The character '" + type[i] + "' is invalid.");

                        typeBytes[i] = (byte)type[i];
                    }

                    Length = (uint)length;
                    Type = BinaryPrimitives.ReadUInt32BigEndian(typeBytes);
                    Crc = Crc32.Calculate(typeBytes);
                }

                public void WriteHeader(Span<byte> destination, ref int position)
                {
                    ImageWriteHelpers.WriteUInt(Length, destination, ref position);
                    ImageWriteHelpers.WriteUInt(Type, destination, ref position);
                }

                public void WriteFooter(Span<byte> destination, ref int position)
                {
                    ImageWriteHelpers.WriteUInt(~Crc, destination, ref position);
                }

                #region Slurp

                public void HashData(ReadOnlySpan<byte> data)
                {
                    Crc = Crc32.Calculate(data, Crc);
                }

                private void HashData(ReadOnlySpan<byte> data, int size, int position)
                {
                    HashData(data.Slice(position - size, size));
                }

                public void WriteUInt32(uint value, Span<byte> destination, ref int position)
                {
                    ImageWriteHelpers.WriteUInt(value, destination, ref position);
                    HashData(destination, sizeof(uint), position);
                }

                public void WriteByte(byte value, Span<byte> destination, ref int position)
                {
                    destination[position++] = value;
                    HashData(destination, sizeof(byte), position);
                }

                #endregion
            }

            #endregion

            public static void EncodeLine(
                ReadOnlySpan<byte> previousRow,
                ReadOnlySpan<byte> row,
                int y,
                int n,
                int filterType,
                Span<byte> scanline)
            {
                var filterMap = (y != 0) ? FilterMapping : FirstFilterMapping;
                int type = filterMap[filterType];
                if (type == 0)
                {
                    row.CopyTo(scanline);
                    return;
                }

                switch (type)
                {
                    case 1:
                    case 5:
                    case 6:
                        row.Slice(0, n).CopyTo(scanline);
                        break;

                    case 2:
                        for (int i = 0; i < n; ++i)
                            scanline[i] = (byte)(row[i] - previousRow[i]);
                        break;

                    case 3:
                        for (int i = 0; i < n; ++i)
                            scanline[i] = (byte)(row[i] - (previousRow[i] / 2));
                        break;

                    case 4:
                        for (int i = 0; i < n; ++i)
                            scanline[i] = (byte)(row[i] - CRuntime.Paeth32(0, previousRow[i], 0));
                        break;
                }

                switch (type)
                {
                    case 1:
                        for (int i = n; i < scanline.Length; ++i)
                            scanline[i] = (byte)(row[i] - row[i - n]);
                        break;

                    case 2:
                        for (int i = n; i < scanline.Length; ++i)
                            scanline[i] = (byte)(row[i] - previousRow[i]);
                        break;

                    case 3:
                        for (int i = n; i < scanline.Length; ++i)
                            scanline[i] = (byte)(row[i] - ((row[i - n] + previousRow[i]) / 2));
                        break;

                    case 4:
                        for (int i = n; i < scanline.Length; ++i)
                            scanline[i] = (byte)(
                                row[i] - CRuntime.Paeth32(row[i - n], previousRow[i], previousRow[i - n]));
                        break;

                    case 5:
                        for (int i = n; i < scanline.Length; ++i)
                            scanline[i] = (byte)(row[i] - (row[i - n] / 2));
                        break;

                    case 6:
                        for (int i = n; i < scanline.Length; ++i)
                            scanline[i] = (byte)(row[i] - CRuntime.Paeth32(row[i - n], 0, 0));
                        break;
                }
            }
        }
    }
}