using System;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe class Tga
        {
            public static int WriteCore(in WriteContext s, bool writeRLE)
            {
                int x = s.Width;
                int y = s.Height;
                int comp = s.Comp;

                int has_alpha = (comp == 2 || comp == 4) ? 1 : 0;
                int colorbytes = has_alpha != 0 ? comp - 1 : comp;
                int format = colorbytes < 2 ? 3 : 2;
                if ((y < 0) || (x < 0))
                    return 0;

                if (!writeRLE)
                {
                    object[] headers = new object[]
                    {
                        0, 0, format, 0, 0,
                        0, 0,
                        0, x, y, (colorbytes + has_alpha) * 8, has_alpha * 8
                    };
                    return WriteHelpers.Outfile(s, true, -1, false, has_alpha, 0, "111 221 2222 11", headers);
                }
                else
                {
                    int i, j, k;
                    var headers = new object[]
                    {
                        0, 0, format + 8, 0, 0,
                        0,
                        0, 0, x, y, (colorbytes + has_alpha) * 8,
                        has_alpha * 8
                    };
                    WriteHelpers.WriteFormat(s, "111 221 2222 11", headers);

                    byte* rowPixel = stackalloc byte[comp];
                    byte* beginPixel = stackalloc byte[comp];
                    byte* prevPixel = stackalloc byte[comp];

                    var rowPixelSpan = new Span<byte>(rowPixel, comp);
                    var beginPixelSpan = new Span<byte>(beginPixel, comp);
                    var prevPixelSpan = new Span<byte>(prevPixel, comp);

                    Span<byte> headerBuffer = stackalloc byte[1];
                    Span<byte> outputBuffer = stackalloc byte[4];
                    for (j = y - 1; j >= 0; --j)
                    {
                        int rowOffset = j * x * comp;

                        int len;
                        for (i = 0; i < x; i += len)
                        {
                            int beginOffset = rowOffset + i * comp;
                            s.ReadBytes(beginPixelSpan, beginOffset);

                            int diff = 1;
                            len = 1;
                            if (i < (x - 1))
                            {
                                ++len;
                                s.ReadBytes(rowPixelSpan, rowOffset + (i + 1) * comp);
                                diff = CRuntime.memcmp(beginPixel, rowPixel, (ulong)comp);
                                if (diff != 0)
                                {
                                    beginPixelSpan.CopyTo(prevPixelSpan);
                                    int prevOffset = beginOffset;

                                    for (k = i + 2; (k < x) && (len < 128); ++k)
                                    {
                                        s.ReadBytes(rowPixelSpan, rowOffset + k * comp);
                                        if (CRuntime.memcmp(prevPixel, rowPixel, (ulong)comp) != 0)
                                        {
                                            s.ReadBytes(prevPixelSpan, prevOffset);
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
                                        s.ReadBytes(rowPixelSpan, rowOffset + k * comp);
                                        if (CRuntime.memcmp(beginPixel, rowPixel, (ulong)comp) == 0)
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
                                    s.ReadBytes(beginPixelSpan, beginOffset + k * comp);
                                    int pixlen = WriteHelpers.WritePixel(true, has_alpha, false, beginPixelSpan, outputBuffer);
                                    s.Write(s, outputBuffer.Slice(0, pixlen));
                                }
                            }
                            else
                            {
                                headerBuffer[0] = (byte)((len - 129) & 0xff);
                                s.Write(s, headerBuffer);

                                int pixlen = WriteHelpers.WritePixel(true, has_alpha, false, beginPixelSpan, outputBuffer);
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