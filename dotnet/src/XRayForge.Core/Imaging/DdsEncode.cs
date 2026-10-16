using System.Buffers.Binary;

namespace XRayForge.Core.Imaging;

public static partial class DdsCodec
{
    /// <summary>encodeDxtColorBlock: 8 байт блока цвета (два RGB565 + 2 бита индекса на пиксель).</summary>
    public static byte[] EncodeDxtColorBlock(ReadOnlySpan<byte> block, bool fourColor)
    {
        if (block.Length < 64)
            throw new InvalidOperationException(
                "Блок DXT должен содержать 16 RGBA-пикселей (64 байта), получено " + block.Length + ".");

        int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
        for (var i = 0; i < 16; i++)
        {
            int r = block[i * 4], g = block[i * 4 + 1], b = block[i * 4 + 2];
            if (r < minR) minR = r;
            if (g < minG) minG = g;
            if (b < minB) minB = b;
            if (r > maxR) maxR = r;
            if (g > maxG) maxG = g;
            if (b > maxB) maxB = b;
        }

        int c0 = RgbTo565(maxR, maxG, maxB), c1 = RgbTo565(minR, minG, minB);
        if (fourColor && c0 <= c1)
        {
            (c0, c1) = (c1, c0);
            (maxR, minR) = (minR, maxR);
            (maxG, minG) = (minG, maxG);
            (maxB, minB) = (minB, maxB);
        }

        var cols = new int[4][];
        cols[0] = new[] { maxR, maxG, maxB };
        cols[1] = new[] { minR, minG, minB };
        if (c0 > c1)
        {
            cols[2] = new[] { (2 * maxR + minR) / 3, (2 * maxG + minG) / 3, (2 * maxB + minB) / 3 };
            cols[3] = new[] { (maxR + 2 * minR) / 3, (maxG + 2 * minG) / 3, (maxB + 2 * minB) / 3 };
        }
        else
        {
            cols[2] = new[] { (maxR + minR) / 2, (maxG + minG) / 2, (maxB + minB) / 2 };
            cols[3] = new[] { 0, 0, 0 };
        }

        var ind = 0u;
        for (var i = 0; i < 16; i++)
        {
            int r = block[i * 4], g = block[i * 4 + 1], b = block[i * 4 + 2];
            var best = 0;
            var bestDistance = 1e9;
            for (var c = 0; c < 4; c++)
            {
                var dr = cols[c][0] - r;
                var dg = cols[c][1] - g;
                var db = cols[c][2] - b;
                var d = dr * dr + dg * dg + db * db;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }

            ind |= (uint)best << (i * 2);
        }

        return new[]
        {
            (byte)(c0 & 255), (byte)((c0 >> 8) & 255),
            (byte)(c1 & 255), (byte)((c1 >> 8) & 255),
            (byte)(ind & 255), (byte)((ind >> 8) & 255),
            (byte)((ind >> 16) & 255), (byte)((ind >> 24) & 255)
        };
    }

    /// <summary>sampleBlock: 16 RGBA-пикселей блока 4×4 с прижатием к краю изображения.</summary>
    public static byte[] SampleBlock(ReadOnlySpan<byte> pixels, int w, int h, int bx, int by)
    {
        var block = new byte[64];
        var i = 0;
        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                var x = Math.Min(bx * 4 + px, w - 1);
                var y = Math.Min(by * 4 + py, h - 1);
                var o = (y * w + x) * 4;
                block[i++] = pixels[o];
                block[i++] = pixels[o + 1];
                block[i++] = pixels[o + 2];
                block[i++] = pixels[o + 3];
            }
        }

        return block;
    }

    /// <summary>
    /// Проверка размера RGBA-буфера. В JS выход за границы `Uint8Array` даёт `undefined`
    /// и молча портит результат — в порте это явная ошибка (см. примечания к <see cref="DdsCodec"/>).
    /// </summary>
    private static void EnsurePixelBuffer(ReadOnlySpan<byte> pixels, int w, int h)
    {
        if (w < 1 || h < 1)
            throw new InvalidOperationException("Некорректные размеры изображения: " + w + "x" + h);
        if (pixels.Length < w * h * 4)
            throw new InvalidOperationException(
                "Пикселей меньше ожидаемого: " + pixels.Length + " < " + (w * h * 4) + ".");
    }

    /// <summary>encodeDxt1: блоки 4×4 по 8 байт, прозрачность не кодируется (fourColor = false).</summary>
    public static byte[] EncodeDxt1(ReadOnlySpan<byte> pixels, int w, int h)
    {
        EnsurePixelBuffer(pixels, w, h);
        var outBytes = new List<byte>();
        var bw = Math.Max(1, (int)Math.Ceiling(w / 4.0));
        var bh = Math.Max(1, (int)Math.Ceiling(h / 4.0));
        for (var by = 0; by < bh; by++)
        {
            for (var bx = 0; bx < bw; bx++)
                outBytes.AddRange(EncodeDxtColorBlock(SampleBlock(pixels, w, h, bx, by), false));
        }

        return outBytes.ToArray();
    }

    /// <summary>encodeDxt3: 8 байт альфа-нибблов + 8 байт цвета на блок 4×4 (fourColor = true).</summary>
    public static byte[] EncodeDxt3(ReadOnlySpan<byte> pixels, int w, int h)
    {
        EnsurePixelBuffer(pixels, w, h);
        var outBytes = new List<byte>();
        var bw = Math.Max(1, (int)Math.Ceiling(w / 4.0));
        var bh = Math.Max(1, (int)Math.Ceiling(h / 4.0));
        for (var by = 0; by < bh; by++)
        {
            for (var bx = 0; bx < bw; bx++)
            {
                var block = SampleBlock(pixels, w, h, bx, by);
                for (var i = 0; i < 16; i += 2)
                {
                    var a0 = (block[i * 4 + 3] >> 4) & 0xF;
                    var a1 = (block[(i + 1) * 4 + 3] >> 4) & 0xF;
                    outBytes.Add((byte)((a0 & 0xF) | ((a1 & 0xF) << 4)));
                }

                outBytes.AddRange(EncodeDxtColorBlock(block, true));
            }
        }

        return outBytes.ToArray();
    }

    /// <summary>
    /// encodeDxt5: 2 байта альфа-эталона + 6 байт 3-битных индексов + 8 байт цвета.
    /// Индексы накапливаются в <see cref="ulong"/> (48 бит) — см. примечания о переполнении int32 в JS.
    /// </summary>
    public static byte[] EncodeDxt5(ReadOnlySpan<byte> pixels, int w, int h)
    {
        EnsurePixelBuffer(pixels, w, h);
        var outBytes = new List<byte>();
        var bw = Math.Max(1, (int)Math.Ceiling(w / 4.0));
        var bh = Math.Max(1, (int)Math.Ceiling(h / 4.0));
        for (var by = 0; by < bh; by++)
        {
            for (var bx = 0; bx < bw; bx++)
            {
                var block = SampleBlock(pixels, w, h, bx, by);
                int minA = 255, maxA = 0;
                for (var i = 0; i < 16; i++)
                {
                    int a = block[i * 4 + 3];
                    if (a < minA) minA = a;
                    if (a > maxA) maxA = a;
                }

                int a0 = maxA, a1 = minA;
                if (a0 <= a1)
                {
                    a0 = 255;
                    a1 = 0;
                }

                var alphas = new int[8];
                alphas[0] = a0;
                alphas[1] = a1;
                if (a0 > a1)
                {
                    alphas[2] = (6 * a0 + 1 * a1) / 7;
                    alphas[3] = (5 * a0 + 2 * a1) / 7;
                    alphas[4] = (4 * a0 + 3 * a1) / 7;
                    alphas[5] = (3 * a0 + 4 * a1) / 7;
                    alphas[6] = (2 * a0 + 5 * a1) / 7;
                    alphas[7] = (1 * a0 + 6 * a1) / 7;
                }
                else
                {
                    alphas[2] = (4 * a0 + 1 * a1) / 5;
                    alphas[3] = (3 * a0 + 2 * a1) / 5;
                    alphas[4] = (2 * a0 + 3 * a1) / 5;
                    alphas[5] = (1 * a0 + 4 * a1) / 5;
                    alphas[6] = 0;
                    alphas[7] = 255;
                }

                var alphaInd = 0ul;
                for (var i = 0; i < 16; i++)
                {
                    int a = block[i * 4 + 3];
                    var best = 0;
                    var bestDistance = 1e9;
                    for (var c = 0; c < 8; c++)
                    {
                        var d = Math.Abs(alphas[c] - a);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = c;
                        }
                    }

                    alphaInd |= (ulong)best << (i * 3);
                }

                outBytes.Add((byte)a0);
                outBytes.Add((byte)a1);
                for (var b = 0; b < 6; b++)
                    outBytes.Add((byte)((alphaInd >> (b * 8)) & 255));
                outBytes.AddRange(EncodeDxtColorBlock(block, true));
            }
        }

        return outBytes.ToArray();
    }

    /// <summary>downsampleRgba: уменьшение вдвое усреднением блока 2×2 (прижатие к краю).</summary>
    public static byte[] DownsampleRgba(ReadOnlySpan<byte> pixels, int w, int h)
    {
        EnsurePixelBuffer(pixels, w, h);
        var nw = Math.Max(1, w / 2);
        var nh = Math.Max(1, h / 2);
        var outBytes = new byte[nw * nh * 4];
        for (var y = 0; y < nh; y++)
        {
            for (var x = 0; x < nw; x++)
            {
                int r = 0, g = 0, b = 0, a = 0, n = 0;
                for (var dy = 0; dy < 2; dy++)
                {
                    for (var dx = 0; dx < 2; dx++)
                    {
                        var sx = Math.Min(x * 2 + dx, w - 1);
                        var sy = Math.Min(y * 2 + dy, h - 1);
                        var o = (sy * w + sx) * 4;
                        r += pixels[o];
                        g += pixels[o + 1];
                        b += pixels[o + 2];
                        a += pixels[o + 3];
                        n++;
                    }
                }

                var dst = (y * nw + x) * 4;
                outBytes[dst] = (byte)JsRound((double)r / n);
                outBytes[dst + 1] = (byte)JsRound((double)g / n);
                outBytes[dst + 2] = (byte)JsRound((double)b / n);
                outBytes[dst + 3] = (byte)JsRound((double)a / n);
            }
        }

        return outBytes;
    }

    /// <summary>buildMipChain: цепочка уровней от исходного до 1×1 (каждый следующий — ½ предыдущего).</summary>
    public static List<DdsMipLevel> BuildMipChain(ReadOnlySpan<byte> pixels, int w, int h)
    {
        EnsurePixelBuffer(pixels, w, h);
        var levels = new List<DdsMipLevel>
        {
            new() { Pixels = pixels.ToArray(), Width = w, Height = h }
        };

        var cw = w;
        var ch = h;
        var cur = pixels.ToArray();
        while (cw > 1 || ch > 1)
        {
            cur = DownsampleRgba(cur, cw, ch);
            cw = Math.Max(1, cw / 2);
            ch = Math.Max(1, ch / 2);
            levels.Add(new DdsMipLevel { Pixels = cur, Width = cw, Height = ch });
        }

        return levels;
    }

    /// <summary>
    /// encodeDds: собрать DDS-файл (заголовок 128 байт + блоки/уровни) из RGBA-пикселей.
    /// Соответствие форматов: `DdsFormat.Dxt1/Dxt3/Dxt5` → fourCC `DXT1/DXT3/DXT5`.
    /// </summary>
    public static byte[] EncodeDds(
        ReadOnlySpan<byte> pixels, int width, int height, DdsFormat format, bool mipmaps)
    {
        EnsurePixelBuffer(pixels, width, height);
        var fmt = format switch
        {
            DdsFormat.Dxt3 => 0x33545844u,
            DdsFormat.Dxt5 => 0x35545844u,
            _ => 0x31545844u
        };

        byte[] EncodeLevel(byte[] p, int w, int h) => fmt switch
        {
            0x33545844u => EncodeDxt3(p, w, h),
            0x35545844u => EncodeDxt5(p, w, h),
            _ => EncodeDxt1(p, w, h)
        };

        var levels = mipmaps
            ? BuildMipChain(pixels, width, height)
            : new List<DdsMipLevel> { new() { Pixels = pixels.ToArray(), Width = width, Height = height } };

        var payloads = levels.Select(l => EncodeLevel(l.Pixels, l.Width, l.Height)).ToList();
        var topSize = payloads[0].Length;
        var total = payloads.Sum(p => p.Length);

        var header = new byte[128];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], 0x20534444u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 124u);
        var flags = 0x1u | 0x2u | 0x4u | 0x1000u | 0x80000u;
        if (mipmaps) flags |= 0x20000u; // DDSD_MIPMAPCOUNT
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], flags);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)topSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)levels.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(span[76..], 32u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[80..], 0x4u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[84..], fmt);
        BinaryPrimitives.WriteUInt32LittleEndian(span[108..], 0x1000u);

        var outBytes = new byte[128 + total];
        header.CopyTo(outBytes, 0);
        var off = 128;
        foreach (var p in payloads)
        {
            p.CopyTo(outBytes, off);
            off += p.Length;
        }

        return outBytes;
    }
}