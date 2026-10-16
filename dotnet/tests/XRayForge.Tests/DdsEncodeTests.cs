using System.Buffers.Binary;
using XRayForge.Core.Imaging;

namespace XRayForge.Tests;

/// <summary>Кодирование `src/lib/dds.js`: блоки DXT, mip-цепочка, сборка контейнера.</summary>
public class DdsEncodeTests
{
    [Fact]
    public void EncodeDxtColorBlock_rejects_short_block()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.EncodeDxtColorBlock(new byte[60], true));

        Assert.Equal("Блок DXT должен содержать 16 RGBA-пикселей (64 байта), получено 60.", ex.Message);
    }

    [Fact]
    public void EncodeDxtColorBlock_writes_endpoints_and_indices_for_two_colors()
    {
        // первые 8 пикселей — чёрные, вторые 8 — белые
        var block = Block16(0, 0, 0, 255, 255, 255);

        var encoded = DdsCodec.EncodeDxtColorBlock(block, true);

        Assert.Equal(new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x55, 0x55, 0x00, 0x00 }, encoded);
    }

    [Fact]
    public void EncodeDxtColorBlock_writes_equal_endpoints_for_solid_color()
    {
        var block = Block16(200, 100, 50, 200, 100, 50);

        var encoded = DdsCodec.EncodeDxtColorBlock(block, true);

        // c0 = c1 = rgbTo565(200, 100, 50) = 0xCB26, все индексы указывают на c0
        Assert.Equal(new byte[] { 0x26, 0xCB, 0x26, 0xCB, 0, 0, 0, 0 }, encoded);
    }

    [Fact]
    public void EncodeDxtColorBlock_four_color_flag_is_irrelevant_for_solid_block()
    {
        var block = Block16(200, 100, 50, 200, 100, 50);

        // Квантование монотонно, поэтому c0 >= c1 всегда: обмен местами возможен только при равенстве.
        Assert.Equal(
            DdsCodec.EncodeDxtColorBlock(block, true),
            DdsCodec.EncodeDxtColorBlock(block, false));
    }

    [Fact]
    public void SampleBlock_clamps_to_image_edges()
    {
        var pixels = new byte[]
        {
            255, 0, 0, 255,     0, 255, 0, 255,
            0, 0, 255, 255,     255, 255, 255, 255
        };

        var block = DdsCodec.SampleBlock(pixels, 2, 2, 0, 0);

        Assert.Equal(64, block.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, block[0..4]);       // (0,0)
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, block[4..8]);       // (1,0)
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, block[16..20]);     // (0,1)
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, block[60..64]); // (3,3) → (1,1)
    }

    [Fact]
    public void EncodeDxt1_pads_small_images_to_one_block()
    {
        var expected = new byte[] { 0x26, 0xCB, 0x26, 0xCB, 0, 0, 0, 0 };

        Assert.Equal(expected, DdsCodec.EncodeDxt1(Solid(1, 1, 200, 100, 50), 1, 1));
        Assert.Equal(expected, DdsCodec.EncodeDxt1(Solid(2, 3, 200, 100, 50), 2, 3));
    }

    [Fact]
    public void EncodeDxt1_covers_all_blocks_for_image_larger_than_four()
    {
        // 5×7 → ceil(5/4) × ceil(7/4) = 2 × 2 блока
        Assert.Equal(4 * 8, DdsCodec.EncodeDxt1(Solid(5, 7, 10, 20, 30), 5, 7).Length);
        Assert.Equal(4 * 8, DdsCodec.EncodeDxt1(Solid(8, 8, 10, 20, 30), 8, 8).Length);
    }

    [Fact]
    public void EncodeDxt3_writes_alpha_nibbles_then_color()
    {
        var encoded = DdsCodec.EncodeDxt3(Solid(1, 1, 200, 100, 50), 1, 1);

        Assert.Equal(16, encoded.Length);
        Assert.Equal(
            new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 0x26, 0xCB, 0x26, 0xCB, 0, 0, 0, 0 },
            encoded);
    }

    [Fact]
    public void EncodeDxt3_writes_16_bytes_per_block()
    {
        Assert.Equal(16, DdsCodec.EncodeDxt3(Solid(4, 4, 10, 20, 30), 4, 4).Length);
        Assert.Equal(4 * 16, DdsCodec.EncodeDxt3(Solid(5, 7, 10, 20, 30), 5, 7).Length);
    }

    [Fact]
    public void EncodeDxt5_writes_alpha_endpoints_and_color()
    {
        var encoded = DdsCodec.EncodeDxt5(Solid(1, 1, 200, 100, 50), 1, 1);

        // Плоская альфа: minA = maxA → a0 = 255, a1 = 0, индекс 0
        Assert.Equal(16, encoded.Length);
        Assert.Equal(
            new byte[] { 255, 0, 0, 0, 0, 0, 0, 0, 0x26, 0xCB, 0x26, 0xCB, 0, 0, 0, 0 },
            encoded);
    }

    [Fact]
    public void EncodeDxt5_writes_16_bytes_per_block()
    {
        Assert.Equal(4 * 16, DdsCodec.EncodeDxt5(Solid(5, 7, 10, 20, 30), 5, 7).Length);
    }

    [Fact]
    public void Encode_rejects_too_small_pixel_buffer()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.EncodeDxt1(new byte[15], 2, 2));

        Assert.Equal("Пикселей меньше ожидаемого: 15 < 16.", ex.Message);
    }

    [Fact]
    public void Encode_rejects_invalid_dimensions()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DdsCodec.DownsampleRgba(new byte[16], 2, 0));

        Assert.Equal("Некорректные размеры изображения: 2x0", ex.Message);
    }

    [Fact]
    public void DownsampleRgba_rounds_half_up_like_js()
    {
        // (255 + 255 + 4 + 0) / 4 = 128.5 → Math.round даёт 129
        var pixels = new byte[]
        {
            255, 0, 0, 255,     255, 0, 0, 255,
            4, 0, 0, 255,       0, 0, 0, 255
        };

        var outBytes = DdsCodec.DownsampleRgba(pixels, 2, 2);

        Assert.Equal(new byte[] { 129, 0, 0, 255 }, outBytes);
    }

    [Fact]
    public void DownsampleRgba_keeps_single_pixel()
    {
        var outBytes = DdsCodec.DownsampleRgba(new byte[] { 10, 20, 30, 40 }, 1, 1);

        Assert.Equal(new byte[] { 10, 20, 30, 40 }, outBytes);
    }

    [Fact]
    public void DownsampleRgba_halves_odd_dimensions()
    {
        // 4×2 → 2×1: R = x·10, G = y·10
        var pixels = new byte[]
        {
            0, 10, 0, 255,   10, 10, 0, 255,   20, 10, 0, 255,   30, 10, 0, 255,
            0, 20, 0, 255,   10, 20, 0, 255,   20, 20, 0, 255,   30, 20, 0, 255
        };

        var outBytes = DdsCodec.DownsampleRgba(pixels, 4, 2);

        Assert.Equal(new byte[] { 5, 15, 0, 255, 25, 15, 0, 255 }, outBytes);
    }

    [Fact]
    public void BuildMipChain_halves_until_single_pixel()
    {
        var levels = DdsCodec.BuildMipChain(Solid(8, 8, 10, 20, 30), 8, 8);

        Assert.Equal(4, levels.Count);
        Assert.Equal(new[] { 8, 4, 2, 1 }, levels.Select(l => l.Width));
        Assert.Equal(new[] { 8, 4, 2, 1 }, levels.Select(l => l.Height));
        Assert.Equal(new[] { 256, 64, 16, 4 }, levels.Select(l => l.Pixels.Length));
    }

    [Fact]
    public void BuildMipChain_handles_odd_dimensions()
    {
        var levels = DdsCodec.BuildMipChain(Solid(6, 4, 10, 20, 30), 6, 4);

        Assert.Equal(3, levels.Count);
        Assert.Equal(new[] { 6, 3, 1 }, levels.Select(l => l.Width));
        Assert.Equal(new[] { 4, 2, 1 }, levels.Select(l => l.Height));
        Assert.Equal(new[] { 96, 24, 4 }, levels.Select(l => l.Pixels.Length));
    }

    [Fact]
    public void BuildMipChain_returns_single_level_for_single_pixel()
    {
        var levels = DdsCodec.BuildMipChain(new byte[] { 1, 2, 3, 4 }, 1, 1);

        Assert.Single(levels);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, levels[0].Pixels);
    }

    [Fact]
    public void BuildMipChain_averages_each_level()
    {
        var pixels = new byte[]
        {
            255, 255, 255, 255,   0, 0, 0, 0,
            0, 0, 0, 0,           0, 0, 0, 0
        };

        var levels = DdsCodec.BuildMipChain(pixels, 2, 2);

        Assert.Equal(2, levels.Count);
        Assert.Equal(2, levels[0].Width);
        Assert.Equal(1, levels[1].Width);
        Assert.Equal(new byte[] { 64, 64, 64, 64 }, levels[1].Pixels); // 255/4 = 63.75 → 64
    }

    [Fact]
    public void EncodeDds_writes_header_fields_with_mipmaps()
    {
        var outBytes = DdsCodec.EncodeDds(Solid(8, 8, 255, 0, 0), 8, 8, DdsFormat.Dxt1, true);
        var span = outBytes.AsSpan();

        Assert.Equal(184, outBytes.Length);
        Assert.Equal(0x20534444u, BinaryPrimitives.ReadUInt32LittleEndian(span));
        Assert.Equal(124u, BinaryPrimitives.ReadUInt32LittleEndian(span[4..]));
        Assert.Equal(
            (uint)(0x1 | 0x2 | 0x4 | 0x1000 | 0x80000 | 0x20000),
            BinaryPrimitives.ReadUInt32LittleEndian(span[8..]));
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(span[12..]));  // height
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(span[16..]));  // width
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(span[20..])); // topSize
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(span[28..]));  // mipCount
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(span[76..])); // размер DDS_PIXELFORMAT
        Assert.Equal(0x4u, BinaryPrimitives.ReadUInt32LittleEndian(span[80..]));
        Assert.Equal(0x31545844u, BinaryPrimitives.ReadUInt32LittleEndian(span[84..]));
        Assert.Equal(0x1000u, BinaryPrimitives.ReadUInt32LittleEndian(span[108..]));
    }

    [Fact]
    public void EncodeDds_omits_mipmap_flag_without_mipmaps()
    {
        var outBytes = DdsCodec.EncodeDds(Solid(4, 4, 0, 0, 0), 4, 4, DdsFormat.Dxt1, false);

        Assert.Equal(136, outBytes.Length);
        Assert.Equal(0x81007u, BinaryPrimitives.ReadUInt32LittleEndian(outBytes.AsSpan(8)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(outBytes.AsSpan(28)));
    }

    [Theory]
    [InlineData(DdsFormat.Dxt1, 0x31545844u, 136)]
    [InlineData(DdsFormat.Dxt3, 0x33545844u, 144)]
    [InlineData(DdsFormat.Dxt5, 0x35545844u, 144)]
    public void EncodeDds_maps_format_to_four_cc(DdsFormat format, uint fourCc, int length)
    {
        var outBytes = DdsCodec.EncodeDds(Solid(4, 4, 10, 20, 30), 4, 4, format, false);

        Assert.Equal(length, outBytes.Length);
        Assert.Equal(fourCc, BinaryPrimitives.ReadUInt32LittleEndian(outBytes.AsSpan(84)));
    }

    [Fact]
    public void EncodeDds_round_trips_solid_color_with_565_quantization()
    {
        var encoded = DdsCodec.EncodeDds(Solid(2, 3, 200, 100, 50), 2, 3, DdsFormat.Dxt1, false);

        var image = DdsCodec.DecodeDds(encoded);

        Assert.Equal(2, image.Width);
        Assert.Equal(3, image.Height);
        Assert.Equal(6 * 4, image.Pixels.Length);
        for (var i = 0; i < image.Pixels.Length; i += 4)
            Assert.Equal(new byte[] { 206, 101, 49, 255 }, image.Pixels[i..(i + 4)]); // 200 → 206, 100 → 101, 50 → 49
    }

    [Fact]
    public void EncodeDds_round_trips_checkerboard_without_color_error()
    {
        var encoded = DdsCodec.EncodeDds(Checkerboard(8, 8), 8, 8, DdsFormat.Dxt1, false);

        var image = DdsCodec.DecodeDds(encoded);

        Assert.Equal(new byte[] { 0, 0, 0, 255 }, PixelAt(image.Pixels, 8, 0, 0));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, PixelAt(image.Pixels, 8, 1, 0));
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, PixelAt(image.Pixels, 8, 0, 1));
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, PixelAt(image.Pixels, 8, 7, 7));
    }

    [Fact]
    public void EncodeDds_round_trips_dxt3_alpha_nibbles()
    {
        var encoded = DdsCodec.EncodeDds(AlphaRamp(4, 4), 4, 4, DdsFormat.Dxt3, false);

        var image = DdsCodec.DecodeDds(encoded);

        // Ни один ниббл 0..15 не теряется: 17·i → (17·i >> 4) → снова 17·i
        for (var i = 0; i < 16; i++)
            Assert.Equal(i * 17, image.Pixels[i * 4 + 3]);
        Assert.Equal(new byte[] { 206, 101, 49 }, PixelAt(image.Pixels, 4, 0, 0)[0..3]);
    }

    [Fact]
    public void EncodeDxt5_keeps_alpha_index_of_high_pixels()
    {
        var pixels = Solid(4, 4, 200, 100, 50);
        pixels[(2 * 4 + 3) * 4 + 3] = 0; // пиксель 11 — единственный прозрачный

        var encoded = DdsCodec.EncodeDxt5(pixels, 4, 4);

        // a0 = 255, a1 = 0, у пикселя 11 индекс 1 → биты 33..35, то есть байт 4 = 0x02.
        // В JS сдвиг маскируется по mod 32, и индекс попал бы в младший байт (0x02 в байте 0).
        Assert.Equal(16, encoded.Length);
        Assert.Equal(
            new byte[] { 255, 0, 0, 0, 0, 0, 2, 0, 0x26, 0xCB, 0x26, 0xCB, 0, 0, 0, 0 },
            encoded);
    }

    [Fact]
    public void EncodeDds_round_trips_dxt5_flat_alpha()
    {
        // 48-битные индексы альфы JS-энкодер пишет младшими байтами вперёд, а JS-декодер читает
        // старшим байтом вперёд, поэтому при неодинаковых индексах round-trip DXT5-альфы в JS
        // переставляет пиксели. Порт сохраняет обе стороны (паритет), так что round-trip
        // проверяется на плоской альфе, где сдвиг не наблюдаем.
        var encoded = DdsCodec.EncodeDds(Solid(4, 4, 200, 100, 50), 4, 4, DdsFormat.Dxt5, false);

        var image = DdsCodec.DecodeDds(encoded);

        Assert.Equal(144, encoded.Length);
        for (var i = 0; i < image.Pixels.Length; i += 4)
        {
            Assert.Equal(new byte[] { 206, 101, 49 }, image.Pixels[i..(i + 3)]);
            Assert.Equal(255, image.Pixels[i + 3]);
        }
    }

    [Fact]
    public void EncodeDds_round_trips_mipmapped_texture()
    {
        var encoded = DdsCodec.EncodeDds(Solid(4, 4, 255, 0, 0), 4, 4, DdsFormat.Dxt1, true);

        var image = DdsCodec.DecodeDds(encoded);

        Assert.Equal(152, encoded.Length); // 128 + (4,4) 8 + (2,2) 8 + (1,1) 8
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, image.Pixels[0..4]);
    }

    private static byte[] Block16(byte r, byte g, byte b, byte r2, byte g2, byte b2)
    {
        var block = new byte[64];
        for (var i = 0; i < 8; i++)
        {
            block[i * 4] = r;
            block[i * 4 + 1] = g;
            block[i * 4 + 2] = b;
            block[(i + 8) * 4] = r2;
            block[(i + 8) * 4 + 1] = g2;
            block[(i + 8) * 4 + 2] = b2;
        }
        return block;
    }

    private static byte[] Solid(int w, int h, byte r, byte g, byte b, byte a = 255)
    {
        var pixels = new byte[w * h * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = a;
        }
        return pixels;
    }

    /// <summary>Шахматка: чётные (x + y) — чёрные, нечётные — белые, альфа 255.</summary>
    private static byte[] Checkerboard(int w, int h)
    {
        var pixels = Solid(w, h, 0, 0, 0);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var v = (byte)((x + y) % 2 == 0 ? 0 : 255);
                var o = (y * w + x) * 4;
                pixels[o] = v;
                pixels[o + 1] = v;
                pixels[o + 2] = v;
            }
        }
        return pixels;
    }

    /// <summary>Пиксели с альфой i·17: шаг кратен 1/15 и 1/8, поэтому кодируется без потерь.</summary>
    private static byte[] AlphaRamp(int w, int h)
    {
        var pixels = Solid(w, h, 200, 100, 50);
        for (var i = 0; i < w * h; i++) pixels[i * 4 + 3] = (byte)(i * 17);
        return pixels;
    }

    private static byte[] PixelAt(byte[] pixels, int w, int x, int y)
    {
        var o = (y * w + x) * 4;
        return pixels[o..(o + 4)];
    }
}
