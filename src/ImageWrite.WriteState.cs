using System;
using System.IO;
using System.Threading;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public delegate void WriteProgressCallback(float progress, Rect? rectangle);

        /// <summary>
        /// Acts as a pixel source and output for encoded data.
        /// </summary>
        public abstract class WriteState : IDisposable
        {
            private byte[] _buffer;
            private int _bufferOffset;
            private bool _isDisposed;

            public Stream Stream { get; }
            public WriteProgressCallback? ProgressCallback { get; }
            public CancellationToken CancellationToken { get; }

            public abstract int Width { get; }
            public abstract int Height { get; }

            // TODO: make similar system to VectorComponentInfo
            public abstract int Depth { get; }
            public abstract int Components { get; }

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

            public abstract void GetByteRow(int row, Span<byte> destination);

            public abstract void GetFloatRow(int row, Span<float> destination);

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

        /// <summary>
        /// Acts as a wrapper <see cref="WriteState"/> for a <see cref="IPixelRowProvider"/>.
        /// </summary>
        /// <remarks>
        /// The class is sealed to allow for devirtualization of generics.
        /// </remarks>
        /// <typeparam name="TPixelRowProvider"></typeparam>
        public sealed class WriteState<TPixelRowProvider> : WriteState
            where TPixelRowProvider : IPixelRowProvider
        {
            public TPixelRowProvider PixelRowProvider { get; }

            public override int Width => PixelRowProvider.Width;
            public override int Height => PixelRowProvider.Height;

            // TODO: replace with bit masks or something similar
            public override int Depth => PixelRowProvider.Depth;
            public override int Components => PixelRowProvider.Components;

            #region Constructors

            public WriteState(
                Stream stream,
                byte[] buffer,
                TPixelRowProvider pixelRowProvider,
                WriteProgressCallback? progressCallback,
                CancellationToken cancellationToken) :
                base(stream, buffer, progressCallback, cancellationToken)
            {
                PixelRowProvider = pixelRowProvider;
            }

            #endregion

            public override void GetByteRow(int row, Span<byte> destination)
            {
                CancellationToken.ThrowIfCancellationRequested();
                PixelRowProvider.GetRow(row, destination);
            }

            public override void GetFloatRow(int row, Span<float> destination)
            {
                CancellationToken.ThrowIfCancellationRequested();
                PixelRowProvider.GetRow(row, destination);
            }
        }
    }
}