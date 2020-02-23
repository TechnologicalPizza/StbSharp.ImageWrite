using System;
using System.IO;
using System.Threading;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public delegate void ReadBytePixelsCallback(Span<byte> destination, int dataOffset);
        public delegate void ReadFloatPixelsCallback(Span<float> destination, int dataOffset);

        public delegate int WriteCallback(in WriteContext context, ReadOnlySpan<byte> data);
        public delegate void WriteProgressCallback(double progress);

        public readonly struct WriteContext
        {
            public readonly ReadBytePixelsCallback ReadBytes;
            public readonly ReadFloatPixelsCallback ReadFloats;

            public readonly WriteCallback WriteCallback;
            public readonly WriteProgressCallback ProgressCallback;

            public readonly int Width;
            public readonly int Height;

            // TODO: replace with bit masks or something similar
            public readonly int Components;

            public readonly Stream Output;
            public readonly CancellationToken CancellationToken;

            public readonly ArraySegment<byte> WriteBuffer;
            public readonly ArraySegment<byte> ScratchBuffer;

            #region Constructors

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                WriteCallback writeCallback,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int components,
                Stream output,
                CancellationToken cancellation,
                ArraySegment<byte> writeBuffer,
                ArraySegment<byte> scratchBuffer)
            {
                WriteCallback = writeCallback;
                ReadBytes = readBytePixels;
                ReadFloats = readFloatPixels;
                ProgressCallback = progressCallback;

                Width = width;
                Height = height;
                Components = components;

                Output = output;
                CancellationToken = cancellation;
                WriteBuffer = writeBuffer;
                ScratchBuffer = scratchBuffer;
            }

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                WriteCallback writeCallback,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int components,
                Stream output,
                CancellationToken cancellationToken,
                byte[] writeBuffer,
                byte[] scratchBuffer) :
                this(
                    readBytePixels, readFloatPixels, writeCallback, progressCallback,
                    width, height, components,
                    output, cancellationToken,
                    new ArraySegment<byte>(writeBuffer),
                    new ArraySegment<byte>(scratchBuffer))
            {
            }

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int components,
                Stream output,
                CancellationToken cancellationToken,
                ArraySegment<byte> writeBuffer,
                ArraySegment<byte> scratchBuffer) :
                this(
                    readBytePixels, readFloatPixels, DefaultWrite, progressCallback,
                    width, height, components,
                    output, cancellationToken, writeBuffer, scratchBuffer)
            {
            }

            #endregion

            public int Write(ReadOnlySpan<byte> data)
            {
                if (WriteCallback == null)
                    return data.Length;
                return WriteCallback(this, data);
            }

            public void Progress(double percentage)
            {
                ProgressCallback?.Invoke(percentage);
            }

            public ScratchBuffer GetScratch(int minSize)
            {
                return new ScratchBuffer(ScratchBuffer, minSize);
            }

            public static int DefaultWrite(in WriteContext context, ReadOnlySpan<byte> data)
            {
                if (data.IsEmpty)
                    return 0;

                var bufferSegment = context.WriteBuffer;
                var bufferArray = bufferSegment.Array;

                int left = data.Length;
                int offset = 0;
                while (left > 0)
                {
                    int sliceLength = Math.Min(left, bufferSegment.Count);
                    for (int i = 0; i < sliceLength; i++)
                        bufferArray[i + bufferSegment.Offset] = data[i + offset];
                    context.Output.Write(bufferArray, bufferSegment.Offset, sliceLength);

                    left -= sliceLength;
                    offset += sliceLength;
                }
                return data.Length;
            }
        }
    }
}