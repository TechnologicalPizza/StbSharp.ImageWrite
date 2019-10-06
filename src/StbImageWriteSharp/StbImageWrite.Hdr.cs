using System;
using System.Text;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe class Hdr
        {
            private static readonly byte[] stbi_hdr_radiance_header =
                Encoding.UTF8.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n");

            public static int stbi_write_hdr_core(in WriteContext s)
            {
                int x = s.Width;
                int y = s.Height;
                if (y <= 0 || x <= 0)
                    return 0;

                s.Write(s, stbi_hdr_radiance_header);

                byte[] bytes = Encoding.UTF8.GetBytes(string.Format(
                    "EXPOSURE=1.0\n\n-Y {0} +X {1}\n", y.ToString(), x.ToString()));
                s.Write(s, bytes);

                ScratchBuffer scratch = default;
                try
                {
                    if (x < 8 || x >= s.ScratchBuffer.Length / 4) // TODO: try to remove "x < 8" condition
                        scratch = s.GetScratch(x * 4);

                    for (int line = 0; line < y; line++)
                        stbiw__write_hdr_scanline(s, line, scratch);

                    return 1;
                }
                finally
                {
                    scratch.Dispose();
                }
            }

            public static void stbiw__linear_to_rgbe(Span<byte> rgbe, float* linear)
            {
                float maxcomp = linear[0] > (linear[1] > linear[2]
                    ? linear[1] : linear[2])
                    ? linear[0] : (linear[1] > linear[2] ? linear[1] : linear[2]);

                if (maxcomp < 1e-32f)
                {
                    for (int i = 0; i < 4; i++)
                        rgbe[i] = 0;
                }
                else
                {
                    float normalize = (float)(CRuntime.frexp(maxcomp, out int exponent) * 256.0 / maxcomp);
                    rgbe[0] = (byte)(linear[0] * normalize);
                    rgbe[1] = (byte)(linear[1] * normalize);
                    rgbe[2] = (byte)(linear[2] * normalize);
                    rgbe[3] = (byte)(exponent + 128);
                }
            }

            public static void stbiw__write_run_data(in WriteContext s, int length, byte databyte)
            {
                Span<byte> tmp = stackalloc byte[1];
                tmp[0] = (byte)((length + 128) & 0xff); // lengthbyte
                s.Write(s, tmp);

                tmp[0] = databyte;
                s.Write(s, tmp);
            }

            public static void stbiw__write_dump_data(in WriteContext s, Span<byte> data)
            {
                Span<byte> tmp = stackalloc byte[1];
                tmp[0] = (byte)((data.Length) & 0xff); // lengthbyte
                s.Write(s, tmp);

                s.Write(s, data);
            }

            public static void stbiw__write_hdr_scanline(in WriteContext s, int y, ScratchBuffer scratch)
            {
                int w = s.Width;
                int n = s.Comp;
                int x;

                Span<byte> scanlineheader = stackalloc byte[4];
                scanlineheader[0] = 2;
                scanlineheader[1] = 2;
                scanlineheader[2] = (byte)((w & 0xff00) >> 8);
                scanlineheader[3] = (byte)(w & 0x00ff);

                Span<byte> rgbe = stackalloc byte[4];
                float* linear = stackalloc float[3];

                Span<float> scanline = stackalloc float[n];
                if (w < 8 || w >= s.ScratchBuffer.Length / 4)
                {
                    for (x = 0; x < w; x++)
                    {
                        s.ReadFloats(scanline, (x + y * w) * n);

                        switch (n)
                        {
                            case 4:
                            case 3:
                                linear[0] = scanline[x * n + 0];
                                linear[1] = scanline[x * n + 1];
                                linear[2] = scanline[x * n + 2];
                                break;

                            default:
                                linear[0] = linear[1] = linear[2] = scanline[x * n];
                                break;
                        }

                        stbiw__linear_to_rgbe(rgbe, linear);
                        s.Write(s, rgbe);
                    }
                }
                else
                {
                    Span<byte> scratchSpan = scratch.AsSpan();
                    for (x = 0; x < w; x++)
                    {
                        s.ReadFloats(scanline, (x + y * w) * n);

                        switch (n)
                        {
                            case 4:
                            case 3:
                                linear[0] = scanline[x * n + 0];
                                linear[1] = scanline[x * n + 1];
                                linear[2] = scanline[x * n + 2];
                                break;

                            default:
                                linear[0] = linear[1] = linear[2] = scanline[x * n];
                                break;
                        }

                        stbiw__linear_to_rgbe(rgbe, linear);
                        scratchSpan[x + w * 0] = rgbe[0];
                        scratchSpan[x + w * 1] = rgbe[1];
                        scratchSpan[x + w * 2] = rgbe[2];
                        scratchSpan[x + w * 3] = rgbe[3];
                    }

                    s.Write(s, scanlineheader);

                    for (int c = 0; c < 4; c++)
                    {
                        Span<byte> comp = scratch.AsSpan(w * c);

                        x = 0;
                        while (x < w)
                        {
                            int r = x;
                            while ((r + 2) < w)
                            {
                                if (comp[r] == comp[r + 1] &&
                                    comp[r] == comp[r + 2])
                                    break;
                                r++;
                            }

                            if (r + 2 >= w)
                                r = w;

                            while (x < r)
                            {
                                int len = r - x;
                                if (len > 128)
                                    len = 128;
                                stbiw__write_dump_data(s, comp.Slice(x, len));
                                x += len;
                            }

                            if (r + 2 < w)
                            {
                                while ((r < w) && (comp[r] == comp[x]))
                                    r++;

                                while (x < r)
                                {
                                    int len = r - x;
                                    if (len > 127)
                                        len = 127;
                                    stbiw__write_run_data(s, len, comp[x]);
                                    x += len;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}