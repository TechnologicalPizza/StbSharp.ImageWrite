/*
using System;
using System.IO;

namespace StbImageWriteSharp
{
    public unsafe class ImageWriter
    {
        private void CheckParams(byte[] data, int width, int height, ColorComponents components)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (width <= 0)
                throw new ArgumentOutOfRangeException("width");

            if (height <= 0)
                throw new ArgumentOutOfRangeException("height");

            int requiredDataSize = width * height * (int)components;
            if (data.Length < requiredDataSize)
            {
                throw new ArgumentException(
                    string.Format("Not enough data. 'data' variable should contain at least {0} bytes.", requiredDataSize));
            }
        }

        public void WriteHdr(byte[] data, int width, int height, ColorComponents components, Stream dest)
        {
            CheckParams(data, width, height, components);

            var f = new float[data.Length];
            for (var i = 0; i < data.Length; ++i)
            {
                f[i] = data[i] / 255.0f;
            }

            fixed (float* fptr = f)
            {
                StbImageWrite.stbi_write_hdr_to_func(WriteCallback, null, width, height, (int)components, fptr);
            }
        }
    }
}
*/