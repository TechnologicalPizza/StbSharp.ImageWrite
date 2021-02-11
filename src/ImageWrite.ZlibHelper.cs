using System.IO;
using System.IO.Compression;

namespace StbSharp.ImageWrite
{
    public static class ZlibHelper
    {
        /// <summary>
        /// Delegate for a wrapping a <see cref="Stream"/> in a zlib deflate (RFC 1951) compressor.
        /// </summary>
        public delegate Stream DeflateCompressorFactory(Stream input, CompressionLevel compressionLevel, bool leaveOpen);

        /// <summary>
        /// Compresses data using a <see cref="DeflateStream"/>.
        /// </summary>
        /// <param name="output">The stream to output compressed data to.</param>
        /// <param name="deflateCompressorFactory">
        /// Custom zlib deflate (RFC 1951) compressor factory that replaces the default.
        /// </param>
        public static Stream CreateCompressor(
            Stream output, bool leaveOpen, CompressionLevel compressionLevel, DeflateCompressorFactory? deflateCompressorFactory)
        {
            if (deflateCompressorFactory != null)
                return deflateCompressorFactory.Invoke(output, compressionLevel, leaveOpen);

            return new ZlibStream(output, compressionLevel, leaveOpen);
        }
    }
}