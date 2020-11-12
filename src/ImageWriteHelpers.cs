using System;
using System.Globalization;

namespace StbSharp.ImageWrite
{
    public static class ImageWriteHelpers
    {
        /// <summary>
        /// Writes integers in different sizes based on 
        /// digit characters in the <paramref name="format"/> string.
        /// <para>
        /// Zero skips a value, while other characters are ignored.
        /// Maximum size of one integer is 8 bytes.
        /// </para>
        /// </summary>
        public static void WriteFormat(
            this WriteState s, ReadOnlySpan<char> format, ReadOnlySpan<long> values)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            Span<byte> buffer = stackalloc byte[sizeof(long)];

            int valueIndex = 0;
            for (int i = 0; i < format.Length; i++)
            {
                int digit = CharUnicodeInfo.GetDecimalDigitValue(format[i]);
                if (digit == -1 || digit > buffer.Length)
                    continue;

                if (digit > 0)
                {
                    long x = values[valueIndex];
                    for (int j = 0; j < digit; j++)
                    {
                        int shift = 8 * j;
                        buffer[j] = (byte)((x >> shift) & 0xff);
                    }
                    s.Write(buffer.Slice(0, digit));
                }
                valueIndex++;
            }
        }

        /// <summary>
        /// Used for writing raw data with headers.
        /// </summary>
        public static void OutFile(
            this WriteState s,
            bool flipRgb, int verticalDirection, bool expandMono, int alphaDirection, int scanlinePad,
            ReadOnlySpan<char> format, ReadOnlySpan<long> values)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            if (s.Width <= 0 || s.Height <= 0)
                throw new ArgumentException("Invalid image dimensions.", nameof(s));

            WriteFormat(s, format, values);
            WritePixels(s, flipRgb, verticalDirection, alphaDirection, scanlinePad, expandMono);
        }

        public static void WritePixels(
            this WriteState s,
            bool flipRgb, int verticalDirection, int alphaDirection, int scanlinePad, bool expandMono)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            if (scanlinePad < 0 || scanlinePad > 4)
                throw new ArgumentOutOfRangeException(nameof(scanlinePad));

            if (s.Width <= 0 || s.Height <= 0)
                return;

            int row;
            int rowEnd;
            if (verticalDirection < 0)
            {
                rowEnd = -1;
                row = s.Height - 1;
            }
            else
            {
                rowEnd = s.Height;
                row = 0;
            }

            int width = s.Width;
            int comp = s.Components;
            int stride = width * comp;
            int scanlineMax = stride + scanlinePad;

            Span<byte> scanline = scanlineMax <= 4100 ? stackalloc byte[scanlineMax] : new byte[scanlineMax];

            for (; row != rowEnd; row += verticalDirection)
            {
                s.GetByteRow(row, scanline);

                int offset = 0;
                for (int x = 0; x < width; x++)
                {
                    offset += WritePixel(
                        flipRgb, alphaDirection, expandMono,
                        scanline.Slice(x * comp, comp),
                        scanline.Slice(offset, comp));
                }

                if (offset != stride)
                {
                    var padSlice = scanline.Slice(offset, scanlinePad);
                    padSlice.Clear(); // clear possible garbage from last row
                    offset += scanlinePad;
                }
                s.Write(scanline.Slice(0, offset));
            }
        }

        public static int WritePixel(
            bool flipRgb, int alphaDirection, bool expandMono,
            ReadOnlySpan<byte> pixel, Span<byte> destination)
        {
            int offset = 0;
            int length = pixel.Length;

            if (alphaDirection < 0)
                destination[offset++] = pixel[length - 1];

            switch (length)
            {
                case 1:
                case 2:
                    if (expandMono)
                    {
                        byte mono = pixel[0];
                        for (int i = 0; i < 3; i++)
                            destination[offset++] = mono;
                    }
                    else
                    {
                        destination[offset++] = pixel[0];
                    }
                    break;

                case 3:
                case 4:
                    int start = offset;
                    if ((length == 4) && (alphaDirection == 0))
                    {
                        destination[offset++] = (byte)(255 + (pixel[0] - 255) * pixel[3] / 255);
                        destination[offset++] = (byte)(pixel[1] * pixel[3] / 255);
                        destination[offset++] = (byte)(255 + (pixel[2] - 255) * pixel[3] / 255);
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                            destination[offset++] = pixel[i];
                    }

                    if (flipRgb)
                    {
                        byte first = destination[start];
                        destination[start] = destination[start + 2];
                        destination[start + 2] = first;
                    }
                    break;
            }

            if (alphaDirection > 0)
                destination[offset++] = pixel[length - 1];

            return offset;
        }
    }
}