using System;
using System.Text;
using System.Threading.Tasks;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Hdr
        {
            public static ReadOnlyMemory<byte> FileHeaderBase { get; } =
                Encoding.UTF8.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n");

            public static async Task Write(WriteState s)
            {
                int width = s.Width;
                int height = s.Height;
                if (height <= 0 || width <= 0)
                    throw new ArgumentException("Invalid image dimensions.", nameof(s));

                byte[] headerBytes = Encoding.UTF8.GetBytes(string.Format(
                    "EXPOSURE=1.0\n\n-Y {0} +X {1}\n", height.ToString(), width.ToString()));

                await s.Write(FileHeaderBase);
                await s.Write(headerBytes);

                byte[] scratch = default;
                if (width < 8 || width >= 32768)
                    scratch = new byte[width * 4];

                var rowBuffer = new float[width];

                for (int row = 0; row < height; row++)
                {
                    s.GetFloatRow(row, rowBuffer);
                    await WriteHdrScanline(s, rowBuffer, scratch);
                }
            }

            public static async ValueTask WriteRunData(WriteState s, int length, byte databyte)
            {
                await s.WriteByte((byte)((length + 128) & 0xff)); // lengthbyte
                await s.WriteByte(databyte);
            }

            public static async ValueTask WriteDumpData(WriteState s, ReadOnlyMemory<byte> data)
            {
                await s.WriteByte((byte)((data.Length) & 0xff)); // lengthbyte
                await s.Write(data);
            }

            public static async ValueTask WriteHdrScanline(
                WriteState s, float[] data, byte[] buffer)
            {
                int width = s.Width;
                int n = s.Components;

                var scanlineHeader = new byte[4] {
                    2,
                    2,
                    (byte)((width & 0xff00) >> 8),
                    (byte)(width & 0x00ff),
                };

                var rgbe = new byte[4];
                var linear = new float[3];

                if (width < 8 || width >= buffer.Length / 4)
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
                        await s.Write(rgbe);
                    }
                }
                else
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
                        buffer[x + width * 0] = rgbe[0];
                        buffer[x + width * 1] = rgbe[1];
                        buffer[x + width * 2] = rgbe[2];
                        buffer[x + width * 3] = rgbe[3];
                    }

                    await s.Write(scanlineHeader);

                    for (int c = 0; c < 4; c++)
                    {
                        int o = width * c;

                        int x = 0;
                        while (x < width)
                        {
                            int r = x;
                            while ((r + 2) < width)
                            {
                                if (buffer[o + r] == buffer[o + r + 1] &&
                                    buffer[o + r] == buffer[o + r + 2])
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

                                await WriteDumpData(s, buffer.AsMemory(o + x, len));
                                x += len;
                            }

                            if (r + 2 < width)
                            {
                                while ((r < width) && (buffer[o + r] == buffer[o + x]))
                                    r++;

                                while (x < r)
                                {
                                    int len = r - x;
                                    if (len > 127)
                                        len = 127;

                                    await WriteRunData(s, len, buffer[o + x]);
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