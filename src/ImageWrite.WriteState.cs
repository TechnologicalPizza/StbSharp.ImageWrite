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
            public readonly GetPixelByteRowCallback GetByteRowCallback;
            public readonly GetPixelFloatRowCallback GetFloatRowCallback;

            public readonly WriteCallback WriteCallback;
            public readonly WriteProgressCallback ProgressCallback;

            public readonly int Width;
            public readonly int Height;
            public readonly int Components; // TODO: replace with bit masks or something similar

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
                CancellationToken cancellation,
                Memory<byte> scratchBuffer)
            {
                WriteCallback = writeCallback;
                GetByteRowCallback = getPixelByteRow;
                GetFloatRowCallback = getPixelFloatRow;
                ProgressCallback = progressCallback;

                Width = width;
                Height = height;
                Components = components;

                CancellationToken = cancellation;
                ScratchBuffer = scratchBuffer;
            }

            #endregion

            public void GetByteRow(int row, Span<byte> destination)
            {
                CancellationToken.ThrowIfCancellationRequested();
                GetByteRowCallback?.Invoke(row, destination);
            }

            public void GetFloatRow(int row, Span<float> destination)
            {
                CancellationToken.ThrowIfCancellationRequested();
                GetFloatRowCallback?.Invoke(row, destination);
            }

            public void Write(ReadOnlySpan<byte> data)
            {
                WriteCallback?.Invoke(data);
            }

            public void WriteByte(byte value)
            {
                Write(stackalloc[] { value });
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