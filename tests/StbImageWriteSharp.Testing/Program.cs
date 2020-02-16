using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using static StbSharp.ImageWrite;

namespace StbImageWriteSharp.Testing
{
    class Program
    {
        private static Random _rng = new Random();

        public static void Main(string[] args)
        {
            byte[] tmpBuffer = new byte[1024 * 80];

            var watch = new Stopwatch();
            watch.Start();
            using (var outputStream = new FileStream("out.png", FileMode.Create))
            {
                int width = 8000;
                int height = 2500;

                var context = new WriteContext(
                    readBytePixels: FillBytes,
                    readFloatPixels: null,
                    progressCallback: null,
                    width, height, components: 4,
                    outputStream, 
                    CancellationToken.None,
                    new ArraySegment<byte>(tmpBuffer, 0, 1024 * 40),
                    new ArraySegment<byte>(tmpBuffer, 1024 * 40, 1024 * 40));

                if (Png.WriteCore(context, System.IO.Compression.CompressionLevel.Optimal))
                {
                    Console.WriteLine("Write Successful");
                }
            }
            watch.Stop();

            Console.WriteLine(watch.ElapsedMilliseconds);
        }

        private static void FillBytes(Span<byte> dst, int dataOffset)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = (byte)(i % 4 == 3 ? 255 : _rng.Next(byte.MaxValue));
            }
        }
    }
}
