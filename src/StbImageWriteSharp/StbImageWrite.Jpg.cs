using System;

namespace StbSharp
{
    public static unsafe partial class StbImageWrite
    {
        public static unsafe partial class Jpg
        {
            public static void WriteBits(
                in WriteContext s, int* bitBufP, int* bitCntP, ushort bs0, ushort bs1)
            {
                int bitBuf = *bitBufP;
                int bitCnt = *bitCntP;
                bitCnt += bs1;
                bitBuf |= bs0 << (24 - bitCnt);
                while (bitCnt >= 8)
                {
                    byte c = (byte)((bitBuf >> 16) & 255);
                    WriteHelpers.WriteChar(s, c);
                    if (c == 255)
                        WriteHelpers.WriteChar(s, 0);

                    bitBuf <<= 8;
                    bitCnt -= 8;
                }

                *bitBufP = bitBuf;
                *bitCntP = bitCnt;
            }

            public static void DCT(
                float* d0p, float* d1p, float* d2p, float* d3p,
                float* d4p, float* d5p, float* d6p, float* d7p)
            {
                float d0 = *d0p;
                float d1 = *d1p;
                float d2 = *d2p;
                float d3 = *d3p;
                float d4 = *d4p;
                float d5 = *d5p;
                float d6 = *d6p;
                float d7 = *d7p;
                float tmp0 = d0 + d7;
                float tmp7 = d0 - d7;
                float tmp1 = d1 + d6;
                float tmp6 = d1 - d6;
                float tmp2 = d2 + d5;
                float tmp5 = d2 - d5;
                float tmp3 = d3 + d4;
                float tmp4 = d3 - d4;
                float tmp10 = tmp0 + tmp3;
                float tmp13 = tmp0 - tmp3;
                float tmp11 = tmp1 + tmp2;
                float tmp12 = tmp1 - tmp2;
                d0 = tmp10 + tmp11;
                d4 = tmp10 - tmp11;
                tmp10 = tmp4 + tmp5;
                tmp11 = tmp5 + tmp6;
                tmp12 = tmp6 + tmp7;

                float z1 = (tmp12 + tmp13) * 0.707106781f;
                d2 = tmp13 + z1;
                d6 = tmp13 - z1;

                float z5 = (tmp10 - tmp12) * 0.382683433f;
                float z2 = tmp10 * 0.541196100f + z5;
                float z4 = tmp12 * 1.306562965f + z5;
                float z3 = tmp11 * 0.707106781f;
                float z11 = tmp7 + z3;
                float z13 = tmp7 - z3;

                *d5p = z13 + z2;
                *d3p = z13 - z2;
                *d1p = z11 + z4;
                *d7p = z11 - z4;
                *d0p = d0;
                *d2p = d2;
                *d4p = d4;
                *d6p = d6;
            }

            public static void CalcBits(int val, ushort* bits)
            {
                int tmp1 = val < 0 ? -val : val;
                val = val < 0 ? val - 1 : val;
                bits[1] = 1;
                while ((tmp1 >>= 1) != 0)
                {
                    ++bits[1];
                }

                bits[0] = (ushort)(val & ((1 << bits[1]) - 1));
            }

            public static int ProcessDU(
                in WriteContext s, int* bitBuf, int* bitCnt, float* CDU,
                float* fdtbl, int DC, ushort[,] HTDC, ushort[,] HTAC)
            {
                ushort* EOB = stackalloc ushort[2];
                EOB[0] = HTAC[0x00, 0];
                EOB[1] = HTAC[0x00, 1];

                ushort* M16zeroes = stackalloc ushort[2];
                M16zeroes[0] = HTAC[0xF0, 0];
                M16zeroes[1] = HTAC[0xF0, 1];

                int dataOff;
                int i;
                int diff;
                int end0pos;
                int* DU = stackalloc int[64];
                ushort* bits = stackalloc ushort[2];

                for (dataOff = 0; dataOff < 64; dataOff += 8)
                {
                    DCT(
                        &CDU[dataOff], &CDU[dataOff + 1], &CDU[dataOff + 2], &CDU[dataOff + 3],
                        &CDU[dataOff + 4], &CDU[dataOff + 5], &CDU[dataOff + 6], &CDU[dataOff + 7]);
                }

                for (dataOff = 0; dataOff < 8; ++dataOff)
                {
                    DCT(
                        &CDU[dataOff], &CDU[dataOff + 8], &CDU[dataOff + 16], &CDU[dataOff + 24],
                        &CDU[dataOff + 32], &CDU[dataOff + 40], &CDU[dataOff + 48], &CDU[dataOff + 56]);
                }

                for (i = 0; i < 64; ++i)
                {
                    float v = CDU[i] * fdtbl[i];
                    DU[ZigZag[i]] = (int)(v < 0 ? v - 0.5f : v + 0.5f);
                }

                diff = DU[0] - DC;
                if (diff == 0)
                {
                    WriteBits(s, bitBuf, bitCnt, HTDC[0, 0], HTDC[0, 1]);
                }
                else
                {
                    CalcBits(diff, bits);
                    WriteBits(s, bitBuf, bitCnt, HTDC[bits[1], 0], HTDC[bits[1], 1]);
                    WriteBits(s, bitBuf, bitCnt, bits[0], bits[1]);
                }

                end0pos = 63;
                for (; (end0pos > 0) && (DU[end0pos] == 0); --end0pos) ;

                if (end0pos == 0)
                {
                    WriteBits(s, bitBuf, bitCnt, EOB[0], EOB[1]);
                    return DU[0];
                }

                for (i = 1; i <= end0pos; ++i)
                {
                    int startpos = i;
                    int nrzeroes;
                    for (; (DU[i] == 0) && (i <= end0pos); ++i) ;

                    nrzeroes = i - startpos;
                    if (nrzeroes >= 16)
                    {
                        int lng = nrzeroes >> 4;
                        for (int nrmarker = 1; nrmarker <= lng; ++nrmarker)
                            WriteBits(s, bitBuf, bitCnt, M16zeroes[0], M16zeroes[1]);

                        nrzeroes &= 15;
                    }

                    CalcBits(DU[i], bits);
                    WriteBits(s, bitBuf, bitCnt, HTAC[(nrzeroes << 4) + bits[1], 0],
                        HTAC[(nrzeroes << 4) + bits[1], 1]);
                    WriteBits(s, bitBuf, bitCnt, bits[0], bits[1]);
                }

                if (end0pos != 63)
                    WriteBits(s, bitBuf, bitCnt, EOB[0], EOB[1]);

                return DU[0];
            }

            public static int WriteCore(in WriteContext s, bool readFloatPixels, int quality)
            {
                int width = s.Width;
                int height = s.Height;
                int comp = s.Comp;

                if ((s.ReadBytes == null) || (width == 0) || (height == 0) || (comp > 4) || (comp < 1))
                    return 0;

                int row;
                int col;
                int i;
                int k;
                float* fdtbl_Y = stackalloc float[64];
                float* fdtbl_UV = stackalloc float[64];
                Span<byte> YTable = stackalloc byte[64];
                Span<byte> UVTable = stackalloc byte[64];

                quality = quality != 0 ? quality : 90;
                quality = quality < 1 ? 1 : quality > 100 ? 100 : quality;
                quality = quality < 50 ? 5000 / quality : 200 - quality * 2;

                for (i = 0; i < 64; ++i)
                {
                    int yti = (YQT[i] * quality + 50) / 100;
                    YTable[ZigZag[i]] = (byte)(yti < 1 ? 1 : yti > 255 ? 255 : yti);
                    int uvti = (UVQT[i] * quality + 50) / 100;
                    UVTable[ZigZag[i]] = (byte)(uvti < 1 ? 1 : uvti > 255 ? 255 : uvti);
                }

                for (row = 0, k = 0; row < 8; ++row)
                {
                    for (col = 0; col < 8; ++col, ++k)
                    {
                        fdtbl_Y[k] = 1 / (YTable[ZigZag[k]] * aasf[row] * aasf[col]);
                        fdtbl_UV[k] = 1 / (UVTable[ZigZag[k]] * aasf[row] * aasf[col]);
                    }
                }

                {
                    Span<byte> head1 = stackalloc byte[24];
                    head1[0] = 0xFF;
                    head1[1] = 0xC0;
                    head1[2] = 0;
                    head1[3] = 0x11;
                    head1[4] = 8;
                    head1[5] = (byte)(height >> 8);
                    head1[6] = (byte)((height) & 0xff);
                    head1[7] = (byte)(width >> 8);
                    head1[8] = (byte)((width) & 0xff);
                    head1[9] = 3;
                    head1[10] = 1;
                    head1[11] = 0x11;
                    head1[12] = 0;
                    head1[13] = 2;
                    head1[14] = 0x11;
                    head1[15] = 1;
                    head1[16] = 3;
                    head1[17] = 0x11;
                    head1[18] = 1;
                    head1[19] = 0xFF;
                    head1[20] = 0xC4;
                    head1[21] = 0x01;
                    head1[22] = 0xA2;
                    head1[23] = 0;

                    s.Write(s, head0);

                    s.Write(s, YTable);
                    WriteHelpers.WriteChar(s, 1);
                    s.Write(s, UVTable);
                    s.Write(s, head1);

                    s.Write(s, std_dc_luminance_nrcodes.AsSpan(1, std_dc_chrominance_nrcodes.Length - 1));
                    s.Write(s, std_dc_luminance_values.AsSpan(0, std_dc_chrominance_values.Length));

                    WriteHelpers.WriteChar(s, 0x10);

                    s.Write(s, std_ac_luminance_nrcodes.AsSpan(1));
                    s.Write(s, std_ac_luminance_values);

                    WriteHelpers.WriteChar(s, 1);

                    s.Write(s, std_dc_chrominance_nrcodes.AsSpan(1));
                    s.Write(s, std_dc_chrominance_values);

                    WriteHelpers.WriteChar(s, 0x11);

                    s.Write(s, std_ac_chrominance_nrcodes.AsSpan(1));
                    s.Write(s, std_ac_chrominance_values);

                    s.Write(s, head2);
                }

                {
                    int DCY = 0;
                    int DCU = 0;
                    int DCV = 0;
                    int bitBuf = 0;
                    int bitCnt = 0;
                    int stride = width * comp;
                    int x;
                    int y;
                    int pos;
                    float* YDU = stackalloc float[64];
                    float* UDU = stackalloc float[64];
                    float* VDU = stackalloc float[64];
                    Span<byte> byteDataBuffer = stackalloc byte[comp > 2 ? 3 : 1];
                    Span<float> floatDataBuffer = stackalloc float[comp > 2 ? 3 : 1];
                    float r, g, b;

                    for (y = 0; y < height; y += 8)
                    {
                        for (x = 0; x < width; x += 8)
                        {
                            for (row = y, pos = 0; row < (y + 8); ++row)
                            {
                                for (col = x; col < (x + 8); ++col, ++pos)
                                {
                                    int p = row * stride + col * comp;

                                    if (row >= height)
                                        p -= stride * (row + 1 - height);

                                    if (col >= width)
                                        p -= comp * (col + 1 - width);

                                    if (readFloatPixels)
                                    {
                                        s.ReadFloats(floatDataBuffer, p);
                                        if (comp > 2)
                                        {
                                            r = floatDataBuffer[0] * byte.MaxValue;
                                            g = floatDataBuffer[1] * byte.MaxValue;
                                            b = floatDataBuffer[2] * byte.MaxValue;
                                        }
                                        else
                                            r = g = b = floatDataBuffer[0] * byte.MaxValue;
                                    }
                                    else
                                    {
                                        s.ReadBytes(byteDataBuffer, p);
                                        if (comp > 2)
                                        {
                                            r = byteDataBuffer[0];
                                            g = byteDataBuffer[1];
                                            b = byteDataBuffer[2];
                                        }
                                        else
                                            r = g = b = byteDataBuffer[0];
                                    }

                                    YDU[pos] = +0.29900f * r + 0.58700f * g + 0.11400f * b - 128;
                                    UDU[pos] = -0.16874f * r - 0.33126f * g + 0.50000f * b;
                                    VDU[pos] = +0.50000f * r - 0.41869f * g - 0.08131f * b;
                                }
                            }

                            DCY = ProcessDU(s, &bitBuf, &bitCnt, YDU, fdtbl_Y, DCY, YDC_HT, YAC_HT);
                            DCU = ProcessDU(s, &bitBuf, &bitCnt, UDU, fdtbl_UV, DCU, UVDC_HT, UVAC_HT);
                            DCV = ProcessDU(s, &bitBuf, &bitCnt, VDU, fdtbl_UV, DCV, UVDC_HT, UVAC_HT);
                        }
                    }

                    WriteBits(s, &bitBuf, &bitCnt, 0x7F, 7);
                }

                WriteHelpers.WriteChar(s, 0xFF);
                WriteHelpers.WriteChar(s, 0xD9);
                return 1;
            }
        }
    }
}