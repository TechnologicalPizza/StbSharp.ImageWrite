using System;
using System.IO;
using System.Threading;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public delegate void GetPixelByteRowCallback(int row, Span<byte> destination);
        public delegate void GetPixelFloatRowCallback(int row, Span<float> destination);

        public delegate void WriteCallback(ReadOnlySpan<byte> data);
        public delegate void WriteProgressCallback(double progress);

        public readonly struct WriteState
        {
            public readonly GetPixelByteRowCallback GetByteRow;
            public readonly GetPixelFloatRowCallback GetFloatRow;

            public readonly WriteCallback WriteCallback;
            public readonly WriteProgressCallback ProgressCallback;

            public readonly int Width;
            public readonly int Height;
            public readonly int Components; // TODO: replace with bit masks or something similar

            public readonly Stream Output;
            public readonly CancellationToken CancellationToken;
            public readonly Memory<byte> ScratchBuffer;

            #region Constructors

            public WriteState(
                GetPixelByteRowCallback getPixelByteRow,
                GetPixelFloatRowCallback getPixelFloatRow,
                WriteCallback writeCallback,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int components,
                Stream output,
                CancellationToken cancellation,
                Memory<byte> scratchBuffer)
            {
                WriteCallback = writeCallback;
                GetByteRow = getPixelByteRow;
                GetFloatRow = getPixelFloatRow;
                ProgressCallback = progressCallback;

                Width = width;
                Height = height;
                Components = components;

                Output = output;
                CancellationToken = cancellation;
                ScratchBuffer = scratchBuffer;
            }

            public WriteState(
                GetPixelByteRowCallback getPixelByteRow,
                GetPixelFloatRowCallback getPixelFloatRow,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int components,
                Stream output,
                CancellationToken cancellationToken,
                Memory<byte> scratchBuffer) :
                this(
                    getPixelByteRow, getPixelFloatRow, (d) => output.Write(d), progressCallback,
                    width, height, components,
                    output, cancellationToken, scratchBuffer)
            {
            }

            #endregion

            public void Write(ReadOnlySpan<byte> data)
            {
                WriteCallback?.Invoke(data);
            }

            public void Progress(double percentage)
            {
                ProgressCallback?.Invoke(percentage);
            }

            public ScratchBuffer GetScratch(int minSize)
            {
                return new ScratchBuffer(ScratchBuffer.Span, minSize);
            }
        }
    }
}