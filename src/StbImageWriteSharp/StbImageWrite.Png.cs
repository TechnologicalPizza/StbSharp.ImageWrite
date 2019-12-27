using System;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public static partial class StbImageWrite
    {
        public static unsafe class Png
        {
            public static int FlipVerticallyOnWrite = 0;
            public static int WriteForceFilter = -1;

            // TODO: add more color formats and a palette
            // TODO: split IDAT chunk into multiple
            // http://www.libpng.org/pub/png/spec/1.2/PNG-Chunks.html

            public static bool WriteCore(in WriteContext s, CompressionLevel level)
            {
                ZlibHeader.ConvertLevel(level); // acts as a parameter check

                s.Cancellation.ThrowIfCancellationRequested();

                int w = s.Width;
                int h = s.Height;
                int n = s.Components;
                int stride = w * n;

                int forceFilter = WriteForceFilter;
                if (forceFilter >= 5)
                    forceFilter = -1;

                int filtLength = (stride + 1) * h;
                byte* filt = (byte*)CRuntime.MAlloc(filtLength);
                if (filt == null)
                    return false;

                sbyte* lineBuffer = (sbyte*)CRuntime.MAlloc(stride);
                if (lineBuffer == null)
                {
                    CRuntime.Free(filt);
                    return false;
                }

                s.Cancellation.ThrowIfCancellationRequested();

                double progressStep = 0;
                int pixels = w * h;
                double progressStepCount = pixels / (1000 * Math.Log(pixels, 2));
                double progressStepSize = Math.Max(1, pixels / progressStepCount);

                ScratchBuffer rowScratch = s.GetScratch(stride);
                try
                {
                    Span<byte> row = rowScratch.AsSpan();
                    fixed (byte* rowPtr = &MemoryMarshal.GetReference(row))
                    {
                        for (int y = 0; y < h; ++y)
                        {
                            s.Cancellation.ThrowIfCancellationRequested();

                            int dataOffset = stride * (FlipVerticallyOnWrite != 0 ? h - 1 - y : y);
                            s.ReadBytes(row, dataOffset);

                            int filterType = 0;
                            if (forceFilter > (-1))
                            {
                                filterType = forceFilter;
                                EncodeLine(rowPtr, stride, w, y, n, forceFilter, lineBuffer);
                            }
                            else
                            {
                                int bestFilter = 0;
                                int bestFilterValue = 0x7fffffff;
                                int est = 0;
                                int i = 0;
                                for (filterType = 0; filterType < 5; filterType++)
                                {
                                    EncodeLine(rowPtr, stride, w, y, n, filterType, lineBuffer);

                                    est = 0;
                                    for (i = 0; i < stride; ++i)
                                        est += CRuntime.FastAbs(lineBuffer[i]);

                                    if (est < bestFilterValue)
                                    {
                                        bestFilterValue = est;
                                        bestFilter = filterType;
                                    }

                                    s.Cancellation.ThrowIfCancellationRequested();
                                }

                                if (filterType != bestFilter)
                                {
                                    EncodeLine(rowPtr, stride, w, y, n, bestFilter, lineBuffer);
                                    filterType = bestFilter;
                                }
                            }

                            s.Cancellation.ThrowIfCancellationRequested();

                            filt[y * (stride + 1)] = (byte)filterType;
                            CRuntime.MemCopy(filt + y * (stride + 1) + 1, lineBuffer, stride);

                            // TODO: tidy this up a notch so it's easier to reuse in other implementations
                            if (s.Progress != null)
                            {
                                progressStep += w;
                                while (progressStep >= progressStepSize)
                                {
                                    s.Progress(y / (double)h * 0.5);
                                    progressStep -= progressStepSize;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    CRuntime.Free(filt);
                    throw;
                }
                finally
                {
                    CRuntime.Free(lineBuffer);
                    rowScratch.Dispose();
                }

                s.Cancellation.ThrowIfCancellationRequested();

                // TODO: redesign chunk encoding to write partial chunks instead of one large
                IMemoryResult compressed;
                try
                {
                    WriteProgressCallback weightedProgress = null;
                    if (s.Progress != null)
                    {
                        var wpc = s.Progress;
                        weightedProgress = (p) => wpc.Invoke(p * 0.49 + 0.5);
                    }

                    var filtSpan = new ReadOnlySpan<byte>(filt, filtLength);
                    compressed = ZlibCompress.DeflateCompress(filtSpan, level, s.Cancellation, weightedProgress);

                    if (compressed == null)
                        return false;
                }
                finally
                {
                    CRuntime.Free(filt);
                    s.Cancellation.ThrowIfCancellationRequested();
                }

                try
                {
                    Span<byte> colorTypeMap = stackalloc byte[5];
                    colorTypeMap[0] = 255;
                    colorTypeMap[1] = 0;
                    colorTypeMap[2] = 4;
                    colorTypeMap[3] = 2;
                    colorTypeMap[4] = 6;

                    // sizeof sig + (fields + CRCs)
                    Span<byte> tmp = stackalloc byte[8 + sizeof(uint) * (5 + 2) + 5];
                    int pos = 0;

                    #region PNG Signature

                    tmp[pos++] = 137;
                    tmp[pos++] = 80;
                    tmp[pos++] = 78;
                    tmp[pos++] = 71;
                    tmp[pos++] = 13;
                    tmp[pos++] = 10;
                    tmp[pos++] = 26;
                    tmp[pos++] = 10;

                    #endregion

                    #region IHDR chunk

                    var hdrChunk = new PngChunk(13, "IHDR");
                    hdrChunk.WriteHeader(tmp, ref pos);

                    hdrChunk.WriteUInt32((uint)w, tmp, ref pos); // width
                    hdrChunk.WriteUInt32((uint)h, tmp, ref pos); // height

                    byte colorType = colorTypeMap[n];
                    hdrChunk.WriteInt8(8, tmp, ref pos); // bit depth
                    hdrChunk.WriteInt8(colorType, tmp, ref pos); // color type
                    hdrChunk.WriteInt8(0, tmp, ref pos); // compression method
                    hdrChunk.WriteInt8(0, tmp, ref pos); // filter method
                    hdrChunk.WriteInt8(0, tmp, ref pos); // interlace method

                    hdrChunk.WriteFooter(tmp, ref pos);

                    #endregion

                    // TODO: write multiple IDAT chunks instead of one large to lower memory usage,
                    //       this requires quite a lot of work as the encoding needs to be redesigned
                    //       as it currently encodes one line at the time, 
                    //       encoding needs to happen "on demand"
                    //       (instead of lines encode a specified amount of pixels)

                    s.Cancellation.ThrowIfCancellationRequested();

                    #region IDAT chunk

                    var datChunk = new PngChunk(compressed.Length, "IDAT");
                    datChunk.WriteHeader(tmp, ref pos);

                    s.Write(s, tmp.Slice(0, pos));
                    pos = 0;

                    var compressedSpan = new Span<byte>((void*)compressed.Pointer, compressed.Length);
                    int written = 0;
                    while (written < compressed.Length)
                    {
                        s.Cancellation.ThrowIfCancellationRequested();

                        int sliceLength = Math.Min(compressed.Length - written, s.WriteBuffer.Count);
                        s.Write(s, compressedSpan.Slice(written, sliceLength));

                        written += sliceLength;
                        s.Progress?.Invoke(written / (double)compressed.Length * 0.01 + 0.99);
                    }

                    datChunk.HashData(compressedSpan);
                    datChunk.WriteFooter(tmp, ref pos);

                    #endregion

                    #region IEND chunk

                    var endChunk = new PngChunk(0, "IEND");
                    endChunk.WriteHeader(tmp, ref pos);
                    endChunk.WriteFooter(tmp, ref pos);

                    s.Write(s, tmp.Slice(0, pos));

                    #endregion

                    return true;
                }
                finally
                {
                    compressed.Dispose();
                }
            }

            #region PngChunk

            private struct PngChunk
            {
                public readonly uint Length;
                public readonly uint Type;
                public uint Crc;

                public unsafe PngChunk(int length, string type)
                {
                    if (length < 0)
                        throw new ArgumentOutOfRangeException(nameof(length), "The value may not be negative.");
                    if (type == null)
                        throw new ArgumentNullException(nameof(type));
                    if (type.Length != 4)
                        throw new ArgumentException(nameof(type), "The type must be exactly 4 characters long.");

                    uint u32Type = 0;
                    var u32TypeSpan = new Span<byte>(&u32Type, sizeof(uint));
                    for (int i = 0; i < type.Length; i++)
                    {
                        if (type[i] > byte.MaxValue)
                            throw new ArgumentException(
                                nameof(type), "The character '" + type[i] + "' is invalid.");

                        u32TypeSpan[i] = (byte)type[i];
                    }

                    // WriteUInt writes u32 as big endian but the type should be 
                    // little endian so it needs to be reversed for WriteUInt,
                    // but after calculating the crc
                    uint crc = Crc32.Calculate(u32TypeSpan);
                    u32TypeSpan.Reverse();

                    Length = (uint)length;
                    Type = u32Type;
                    Crc = crc;
                }

                public void WriteHeader(Span<byte> output, ref int position)
                {
                    WriteHelpers.WriteUInt(Length, output, ref position);
                    WriteHelpers.WriteUInt(Type, output, ref position);
                }

                public void WriteFooter(Span<byte> output, ref int position)
                {
                    WriteHelpers.WriteUInt(~Crc, output, ref position);
                }

                #region Slurp

                public void HashData(ReadOnlySpan<byte> data)
                {
                    Crc = Crc32.Calculate(data, Crc);
                }

                private void HashData(ReadOnlySpan<byte> span, int size, int position)
                {
                    HashData(span.Slice(position - size, size));
                }

                public void WriteUInt32(uint value, Span<byte> output, ref int position)
                {
                    WriteHelpers.WriteUInt(value, output, ref position);
                    HashData(output, sizeof(uint), position);
                }

                public void WriteInt8(byte value, Span<byte> output, ref int position)
                {
                    output[position++] = value;
                    HashData(output, sizeof(byte), position);
                }

                #endregion
            }

            #endregion

            public static void EncodeLine(
                byte* pixels, int strideBytes, int width,
                int y, int n, int filterType, sbyte* lineBuffer)
            {
                int* mapping = stackalloc int[5];
                mapping[0] = 0;
                mapping[1] = 1;
                mapping[2] = 2;
                mapping[3] = 3;
                mapping[4] = 4;

                int* firstmap = stackalloc int[5];
                firstmap[0] = 0;
                firstmap[1] = 1;
                firstmap[2] = 0;
                firstmap[3] = 5;
                firstmap[4] = 6;

                int* mymap = (y != 0) ? mapping : firstmap;
                int i = 0;
                int stride = width * n;
                int type = mymap[filterType];
                byte* z = pixels;
                int signedStride = FlipVerticallyOnWrite != 0 ? -strideBytes : strideBytes;

                if (type == 0)
                {
                    CRuntime.MemCopy(lineBuffer, z, stride);
                    return;
                }

                i = 0;
                switch (type)
                {
                    case 1:
                    case 5:
                    case 6:
                        if (n == 4)
                            *(int*)lineBuffer = *(int*)z;
                        else
                            for (; i < n; ++i)
                                lineBuffer[i] = (sbyte)z[i];
                        break;

                    case 2:
                        for (; i < n; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - z[i - signedStride]);
                        break;

                    case 3:
                        for (; i < n; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - (z[i - signedStride] >> 1));
                        break;

                    case 4:
                        for (; i < n; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - CRuntime.Paeth32(0, z[i - signedStride], 0));
                        break;
                }

                i = n;
                switch (type)
                {
                    case 1:
                        for (; i < stride; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - z[i - n]);
                        break;

                    case 2:
                        for (; i < stride; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - z[i - signedStride]);
                        break;

                    case 3:
                        for (; i < stride; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - ((z[i - n] + z[i - signedStride]) >> 1));
                        break;

                    case 4:
                        for (; i < stride; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - CRuntime.Paeth32(
                                z[i - n], z[i - signedStride], z[i - signedStride - n]));
                        break;

                    case 5:
                        for (; i < stride; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - (z[i - n] >> 1));
                        break;

                    case 6:
                        for (; i < stride; ++i)
                            lineBuffer[i] = (sbyte)(z[i] - CRuntime.Paeth32(z[i - n], 0, 0));
                        break;
                }
            }
        }
    }
}