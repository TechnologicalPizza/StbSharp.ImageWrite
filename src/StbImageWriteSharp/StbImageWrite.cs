using System;
using System.IO;
using System.Text;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public delegate int WriteCallback(in WriteContext context, ReadOnlySpan<byte> data);
        public delegate void ReadBytePixelsCallback(Span<byte> destination, int dataOffset);
        public delegate void ReadFloatPixelsCallback(Span<float> destination, int dataOffset);

        public readonly struct WriteContext
        {
            public readonly ReadBytePixelsCallback ReadBytes;
            public readonly ReadFloatPixelsCallback ReadFloats;
            public readonly WriteCallback Write;

            public readonly int Width;
            public readonly int Height;
            public readonly int Comp;

            public readonly Stream Output;
            public readonly byte[] WriteBuffer;
            public readonly byte[] ScratchBuffer;

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                WriteCallback writeCallback,
                int width,
                int height,
                int comp,
                Stream output,
                byte[] writeBuffer,
                byte[] scratchBuffer)
            {
                Write = writeCallback;
                ReadBytes = readBytePixels;
                ReadFloats = readFloatPixels;

                Width = width;
                Height = height;
                Comp = comp;

                Output = output;
                WriteBuffer = writeBuffer;
                ScratchBuffer = scratchBuffer;
            }

            public WriteContext(
                ReadBytePixelsCallback readBytePixels,
                ReadFloatPixelsCallback readFloatPixels,
                int width,
                int height,
                int comp,
                Stream output,
                byte[] writeBuffer,
                byte[] scratchBuffer) :
                this(
                    readBytePixels, readFloatPixels, DefaultWrite,
                    width, height, comp,
                    output, writeBuffer, scratchBuffer)
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
                if (minSize > ctx.ScratchBuffer.Length)
                {
                    _ptr = (byte*)CRuntime.malloc(minSize);
                    _span = new Span<byte>(_ptr, minSize);
                }
                else
                {
                    _ptr = null;
                    _span = ctx.ScratchBuffer.AsSpan(0, minSize);
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

        public static void stbiw__writefv(in WriteContext s, string fmt, Span<object> v)
        {
            Span<byte> buf = stackalloc byte[4];

            int vindex = 0;
            for (var i = 0; i < fmt.Length; ++i)
            {
                var c = fmt[i];
                switch (c)
                {
                    case ' ':
                        break;

                    case '1':
                        buf[0] = (byte)((int)v[vindex++] & 0xff);
                        s.Write(s, buf.Slice(0, 1));
                        break;

                    case '2':
                    {
                        var x = (int)v[vindex++];
                        buf[0] = (byte)(x & 0xff);
                        buf[1] = (byte)((x >> 8) & 0xff);
                        s.Write(s, buf.Slice(0, 2));
                        break;
                    }

                    case '4':
                    {
                        var x = (int)v[vindex++];
                        buf[0] = (byte)(x & 0xff);
                        buf[1] = (byte)((x >> 8) & 0xff);
                        buf[2] = (byte)((x >> 16) & 0xff);
                        buf[3] = (byte)((x >> 24) & 0xff);
                        s.Write(s, buf.Slice(0, 4));
                        break;
                    }
                }
            }
        }

        public static void stbiw__writef(in WriteContext s, string fmt, Span<object> v)
        {
            stbiw__writefv(s, fmt, v);
        }

        public static int stbiw__outfile(
            in WriteContext s, bool flip_rgb, int vdir,
            int expand_mono, int alpha_dir, int pad, string fmt, Span<object> v)
        {
            if ((s.Height < 0) || (s.Width < 0))
                return 0;

            stbiw__writefv(s, fmt, v);
            stbiw__write_pixels(s, flip_rgb, vdir, alpha_dir, pad, expand_mono);
            return 1;
        }

        private static readonly byte[] stbi_hdr_radiance_header =
            Encoding.UTF8.GetBytes("#?RADIANCE\n# Written by stb_image_write.h\nFORMAT=32-bit_rle_rgbe\n");

        public static int stbi_write_hdr_core(in WriteContext s)
        {
            int x = s.Width;
            int y = s.Height;
            if (y <= 0 || x <= 0)
                return 0;

            s.Write(s, stbi_hdr_radiance_header);

            byte[] bytes = Encoding.UTF8.GetBytes(string.Format(
                "EXPOSURE=          1.0000000000000\n\n-Y {0} +X {1}\n", y.ToString(), x.ToString()));
            s.Write(s, bytes);

            byte* scratch = null;
            if ((x < 8) || (x >= 32768))
                scratch = (byte*)CRuntime.malloc((ulong)(x * 4));

            for (int line = 0; line < y; line++)
                stbiw__write_hdr_scanline(s, line, scratch);

            CRuntime.free(scratch);
            return 1;
        }
    }
}