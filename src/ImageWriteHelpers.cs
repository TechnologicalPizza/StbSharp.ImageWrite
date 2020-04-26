using System;
using System.Buffers.Binary;
using System.Globalization;
using static StbSharp.ImageWrite;

namespace StbSharp
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
        public static void WriteFormat(this in WriteState s, string format, ReadOnlySpan<long> values)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];

            int valueIndex = 0;
            for (int i = 0; i < format.Length; ++i)
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
        public static bool OutFile(this in WriteState s,
            bool flipRgb, int verticalDirection, bool expandMono, int alphaDirection, int pad,
            string format, ReadOnlySpan<long> values)
        {
            if (s.Width <= 0 || s.Height <= 0)
                return false;

            WriteFormat(s, format, values);
            WritePixels(s, flipRgb, verticalDirection, alphaDirection, pad, expandMono);
            return true;
        }

        public static void WriteUInt(uint value, Span<byte> destination, ref int position)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(position), value);
            position += sizeof(uint);
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

        public static void WritePixels(this in WriteState s,
            bool flipRgb, int verticalDirection, int alphaDirection, int scanlinePad, bool expandMono)
        {
            if (scanlinePad < 0 || scanlinePad > 4)
                throw new ArgumentOutOfRangeException(nameof(scanlinePad));

            Span<byte> scanlinePadSpan = stackalloc byte[scanlinePad];

            if (s.Height <= 0)
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

            ScratchBuffer scratch = s.GetScratch(stride);
            Span<byte> scanline = scratch.AsSpan(0, stride);
            
            for (; row != rowEnd; row += verticalDirection)
            {
                s.GetByteRow(row, scanline);

                int offset = 0;
                for (int i = 0; i < width; ++i)
                {
                    var pixel = scanline.Slice(i * comp, comp);
                    var output = scanline.Slice(offset, comp);
                    offset += WritePixel(flipRgb, alphaDirection, expandMono, pixel, output);
                }

                if (offset != stride)
                {
                    s.Write(scanline.Slice(0, offset));
                    s.Write(scanlinePadSpan);
                }
                else
                {
                    s.Write(scanline);
                }
            }
        }
    }
}