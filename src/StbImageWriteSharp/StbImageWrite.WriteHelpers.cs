using System;

namespace StbSharp
{
    public static partial class StbImageWrite
    {
        public static class WriteHelpers
        {
            public static void GetBytes(uint value, Span<byte> output)
            {
                output[0] = (byte)((value >> 24) & 0xff);
                output[1] = (byte)((value >> 16) & 0xff);
                output[2] = (byte)((value >> 8) & 0xff);
                output[3] = (byte)((value) & 0xff);
            }

            public static void WriteUInt(uint value, Span<byte> output, ref int position)
            {
                GetBytes(value, output.Slice(position));
                position += sizeof(uint);
            }

            /// <summary>
            /// Writes <see cref="object"/>s in certain ways depending on the <paramref name="format"/>.
            /// <para>1 is <see cref="byte"/>, 2 is <see cref="short"/>,
            /// 4 is <see cref="int"/>, and whitespaces are skipped.</para>
            /// </summary>
            public static void WriteFormat(in WriteContext s, string format, Span<object> v)
            {
                Span<byte> buf = stackalloc byte[4];

                int vindex = 0;
                for (var i = 0; i < format.Length; ++i)
                {
                    var c = format[i];
                    switch (c)
                    {
                        case ' ':
                            break;

                        case '1':
                            buf[0] = (byte)((int)v[vindex++] & 0xff);
                            s.Write(s, buf.Slice(0, 1));
                            break;

                        case '2':
                        {
                            int x = (int)v[vindex++];
                            buf[0] = (byte)(x & 0xff);
                            buf[1] = (byte)((x >> 8) & 0xff);
                            s.Write(s, buf.Slice(0, 2));
                            break;
                        }

                        case '4':
                        {
                            var x = (int)v[vindex++];
                            buf[0] = (byte)(x & 0xff);
                            buf[1] = (byte)((x >> 8) & 0xff);
                            buf[2] = (byte)((x >> 16) & 0xff);
                            buf[3] = (byte)((x >> 24) & 0xff);
                            s.Write(s, buf.Slice(0, 4));
                            break;
                        }
                    }
                }
            }

            public static void WriteChar(in WriteContext s, byte c)
            {
                Span<byte> tmp = stackalloc byte[] { c };
                s.Write(s, tmp);
            }

            public static int WritePixel(
                bool flipRgb, int alphaDir, bool expandMono, Span<byte> pixel, Span<byte> output)
            {
                int offset = 0;

                if (alphaDir < 0)
                    output[offset++] = pixel[pixel.Length - 1];

                switch (pixel.Length)
                {
                    case 1:
                    case 2:
                        if (expandMono)
                        {
                            byte mono = pixel[0];
                            for (int i = 0; i < 3; i++)
                                output[offset++] = mono;
                        }
                        else
                        {
                            output[offset++] = pixel[0];
                        }
                        break;

                    case 3:
                    case 4:
                        int start = offset;
                        if ((pixel.Length == 4) && (alphaDir == 0))
                        {
                            output[offset++] = (byte)(255 + (pixel[0] - 255) * pixel[3] / 255);
                            output[offset++] = (byte)(pixel[1] * pixel[3] / 255);
                            output[offset++] = (byte)(255 + (pixel[2] - 255) * pixel[3] / 255);
                        }
                        else
                        {
                            for (int i = 0; i < 3; i++)
                                output[offset++] = pixel[i];
                        }

                        if (flipRgb)
                        {
                            byte first = output[start];
                            output[start] = output[start + 2];
                            output[start + 2] = first;
                        }
                        break;
                }

                if (alphaDir > 0)
                    output[offset++] = pixel[pixel.Length - 1];

                return offset;
            }

            public static void WritePixels(
                in WriteContext s, bool flipRgb, int vdir,
                int alphaDir, int scanlinePad, bool expandMono)
            {
                if (scanlinePad < 0 || scanlinePad > 4)
                    throw new ArgumentOutOfRangeException(nameof(scanlinePad));

                if (s.Height <= 0)
                    return;

                int i;
                int j;
                int jEnd;
                if (vdir < 0)
                {
                    jEnd = -1;
                    j = s.Height - 1;
                }
                else
                {
                    jEnd = s.Height;
                    j = 0;
                }

                int x = s.Width;
                int comp = s.Components;
                int stride = x * comp;

                int scratchSize = stride + scanlinePad;
                ScratchBuffer scratch = s.GetScratch(scratchSize);
                try
                {
                    Span<byte> scratchSpan = scratch.AsSpan(0, scratchSize);
                    Span<byte> scanlinePadSpan = scratchSpan.Slice(stride, scanlinePad);
                    scanlinePadSpan.Fill(0);

                    for (; j != jEnd; j += vdir)
                    {
                        s.ReadBytes(scratchSpan, j * stride);
                        int offset = 0;
                        for (i = 0; i < x; ++i)
                        {
                            var pixel = scratchSpan.Slice(i * comp, comp);
                            var output = scratchSpan.Slice(offset, comp);
                            offset += WritePixel(flipRgb, alphaDir, expandMono, pixel, output);
                        }

                        if (offset != stride)
                        {
                            s.Write(s, scratchSpan.Slice(0, offset));
                            s.Write(s, scanlinePadSpan);
                        }
                        else
                            s.Write(s, scratchSpan);
                    }
                }
                finally
                {
                    scratch.Dispose();
                }
            }

            /// <summary>
            /// Used for writing raw data with headers.
            /// </summary>
            public static int Outfile(
                in WriteContext s, bool flipRgb, int vdir,
                bool expandMono, int alphaDir, int pad, string fmt, Span<object> v)
            {
                if (s.Width <= 0 || s.Height <= 0)
                    return 0;

                WriteFormat(s, fmt, v);
                WritePixels(s, flipRgb, vdir, alphaDir, pad, expandMono);
                return 1;
            }
        }
    }
}