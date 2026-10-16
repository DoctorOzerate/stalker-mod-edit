using System.Buffers.Binary;

namespace XRayForge.Core.Imaging;

public static partial class DdsCodec
{
    /// <summary>decodeDxt1: блоки 4×4 по 8 байт (два RGB565 + 2 бита индекса на пиксель).</summary>
    public static byte[] DecodeDxt1(ReadOnlySpan<byte> data, int offset, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var by = 0; by < height; by += 4)
        {
            for (var bx = 0; bx < width; bx += 4)
            {
                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]);
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);
                offset += 8;

                var col0 = Rgb565(c0);
                var col1 = Rgb565(c1);
                var cols = new byte[4][];
                cols[0] = col0;
                cols[1] = col1;
                if (c0 > c1)
                {
                    cols[2] = Mix(2, col0, 1, col1, 3);
                    cols[3] = Mix(1, col0, 2, col1, 3);
                }
                else
                {
                    cols[2] = Mix(1, col0, 1, col1, 2);
                    cols[3] = new byte[] { 0, 0, 0, 0 };
                }

                WriteColorBlock(pixels, width, height, bx, by, cols, bits);
            }
        }

        return pixels;
    }

    /// <summary>decodeDxt3: блоки 4×4 по 16 байт (8 байт альфа-нибблов + цвет).</summary>
    public static byte[] DecodeDxt3(ReadOnlySpan<byte> data, int offset, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var by = 0; by < height; by += 4)
        {
            for (var bx = 0; bx < width; bx += 4)
            {
                var alpha = data.Slice(offset, 8);
                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 8)..]);
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 10)..]);
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 12)..]);
                offset += 16;

                var col0 = Rgb565(c0);
                var col1 = Rgb565(c1);
                var cols = new byte[4][]
                {
                    col0,
                    col1,
                    Mix(2, col0, 1, col1, 3),
                    Mix(1, col0, 2, col1, 3)
                };

                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        var px = bx + x;
                        var py = by + y;
                        if (px >= width || py >= height) continue;

                        var i = y * 4 + x;
                        var nibble = (alpha[i >> 1] >> ((i & 1) * 4)) & 15;
                        var idx = (int)((bits >> (2 * i)) & 3u);
                        var o = (py * width + px) * 4;
                        pixels[o] = cols[idx][0];
                        pixels[o + 1] = cols[idx][1];
                        pixels[o + 2] = cols[idx][2];
                        pixels[o + 3] = (byte)JsRound(nibble * 255 / 15.0);
                    }
                }
            }
        }

        return pixels;
    }

    /// <summary>Интерполяция пары RGB565-цветов: round((ka·c0 + kb·c1) / div), альфа = 255.</summary>
    private static byte[] Mix(int ka, byte[] c0, int kb, byte[] c1, int div) => new[]
    {
        (byte)JsRound((ka * c0[0] + kb * c1[0]) / (double)div),
        (byte)JsRound((ka * c0[1] + kb * c1[1]) / (double)div),
        (byte)JsRound((ka * c0[2] + kb * c1[2]) / (double)div),
        (byte)255
    };

    /// <summary>Пиксели блока 4×4: индекс 2 бита на пиксель, цвет берётся из <paramref name="cols"/>.</summary>
    private static void WriteColorBlock(
        byte[] pixels, int width, int height, int bx, int by, byte[][] cols, uint bits)
    {
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var px = bx + x;
                var py = by + y;
                if (px >= width || py >= height) continue;

                var idx = (int)((bits >> (2 * (y * 4 + x))) & 3u);
                var o = (py * width + px) * 4;
                pixels[o] = cols[idx][0];
                pixels[o + 1] = cols[idx][1];
                pixels[o + 2] = cols[idx][2];
                pixels[o + 3] = cols[idx][3];
            }
        }
    }

    /// <summary>decodeDxt5: 8 байт альфа-канала (2 опорных + 3-битные индексы) и 8 байт цвета.</summary>
    public static byte[] DecodeDxt5(ReadOnlySpan<byte> data, int offset, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var by = 0; by < height; by += 4)
        {
            for (var bx = 0; bx < width; bx += 4)
            {
                var a0 = data[offset];
                var a1 = data[offset + 1];
                ulong alphaBits = 0;
                for (var i = 0; i < 6; i++) alphaBits = (alphaBits << 8) | data[offset + 2 + i];

                var alphaVals = new List<int> { a0, a1 };
                if (a0 > a1)
                {
                    for (var i = 1; i <= 6; i++) alphaVals.Add(JsRound(a0 + (a1 - a0) * i / 7.0));
                }
                else
                {
                    for (var i = 1; i <= 4; i++) alphaVals.Add(JsRound(a0 + (a1 - a0) * i / 5.0));
                    alphaVals.Add(0);
                    alphaVals.Add(255);
                }

                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 8)..]);
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 10)..]);
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 12)..]);
                offset += 16;

                var col0 = Rgb565(c0);
                var col1 = Rgb565(c1);
                var cols = new byte[4][]
                {
                    col0,
                    col1,
                    Mix(2, col0, 1, col1, 3),
                    Mix(1, col0, 2, col1, 3)
                };

                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        var px = bx + x;
                        var py = by + y;
                        if (px >= width || py >= height) continue;

                        var i = y * 4 + x;
                        var ai = (int)((alphaBits >> (45 - i * 3)) & 7UL);
                        var idx = (int)((bits >> (2 * i)) & 3u);
                        var o = (py * width + px) * 4;
                        pixels[o] = cols[idx][0];
                        pixels[o + 1] = cols[idx][1];
                        pixels[o + 2] = cols[idx][2];
                        pixels[o + 3] = ai < alphaVals.Count ? (byte)alphaVals[ai] : (byte)255;
                    }
                }
            }
        }

        return pixels;
    }

    /// <summary>decodeBc4Values: 16 альфа-значений блока BC4 (2 опорных + 3-битные индексы).</summary>
    private static byte[] DecodeBc4Values(ReadOnlySpan<byte> data, int offset)
    {
        var a0 = data[offset];
        var a1 = data[offset + 1];
        ulong bits = 0;
        for (var i = 0; i < 6; i++) bits = (bits << 8) | data[offset + 2 + i];

        var vals = new List<int> { a0, a1 };
        if (a0 > a1)
        {
            for (var i = 1; i <= 6; i++) vals.Add(JsRound(a0 + (a1 - a0) * i / 7.0));
        }
        else
        {
            for (var i = 1; i <= 4; i++) vals.Add(JsRound(a0 + (a1 - a0) * i / 5.0));
            vals.Add(0);
            vals.Add(255);
        }

        var result = new byte[16];
        for (var i = 0; i < 16; i++)
        {
            var idx = (int)((bits >> (45 - i * 3)) & 7UL);
            result[i] = idx < vals.Count ? (byte)vals[idx] : (byte)255;
        }
        return result;
    }

    /// <summary>decodeBc4: одноканальный блок (значения в R/G/B, альфа = 255), 8 байт на блок.</summary>
    public static byte[] DecodeBc4(ReadOnlySpan<byte> data, int offset, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var by = 0; by < height; by += 4)
        {
            for (var bx = 0; bx < width; bx += 4)
            {
                var vals = DecodeBc4Values(data, offset);
                offset += 8;
                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        var px = bx + x;
                        var py = by + y;
                        if (px >= width || py >= height) continue;

                        var v = vals[y * 4 + x];
                        var o = (py * width + px) * 4;
                        pixels[o] = v;
                        pixels[o + 1] = v;
                        pixels[o + 2] = v;
                        pixels[o + 3] = 255;
                    }
                }
            }
        }

        return pixels;
    }

    /// <summary>decodeBc5: два блока BC4 — красный и зелёный каналы, синий = 255, 16 байт на блок.</summary>
    public static byte[] DecodeBc5(ReadOnlySpan<byte> data, int offset, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var by = 0; by < height; by += 4)
        {
            for (var bx = 0; bx < width; bx += 4)
            {
                var r = DecodeBc4Values(data, offset);
                offset += 8;
                var g = DecodeBc4Values(data, offset);
                offset += 8;
                for (var y = 0; y < 4; y++)
                {
                    for (var x = 0; x < 4; x++)
                    {
                        var px = bx + x;
                        var py = by + y;
                        if (px >= width || py >= height) continue;

                        var o = (py * width + px) * 4;
                        pixels[o] = r[y * 4 + x];
                        pixels[o + 1] = g[y * 4 + x];
                        pixels[o + 2] = 255;
                        pixels[o + 3] = 255;
                    }
                }
            }
        }

        return pixels;
    }

    /// <summary>decodeUncompressed: несжатые 24/32-битные пиксели с битовыми масками каналов.</summary>
    public static byte[] DecodeUncompressed(
        ReadOnlySpan<byte> data, int offset, int width, int height,
        int bitCount, uint rMask, uint gMask, uint bMask, uint aMask)
    {
        var pixels = new byte[width * height * 4];
        var ri = MaskInfo(rMask);
        var gi = MaskInfo(gMask);
        var bi = MaskInfo(bMask);
        var ai = MaskInfo(aMask);
        var total = width * height;

        if (bitCount == 32)
        {
            for (var i = 0; i < total; i++)
            {
                var c = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + i * 4)..]);
                var o = i * 4;
                pixels[o] = Scale((c & rMask) >> ri.Shift, ri);
                pixels[o + 1] = Scale((c & gMask) >> gi.Shift, gi);
                pixels[o + 2] = Scale((c & bMask) >> bi.Shift, bi);
                pixels[o + 3] = Scale((c & aMask) >> ai.Shift, ai);
            }
            return pixels;
        }

        if (bitCount == 24)
        {
            for (var i = 0; i < total; i++)
            {
                var p = offset + i * 3;
                var c = (uint)(data[p] | (data[p + 1] << 8) | (data[p + 2] << 16));
                var o = i * 4;
                pixels[o] = Scale((c & rMask) >> ri.Shift, ri);
                pixels[o + 1] = Scale((c & gMask) >> gi.Shift, gi);
                pixels[o + 2] = Scale((c & bMask) >> bi.Shift, bi);
                pixels[o + 3] = 255;
            }
            return pixels;
        }

        throw new InvalidOperationException("Неподдерживаемая битность: " + bitCount);
    }

    /// <summary>scale: канал → 8 бит (маски нет → 255, ровно 8 бит → как есть, иначе масштаб).</summary>
    private static byte Scale(uint v, ChannelMaskInfo info)
    {
        if (info.Bits == 0) return 255;
        if (info.Bits == 8) return (byte)v;
        return (byte)JsRound(v * 255.0 / ((1 << info.Bits) - 1));
    }

    /// <summary>decodeDDS: заголовок контейнера, выбор кодека по FourCC или маскам, RGBA-пиксели.</summary>
    public static DdsImage DecodeDds(byte[] buffer)
    {
        if (buffer.Length < 128) throw new InvalidOperationException("Файл слишком мал для DDS.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != 0x20534444)
            throw new InvalidOperationException("Неверная сигнатура DDS.");

        var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12));
        var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(16));
        if (width < 1 || height < 1 || width > 8192 || height > 8192)
            throw new InvalidOperationException("Некорректные размеры DDS: " + width + "x" + height);

        var pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(80));
        var fourCC = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(84));
        var rgbBitCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(88));
        var rMask = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(92));
        var gMask = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(96));
        var bMask = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(100));
        var aMask = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(104));

        var fourCCStr = new string(new[]
        {
            (char)(fourCC & 255),
            (char)((fourCC >> 8) & 255),
            (char)((fourCC >> 16) & 255),
            (char)((fourCC >> 24) & 255)
        });

        var blocks = Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4);
        byte[] pixels;
        switch (fourCCStr)
        {
            case "DXT1":
                if (buffer.Length < 128 + blocks * 8) throw new InvalidOperationException("Обрезанный DXT1.");
                pixels = DecodeDxt1(buffer, 128, width, height);
                break;
            case "DXT3":
                if (buffer.Length < 128 + blocks * 16) throw new InvalidOperationException("Обрезанный DXT3.");
                pixels = DecodeDxt3(buffer, 128, width, height);
                break;
            case "DXT5":
                if (buffer.Length < 128 + blocks * 16) throw new InvalidOperationException("Обрезанный DXT5.");
                pixels = DecodeDxt5(buffer, 128, width, height);
                break;
            case "BC4U":
            case "BC4S":
            case "ATI1":
                if (buffer.Length < 128 + blocks * 8) throw new InvalidOperationException("Обрезанный BC4.");
                pixels = DecodeBc4(buffer, 128, width, height);
                break;
            case "BC5U":
            case "BC5S":
            case "ATI2":
                if (buffer.Length < 128 + blocks * 16) throw new InvalidOperationException("Обрезанный BC5.");
                pixels = DecodeBc5(buffer, 128, width, height);
                break;
            default:
                if ((pfFlags & 0x40u) != 0)
                {
                    pixels = DecodeUncompressed(buffer, 128, width, height, rgbBitCount, rMask, gMask, bMask, aMask);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Неподдерживаемый формат DDS: " + (fourCCStr.Length > 0 ? fourCCStr : "RGBA без масок"));
                }
                break;
        }

        return new DdsImage { Width = width, Height = height, Pixels = pixels };
    }
}
