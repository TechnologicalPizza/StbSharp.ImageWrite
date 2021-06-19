using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace StbSharp.ImageWrite
{
    [SkipLocalsInit]
    public static class Hdr
    {
        public static ReadOnlyMemory<byte> Head0 { get; } =
            Encoding.UTF8.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n");

        public static void Write<TPixelRowProvider>(ImageBinWriter state, TPixelRowProvider image)
            where TPixelRowProvider : IPixelRowProvider
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            image.ThrowIfCancelled();

            int width = image.Width;
            int height = image.Height;
            if (height <= 0 || width <= 0)
                throw new ArgumentException("Invalid image dimensions.", nameof(state));

            CultureInfo cult = CultureInfo.InvariantCulture;
            byte[] Head1 = Encoding.UTF8.GetBytes(string.Format(cult,
                "EXPOSURE=1.0\n\n-Y {0} +X {1}\n", height.ToString(cult), width.ToString(cult)));

            state.Write(Head0.Span);
            state.Write(Head1);

            // TODO: pool buffers

            byte[]? scratch = null;
            if (width >= 8 && width < 32768)
                scratch = new byte[width * 4];

            float[] rowBuffer = new float[width];

            for (int row = 0; row < height; row++)
            {
                image.ThrowIfCancelled();
                image.GetFloatRow(row, rowBuffer);
                WriteHdrScanline(state, image, rowBuffer, scratch);
            }
        }

        private static void WriteRunData(ImageBinWriter s, int length, byte databyte)
        {
            s.WriteByte((byte)((length + 128) & 0xff)); // lengthbyte
            s.WriteByte(databyte);
        }

        private static void WriteDumpData(ImageBinWriter s, ReadOnlySpan<byte> data)
        {
            s.WriteByte((byte)((data.Length) & 0xff)); // lengthbyte
            s.Write(data);
        }

        private static void WriteHdrScanline<TPixelRowProvider>(
            ImageBinWriter state, TPixelRowProvider image, float[] data, byte[]? buffer)
            where TPixelRowProvider : IPixelRowProvider
        {
            int width = image.Width;
            int n = image.Components;

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

                Span<float> src = data.AsSpan(0, width * n);
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

                state.Write(scanlineHeader);

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

                            WriteDumpData(state, buffer.AsSpan(o + x, len));
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

                                WriteRunData(state, len, buffer[o + x]);
                                x += len;
                            }
                        }
                    }
                }
            }
            else
            {
                Span<float> src = data.AsSpan(0, width * n);
                for (int x = 0; x < src.Length; x += n)
                {
                    linear[2] = src[x + ofsB];
                    linear[1] = src[x + ofsG];
                    linear[0] = src[x + ofsR];
                    LinearToRgbe(linear, rgbe);

                    state.Write(rgbe);
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