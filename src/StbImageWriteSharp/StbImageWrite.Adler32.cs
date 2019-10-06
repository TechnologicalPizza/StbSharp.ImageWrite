using System;
using System.Threading;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe class Adler32 
        {
            // TODO: copy adler32 implementation from zlib library (as it's should be faster)
            public static uint Calculate(
                ReadOnlySpan<byte> data, CancellationToken cancellation)
            {
                uint s1 = 1;
                uint s2 = 0;

                int j = 0;
                int blocklen = data.Length % 5552;

                while (j < data.Length)
                {
                    cancellation.ThrowIfCancellationRequested();

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