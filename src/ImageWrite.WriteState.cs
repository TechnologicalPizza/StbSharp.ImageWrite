using System;
using System.IO;
using System.Threading;

namespace StbSharp.ImageWrite
{
    public delegate void WriteProgressCallback(float progress, Rect? rectangle);

    /// <summary>
    /// Acts as an output for encoded data.
    /// </summary>
    public class WriteState : IDisposable
    {
        private byte[] _buffer;
        private int _bufferOffset;
        private bool _isDisposed;

        public Stream Stream { get; }
        public WriteProgressCallback? ProgressCallback { get; }
        public CancellationToken CancellationToken { get; }

        public WriteState(
            Stream stream,
            byte[] buffer,
            WriteProgressCallback? progressCallback,
            CancellationToken cancellationToken)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            CancellationToken = cancellationToken;
            ProgressCallback = progressCallback;
        }

        public void ThrowIfCancelled()
        {
            CancellationToken.ThrowIfCancellationRequested();
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            if (_bufferOffset + buffer.Length > _buffer.Length)
            {
                TryFlush();
                Stream.Write(buffer);
            }
            else
            {
                buffer.CopyTo(_buffer.AsSpan(_bufferOffset));
                _bufferOffset += buffer.Length;
            }
        }

        public void WriteByte(byte value)
        {
            if (_bufferOffset + sizeof(byte) >= _buffer.Length)
                Flush();

            _buffer[_bufferOffset++] = value;
        }

        private void Flush()
        {
            var slice = _buffer.AsSpan(0, _bufferOffset);
            Stream.Write(slice);
            _bufferOffset = 0;
        }

        public void TryFlush()
        {
            if (_bufferOffset > 0)
                Flush();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                TryFlush();

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}