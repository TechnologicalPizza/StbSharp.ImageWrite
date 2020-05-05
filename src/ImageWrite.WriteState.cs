using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public delegate void GetPixelByteRowCallback(int row, Span<byte> destination);
        public delegate void GetPixelFloatRowCallback(int row, Span<float> destination);
        public delegate void WriteProgressCallback(double progress);

        public class WriteState
        {
            // TODO: make this into a buffering writer

            private byte[] _byteBuffer = new byte[1];

            public Stream Stream { get; }
            public CancellationToken CancellationToken { get; }

            public GetPixelByteRowCallback GetByteRowCallback { get; }
            public GetPixelFloatRowCallback GetFloatRowCallback { get; }
            public WriteProgressCallback ProgressCallback { get; }

            public int Width { get; }
            public int Height { get; }
            public int Components { get; } // TODO: replace with bit masks or something similar

            #region Constructors

            public WriteState(
                Stream stream,
                CancellationToken cancellationToken,
                GetPixelByteRowCallback getPixelByteRow,
                GetPixelFloatRowCallback getPixelFloatRow,
                WriteProgressCallback progressCallback,
                int width,
                int height,
                int components)
            {
                Stream = stream;
                CancellationToken = cancellationToken;

                GetByteRowCallback = getPixelByteRow;
                GetFloatRowCallback = getPixelFloatRow;
                ProgressCallback = progressCallback;

                Width = width;
                Height = height;
                Components = components;
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

            public ValueTask Write(ReadOnlyMemory<byte> buffer)
            {
                return Stream.WriteAsync(buffer, CancellationToken);
            }

            public ValueTask WriteByte(byte value)
            {
                _byteBuffer[0] = value;
                return Write(_byteBuffer);
            }

            public void Progress(double percentage)
            {
                ProgressCallback?.Invoke(percentage);
            }
        }
    }
}