using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace StbSharp.ImageWrite
{
    public static class ZlibHelper
    {
        /// <summary>
        /// Delegate for a wrapping a <see cref="Stream"/> in a zlib deflate (RFC 1951) compressor.
        /// </summary>
        public delegate Stream DeflateCompressorDelegate(Stream input, CompressionLevel level, bool leaveOpen);

        /// <summary>
        /// Custom zlib deflate (RFC 1951) compressor implementation 
        /// that replaces the default <see cref="DeflateCompress"/>.
        /// </summary>
        public static DeflateCompressorDelegate? CustomDeflateCompressor { get; set; }

        /// <summary>
        /// Compresses data using a <see cref="DeflateStream"/>.
        /// <para>Can be replaced by assigning <see cref="CustomDeflateCompressor"/>.</para>
        /// </summary>
        public static Stream CreateCompressor(Stream stream, CompressionLevel level, bool leaveOpen)
        {
            if (CustomDeflateCompressor != null)
                return CustomDeflateCompressor.Invoke(stream, level, leaveOpen);

            return new ZlibStream(stream, level, leaveOpen);
        }
    }

    public class ZlibStream : Stream
    {
        private DeflateStream _deflater;
		private bool _leaveOpen;
        private uint _adlerChecksum;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _deflater?.CanWrite ?? false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
		
		public Stream BaseStream => _deflater.BaseStream;

        public ZlibStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen)
        {
            _deflater = new DeflateStream(stream, compressionLevel, leaveOpen: true);
            _leaveOpen = leaveOpen;

            var header = ZlibHeader.CreateForDeflateStream(compressionLevel);
            _deflater.BaseStream.Write(stackalloc byte[] {
                header.GetCMF(),
                header.GetFLG()
            });

            _adlerChecksum = 1;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _deflater.Write(buffer);
            _adlerChecksum = Adler32.Calculate(buffer, _adlerChecksum);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _deflater.Write(buffer, offset, count);
            _adlerChecksum = Adler32.Calculate(buffer.AsSpan(offset, count), _adlerChecksum);
        }

        public override void WriteByte(byte value)
        {
            _deflater.WriteByte(value);
            _adlerChecksum = Adler32.Calculate(stackalloc byte[] { value }, _adlerChecksum);
        }

        public override void Flush()
        {
            _deflater.Flush();
        }

        public override int Read(Span<byte> buffer) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int ReadByte() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (_deflate != null)
            {
                var baseStream = BaseStream;
                _deflate.Dispose();
                _deflate = null!;

                Span<byte> checksumBytes = stackalloc byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(checksumBytes, _adlerChecksum);
                baseStream.Write(checksumBytes);

                if (!_leaveOpen)
                    baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}