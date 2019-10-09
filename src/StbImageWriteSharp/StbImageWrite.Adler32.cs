using System;

namespace StbSharp
{
    public static partial class StbImageWrite
    {
        public static class Adler32 
        {
            // TODO: copy adler32 implementation from zlib library (it should be faster)
            public static uint Calculate(ReadOnlySpan<byte> data)
            {
                uint s1 = 1;
                uint s2 = 0;

                int j = 0;
                int blocklen = data.Length % 5552;

                while (j < data.Length)
                {
                    for (int i = 0; i < blocklen; ++i)
                    {
                        s1 += data[j + i];
                        s2 += s1;
                    }
                    s1 %= 65521;
                    s2 %= 65521;
                    j += blocklen;
                    blocklen = 5552;
                }

                return (s2 << 16) | s1;
            }
        }
    }
}