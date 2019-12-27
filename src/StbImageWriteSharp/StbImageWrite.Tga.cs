using System;

namespace StbSharp
{
    public static partial class StbImageWrite
    {
        public static class Tga
        {
            public static int WriteCore(in WriteContext s, bool writeRLE)
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
                    object[] headers = new object[]
                    {
                        0, 0, format, 0, 0,
                        0, 0,
                        0, x, y, (colorbytes + hasAlpha) * 8, hasAlpha * 8
                    };
                    return WriteHelpers.Outfile(s, true, -1, false, hasAlpha, 0, "111 221 2222 11", headers);
                }
                else
                {
                    int i, j, k;
                    var headers = new object[]
                    {
                        0, 0, format + 8, 0, 0,
                        0,
                        0, 0, x, y, (colorbytes + hasAlpha) * 8,
                        hasAlpha * 8
                    };
                    WriteHelpers.WriteFormat(s, "111 221 2222 11", headers);

                    Span<byte> rowPixel = stackalloc byte[comp];
                    Span<byte> beginPixel = stackalloc byte[comp];
                    Span<byte> prevPixel = stackalloc byte[comp];

                    Span<byte> headerBuffer = stackalloc byte[1];
                    Span<byte> outputBuffer = stackalloc byte[4];
                    for (j = y - 1; j >= 0; --j)
                    {
                        int rowOffset = j * x * comp;

                        int len;
                        for (i = 0; i < x; i += len)
                        {
                            int beginOffset = rowOffset + i * comp;
                            s.ReadBytes(beginPixel, beginOffset);

                            int diff = 1;
                            len = 1;
                            if (i < (x - 1))
                            {
                                ++len;
                                s.ReadBytes(rowPixel, rowOffset + (i + 1) * comp);
                                diff = CRuntime.MemCompare(beginPixel, rowPixel, comp);
                                if (diff != 0)
                                {
                                    beginPixel.CopyTo(prevPixel);
                                    int prevOffset = beginOffset;

                                    for (k = i + 2; (k < x) && (len < 128); ++k)
                                    {
                                        s.ReadBytes(rowPixel, rowOffset + k * comp);
                                        if (CRuntime.MemCompare(prevPixel, rowPixel, comp) != 0)
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
                                    for (k = i + 2; (k < x) && (len < 128); ++k)
                                    {
                                        s.ReadBytes(rowPixel, rowOffset + k * comp);
                                        if (CRuntime.MemCompare(beginPixel, rowPixel, comp) == 0)
                                            ++len;
                                        else
                                            break;
                                    }
                                }
                            }

                            if (diff != 0)
                            {
                                headerBuffer[0] = (byte)((len - 1) & 0xff);
                                s.Write(s, headerBuffer);

                                for (k = 0; k < len; ++k)
                                {
                                    s.ReadBytes(beginPixel, beginOffset + k * comp);
                                    int pixlen = WriteHelpers.WritePixel(true, hasAlpha, false, beginPixel, outputBuffer);
                                    s.Write(s, outputBuffer.Slice(0, pixlen));
                                }
                            }
                            else
                            {
                                headerBuffer[0] = (byte)((len - 129) & 0xff);
                                s.Write(s, headerBuffer);

                                int pixlen = WriteHelpers.WritePixel(true, hasAlpha, false, beginPixel, outputBuffer);
                                s.Write(s, outputBuffer.Slice(0, pixlen));
                            }
                        }
                    }
                }

                return 1;
            }
        }
    }
}