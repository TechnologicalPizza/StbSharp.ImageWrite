using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace StbSharp
{
    public static partial class StbImageWrite
    {
        public static class ZlibCompress
        {
            /// <summary>
            /// Delegate for a zlib deflate (RFC 1951) compression implementation.
            /// </summary>
            public delegate IMemoryResult DeflateCompressDelegate(
                ReadOnlySpan<byte> data,
                CompressionLevel level,
                CancellationToken cancellationToken,
                WriteProgressCallback onProgress = null);

            /// <summary>
            /// Custom zlib deflate (RFC 1951) compression implementation 
            /// that replaces the default <see cref="DeflateCompress"/>.
            /// </summary>
            public static DeflateCompressDelegate CustomDeflateCompress;

            /// <summary>
            /// Compresses data using a <see cref="DeflateStream"/> and
            /// adds zlib (RFC 1951) headers and checksum.
            /// <para>Can be replaced by assigning <see cref="CustomDeflateCompress"/>.</para>
            /// </summary>
            public static IMemoryResult DeflateCompress(
                ReadOnlySpan<byte> data,
                CompressionLevel level,
                CancellationToken cancellationToken,
                WriteProgressCallback onProgress = null)
            {
                if (CustomDeflateCompress != null)
                    return CustomDeflateCompress.Invoke(data, level, cancellationToken, onProgress);

                cancellationToken.ThrowIfCancellationRequested();

                var header = ZlibHeader.CreateForDeflateStream(level);
                var output = new MemoryStream();
                output.WriteByte(header.GetCMF());
                output.WriteByte(header.GetFLG());

                byte[] copyBuffer = new byte[1024 * 8];
                cancellationToken.ThrowIfCancellationRequested();

                using (var deflate = new DeflateStream(output, level, leaveOpen: true))
                {
                    int totalRead = 0;
                    while (totalRead < data.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int count = Math.Min(data.Length - totalRead, copyBuffer.Length);
                        data.Slice(totalRead, count).CopyTo(copyBuffer);
                        deflate.Write(copyBuffer, 0, count);

                        totalRead += count;
                        onProgress?.Invoke(totalRead / (double)data.Length);
                    }
                }

                uint adlerSum = Adler32.Calculate(data);
                byte[] adlerSumBytes = new byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(adlerSumBytes, adlerSum);
                output.Write(adlerSumBytes, 0, adlerSumBytes.Length);

                cancellationToken.ThrowIfCancellationRequested();

                byte[] result = output.GetBuffer();
                var gcHandle = GCHandle.Alloc(result, GCHandleType.Pinned);
                return new GCHandleResult(gcHandle, (int)output.Length);
            }
        }
    }
}