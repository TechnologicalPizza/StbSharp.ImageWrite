using System;

namespace StbSharp.ImageWrite
{
    public static unsafe class Gif
    {
        public static void Write<TImage>(WriteState state, TImage image)
            where TImage : IPixelRowProvider
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            throw new NotImplementedException();
        }


    }
}