using System;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static class Tga
        {
            public static int WriteCore(in WriteState s, bool writeRLE)
            {
                int x = s.Width;
                int y = s.Height;
                int comp = s.Components;

                int hasAlpha = (comp == 2 || comp == 4) ? 1 : 0;
                int colorbytes = hasAlpha != 0 ? comp - 1 : comp;
                int format = colorbytes < 2 ? 3 : 2;
                if ((y < 0) || (x < 0))
                    return 0;

                if (!writeRLE)
                {
                    var headers = new long[]
                    {
                        0, 0, format,
                        0, 0, 0,
                        0, 0, x, y,
                        (colorbytes + hasAlpha) * 8, hasAlpha * 8
                    };
                    return ImageWriteHelpers.OutFile(s, true, -1, false, hasAlpha, 0, "111 221 2222 11", headers);
                }
                else
                {
                    var headers = new long[]
                    {
                        0, 0, format + 8,
                        0, 0, 0,
                        0, 0, x, y,
                        (colorbytes + hasAlpha) * 8, hasAlpha * 8
                    };
                    ImageWriteHelpers.WriteFormat(s, "111 221 2222 11", headers);

                    // TODO: read rows into a row buffer instead
                    Span<byte> rowPixel = stackalloc byte[comp];
                    Span<byte> beginPixel = stackalloc byte[comp];
                    Span<byte> prevPixel = stackalloc byte[comp];
                    Span<byte> outputBuffer = stackalloc byte[4];
                    
                    int k;
                    for (int row = y - 1; row >= 0; --row)
                    {
                        int rowOffset = row * x * comp;

                        int len;
                        for (int column = 0; column < x; column += len)
                        {
                            int beginOffset = rowOffset + column * comp;
                            s.ReadBytes(beginPixel, beginOffset);

                            int diff = 1;
                            len = 1;
                            if (column < (x - 1))
                            {
                                ++len;
                                s.ReadBytes(rowPixel, rowOffset + (column + 1) * comp);
                                diff = CRuntime.MemCompare<byte>(beginPixel, rowPixel, comp);
                                if (diff != 0)
                                {
                                    beginPixel.CopyTo(prevPixel);
                                    int prevOffset = beginOffset;

                                    for (k = column + 2; (k < x) && (len < 128); ++k)
                                    {
                                        s.ReadBytes(rowPixel, rowOffset + k * comp);
                                        if (CRuntime.MemCompare<byte>(prevPixel, rowPixel, comp) != 0)
                                        {
                                            s.ReadBytes(prevPixel, prevOffset);
                                            prevOffset += comp;
                                            ++len;
                                        }
                                        else
                                        {
                                            --len;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    for (k = column + 2; (k < x) && (len < 128); ++k)
                                    {
                                        s.ReadBytes(rowPixel, rowOffset + k * comp);
                                        if (CRuntime.MemCompare<byte>(beginPixel, rowPixel, comp) == 0)
                                            ++len;
                                        else
                                            break;
                                    }
                                }
                            }

                            if (diff != 0)
                            {
                                s.WriteByte((byte)((len - 1) & 0xff));

                                for (k = 0; k < len; ++k)
                                {
                                    s.ReadBytes(beginPixel, beginOffset + k * comp);
                                    int pixlen = ImageWriteHelpers.WritePixel(true, hasAlpha, false, beginPixel, outputBuffer);
                                    s.Write(outputBuffer.Slice(0, pixlen));
                                }
                            }
                            else
                            {
                                s.WriteByte((byte)((len - 129) & 0xff));

                                int pixlen = ImageWriteHelpers.WritePixel(true, hasAlpha, false, beginPixel, outputBuffer);
                                s.Write(outputBuffer.Slice(0, pixlen));
                            }
                        }
                    }
                }

                return 1;
            }
        }
    }
}