using System;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe class Bmp
        {
            public static int WriteCore(in WriteContext s)
            {
                // we only support RGB and RGBA, no palette indexing
                // TODO: support for palette indexing

                int bytesPerPixel = s.Comp == 4 ? 4 : 3;
                int bitDepth = bytesPerPixel * 8;

                int pad = (-s.Width * bytesPerPixel) & bytesPerPixel;
                int extraPad = s.Comp == 4 ? 68 : 0; // extra bytes for compression headers
                int dataSize = (s.Width * bytesPerPixel + pad) * s.Height;

                int dibHeaderLen = 40 + extraPad;
                int fileSize = 14 + dibHeaderLen + dataSize;
                int dataOffset = 14 + dibHeaderLen;
                int compression = s.Comp == 4 ? 3 : 0; // 3 == bitfields | 0 == no compression
                object[] headers = new object[]
                {
                (int)'B', (int)'M', fileSize, 0, 0, dataOffset, // BMP header
                dibHeaderLen, s.Width, s.Height, 1, bitDepth, compression, dataSize, 0, 0, 0, 0, // DIB header

                // needed for 32bit bitmaps
                0x00ff0000, 0x0000ff00, 0x000000ff, unchecked((int)0xff000000), // RGBA masks
                0x206E6957, // little-endian (value equal to string "Win ")
                0, 0, 0, 0, 0, 0, 0, 0, 0, // colorspace endpoints (unused)
                0, 0, 0 // RGB gamma (unused)
                };

                int alpha_dir = s.Comp == 4 ? 1 : 0;
                string fmt = extraPad > 0
                    ? "11 4 22 44 44 22 444444 4444 4 444444444 444" // with compression headers
                    : "11 4 22 44 44 22 444444";
                int headerCount = 17 + extraPad / 4; // divide by 4 as all the compression headers are int32
                return WriteHelpers.Outfile(
                    s, true, -1, true, alpha_dir, pad, fmt, headers.AsSpan(0, headerCount));
            }
        }
    }
}