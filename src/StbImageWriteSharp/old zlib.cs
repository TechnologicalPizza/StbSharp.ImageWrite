
#region zlib

public static void* stbiw__sbgrowf(void** arr, int increment, int itemsize)
{
    int m = (int)(*arr != null ? 2 * ((int*)(*arr) - 2)[0] + increment : increment + 1);
    void* p = CRuntime.realloc(*arr != null ? ((int*)(*arr) - 2) : ((int*)(0)),
        (ulong)(itemsize * m + sizeof(int) * 2));
    if ((p) != null)
    {
        if (*arr == null)
            ((int*)(p))[1] = (int)(0);
        *arr = (void*)((int*)(p) + 2);
        ((int*)(*arr) - 2)[0] = (int)(m);
    }

    return *arr;
}

public static byte* stbiw__zlib_flushf(byte* data, uint* bitbuffer, int* bitcount)
{
    while ((*bitcount) >= (8))
    {
        if ((((data) == null) || ((((int*)(data) - 2)[1] + (1)) >= (((int*)(data) - 2)[0]))))
        {
            stbiw__sbgrowf((void**)(&(data)), (int)(1), sizeof(byte));
        }

        (data)[((int*)(data) - 2)[1]++] = ((byte)((*bitbuffer) & 0xff));
        *bitbuffer >>= 8;
        *bitcount -= (int)(8);
    }

    return data;
}

public static int stbiw__zlib_bitrev(int code, int codebits)
{
    int res = (int)(0);
    while ((codebits--) != 0)
    {
        res = (int)((res << 1) | (code & 1));
        code >>= 1;
    }

    return (int)(res);
}

public static uint stbiw__zlib_countm(byte* a, byte* b, int limit)
{
    const int secondLimit = 258;
    const int iSecondLimit = secondLimit / sizeof(int);

    // pretty good optimization (this method gets called a lot)
    int j = 0;
    int iLimit = limit / sizeof(int);
    int* ia = (int*)a;
    int* ib = (int*)b;
    while ((j < iLimit) && (j < iSecondLimit))
    {
        if (ia[j] != ib[j])
            break; // break before incrementing j so code beneath can get a precise index
        j++;
    }

    // handles leftover bytes
    int i = j * sizeof(int);
    for (; (i < limit) && (i < secondLimit); i++)
    {
        if (a[i] != b[i])
            break;
    }

    return (uint)(i);
}

public static uint stbiw__zhash(byte* data)
{
    uint hash = (uint)(data[0] + (data[1] << 8) + (data[2] << 16));
    hash ^= (uint)(hash << 3);
    hash += (uint)(hash >> 5);
    hash ^= (uint)(hash << 4);
    hash += (uint)(hash >> 17);
    hash ^= (uint)(hash << 25);
    hash += (uint)(hash >> 6);
    return (uint)(hash);
}

public static byte* stbi_zlib_compress(byte* data, int data_len, int* out_len, int quality)
{
    uint bitbuf = (uint)(0);
    int i;
    int j;
    int bitcount = (int)(0);
    byte* _out_ = null;
    byte*** hash_table = (byte***)(CRuntime.malloc((ulong)(16384 * sizeof(byte**))));
    if ((quality) < stbi_png_minimum_compression_quality)
        quality = stbi_png_minimum_compression_quality;

    if ((((_out_) == null) || ((((int*)(_out_) - 2)[1] + (1)) >= (((int*)(_out_) - 2)[0]))))
    {
        stbiw__sbgrowf((void**)(&(_out_)), (int)(1), sizeof(byte));
    }

    (_out_)[((int*)(_out_) - 2)[1]++] = (byte)(0x78);
    if ((((_out_) == null) || ((((int*)(_out_) - 2)[1] + (1)) >= (((int*)(_out_) - 2)[0]))))
    {
        stbiw__sbgrowf((void**)(&(_out_)), (int)(1), sizeof(byte));
    }

    (_out_)[((int*)(_out_) - 2)[1]++] = (byte)(0x5e);
    {
        bitbuf |= (uint)((1) << bitcount);
        bitcount += (int)(1);
        _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    }

    {
        bitbuf |= (uint)((1) << bitcount);
        bitcount += (int)(2);
        _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    }

    for (i = (int)(0); (i) < (16384); ++i)
    {
        hash_table[i] = null;
    }

    i = (int)(0);
    while ((i) < (data_len - 3))
    {
        int h = (int)(stbiw__zhash(data + i) & (16384 - 1));
        int best = (int)(3);
        byte* bestloc = null;
        byte** hlist = hash_table[h];
        int n = (int)(hlist != null ? ((int*)(hlist) - 2)[1] : 0);
        for (j = (int)(0); (j) < (n); ++j)
        {
            if ((hlist[j] - data) > (i - 32768))
            {
                int d = (int)(stbiw__zlib_countm(hlist[j], data + i, (int)(data_len - i)));
                if ((d) >= (best))
                {
                    best = (int)(d);
                    bestloc = hlist[j];
                }
            }
        }

        if (((hash_table[h]) != null) && ((((int*)(hash_table[h]) - 2)[1]) == (2 * quality)))
        {
            CRuntime.memmove(hash_table[h], hash_table[h] + quality, (ulong)(sizeof(byte*) * quality));
            ((int*)(hash_table[h]) - 2)[1] = (int)(quality);
        }

        if ((((hash_table[h]) == null) ||
             ((((int*)(hash_table[h]) - 2)[1] + (1)) >= (((int*)(hash_table[h]) - 2)[0]))))
        {
            stbiw__sbgrowf((void**)(&(hash_table[h])), (int)(1), sizeof(byte*));
        }

        (hash_table[h])[((int*)(hash_table[h]) - 2)[1]++] = (data + i);
        if ((bestloc) != null)
        {
            h = (int)(stbiw__zhash(data + i + 1) & (16384 - 1));
            hlist = hash_table[h];
            n = (int)(hlist != null ? ((int*)(hlist) - 2)[1] : 0);
            for (j = (int)(0); (j) < (n); ++j)
            {
                if ((hlist[j] - data) > (i - 32767))
                {
                    int e = (int)(stbiw__zlib_countm(hlist[j], data + i + 1, (int)(data_len - i - 1)));
                    if ((e) > (best))
                    {
                        bestloc = null;
                        break;
                    }
                }
            }
        }

        if ((bestloc) != null)
        {
            int d = (int)(data + i - bestloc);
            for (j = (int)(0); (best) > (lengthc[j + 1] - 1); ++j)
            {
            }

            if (j + 257 <= 143)
            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x30 + (j + 257)), (int)(8))) << bitcount);
                bitcount += (int)(8);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }
            else if (j + 257 <= 255)
            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x190 + (j + 257) - 144), (int)(9))) << bitcount);
                bitcount += (int)(9);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }
            else if (j + 257 <= 279)
            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0 + (j + 257) - 256), (int)(7))) << bitcount);
                bitcount += (int)(7);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }
            else
            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0xc0 + (j + 257) - 280), (int)(8))) << bitcount);
                bitcount += (int)(8);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }

            if ((lengtheb[j]) != 0)
            {
                bitbuf |= (uint)((best - lengthc[j]) << bitcount);
                bitcount += (int)(lengtheb[j]);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }

            for (j = (int)(0); (d) > (distc[j + 1] - 1); ++j)
            {
            }

            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(j), (int)(5))) << bitcount);
                bitcount += (int)(5);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }
            if ((disteb[j]) != 0)
            {
                bitbuf |= (uint)((d - distc[j]) << bitcount);
                bitcount += (int)(disteb[j]);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }

            i += (int)(best);
        }
        else
        {
            if (data[i] <= 143)
            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x30 + (data[i])), (int)(8))) << bitcount);
                bitcount += (int)(8);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }
            else
            {
                bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x190 + (data[i]) - 144), (int)(9))) << bitcount);
                bitcount += (int)(9);
                _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
            }

            ++i;
        }
    }

    for (; (i) < (data_len); ++i)
    {
        if (data[i] <= 143)
        {
            bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x30 + (data[i])), (int)(8))) << bitcount);
            bitcount += (int)(8);
            _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
        }
        else
        {
            bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x190 + (data[i]) - 144), (int)(9))) << bitcount);
            bitcount += (int)(9);
            _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
        }
    }

    // constant if-statements that only ever call one block for some reason
    //if (256 <= 143)
    //{
    //	bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x30 + (256)), (int)(8))) << bitcount);
    //	bitcount += (int)(8);
    //	_out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    //}
    //else if (256 <= 255)
    //{
    //	bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0x190 + (256) - 144), (int)(9))) << bitcount);
    //	bitcount += (int)(9);
    //	_out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    //}
    //else if (256 <= 279)
    //{
    bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0 + (256) - 256), (int)(7))) << bitcount);
    bitcount += (int)(7);
    _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    //}
    //else
    //{
    //	bitbuf |= (uint)((stbiw__zlib_bitrev((int)(0xc0 + (256) - 280), (int)(8))) << bitcount);
    //	bitcount += (int)(8);
    //	_out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    //}

    while ((bitcount) != 0)
    {
        bitbuf |= (uint)((0) << bitcount);
        bitcount += (int)(1);
        _out_ = stbiw__zlib_flushf(_out_, &bitbuf, &bitcount);
    }

    for (i = (int)(0); (i) < (16384); ++i)
    {
        if ((hash_table[i]) != null)
            CRuntime.free(((int*)(hash_table[i]) - 2));
    }

    CRuntime.free(hash_table);
    {
        uint s1 = (uint)(1);
        uint s2 = (uint)(0);
        int blocklen = (int)(data_len % 5552);
        j = (int)(0);
        while ((j) < (data_len))
        {
            for (i = (int)(0); (i) < (blocklen); ++i)
            {
                s1 += (uint)(data[j + i]);
                s2 += (uint)(s1);
            }

            s1 %= (uint)(65521);
            s2 %= (uint)(65521);
            j += (int)(blocklen);
            blocklen = (int)(5552);
        }

        if ((((_out_) == null) || ((((int*)(_out_) - 2)[1] + (1)) >= (((int*)(_out_) - 2)[0]))))
            stbiw__sbgrowf((void**)(&(_out_)), (int)(1), sizeof(byte));

        (_out_)[((int*)(_out_) - 2)[1]++] = ((byte)((s2 >> 8) & 0xff));
        if ((((_out_) == null) || ((((int*)(_out_) - 2)[1] + (1)) >= (((int*)(_out_) - 2)[0]))))
            stbiw__sbgrowf((void**)(&(_out_)), (int)(1), sizeof(byte));

        (_out_)[((int*)(_out_) - 2)[1]++] = ((byte)((s2) & 0xff));
        if ((((_out_) == null) || ((((int*)(_out_) - 2)[1] + (1)) >= (((int*)(_out_) - 2)[0]))))
            stbiw__sbgrowf((void**)(&(_out_)), (int)(1), sizeof(byte));

        (_out_)[((int*)(_out_) - 2)[1]++] = ((byte)((s1 >> 8) & 0xff));
        if ((((_out_) == null) || ((((int*)(_out_) - 2)[1] + (1)) >= (((int*)(_out_) - 2)[0]))))
            stbiw__sbgrowf((void**)(&(_out_)), (int)(1), sizeof(byte));

        (_out_)[((int*)(_out_) - 2)[1]++] = ((byte)((s1) & 0xff));
    }

    *out_len = (int)(((int*)(_out_) - 2)[1]);
    CRuntime.memmove(((int*)(_out_) - 2), _out_, (ulong)(*out_len));
    return (byte*)((int*)(_out_) - 2);
}

#endregion