using System;

namespace StbSharp
{
    public static partial class ImageWrite
    {
        public static unsafe partial class Jpeg
        {
            #region Constants

            public static byte[] ZigZag =
            {
                0, 1, 5, 6, 14, 15, 27, 28, 2, 4, 7, 13, 16, 26, 29, 42, 3, 8, 12, 17, 25, 30, 41,
                43, 9, 11, 18, 24, 31, 40, 44, 53, 10, 19, 23, 32, 39, 45, 52, 54, 20, 22, 33, 38,
                46, 51, 55, 60, 21, 34, 37, 47, 50, 56, 59, 61, 35, 36, 48, 49, 57, 58, 62, 63
            };

            public static byte[] std_DcLuminanceNrcodes = { 0, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
            public static byte[] std_DcLuminanceValues = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            public static byte[] std_AcLuminanceNrcodes = { 0, 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d };

            public static byte[] std_AcLuminanceValues =
            {
                0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22, 0x71,
                0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24, 0x33, 0x62, 0x72,
                0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x34, 0x35, 0x36, 0x37,
                0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83,
                0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3,
                0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
                0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
                0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa
            };

            public static byte[] std_DcChrominanceNrcodes = { 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            public static byte[] std_DcChrominanceValues = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            public static byte[] std_AcChrominanceNrcodes = { 0, 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 };

            public static byte[] std_AcChrominanceValues =
            {
                0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71, 0x13, 0x22,
                0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1,
                0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x35, 0x36,
                0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
                0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
                0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a,
                0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba,
                0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
                0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa
            };

            public static ushort[,] YDC_HT =
            {
                {0, 2}, {2, 3}, {3, 3}, {4, 3}, {5, 3}, {6, 3}, {14, 4}, {30, 5}, {62, 6}, {126, 7}, {254, 8}, {510, 9}
            };

            public static ushort[,] UVDC_HT =
            {
                {0, 2}, {1, 2}, {2, 2}, {6, 3}, {14, 4}, {30, 5}, {62, 6}, {126, 7}, {254, 8}, {510, 9}, {1022, 10}, {2046, 11}
            };

            public static ushort[,] YAC_HT =
            {
                {10, 4}, {0, 2}, {1, 2}, {4, 3}, {11, 4}, {26, 5}, {120, 7}, {248, 8}, {1014, 10}, {65410, 16},
                {65411, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {12, 4}, {27, 5}, {121, 7}, {502, 9},
                {2038, 11}, {65412, 16}, {65413, 16}, {65414, 16}, {65415, 16}, {65416, 16}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {28, 5}, {249, 8}, {1015, 10}, {4084, 12}, {65417, 16}, {65418, 16}, {65419, 16},
                {65420, 16}, {65421, 16}, {65422, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {58, 6}, {503, 9},
                {4085, 12}, {65423, 16}, {65424, 16}, {65425, 16}, {65426, 16}, {65427, 16}, {65428, 16}, {65429, 16},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {59, 6}, {1016, 10}, {65430, 16}, {65431, 16},
                {65432, 16}, {65433, 16}, {65434, 16}, {65435, 16}, {65436, 16}, {65437, 16}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {122, 7}, {2039, 11}, {65438, 16}, {65439, 16}, {65440, 16}, {65441, 16},
                {65442, 16}, {65443, 16}, {65444, 16}, {65445, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {123, 7}, {4086, 12}, {65446, 16}, {65447, 16}, {65448, 16}, {65449, 16}, {65450, 16}, {65451, 16},
                {65452, 16}, {65453, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {250, 8}, {4087, 12},
                {65454, 16}, {65455, 16}, {65456, 16}, {65457, 16}, {65458, 16}, {65459, 16}, {65460, 16}, {65461, 16},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {504, 9}, {32704, 15}, {65462, 16}, {65463, 16},
                {65464, 16}, {65465, 16}, {65466, 16}, {65467, 16}, {65468, 16}, {65469, 16}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {505, 9}, {65470, 16}, {65471, 16}, {65472, 16}, {65473, 16}, {65474, 16},
                {65475, 16}, {65476, 16}, {65477, 16}, {65478, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {506, 9}, {65479, 16}, {65480, 16}, {65481, 16}, {65482, 16},{65483, 16}, {65484, 16}, {65485, 16},
                {65486, 16}, {65487, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {1017, 10}, {65488, 16},
                {65489, 16}, {65490, 16}, {65491, 16}, {65492, 16}, {65493, 16}, {65494, 16}, {65495, 16}, {65496, 16},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {1018, 10}, {65497, 16}, {65498, 16}, {65499, 16},
                {65500, 16}, {65501, 16}, {65502, 16}, {65503, 16}, {65504, 16}, {65505, 16}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {2040, 11}, {65506, 16}, {65507, 16}, {65508, 16}, {65509, 16}, {65510, 16},
                {65511, 16}, {65512, 16}, {65513, 16}, {65514, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {65515, 16}, {65516, 16}, {65517, 16}, {65518, 16}, {65519, 16}, {65520, 16}, {65521, 16}, {65522, 16},
                {65523, 16}, {65524, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {2041, 11}, {65525, 16}, {65526, 16},
                {65527, 16}, {65528, 16}, {65529, 16}, {65530, 16}, {65531, 16}, {65532, 16}, {65533, 16}, {65534, 16},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}
            };

            public static ushort[,] UVAC_HT =
            {
                {0, 2}, {1, 2}, {4, 3}, {10, 4}, {24, 5}, {25, 5}, {56, 6}, {120, 7}, {500, 9}, {1014, 10}, {4084, 12},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {11, 4}, {57, 6}, {246, 8}, {501, 9}, {2038, 11},
                {4085, 12}, {65416, 16}, {65417, 16}, {65418, 16}, {65419, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {26, 5}, {247, 8}, {1015, 10}, {4086, 12}, {32706, 15}, {65420, 16}, {65421, 16}, {65422, 16},
                {65423, 16}, {65424, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {27, 5}, {248, 8}, {1016, 10},
                {4087, 12}, {65425, 16}, {65426, 16}, {65427, 16}, {65428, 16}, {65429, 16}, {65430, 16}, {0, 0}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {58, 6}, {502, 9}, {65431, 16}, {65432, 16}, {65433, 16}, {65434, 16},
                {65435, 16}, {65436, 16}, {65437, 16}, {65438, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {59, 6}, {1017, 10}, {65439, 16}, {65440, 16}, {65441, 16}, {65442, 16}, {65443, 16}, {65444, 16},
                {65445, 16}, {65446, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {121, 7}, {2039, 11},
                {65447, 16}, {65448, 16}, {65449, 16}, {65450, 16}, {65451, 16}, {65452, 16}, {65453, 16}, {65454, 16},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {122, 7}, {2040, 11}, {65455, 16}, {65456, 16},
                {65457, 16}, {65458, 16}, {65459, 16}, {65460, 16}, {65461, 16}, {65462, 16}, {0, 0}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {249, 8}, {65463, 16}, {65464, 16}, {65465, 16}, {65466, 16},
                {65467, 16}, {65468, 16}, {65469, 16}, {65470, 16}, {65471, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {503, 9}, {65472, 16}, {65473, 16}, {65474, 16}, {65475, 16}, {65476, 16}, {65477, 16},
                {65478, 16}, {65479, 16}, {65480, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {504, 9},
                {65481, 16}, {65482, 16}, {65483, 16}, {65484, 16}, {65485, 16}, {65486, 16}, {65487, 16}, {65488, 16},
                {65489, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {505, 9}, {65490, 16}, {65491, 16},
                {65492, 16}, {65493, 16}, {65494, 16}, {65495, 16}, {65496, 16}, {65497, 16}, {65498, 16}, {0, 0},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {506, 9}, {65499, 16}, {65500, 16}, {65501, 16}, {65502, 16},
                {65503, 16}, {65504, 16}, {65505, 16}, {65506, 16}, {65507, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0},
                {0, 0}, {2041, 11}, {65508, 16}, {65509, 16}, {65510, 16}, {65511, 16}, {65512, 16}, {65513, 16},
                {65514, 16}, {65515, 16}, {65516, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {16352, 14},
                {65517, 16}, {65518, 16}, {65519, 16}, {65520, 16}, {65521, 16}, {65522, 16}, {65523, 16}, {65524, 16},
                {65525, 16}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}, {1018, 10}, {32707, 15}, {65526, 16}, {65527, 16},
                {65528, 16}, {65529, 16}, {65530, 16}, {65531, 16}, {65532, 16}, {65533, 16}, {65534, 16},
                {0, 0}, {0, 0}, {0, 0}, {0, 0}, {0, 0}
            };

            public static int[] YQT =
            {
                16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57,
                69, 56, 14, 17, 22, 29, 51, 87, 80, 62, 18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64,
                81, 104, 113, 92, 49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99
            };

            public static int[] UVQT =
            {
                17, 18, 24, 47, 99, 99, 99, 99, 18, 21, 26, 66, 99, 99, 99, 99, 24, 26, 56, 99, 99, 99, 99,
                99, 47, 66, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
                99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99
            };

            public static float[] aasf =
            {
                1f * 2.828427125f, 1.387039845f * 2.828427125f, 1.306562965f * 2.828427125f,
                1.175875602f * 2.828427125f, 1f * 2.828427125f, 0.785694958f * 2.828427125f,
                0.541196100f * 2.828427125f, 0.275899379f * 2.828427125f
            };

            public static byte[] head0 =
            {
                0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F',
                0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0xFF, 0xDB, 0, 0x84, 0
            };

            public static byte[] head2 = { 0xFF, 0xDA, 0, 0xC, 3, 1, 0, 2, 0x11, 3, 0x11, 0, 0x3F, 0 };

            #endregion

            public static void WriteBits(
                in WriteState s, int* bitBuf, int* bitCnt, ushort bs0, ushort bs1)
            {
                *bitCnt += bs1;
                *bitBuf |= bs0 << (24 - *bitCnt);
                while (*bitCnt >= 8)
                {
                    byte c = (byte)((*bitBuf >> 16) & 255);
                    ImageWriteHelpers.WriteByte(s, c);
                    if (c == 255)
                        ImageWriteHelpers.WriteByte(s, 0);

                    *bitBuf <<= 8;
                    *bitCnt -= 8;
                }
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
                in WriteState s, int* bitBuf, int* bitCnt, float* CDU,
                float* fdtbl, int DC, ushort[,] HTDC, ushort[,] HTAC)
            {
                ushort* EOB = stackalloc ushort[2];
                EOB[0] = HTAC[0x00, 0];
                EOB[1] = HTAC[0x00, 1];

                ushort* M16zeroes = stackalloc ushort[2];
                M16zeroes[0] = HTAC[0xF0, 0];
                M16zeroes[1] = HTAC[0xF0, 1];

                int dataOff;
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

                for (int i = 0; i < 64; ++i)
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

                for (int i = 1; i <= end0pos; ++i)
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

            public static bool WriteCore(in WriteState s, bool useFloatPixels, int quality)
            {
                int width = s.Width;
                int height = s.Height;
                int comp = s.Components;

                if ((s.GetByteRow == null) || (width == 0) || (height == 0) || (comp > 4) || (comp < 1))
                    return false;

                quality = quality != 0 ? quality : 90;
                quality = quality < 1 ? 1 : quality > 100 ? 100 : quality;
                quality = quality < 50 ? 5000 / quality : 200 - quality * 2;

                float* fdtbl_Y = stackalloc float[64];
                float* fdtbl_UV = stackalloc float[64];
                Span<byte> YTable = stackalloc byte[64];
                Span<byte> UVTable = stackalloc byte[64];

                for (int i = 0; i < 64; i++)
                {
                    int yti = (YQT[i] * quality + 50) / 100;
                    int uvti = (UVQT[i] * quality + 50) / 100;
                    YTable[ZigZag[i]] = (byte)(yti < 1 ? 1 : yti > 255 ? 255 : yti);
                    UVTable[ZigZag[i]] = (byte)(uvti < 1 ? 1 : uvti > 255 ? 255 : uvti);
                }

                for (int row = 0, k = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++, k++)
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

                    s.Write(head0);

                    s.Write(YTable);
                    ImageWriteHelpers.WriteByte(s, 1);
                    s.Write(UVTable);
                    s.Write(head1);

                    s.Write(std_DcLuminanceNrcodes.AsSpan(1, std_DcChrominanceNrcodes.Length - 1));
                    s.Write(std_DcLuminanceValues.AsSpan(0, std_DcChrominanceValues.Length));

                    ImageWriteHelpers.WriteByte(s, 0x10);

                    s.Write(std_AcLuminanceNrcodes.AsSpan(1));
                    s.Write(std_AcLuminanceValues);

                    ImageWriteHelpers.WriteByte(s, 1);

                    s.Write(std_DcChrominanceNrcodes.AsSpan(1));
                    s.Write(std_DcChrominanceValues);

                    ImageWriteHelpers.WriteByte(s, 0x11);

                    s.Write(std_AcChrominanceNrcodes.AsSpan(1));
                    s.Write(std_AcChrominanceValues);

                    s.Write(head2);
                }

                {
                    int DCY = 0;
                    int DCU = 0;
                    int DCV = 0;
                    int bitBuf = 0;
                    int bitCnt = 0;
                    int stride = width * comp;
                    int ofsG = comp > 2 ? 1 : 0;
                    int ofsB = comp > 2 ? 2 : 0;
                    float* YDU = stackalloc float[64];
                    float* UDU = stackalloc float[64];
                    float* VDU = stackalloc float[64];

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
                                int clampedRow = (row < height) ? row : height - 1;
                                if (useFloatPixels)
                                    s.GetFloatRow(clampedRow, floatRowBuf);
                                else
                                    s.GetByteRow(clampedRow, byteRowBuf);

                                for (int col = x; col < (x + 8); col++, pos++)
                                {
                                    int p = ((col < width) ? col : (width - 1)) * comp;

                                    float r, g, b;
                                    if (useFloatPixels)
                                    {
                                        // TODO: fix these byte.max muls
                                        r = floatRowBuf[p + 0] * byte.MaxValue;
                                        g = floatRowBuf[p + ofsG] * byte.MaxValue;
                                        b = floatRowBuf[p + ofsB] * byte.MaxValue;
                                    }
                                    else
                                    {
                                        r = byteRowBuf[p + 0];
                                        g = byteRowBuf[p + ofsG];
                                        b = byteRowBuf[p + ofsB];
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

                ImageWriteHelpers.WriteByte(s, 0xFF);
                ImageWriteHelpers.WriteByte(s, 0xD9);
                return true;
            }
        }
    }
}