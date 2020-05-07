using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public delegate void WriteProgressCallback(double progress);

        public abstract class WriteState : IAsyncDisposable
        {
            private byte[] _buffer;
            private int _bufferOffset;

            public Stream Stream { get; }
            public CancellationToken CancellationToken { get; }
            public WriteProgressCallback ProgressCallback { get; }

            public abstract int Width { get; }
            public abstract int Height { get; }
            public abstract int Components { get; }

            public WriteState(
                Stream stream,
                byte[] buffer,
                CancellationToken cancellationToken,
                WriteProgressCallback progressCallback)
            {
                _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                CancellationToken = cancellationToken;
                ProgressCallback = progressCallback;
            }

            public abstract void GetByteRow(int row, Span<byte> destination);

            public abstract void GetFloatRow(int row, Span<float> destination);

            public void Progress(double percentage)
            {
                ProgressCallback?.Invoke(percentage);
            }

            public async ValueTask Write(ReadOnlyMemory<byte> buffer)
            {
                if (_bufferOffset + buffer.Length > _buffer.Length)
                {
                    await TryFlush();
                    await Stream.WriteAsync(buffer, CancellationToken);
                }
                else
                {
                    buffer.CopyTo(_buffer.AsMemory(_bufferOffset));
                    _bufferOffset += buffer.Length;
                }
            }

            public async ValueTask WriteByte(byte value)
            {
                if (_bufferOffset + sizeof(byte) >= _buffer.Length)
                    await Flush();

                _buffer[_bufferOffset++] = value;
            }

            private async ValueTask Flush()
            {
                var slice = _buffer.AsMemory(0, _bufferOffset);
                await Stream.WriteAsync(slice, CancellationToken);
                _bufferOffset = 0;
            }

            public async ValueTask TryFlush()
            {
                if (_bufferOffset > 0)
                    await Flush();
            }

            public async ValueTask DisposeAsync()
            {
                await TryFlush();
            }
        }

        public class WriteState<TPixelRowProvider> : WriteState
            where TPixelRowProvider : IPixelRowProvider
        {
            public TPixelRowProvider PixelRowProvider { get; }

            public override int Width => PixelRowProvider.Width;
            public override int Height => PixelRowProvider.Height;

            // TODO: replace with bit masks or something similar
            public override int Components => PixelRowProvider.Components;

            #region Constructors

            public WriteState(
                Stream stream,
                byte[] buffer,
                CancellationToken cancellationToken,
                WriteProgressCallback progressCallback,
                TPixelRowProvider pixelRowProvider) :
                base(stream, buffer, cancellationToken, progressCallback)
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