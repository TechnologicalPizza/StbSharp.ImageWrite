using System;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Bmp
        {
            public static void Write(WriteState s)
            {
                // we only support RGB and RGBA, no palette indexing
                // TODO: support for palette indexing

                if (s == null)
                    throw new ArgumentNullException(nameof(s));

                int bytesPerPixel = s.Components == 4 ? 4 : 3;
                int bitDepth = bytesPerPixel * 8;

                int pad = (-s.Width * bytesPerPixel) & bytesPerPixel;
                int compressionHeaders = s.Components == 4 ? 17 : 0;
                int extraPad = compressionHeaders * sizeof(int); // extra bytes for compression headers
                int dataSize = (s.Width * bytesPerPixel + pad) * s.Height;

                int dibHeaderLen = 40 + extraPad;
                int fileSize = 14 + dibHeaderLen + dataSize;
                int dataOffset = 14 + dibHeaderLen;
                int compression = s.Components == 4 ? 3 : 0; // 3 == bitfields | 0 == no compression

                Span<long> header = stackalloc long[]
                {
                    // BMP header
                    'B',
                    'M',
                    fileSize,
                    0,
                    0,
                    dataOffset,

                    // DIB header
                    dibHeaderLen,
                    s.Width,
                    s.Height,
                    1,
                    bitDepth,
                    compression,
                    dataSize,
                    0,
                    0,
                    0,
                    0,

                    // needed for 32bit bitmaps
                    0x00ff0000,
                    0x0000ff00,
                    0x000000ff,
                    0xff000000, // RGBA masks
                    0x206E6957, // little-endian (value equal to string "Win ")
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0, // colorspace endpoints (unused)
                    0,
                    0,
                    0 // RGB gamma (unused)
                };

                int alphaDir = s.Components == 4 ? 1 : 0;
                string format =
                    "11 4 22 44 44 22 444444 " + // base header
                    "4444 4 444444444 444"; // compression header

                int headerCount = 17 + compressionHeaders;
                ImageWriteHelpers.OutFile(
                    s, true, -1, true, alphaDir, pad, format, header.Slice(0, headerCount));
            }
        }
    }
}