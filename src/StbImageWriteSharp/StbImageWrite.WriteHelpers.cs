using System;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe class WriteHelpers
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
                bool flip_rgb, int alpha_dir, bool expand_mono, Span<byte> pixel, Span<byte> output)
            {
                int offset = 0;

                if (alpha_dir < 0)
                    output[offset++] = pixel[pixel.Length - 1];

                switch (pixel.Length)
                {
                    case 1:
                    case 2:
                        if (expand_mono)
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
                        if ((pixel.Length == 4) && (alpha_dir == 0))
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

                        if (flip_rgb)
                        {
                            byte first = output[start];
                            output[start] = output[start + 2];
                            output[start + 2] = first;
                        }
                        break;
                }

                if (alpha_dir > 0)
                    output[offset++] = pixel[pixel.Length - 1];

                return offset;
            }

            public static void WritePixels(
                in WriteContext s, bool flip_rgb, int vdir,
                int alpha_dir, int scanline_pad, bool expand_mono)
            {
                if (scanline_pad < 0 || scanline_pad > 4)
                    throw new ArgumentOutOfRangeException(nameof(scanline_pad));

                if (s.Height <= 0)
                    return;

                int i;
                int j;
                int j_end;
                if (vdir < 0)
                {
                    j_end = -1;
                    j = s.Height - 1;
                }
                else
                {
                    j_end = s.Height;
                    j = 0;
                }

                int x = s.Width;
                int comp = s.Comp;
                int stride = x * comp;

                int scratchSize = stride + scanline_pad;
                ScratchBuffer scratch = s.GetScratch(scratchSize);
                try
                {
                    Span<byte> scratchSpan = scratch.AsSpan(0, scratchSize);
                    Span<byte> scanlinePadSpan = scratchSpan.Slice(stride, scanline_pad);
                    scanlinePadSpan.Fill(0);

                    for (; j != j_end; j += vdir)
                    {
                        s.ReadBytes(scratchSpan, j * stride);
                        int offset = 0;
                        for (i = 0; i < x; ++i)
                        {
                            var pixel = scratchSpan.Slice(i * comp, comp);
                            var output = scratchSpan.Slice(offset, comp);
                            offset += WritePixel(flip_rgb, alpha_dir, expand_mono, pixel, output);
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
                in WriteContext s, bool flip_rgb, int vdir,
                bool expand_mono, int alpha_dir, int pad, string fmt, Span<object> v)
            {
                if (s.Width <= 0 || s.Height <= 0)
                    return 0;

                WriteFormat(s, fmt, v);
                WritePixels(s, flip_rgb, vdir, alpha_dir, pad, expand_mono);
                return 1;
            }
        }
    }
}