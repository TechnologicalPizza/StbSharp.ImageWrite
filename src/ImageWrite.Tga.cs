using System;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Tga
        {
            public static bool WriteCore(in WriteState s, bool useRLE)
            {
                int width = s.Width;
                int height = s.Height;
                int comp = s.Components;
                
                if ((height < 0) || (width < 0))
                    return false;

                int hasAlpha = (comp == 2 || comp == 4) ? 1 : 0;
                int colorbytes = hasAlpha != 0 ? comp - 1 : comp;
                int format = colorbytes < 2 ? 3 : 2;
                
                if (!useRLE)
                {
                    ReadOnlySpan<long> headers = stackalloc long[]
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

                    return ImageWriteHelpers.OutFile(
                        s, true, -1, false, hasAlpha, 0, "111 221 2222 11", headers);
                }
                else
                {
                    ReadOnlySpan<long> headers = stackalloc long[]
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
                    ImageWriteHelpers.WriteFormat(s, "111 221 2222 11", headers);

                    var rowScratch = s.GetScratch(width * comp);
                    var rowSpan = rowScratch.AsSpan();
                    Span<byte> outBuffer = stackalloc byte[4];

                    for (int y = height; y-- > 0;)
                    {
                        s.GetByteRow(y, rowSpan);

                        int len;
                        for (int x = 0; x < width; x += len)
                        {
                            var begin = rowSpan.Slice(x * comp);

                            int diff = 1;
                            len = 1;
                            if (x < (width - 1))
                            {
                                len++;
                                var next = rowSpan.Slice((x + 1) * comp);
                                diff = CRuntime.MemCompare<byte>(begin, next, comp);
                                if (diff != 0)
                                {
                                    var prev = begin;
                                    for (int k = x + 2; (k < width) && (len < 128); ++k)
                                    {
                                        var pixel = rowSpan.Slice(k * comp);
                                        if (CRuntime.MemCompare<byte>(prev, pixel, comp) != 0)
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
                                        var pixel = rowSpan.Slice(k * comp);
                                        if (CRuntime.MemCompare<byte>(begin, pixel, comp) == 0)
                                            len++;
                                        else
                                            break;
                                    }
                                }
                            }

                            if (diff != 0)
                            {
                                s.WriteByte((byte)((len - 1) & 0xff));

                                for (int k = 0; k < len; k++)
                                {
                                    var pixel = begin.Slice(k * comp, comp);
                                    int count = ImageWriteHelpers.WritePixel(true, hasAlpha, false, pixel, outBuffer);
                                    s.Write(outBuffer.Slice(0, count));
                                }
                            }
                            else
                            {
                                s.WriteByte((byte)((len - 129) & 0xff));

                                var pixel = begin.Slice(0, comp);
                                int count = ImageWriteHelpers.WritePixel(true, hasAlpha, false, pixel, outBuffer);
                                s.Write(outBuffer.Slice(0, count));
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}