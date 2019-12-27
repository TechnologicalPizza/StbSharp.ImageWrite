using System;
using System.IO;
using System.Threading;

namespace StbSharp
{
    public static partial class StbImageWrite
    {
        public delegate void ReadBytePixelsCallback(Span<byte> destination, int dataOffset);
        public delegate void ReadFloatPixelsCallback(Span<float> destination, int dataOffset);

        public delegate int WriteCallback(in WriteContext context, ReadOnlySpan<byte> data);
        public delegate void WriteProgressCallback(double progress);

        public readonly struct WriteContext
        {
            public readonly ReadBytePixelsCallback ReadBytes;
            public readonly ReadFloatPixelsCallback ReadFloats;

            public readonly WriteCallback Write;
            public readonly WriteProgressCallback Progress;

            public readonly int Width;
            public readonly int Height;

            // TODO: replace with bit masks or something similar
            public readonly int Components;

            public readonly Stream Output;
            public readonly CancellationToken Cancellation;

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
                Write = writeCallback;
                ReadBytes = readBytePixels;
                ReadFloats = readFloatPixels;
                Progress = progressCallback;

                Width = width;
                Height = height;
                Components = components;

                Output = output;
                Cancellation = cancellation;
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
                CancellationToken cancellation,
                byte[] writeBuffer,
                byte[] scratchBuffer) :
                this(
                    readBytePixels, readFloatPixels, writeCallback, progressCallback,
                    width, height, components,
                    output, cancellation, 
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
                CancellationToken cancellation,
                ArraySegment<byte> writeBuffer,
                ArraySegment<byte> scratchBuffer) :
                this(
                    readBytePixels, readFloatPixels, DefaultWrite, progressCallback,
                    width, height, components,
                    output, cancellation, writeBuffer, scratchBuffer)
            {
            }

            #endregion

            public ScratchBuffer GetScratch(int minSize) => new ScratchBuffer(this, minSize);

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

        public unsafe ref struct ScratchBuffer
        {
            private byte* _ptr;
            private Span<byte> _span;

            public bool IsEmpty => _span.IsEmpty;

            public ScratchBuffer(in WriteContext ctx, int minSize)
            {
                if (minSize <= ctx.ScratchBuffer.Count)
                {
                    _ptr = null;
                    _span = ctx.ScratchBuffer.AsSpan(0, minSize);
                }
                else // allocate if the assigned buffer is too small
                {
                    _ptr = (byte*)CRuntime.MAlloc(minSize);
                    _span = new Span<byte>(_ptr, minSize);
                }
            }

            public Span<byte> AsSpan() => _span;
            public Span<byte> AsSpan(int start) => _span.Slice(start);
            public Span<byte> AsSpan(int start, int length) => _span.Slice(start, length);

            public void Dispose()
            {
                _span = Span<byte>.Empty;
                if (_ptr != null)
                {
                    CRuntime.Free(_ptr);
                    _ptr = null;
                }
            }
        }
    }
}