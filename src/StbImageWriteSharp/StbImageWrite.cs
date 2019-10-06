using System;
using System.IO;
using System.Threading;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
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
            public readonly int Comp;

            public readonly Stream Output;
            public readonly CancellationToken Cancellation;
            public readonly byte[] WriteBuffer;
            public readonly byte[] ScratchBuffer;

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                WriteCallback writeCallback,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int comp,
                Stream output,
                CancellationToken cancellation,
                byte[] writeBuffer,
                byte[] scratchBuffer)
            {
                Write = writeCallback;
                ReadBytes = readBytePixels;
                ReadFloats = readFloatPixels;
                Progress = progressCallback;

                Width = width;
                Height = height;
                Comp = comp;

                Output = output;
                Cancellation = cancellation;
                WriteBuffer = writeBuffer;
                ScratchBuffer = scratchBuffer;
            }

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int comp,
                Stream output,
                CancellationToken cancellation,
                byte[] writeBuffer,
                byte[] scratchBuffer) :
                this(
                    readBytePixels, readFloatPixels, DefaultWrite, progressCallback,
                    width, height, comp,
                    output, cancellation, writeBuffer, scratchBuffer)
            {
            }

            public ScratchBuffer GetScratch(int minSize)
            {
                return new ScratchBuffer(this, minSize);
            }

            public static int DefaultWrite(in WriteContext context, ReadOnlySpan<byte> data)
            {
                if (data.IsEmpty)
                    return 0;

                byte[] buffer = context.WriteBuffer;
                int left = data.Length;
                int offset = 0;
                while (left > 0)
                {
                    int sliceLength = Math.Min(left, buffer.Length);
                    for (int i = 0; i < sliceLength; i++)
                        buffer[i] = data[i + offset];
                    context.Output.Write(buffer, 0, sliceLength);

                    left -= sliceLength;
                    offset += sliceLength;
                }
                return data.Length;
            }
        }

        public ref struct ScratchBuffer
        {
            private byte* _ptr;
            private Span<byte> _span;

            public bool IsEmpty => _span.IsEmpty;

            public ScratchBuffer(in WriteContext ctx, int minSize)
            {
                if (minSize <= ctx.ScratchBuffer.Length)
                {
                    _ptr = null;
                    _span = ctx.ScratchBuffer.AsSpan(0, minSize);
                }
                else // allocate if the assigned buffer is too small
                {
                    _ptr = (byte*)CRuntime.malloc(minSize);
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
                    CRuntime.free(_ptr);
                    _ptr = null;
                }
            }
        }
    }
}