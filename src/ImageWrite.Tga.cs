using System;
using System.Runtime.CompilerServices;

namespace StbSharp.ImageWrite
{
    [SkipLocalsInit]
    public static class Tga
    {
        public static void Write<TImage>(ImageBinWriter state, TImage image, bool useRLE)
            where TImage : IPixelRowProvider
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            int width = image.Width;
            int height = image.Height;
            int comp = image.Components;

            if ((height < 0) || (width < 0))
                throw new ArgumentException("Invalid image dimensions.", nameof(state));

            image.ThrowIfCancelled();

            int hasAlpha = (comp == 2 || comp == 4) ? 1 : 0;
            int colorbytes = hasAlpha != 0 ? comp - 1 : comp;
            int format = colorbytes < 2 ? 3 : 2;

            if (!useRLE)
            {
                Span<long> headerValues = stackalloc long[]
                {
                    0,
                    0,
                    format,
                    0,
                    0,
                    0,
                    0,
                    0,
                    width,
                    height,
                    (colorbytes + hasAlpha) * 8,
                    hasAlpha * 8
                };

                ImageWriteHelpers.OutFile(
                    state, image, true, -1, false, hasAlpha, 0, "111 221 2222 11", headerValues);
            }
            else
            {
                Span<long> headerValues = stackalloc long[]
                {
                    0,
                    0,
                    format + 8,
                    0,
                    0,
                    0,
                    0,
                    0,
                    width,
                    height,
                    (colorbytes + hasAlpha) * 8,
                    hasAlpha * 8
                };
                ImageWriteHelpers.WriteFormat(state, "111 221 2222 11", headerValues);

                Span<byte> rowScratch = new byte[width * comp].AsSpan();
                Span<byte> outBuffer = stackalloc byte[4];

                for (int y = height; y-- > 0;)
                {
                    image.GetByteRow(y, rowScratch);

                    int len;
                    for (int x = 0; x < width; x += len)
                    {
                        Span<byte> begin = rowScratch[(x * comp)..];

                        // TODO: optimize

                        bool diff = false;
                        len = 1;
                        if (x < (width - 1))
                        {
                            len++;
                            Span<byte> next = rowScratch[((x + 1) * comp)..];
                            diff = begin.Slice(0, comp).SequenceEqual(next.Slice(0, comp));
                            if (!diff)
                            {
                                Span<byte> prev = begin;
                                for (int k = x + 2; (k < width) && (len < 128); ++k)
                                {
                                    Span<byte> pixel = rowScratch[(k * comp)..];
                                    if (!prev.Slice(0, comp).SequenceEqual(pixel.Slice(0, comp)))
                                    {
                                        prev = prev[comp..];
                                        len++;
                                    }
                                    else
                                    {
                                        len--;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                for (int k = x + 2; (k < width) && (len < 128); ++k)
                                {
                                    Span<byte> pixel = rowScratch[(k * comp)..];
                                    if (begin.Slice(0, comp).SequenceEqual(pixel.Slice(0, comp)))
                                        len++;
                                    else
                                        break;
                                }
                            }
                        }

                        if (!diff)
                        {
                            state.WriteByte((byte)((len - 1) & 0xff));

                            for (int k = 0; k < len; k++)
                            {
                                Span<byte> pixel = begin.Slice(k * comp, comp);
                                int count = ImageWriteHelpers.WritePixel(
                                    true, hasAlpha, false, pixel, outBuffer);

                                state.Write(outBuffer.Slice(0, count));
                            }
                        }
                        else
                        {
                            state.WriteByte((byte)((len - 129) & 0xff));

                            Span<byte> pixel = begin.Slice(0, comp);
                            int count = ImageWriteHelpers.WritePixel(
                                true, hasAlpha, false, pixel, outBuffer);

                            state.Write(outBuffer.Slice(0, count));
                        }
                    }
                }
            }
        }
    }
}