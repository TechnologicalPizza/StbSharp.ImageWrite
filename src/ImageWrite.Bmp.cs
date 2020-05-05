using System;
using System.Threading.Tasks;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Bmp
        {
            public static Task Write(WriteState s)
            {
                // we only support RGB and RGBA, no palette indexing
                // TODO: support for palette indexing

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
                var headers = new long[]
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
                string format = extraPad > 0
                    ? "11 4 22 44 44 22 444444 4444 4 444444444 444" // with compression headers
                    : "11 4 22 44 44 22 444444";

                int headerCount = 17 + extraPad / sizeof(int); // all the compression headers are int32
                return ImageWriteHelpers.OutFile(
                    s, true, -1, true, alphaDir, pad, format, headers.AsMemory(0, headerCount));
            }
        }
    }
}