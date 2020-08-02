using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

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
                Debug.Assert(s != null);

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
                Span<float> CDU, int duStride, Span<float> fdtbl, int DC,
                PointU16[] HTDC, PointU16[] HTAC)
            {
                Debug.Assert(HTDC != null);
                Debug.Assert(HTAC != null);

                Span<int> DU = stackalloc int[64];
                CalculateCDU(CDU, duStride, fdtbl, DU);

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            public static void CalculateCDU(Span<float> CDU, int duStride, ReadOnlySpan<float> fdtbl, Span<int> DU)
            {
                for (int i = 0; i < CDU.Length; i += duStride)
                {
                    CalculateDCT(
                        ref CDU[i + 0],
                        ref CDU[i + 1],
                        ref CDU[i + 2],
                        ref CDU[i + 3],
                        ref CDU[i + 4],
                        ref CDU[i + 5],
                        ref CDU[i + 6],
                        ref CDU[i + 7]);
                }

                for (int i = 0; i < 8; i++)
                {
                    CalculateDCT(
                        ref CDU[i + duStride * 0],
                        ref CDU[i + duStride * 1],
                        ref CDU[i + duStride * 2],
                        ref CDU[i + duStride * 3],
                        ref CDU[i + duStride * 4],
                        ref CDU[i + duStride * 5],
                        ref CDU[i + duStride * 6],
                        ref CDU[i + duStride * 7]);
                }

                int j = 0;
                var zigZag = ZigZag;

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++, j++)
                    {
                        int i = y * duStride + x;
                        float v = CDU[i] * fdtbl[j];
                        DU[zigZag[j]] = (int)MathF.Round(v);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public static void WriteCore(
                WriteState s, 
                bool useFloatPixels, int quality, bool allowSubsample, bool forceSubsample)
            {
                if (s == null)
                    throw new ArgumentNullException(nameof(s));

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
                bool subsample = forceSubsample || (allowSubsample && quality <= 90);

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
                        (byte)(subsample ? 0x22 : 0x11),
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

                    s.Write(DefaultDcLuminanceNrcodes[1..DefaultDcChrominanceNrcodes.Length]);
                    s.Write(DefaultDcLuminanceValues);

                    s.WriteByte(0x10);
                    s.Write(DefaultAcLuminanceNrcodes.Slice(1));
                    s.Write(DefaultAcLuminanceValues);

                    s.WriteByte(1);
                    s.Write(DefaultDcChrominanceNrcodes.Slice(1));
                    s.Write(DefaultDcChrominanceValues);

                    s.WriteByte(0x11);
                    s.Write(DefaultAcChrominanceNrcodes.Slice(1));
                    s.Write(DefaultAcChrominanceValues);

                    s.Write(Head2);
                }

                {
                    int DCY = 0;
                    int DCU = 0;
                    int DCV = 0;
                    int stride = width * comp;
                    int ofsG = comp > 2 ? 1 : 0;
                    int ofsB = comp > 2 ? 2 : 0;
                    int duStride = subsample ? 16 : 8;
                    int duSize = subsample ? 256 : 64;
                    int wEdge = width - 1;
                    int hEdge = height - 1;

                    // TODO: pool buffers
                    float[]? floatRowBuf = useFloatPixels ? new float[stride] : null;
                    byte[]? byteRowBuf = !useFloatPixels ? new byte[stride] : null;
                    Span<float> floatRow = floatRowBuf;
                    Span<byte> byteRow = byteRowBuf;

                    var bitBuf = new BitBuffer32();
                    Span<float> subU = stackalloc float[subsample ? 64 : 0];
                    Span<float> subV = stackalloc float[subsample ? 64 : 0];
                    Span<float> U = stackalloc float[duSize];
                    Span<float> V = stackalloc float[duSize];
                    Span<float> Y = stackalloc float[duSize];
                    Span<float> Y1 = subsample ? Y.Slice(0, 128) : default;
                    Span<float> Y2 = subsample ? Y.Slice(8, 128) : default;
                    Span<float> Y3 = subsample ? Y.Slice(128, 128) : default;
                    Span<float> Y4 = subsample ? Y.Slice(136, 120) : default;

                    // TODO: optimize
                    // TODO: split into functions and move some branches outside loops

                    for (int y = 0; y < height; y += duStride)
                    {
                        for (int x = 0; x < width; x += duStride)
                        {
                            int rowMax = y + duStride;
                            int colMax = x + duStride;

                            if (useFloatPixels)
                            {
                                for (int row = y, pos = 0; row < rowMax; row++)
                                {
                                    int clamped_row = (row < height) ? row : hEdge;
                                    s.GetFloatRow(clamped_row, floatRow);

                                    for (int col = x; col < colMax; col++, pos++)
                                    {
                                        int p = (col < width ? col : wEdge) * comp;
                                        float r = floatRow[p + 0000];
                                        float g = floatRow[p + ofsG];
                                        float b = floatRow[p + ofsB];
                                        CalculateDU(Y, U, V, pos, r, g, b, byte.MaxValue);
                                    }
                                }
                            }
                            else
                            {
                                for (int row = y, pos = 0; row < rowMax; row++)
                                {
                                    int clamped_row = (row < height) ? row : hEdge;
                                    s.GetByteRow(clamped_row, byteRow);

                                    for (int col = x; col < colMax; col++, pos++)
                                    {
                                        int p = (col < width ? col : wEdge) * comp;
                                        byte r = byteRow[p + 0000];
                                        byte g = byteRow[p + ofsG];
                                        byte b = byteRow[p + ofsB];
                                        CalculateDU(Y, U, V, pos, r, g, b, 1);
                                    }
                                }
                            }

                            if (subsample)
                            {
                                DCY = ProcessDU(s, ref bitBuf, Y1, 16, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                                DCY = ProcessDU(s, ref bitBuf, Y2, 16, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                                DCY = ProcessDU(s, ref bitBuf, Y3, 16, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                                DCY = ProcessDU(s, ref bitBuf, Y4, 16, fdtbl_Y, DCY, YDC_HT, YAC_HT);

                                // subsample U,V
                                {
                                    for (int yy = 0, pos = 0; yy < 8; yy++)
                                    {
                                        for (int xx = 0; xx < 8; xx++, pos++)
                                        {
                                            int j = yy * 32 + xx * 2;
                                            subU[pos] = (U[j + 17] + U[j + 16] + U[j + 1] + U[j + 0]) * 0.25f;
                                            subV[pos] = (V[j + 17] + V[j + 16] + V[j + 1] + V[j + 0]) * 0.25f;
                                        }
                                    }
                                    DCU = ProcessDU(s, ref bitBuf, subU, 8, fdtbl_UV, DCU, UVDC_HT, UVAC_HT);
                                    DCV = ProcessDU(s, ref bitBuf, subV, 8, fdtbl_UV, DCV, UVDC_HT, UVAC_HT);
                                }
                            }
                            else
                            {
                                DCY = ProcessDU(s, ref bitBuf, Y, 8, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                                DCU = ProcessDU(s, ref bitBuf, U, 8, fdtbl_UV, DCU, UVDC_HT, UVAC_HT);
                                DCV = ProcessDU(s, ref bitBuf, V, 8, fdtbl_UV, DCV, UVDC_HT, UVAC_HT);
                            }
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