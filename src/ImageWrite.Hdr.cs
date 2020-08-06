using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Hdr
        {
            public static ReadOnlyMemory<byte> Head0 { get; } =
                Encoding.UTF8.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n");

            public static void Write(WriteState s)
            {
                if (s == null)
                    throw new ArgumentNullException(nameof(s));

                int width = s.Width;
                int height = s.Height;
                if (height <= 0 || width <= 0)
                    throw new ArgumentException("Invalid image dimensions.", nameof(s));

                var cult = CultureInfo.InvariantCulture;
                byte[] Head1 = Encoding.UTF8.GetBytes(string.Format(cult,
                    "EXPOSURE=1.0\n\n-Y {0} +X {1}\n", height.ToString(cult), width.ToString(cult)));

                s.Write(Head0.Span);
                s.Write(Head1);

                // TODO: pool buffers

                byte[]? scratch = null;
                if (width >= 8 && width < 32768)
                    scratch = new byte[width * 4];

                var rowBuffer = new float[width];

                for (int row = 0; row < height; row++)
                {
                    s.GetFloatRow(row, rowBuffer);
                    WriteHdrScanline(s, rowBuffer, scratch);
                }
            }

            public static void WriteRunData(WriteState s, int length, byte databyte)
            {
                Debug.Assert(s != null);

                s.WriteByte((byte)((length + 128) & 0xff)); // lengthbyte
                s.WriteByte(databyte);
            }

            public static void WriteDumpData(WriteState s, ReadOnlySpan<byte> data)
            {
                Debug.Assert(s != null);

                s.WriteByte((byte)((data.Length) & 0xff)); // lengthbyte
                s.Write(data);
            }

            public static void WriteHdrScanline(
                WriteState s, float[] data, byte[]? buffer)
            {
                Debug.Assert(s != null);
                Debug.Assert(data != null);

                int width = s.Width;
                int n = s.Components;

                Span<byte> scanlineHeader = stackalloc byte[4] {
                    2,
                    2,
                    (byte)((width & 0xff00) >> 8),
                    (byte)(width & 0x00ff),
                };

                Span<byte> rgbe = stackalloc byte[4];
                Span<float> linear = stackalloc float[3];

                bool hasColor = n == 4 || n == 3;
                const int ofsR = 0;
                int ofsG = hasColor ? 1 : 0;
                int ofsB = hasColor ? 2 : 0;

                if (width >= 8 && width < 32768)
                {
                    Debug.Assert(buffer != null);

                    var src = data.AsSpan(0, width * n);
                    for (int x = 0; x < src.Length; x += n)
                    {
                        linear[2] = src[x + ofsB];
                        linear[1] = src[x + ofsG];
                        linear[0] = src[x + ofsR];
                        LinearToRgbe(linear, rgbe);

                        buffer[x + width * 3] = rgbe[3];
                        buffer[x + width * 2] = rgbe[2];
                        buffer[x + width * 1] = rgbe[1];
                        buffer[x + width * 0] = rgbe[0];
                    }

                    s.Write(scanlineHeader);

                    for (int c = 0; c < 4; c++)
                    {
                        int o = width * c;

                        int x = 0;
                        while (x < width)
                        {
                            int r = x;
                            while ((r + 2) < width)
                            {
                                if (buffer[o + r] == buffer[o + r + 2] &&
                                    buffer[o + r] == buffer[o + r + 1])
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

                                WriteDumpData(s, buffer.AsSpan(o + x, len));
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

                                    WriteRunData(s, len, buffer[o + x]);
                                    x += len;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var src = data.AsSpan(0, width * n);
                    for (int x = 0; x < src.Length; x += n)
                    {
                        linear[2] = src[x + ofsB];
                        linear[1] = src[x + ofsG];
                        linear[0] = src[x + ofsR];
                        LinearToRgbe(linear, rgbe);

                        s.Write(rgbe);
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
                    destination[3] = 0;
                    destination[2] = 0;
                    destination[1] = 0;
                    destination[0] = 0;
                }
                else
                {
                    float normalize = (float)(MathHelper.FractionExponent(
                        maxcomp, out int exponent) * 256.0 / maxcomp);

                    destination[3] = (byte)(exponent + 128);
                    destination[2] = (byte)(source[2] * normalize);
                    destination[1] = (byte)(source[1] * normalize);
                    destination[0] = (byte)(source[0] * normalize);
                }
            }
        }
    }
}