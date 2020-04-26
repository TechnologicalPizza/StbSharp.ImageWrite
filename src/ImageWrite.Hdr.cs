using System;
using System.Text;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Hdr
        {
            public static readonly ReadOnlyMemory<byte> FileHeaderBase =
                Encoding.UTF8.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n");

            public static int WriteCore(in WriteState s)
            {
                int width = s.Width;
                int height = s.Height;
                if (height <= 0 || width <= 0)
                    return 0;

                byte[] headerBytes = Encoding.UTF8.GetBytes(string.Format(
                    "EXPOSURE=1.0\n\n-Y {0} +X {1}\n", height.ToString(), width.ToString()));

                s.Write(FileHeaderBase.Span);
                s.Write(headerBytes);

                ScratchBuffer scratch = default;
                if (width < 8 || width >= s.ScratchBuffer.Length / 4)
                    scratch = s.GetScratch(width * 4);

                var rowBuffer = new float[width];
                var rowBufferSpan = rowBuffer.AsSpan();

                for (int row = 0; row < height; row++)
                {
                    s.GetFloatRowCallback.Invoke(row, rowBufferSpan);
                    WriteHdrScanline(s, row, rowBufferSpan, scratch);
                }
                return 1;
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

            public static void WriteHdrScanline(
                in WriteState s, int row, Span<float> data, in ScratchBuffer buffer)
            {
                int width = s.Width;
                int n = s.Components;
                
                ReadOnlySpan<byte> scanlineHeader = stackalloc byte[4] {
                    2,
                    2,
                    (byte)((width & 0xff00) >> 8),
                    (byte)(width & 0x00ff),
                };

                Span<byte> rgbe = stackalloc byte[4];
                Span<float> linear = stackalloc float[3];

                if (width < 8 || width >= s.ScratchBuffer.Length / 4)
                {
                    for (int x = 0; x < width; x++)
                    {
                        switch (n)
                        {
                            case 4:
                            case 3:
                                linear[0] = data[x * n + 0];
                                linear[1] = data[x * n + 1];
                                linear[2] = data[x * n + 2];
                                break;

                            default:
                                linear[0] = linear[1] = linear[2] = data[x * n];
                                break;
                        }

                        LinearToRgbe(linear, rgbe);
                        s.Write(rgbe);
                    }
                }
                else
                {
                    Span<byte> bufferSpan = buffer.AsSpan();
                    for (int x = 0; x < width; x++)
                    {
                        switch (n)
                        {
                            case 4:
                            case 3:
                                linear[0] = data[x * n + 0];
                                linear[1] = data[x * n + 1];
                                linear[2] = data[x * n + 2];
                                break;

                            default:
                                linear[0] = linear[1] = linear[2] = data[x * n];
                                break;
                        }

                        LinearToRgbe(linear, rgbe);
                        bufferSpan[x + width * 0] = rgbe[0];
                        bufferSpan[x + width * 1] = rgbe[1];
                        bufferSpan[x + width * 2] = rgbe[2];
                        bufferSpan[x + width * 3] = rgbe[3];
                    }

                    s.Write(scanlineHeader);

                    for (int c = 0; c < 4; c++)
                    {
                        Span<byte> comp = bufferSpan.Slice(width * c);

                        int x = 0;
                        while (x < width)
                        {
                            int r = x;
                            while ((r + 2) < width)
                            {
                                if (comp[r] == comp[r + 1] &&
                                    comp[r] == comp[r + 2])
                                    break;
                                r++;
                            }

                            if (r + 2 >= width)
                                r = width;

                            while (x < r)
                            {
                                int len = r - x;
                                if (len > 128)
                                    len = 128;
                                WriteDumpData(s, comp.Slice(x, len));
                                x += len;
                            }

                            if (r + 2 < width)
                            {
                                while ((r < width) && (comp[r] == comp[x]))
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

            public static void LinearToRgbe(ReadOnlySpan<float> source, Span<byte> destination)
            {
                float maxcomp = source[0] > (source[1] > source[2] ? source[1] : source[2])
                    ? source[0]
                    : (source[1] > source[2] ? source[1] : source[2]);

                if (maxcomp < 1e-32f)
                {
                    for (int i = 0; i < 4; i++)
                        destination[i] = 0;
                }
                else
                {
                    float normalize = (float)(CRuntime.FractionExponent(
                        maxcomp, out int exponent) * 256.0 / maxcomp);

                    destination[0] = (byte)(source[0] * normalize);
                    destination[1] = (byte)(source[1] * normalize);
                    destination[2] = (byte)(source[2] * normalize);
                    destination[3] = (byte)(exponent + 128);
                }
            }
        }
    }
}