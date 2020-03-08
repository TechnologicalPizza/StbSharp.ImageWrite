using System;
using System.Text;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Hdr
        {
            public static readonly ReadOnlyMemory<byte> FileHeader =
                Encoding.UTF8.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n");

            public static int WriteCore(in WriteState s)
            {
                int x = s.Width;
                int y = s.Height;
                if (y <= 0 || x <= 0)
                    return 0;

                s.Write(FileHeader.Span);

                byte[] bytes = Encoding.UTF8.GetBytes(string.Format(
                    "EXPOSURE=1.0\n\n-Y {0} +X {1}\n", y.ToString(), x.ToString()));
                s.Write(bytes);

                ScratchBuffer scratch = default;
                try
                {
                    if (x < 8 || x >= s.ScratchBuffer.Length / 4) // TODO: try to remove "x < 8" condition
                        scratch = s.GetScratch(x * 4);

                    for (int line = 0; line < y; line++)
                        WriteHdrScanline(s, line, scratch);

                    return 1;
                }
                finally
                {
                    scratch.Dispose();
                }
            }

            public static void LinearToRgbe(Span<byte> output, ReadOnlySpan<float> linear)
            {
                float maxcomp = linear[0] > (linear[1] > linear[2] ? linear[1] : linear[2])
                    ? linear[0]
                    : (linear[1] > linear[2] ? linear[1] : linear[2]);

                if (maxcomp < 1e-32f)
                {
                    for (int i = 0; i < 4; i++)
                        output[i] = 0;
                }
                else
                {
                    float normalize = (float)(CRuntime.FractionExponent(
                        maxcomp, out int exponent) * 256.0 / maxcomp);

                    output[0] = (byte)(linear[0] * normalize);
                    output[1] = (byte)(linear[1] * normalize);
                    output[2] = (byte)(linear[2] * normalize);
                    output[3] = (byte)(exponent + 128);
                }
            }

            public static void WriteRunData(in WriteState s, int length, byte databyte)
            {
                Span<byte> tmp = stackalloc byte[1];
                tmp[0] = (byte)((length + 128) & 0xff); // lengthbyte
                s.Write(tmp);

                tmp[0] = databyte;
                s.Write(tmp);
            }

            public static void WriteDumpData(in WriteState s, ReadOnlySpan<byte> data)
            {
                Span<byte> tmp = stackalloc byte[1];
                tmp[0] = (byte)((data.Length) & 0xff); // lengthbyte
                s.Write(tmp);

                s.Write(data);
            }

            public static void WriteHdrScanline(in WriteState s, int y, ScratchBuffer scratch)
            {
                int w = s.Width;
                int n = s.Components;
                int x;

                Span<byte> scanlineheader = stackalloc byte[4];
                scanlineheader[0] = 2;
                scanlineheader[1] = 2;
                scanlineheader[2] = (byte)((w & 0xff00) >> 8);
                scanlineheader[3] = (byte)(w & 0x00ff);

                Span<byte> rgbe = stackalloc byte[4];
                Span<float> linear = stackalloc float[3];

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

                        LinearToRgbe(rgbe, linear);
                        s.Write(rgbe);
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

                        LinearToRgbe(rgbe, linear);
                        scratchSpan[x + w * 0] = rgbe[0];
                        scratchSpan[x + w * 1] = rgbe[1];
                        scratchSpan[x + w * 2] = rgbe[2];
                        scratchSpan[x + w * 3] = rgbe[3];
                    }

                    s.Write(scanlineheader);

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
                                WriteDumpData(s, comp.Slice(x, len));
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
                                    WriteRunData(s, len, comp[x]);
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