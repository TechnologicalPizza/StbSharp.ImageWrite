using System;
using System.IO;
using System.Text;

namespace StbSharp
{
    internal static unsafe partial class StbImageWrite
    {
        public delegate int WriteCallback(Stream stream, byte[] buffer, Span<byte> data);

        public readonly struct stbi__write_context
        {
            public readonly WriteCallback callback;
            public readonly Stream stream;
            public readonly byte[] buffer;

            public readonly int stbi_write_tga_with_rle;

            public stbi__write_context(WriteCallback callback, Stream stream, byte[] buffer)
            {
                this.callback = callback;
                this.stream = stream;
                this.buffer = buffer;
                this.stbi_write_tga_with_rle = 1;
            }
        }

        public static void stbiw__writefv(stbi__write_context s, string fmt, params object[] v)
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
                        s.callback(s.stream, s.buffer, buf.Slice(0, 1));
                        break;

                    case '2':
                    {
                        var x = (int)v[vindex++];
                        buf[0] = (byte)(x & 0xff);
                        buf[1] = (byte)((x >> 8) & 0xff);
                        s.callback(s.stream, s.buffer, buf.Slice(0, 2));
                        break;
                    }

                    case '4':
                    {
                        var x = (int)v[vindex++];
                        buf[0] = (byte)(x & 0xff);
                        buf[1] = (byte)((x >> 8) & 0xff);
                        buf[2] = (byte)((x >> 16) & 0xff);
                        buf[3] = (byte)((x >> 24) & 0xff);
                        s.callback(s.stream, s.buffer, buf.Slice(0, 4));
                        break;
                    }
                }
            }
        }

        public static void stbiw__writef(stbi__write_context s, string fmt, params object[] v)
        {
            stbiw__writefv(s, fmt, v);
        }

        public static int stbiw__outfile(stbi__write_context s, int rgb_dir, int vdir, int x, int y, int comp,
            int expand_mono, void* data, int alpha, int pad, string fmt, params object[] v)
        {
            if ((y < 0) || (x < 0))
            {
                return 0;
            }

            stbiw__writefv(s, fmt, v);
            stbiw__write_pixels(s, rgb_dir, vdir, x, y, comp, data, alpha, pad, expand_mono);
            return 1;
        }

        public static int stbi_write_bmp_to_func(
            WriteCallback func,
            Stream stream,
            byte[] buffer,
            int x,
            int y,
            int comp,
            void* data)
        {
            var s = new stbi__write_context(func, stream, buffer);
            return stbi_write_bmp_core(s, x, y, comp, data);
        }

        public static int stbi_write_tga_to_func(
            WriteCallback func,
            Stream stream,
            byte[] buffer,
            int x,
            int y,
            int comp,
            void* data)
        {
            var s = new stbi__write_context(func, stream, buffer);
            return stbi_write_tga_core(s, x, y, comp, data);
        }

        public static int stbi_write_hdr_to_func(
            WriteCallback func,
            Stream stream,
            byte[] buffer,
            int x,
            int y,
            int comp,
            float* data
            )
        {
            var s = new stbi__write_context(func, stream, buffer);
            return stbi_write_hdr_core(s, x, y, comp, data);
        }

        public static int stbi_write_png_to_func(
            WriteCallback func,
            Stream stream,
            byte[] buffer,
            int x,
            int y,
            int comp,
            PngCompressionLevel q,
            void* data,
            int stride_bytes)
        {
            int len;
            var png = stbi_write_png_to_mem((byte*)(data), stride_bytes, x, y, comp, q, &len);
            if (png == null)
                return 0;
            func(stream, buffer, new Span<byte>(png, len));
            CRuntime.free(png);
            return 1;
        }

        public static int stbi_write_jpg_to_func(
            WriteCallback func,
            Stream stream,
            byte[] buffer,
            int x,
            int y,
            int comp,
            void* data,
            int quality)
        {
            var s = new stbi__write_context(func, stream, buffer);
            return stbi_write_jpg_core(s, x, y, comp, data, quality);
        }

        private static readonly byte[] stbi_hdr_radiance_header = 
            Encoding.UTF8.GetBytes("#?RADIANCE\n# Written by stb_image_write.h\nFORMAT=32-bit_rle_rgbe\n");

        public static int stbi_write_hdr_core(stbi__write_context s, int x, int y, int comp, float* data)
        {
            if ((y <= 0) || (x <= 0) || (data == null))
                return 0;

            s.callback(s.stream, s.buffer, stbi_hdr_radiance_header);

            byte[] bytes = Encoding.UTF8.GetBytes(string.Format(
                "EXPOSURE=          1.0000000000000\n\n-Y {0} +X {1}\n", y.ToString(), x.ToString()));
            s.callback(s.stream, s.buffer, bytes);

            var scratch = (byte*)(CRuntime.malloc((ulong)(x * 4)));
            for (int i = 0; i < y; i++)
                stbiw__write_hdr_scanline(s, x, comp, scratch, data + comp * i * x);

            CRuntime.free(scratch);
            return 1;
        }
    }
}