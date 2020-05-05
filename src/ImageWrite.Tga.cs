using System;
using System.Threading.Tasks;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Tga
        {
            public static async Task Write(WriteState s, bool useRLE)
            {
                int width = s.Width;
                int height = s.Height;
                int comp = s.Components;

                if ((height < 0) || (width < 0))
                    throw new ArgumentException("Invalid image dimensions.", nameof(s));

                int hasAlpha = (comp == 2 || comp == 4) ? 1 : 0;
                int colorbytes = hasAlpha != 0 ? comp - 1 : comp;
                int format = colorbytes < 2 ? 3 : 2;

                if (!useRLE)
                {
                    var headers = new long[]
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

                    await ImageWriteHelpers.OutFile(
                        s, true, -1, false, hasAlpha, 0, "111 221 2222 11", headers);
                }
                else
                {
                    var headers = new long[]
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
                    await ImageWriteHelpers.WriteFormat(s, "111 221 2222 11", headers);

                    var rowScratch = new byte[width * comp].AsMemory();
                    var outBuffer = new byte[4];

                    for (int y = height; y-- > 0;)
                    {
                        s.GetByteRow(y, rowScratch.Span);

                        int len;
                        for (int x = 0; x < width; x += len)
                        {
                            var begin = rowScratch.Slice(x * comp);

                            int diff = 1;
                            len = 1;
                            if (x < (width - 1))
                            {
                                len++;
                                var next = rowScratch.Slice((x + 1) * comp);
                                diff = CRuntime.MemCompare<byte>(begin.Span, next.Span, comp);
                                if (diff != 0)
                                {
                                    var prev = begin;
                                    for (int k = x + 2; (k < width) && (len < 128); ++k)
                                    {
                                        var pixel = rowScratch.Slice(k * comp);
                                        if (CRuntime.MemCompare<byte>(prev.Span, pixel.Span, comp) != 0)
                                        {
                                            prev = prev.Slice(comp);
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
                                        var pixel = rowScratch.Slice(k * comp);
                                        if (CRuntime.MemCompare<byte>(begin.Span, pixel.Span, comp) == 0)
                                            len++;
                                        else
                                            break;
                                    }
                                }
                            }

                            if (diff != 0)
                            {
                                await s.WriteByte((byte)((len - 1) & 0xff));

                                for (int k = 0; k < len; k++)
                                {
                                    var pixel = begin.Slice(k * comp, comp);
                                    int count = ImageWriteHelpers.WritePixel(
                                        true, hasAlpha, false, pixel.Span, outBuffer);

                                    await s.Write(outBuffer.AsMemory(0, count));
                                }
                            }
                            else
                            {
                                await s.WriteByte((byte)((len - 129) & 0xff));

                                var pixel = begin.Slice(0, comp);
                                int count = ImageWriteHelpers.WritePixel(
                                    true, hasAlpha, false, pixel.Span, outBuffer);

                                await s.Write(outBuffer.AsMemory(0, count));
                            }
                        }
                    }
                }
            }
        }
    }
}