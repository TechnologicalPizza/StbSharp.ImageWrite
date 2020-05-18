using System;
using System.Numerics;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static partial class Jpeg
        {
            public readonly struct PointU16
            {
                public ushort X { get; }
                public ushort Y { get; }

                public PointU16(ushort x, ushort y)
                {
                    X = x;
                    Y = y;
                }
            }

            public struct BitBuffer32
            {
                public int Value;
                public int Count;
            }

            public struct BitBuffer16
            {
                public ushort Value;
                public ushort Count;
            }

            public static void WriteBits(
                WriteState s, ref BitBuffer32 bitBuf, ushort bs0, ushort bs1)
            {
                bitBuf.Count += bs1;
                bitBuf.Value |= bs0 << (24 - bitBuf.Count);

                while (bitBuf.Count >= 8)
                {
                    byte c = (byte)((bitBuf.Value >> 16) & 0xff);
                    s.WriteByte(c);
                    if (c == 0xff)
                        s.WriteByte(0);

                    bitBuf.Value <<= 8;
                    bitBuf.Count -= 8;
                }
            }

            public static void WriteBits(
                WriteState s, ref BitBuffer32 bitBuf, PointU16 point)
            {
                WriteBits(s, ref bitBuf, point.X, point.Y);
            }

            public static void CalculateDCT(
                ref float d0, ref float d1, ref float d2, ref float d3,
                ref float d4, ref float d5, ref float d6, ref float d7)
            {
                float z1, z2, z3, z4, z5, z11, z13;

                float tmp0 = d0 + d7;
                float tmp7 = d0 - d7;
                float tmp1 = d1 + d6;
                float tmp6 = d1 - d6;
                float tmp2 = d2 + d5;
                float tmp5 = d2 - d5;
                float tmp3 = d3 + d4;
                float tmp4 = d3 - d4;

                // Even part
                float tmp10 = tmp0 + tmp3;
                float tmp13 = tmp0 - tmp3;
                float tmp11 = tmp1 + tmp2;
                float tmp12 = tmp1 - tmp2;

                // Odd part
                float tmp14 = tmp4 + tmp5;
                float tmp15 = tmp5 + tmp6;
                float tmp16 = tmp6 + tmp7;

                // The rotator is modified from fig 4-8 to avoid extra negations.
                z5 = (tmp14 - tmp16) * 0.382683433f; // c6
                z2 = tmp14 * 0.541196100f + z5; // c2-c6
                z4 = tmp16 * 1.306562965f + z5; // c2+c6
                z3 = tmp15 * 0.707106781f; // c4
                z1 = (tmp12 + tmp13) * 0.707106781f; // c4
                z11 = tmp7 + z3;
                z13 = tmp7 - z3;

                d0 = tmp10 + tmp11;
                d1 = z11 + z4;
                d2 = tmp13 + z1;
                d3 = z13 - z2;
                d4 = tmp10 - tmp11;
                d5 = z13 + z2;
                d6 = tmp13 - z1;
                d7 = z11 - z4;
            }

            public static BitBuffer16 CalcBitBuffer16(int value)
            {
                int tmp = value < 0 ? -value : value;
                value = value < 0 ? value - 1 : value;

                ushort count = 1;
                while ((tmp >>= 1) != 0)
                    count++;

                return new BitBuffer16
                {
                    Value = (ushort)(value & ((1 << count) - 1)),
                    Count = count
                };
            }

            public static int ProcessDU(
                WriteState s, ref BitBuffer32 bitBuf,
                Span<float> CDU, Span<float> fdtbl, int DC, PointU16[] HTDC, PointU16[] HTAC)
            {
                Span<int> DU = stackalloc int[64];
                CalculateCDU(CDU, fdtbl, DU);

                int diff = DU[0] - DC;
                if (diff == 0)
                {
                    WriteBits(s, ref bitBuf, HTDC[0]);
                }
                else
                {
                    var bitBuf16 = CalcBitBuffer16(diff);
                    WriteBits(s, ref bitBuf, HTDC[bitBuf16.Count]);
                    WriteBits(s, ref bitBuf, bitBuf16.Value, bitBuf16.Count);
                }

                int end0pos = 63;
                for (; (end0pos > 0) && (DU[end0pos] == 0); end0pos--)
                {
                }

                if (end0pos == 0)
                {
                    WriteBits(s, ref bitBuf, HTAC[0x00]);
                    return DU[0];
                }

                for (int i = 1; i <= end0pos; i++)
                {
                    int startpos = i;
                    for (; (DU[i] == 0) && (i <= end0pos); i++)
                    {
                    }

                    int nrzeroes = i - startpos;
                    if (nrzeroes >= 16)
                    {
                        int lng = nrzeroes >> 4;
                        for (int nrmarker = 1; nrmarker <= lng; nrmarker++)
                            WriteBits(s, ref bitBuf, HTAC[0xF0]);

                        nrzeroes &= 15;
                    }

                    var bitBuf16 = CalcBitBuffer16(DU[i]);
                    WriteBits(s, ref bitBuf, HTAC[(nrzeroes << 4) + bitBuf16.Count]);
                    WriteBits(s, ref bitBuf, bitBuf16.Value, bitBuf16.Count);
                }

                if (end0pos != 63)
                    WriteBits(s, ref bitBuf, HTAC[0x00]);

                return DU[0];
            }

            public static void CalculateDU(
                Span<float> YDU, Span<float> UDU, Span<float> VDU,
                int pos, float r, float g, float b, float factor)
            {
                //float sx = +0.29900f * r + 0.58700f * g + 0.11400f * b;
                //float sy = -0.16874f * r - 0.33126f * g + 0.50000f * b;
                //float sz = +0.50000f * r - 0.41869f * g - 0.08131f * b;

                var x = r * new Vector3(+0.29900f, -0.16874f, +0.50000f);
                var y = g * new Vector3(+0.58700f, -0.33126f, -0.41869f);
                var z = b * new Vector3(+0.11400f, +0.50000f, -0.08131f);
                var s = (x + y + z) * factor;

                YDU[pos] = s.X - 128;
                UDU[pos] = s.Y;
                VDU[pos] = s.Z;
            }

            public static void CalculateCDU(Span<float> CDU, ReadOnlySpan<float> fdtbl, Span<int> DU)
            {
                for (int off = 0; off < CDU.Length; off += 8)
                {
                    CalculateDCT(
                        ref CDU[off + 0], ref CDU[off + 1],
                        ref CDU[off + 2], ref CDU[off + 3],
                        ref CDU[off + 4], ref CDU[off + 5],
                        ref CDU[off + 6], ref CDU[off + 7]);
                }

                for (int off = 0; off < 8; off++)
                {
                    CalculateDCT(
                        ref CDU[off + 00], ref CDU[off + 08],
                        ref CDU[off + 16], ref CDU[off + 24],
                        ref CDU[off + 32], ref CDU[off + 40],
                        ref CDU[off + 48], ref CDU[off + 56]);
                }

                var zigZag = ZigZag;
                for (int i = 0; i < 64; i++)
                {
                    float v = CDU[i] * fdtbl[i];
                    DU[zigZag[i]] = (int)MathF.Round(v); //(v < 0 ? v - 0.5f : v + 0.5f);
                }
            }

            public static void WriteCore(WriteState s, int quality, bool useFloatPixels)
            {
                int width = s.Width;
                int height = s.Height;
                int comp = s.Components;

                if (width == 0 || (height == 0))
                    throw new ArgumentException("Invalid dimensions.", nameof(s));

                if ((comp < 1) || (comp > 4))
                    throw new ArgumentException("Invalid component count.", nameof(s));

                Span<float> fdtbl_Y = stackalloc float[64];
                Span<float> fdtbl_UV = stackalloc float[64];

                Span<byte> YTable = stackalloc byte[64];
                Span<byte> UVTable = stackalloc byte[64];

                quality = quality != 0 ? quality : 90;
                quality = quality < 1 ? 1 : quality > 100 ? 100 : quality;
                quality = quality < 50 ? 5000 / quality : 200 - quality * 2;

                var zigZag = ZigZag;
                for (int i = 0; i < 64; i++)
                {
                    int yti = (YQT[i] * quality + 50) / 100;
                    YTable[zigZag[i]] = (byte)(yti < 1 ? 1 : yti > 255 ? 255 : yti);

                    int uvti = (UVQT[i] * quality + 50) / 100;
                    UVTable[zigZag[i]] = (byte)(uvti < 1 ? 1 : uvti > 255 ? 255 : uvti);
                }

                for (int row = 0, k = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++, k++)
                    {
                        fdtbl_Y[k] = 1 / (YTable[zigZag[k]] * aasf[row] * aasf[col]);
                        fdtbl_UV[k] = 1 / (UVTable[zigZag[k]] * aasf[row] * aasf[col]);
                    }
                }

                {
                    Span<byte> head1 = stackalloc byte[24] {
                        0xFF,
                        0xC0,
                        0,
                        0x11,
                        8,
                        (byte)(height >> 8),
                        (byte)((height) & 0xff),
                        (byte)(width >> 8),
                        (byte)((width) & 0xff),
                        3,
                        1,
                        0x11,
                        0,
                        2,
                        0x11,
                        1,
                        3,
                        0x11,
                        1,
                        0xFF,
                        0xC4,
                        0x01,
                        0xA2,
                        0
                    };

                    s.Write(Head0);
                    s.Write(YTable);
                    s.WriteByte(1);
                    s.Write(UVTable);
                    s.Write(head1);

                    s.Write(std_DcLuminanceNrcodes[1..std_DcChrominanceNrcodes.Length]);
                    s.Write(std_DcLuminanceValues);

                    s.WriteByte(0x10);
                    s.Write(std_AcLuminanceNrcodes.Slice(1));
                    s.Write(std_AcLuminanceValues);

                    s.WriteByte(1);
                    s.Write(std_DcChrominanceNrcodes.Slice(1));
                    s.Write(std_DcChrominanceValues);

                    s.WriteByte(0x11);
                    s.Write(std_AcChrominanceNrcodes.Slice(1));
                    s.Write(std_AcChrominanceValues);

                    s.Write(Head2);
                }

                {
                    int DCY = 0;
                    int DCU = 0;
                    int DCV = 0;
                    int stride = width * comp;
                    int ofsG = comp > 2 ? 1 : 0;
                    int ofsB = comp > 2 ? 2 : 0;

                    Span<float> YDU = stackalloc float[64];
                    Span<float> UDU = stackalloc float[64];
                    Span<float> VDU = stackalloc float[64];

                    var bitBuf = new BitBuffer32();
                    Span<int> DU = stackalloc int[64];

                    float[] floatRowBuf = null;
                    byte[] byteRowBuf = null;

                    if (useFloatPixels)
                        floatRowBuf = new float[stride];
                    else
                        byteRowBuf = new byte[stride];

                    for (int y = 0; y < height; y += 8)
                    {
                        for (int x = 0; x < width; x += 8)
                        {
                            for (int row = y, pos = 0; row < (y + 8); row++)
                            {
                                int clamped_row = (row < height) ? row : height - 1;

                                if (useFloatPixels)
                                {
                                    s.GetFloatRow(clamped_row, floatRowBuf);
                                    for (int col = x; col < (x + 8); col++, pos++)
                                    {
                                        int p = ((col < width) ? col : (width - 1)) * comp;
                                        float r = floatRowBuf[p + 0000];
                                        float g = floatRowBuf[p + ofsG];
                                        float b = floatRowBuf[p + ofsB];
                                        CalculateDU(YDU, UDU, VDU, pos, r, g, b, byte.MaxValue);
                                    }
                                }
                                else
                                {
                                    s.GetByteRow(clamped_row, byteRowBuf);
                                    for (int col = x; col < (x + 8); col++, pos++)
                                    {
                                        int p = ((col < width) ? col : (width - 1)) * comp;
                                        float r = byteRowBuf[p + 0000];
                                        float g = byteRowBuf[p + ofsG];
                                        float b = byteRowBuf[p + ofsB];
                                        CalculateDU(YDU, UDU, VDU, pos, r, g, b, 1);
                                    }
                                }
                            }

                            DCY = ProcessDU(s, ref bitBuf, YDU, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                            DCU = ProcessDU(s, ref bitBuf, UDU, fdtbl_UV, DCU, UVDC_HT, UVAC_HT);
                            DCV = ProcessDU(s, ref bitBuf, VDU, fdtbl_UV, DCV, UVDC_HT, UVAC_HT);
                        }
                    }

                    WriteBits(s, ref bitBuf, 0x7F, 7);
                }

                s.WriteByte(0xFF);
                s.WriteByte(0xD9);
            }
        }
    }
}