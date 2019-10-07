using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe class ZlibCompress
        {
            /// <summary>
            /// Delegate for a zlib deflate (RFC 1951) compression implementation.
            /// </summary>
            public delegate IMemoryResult DeflateCompressDelegate(
                ReadOnlySpan<byte> data, CompressionLevel level,
                CancellationToken cancellation, WriteProgressCallback onProgress);

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
                ReadOnlySpan<byte> data, CompressionLevel level,
                CancellationToken cancellation, WriteProgressCallback onProgress)
            {
                if (CustomDeflateCompress != null)
                    return CustomDeflateCompress.Invoke(data, level, cancellation, onProgress);

                cancellation.ThrowIfCancellationRequested();

                var header = ZlibHeader.CreateForDeflateStream(level);
                var output = new MemoryStream();
                output.WriteByte(header.GetCMF());
                output.WriteByte(header.GetFLG());

                byte[] copyBuffer = new byte[1024 * 8];
                fixed (byte* dataPtr = &MemoryMarshal.GetReference(data))
                {
                    cancellation.ThrowIfCancellationRequested();

                    using (var deflate = new DeflateStream(output, level, leaveOpen: true))
                    using (var source = new UnmanagedMemoryStream(dataPtr, data.Length))
                    {
                        // we don't want to use Stream.CopyTo as we want progress reporting
                        int total = 0;
                        int read;
                        while ((read = source.Read(copyBuffer, 0, copyBuffer.Length)) != 0)
                        {
                            cancellation.ThrowIfCancellationRequested();

                            deflate.Write(copyBuffer, 0, read);

                            total += read;
                            onProgress?.Invoke(total / (double)data.Length);
                        }
                    }
                }

                uint adlerSum = Adler32.Calculate(data, cancellation);
                byte[] adlerBytes = BitConverter.GetBytes(adlerSum);
                adlerBytes.AsSpan().Reverse();
                output.Write(adlerBytes, 0, adlerBytes.Length);

                byte[] result = output.GetBuffer();
                var gcHandle = GCHandle.Alloc(result, GCHandleType.Pinned);
                return new GCHandleResult(gcHandle, (int)output.Length);
            }
        }
    }
}