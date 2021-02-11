using System;
using System.IO;

namespace StbSharp.ImageWrite
{
    public delegate void WriteProgressCallback(double progress, Rect? rectangle);

    /// <summary>
    /// Acts as an output for encoded data.
    /// </summary>
    public class ImageBinWriter : IDisposable
    {
        private int _bufferOffset;

        public event WriteProgressCallback? Progress;

        public Stream Stream { get; private set; }
        public byte[] Buffer { get; private set; }

        public bool IsDisposed => Buffer == null;
        public bool HasProgressListener => Progress != null;

        public ImageBinWriter(Stream stream, byte[] buffer)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public void ReportProgress(double progress, Rect? rectangle)
        {
            Progress?.Invoke(progress, rectangle);
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            AssertNotDisposed();

            if (_bufferOffset + buffer.Length > Buffer.Length)
            {
                TryFlush();
                Stream.Write(buffer);
            }
            else
            {
                buffer.CopyTo(Buffer.AsSpan(_bufferOffset));
                _bufferOffset += buffer.Length;
            }
        }

        public void WriteByte(byte value)
        {
            AssertNotDisposed();

            if (_bufferOffset + sizeof(byte) >= Buffer.Length)
                Flush();

            Buffer[_bufferOffset++] = value;
        }

        private void Flush()
        {
            AssertNotDisposed();

            var slice = Buffer.AsSpan(0, _bufferOffset);
            if (slice.Length != 0)
            {
                Stream.Write(slice);
                _bufferOffset = 0;
            }
        }

        public void TryFlush()
        {
            AssertNotDisposed();

            if (_bufferOffset > 0)
                Flush();
        }

        public void AssertNotDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                TryFlush();

                Stream = null!;
                Buffer = null!;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}