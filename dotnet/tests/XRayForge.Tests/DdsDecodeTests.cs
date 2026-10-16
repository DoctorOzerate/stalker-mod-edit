using System.Buffers.Binary;
using XRayForge.Core.Imaging;

namespace XRayForge.Tests;

/// <summary>Декодирование `src/lib/dds.js`: блоки DXT1/3/5, BC4/BC5, несжатые форматы, контейнер.</summary>
public class DdsDecodeTests
{
    [Fact]
    public void DecodeDxt1_expands_four_colors()
    {
        // c0 = 0xF800 (красный) > c1 = 0x001F (синий) → 4 цвета; индексы 0,1,2,3 по кругу
        var block = new byte[] { 0x00, 0xF8, 0x1F, 0x00, 0xE4, 0xE4, 0xE4, 0xE4 };

        var px = DdsCodec.DecodeDxt1(block, 0, 4, 4);

        Assert.Equal(64, px.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px[0..4]);    // 0 — c0
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, px[4..8]);    // 1 — c1
        Assert.Equal(new byte[] { 170, 0, 85, 255 }, px[8..12]);  // 2 — 2/3·c0 + 1/3·c1
        Assert.Equal(new byte[] { 85, 0, 170, 255 }, px[12..16]); // 3 — 1/3·c0 + 2/3·c1
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px[16..20]);  // шаблон повторяется построчно
    }

    [Fact]
    public void DecodeDxt1_uses_transparent_black_when_c0_not_greater_than_c1()
    {
        // c0 = 0x001F (синий) < c1 = 0xF800 (красный) → 3 цвета, индекс 3 прозрачен
        var block = new byte[] { 0x1F, 0x00, 0x00, 0xF8, 0xE4, 0xE4, 0xE4, 0xE4 };

        var px = DdsCodec.DecodeDxt1(block, 0, 4, 4);

        Assert.Equal(new byte[] { 0, 0, 255, 255 }, px[0..4]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px[4..8]);
        Assert.Equal(new byte[] { 128, 0, 128, 255 }, px[8..12]); // (синий + красный) / 2
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, px[12..16]);
    }

    [Fact]
    public void DecodeDxt1_clips_blocks_to_image_size()
    {
        var data = new byte[16];
        data[0] = 0xFF; data[1] = 0xFF; // блок 0 — белый (c0 = c1 = 0xFFFF)
        // блок 1 остаётся чёрным (c0 = c1 = 0)

        var px = DdsCodec.DecodeDxt1(data, 0, 5, 3);

        Assert.Equal(5 * 3 * 4, px.Length);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, px[0..4]);   // (0,0)
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, px[52..56]); // (3,2) — из блока 0
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, px[16..20]);       // (4,0) — из блока 1
    }

    [Fact]
    public void DecodeDxt3_scales_alpha_nibbles()
    {
        var block = new byte[16];
        block[0] = 0x01; // пиксель 0 → ниббл 1 → 17, пиксель 1 → 0 → 0
        block[1] = 0x23; // пиксель 2 → 3 → 51, пиксель 3 → 2 → 34
        block[8] = 0x00; block[9] = 0xF8;   // c0 = 0xF800
        block[10] = 0x1F; block[11] = 0x00; // c1 = 0x001F
        block[12] = 0xE4; block[13] = 0xE4; block[14] = 0xE4; block[15] = 0xE4;

        var px = DdsCodec.DecodeDxt3(block, 0, 4, 4);

        Assert.Equal(new byte[] { 255, 0, 0, 17 }, px[0..4]);
        Assert.Equal(new byte[] { 0, 0, 255, 0 }, px[4..8]);
        Assert.Equal(new byte[] { 170, 0, 85, 51 }, px[8..12]);
        Assert.Equal(new byte[] { 85, 0, 170, 34 }, px[12..16]);
        Assert.Equal(new byte[] { 255, 0, 0, 0 }, px[16..20]); // у остальных пикселей альфа 0
    }

    [Fact]
    public void DecodeDxt5_expands_alpha_ramp()
    {
        // a0 = 255 > a1 = 0 → уровни: 255, 0, 219, 182, 146, 109, 73, 36 (индексам 0..7)
        var block = new byte[16];
        block[0] = 255;
        block[1] = 0;
        // 3 бита на пиксель; декодер читает 6 байт как 48-битное число (старший байт — первый)
        block[2] = 0x05; block[3] = 0x39; block[4] = 0x77;
        block[5] = 0x05; block[6] = 0x39; block[7] = 0x77;
        block[8] = 0x00; block[9] = 0xF8;   // c0 = 0xF800
        block[10] = 0x1F; block[11] = 0x00; // c1 = 0x001F
        block[12] = 0xE4; block[13] = 0xE4; block[14] = 0xE4; block[15] = 0xE4;

        var px = DdsCodec.DecodeDxt5(block, 0, 4, 4);

        Assert.Equal(new byte[] { 255, 0, 0, 255 }, px[0..4]);
        Assert.Equal(new byte[] { 0, 0, 255, 0 }, px[4..8]);
        Assert.Equal(new byte[] { 170, 0, 85, 219 }, px[8..12]);
        Assert.Equal(new byte[] { 85, 0, 170, 182 }, px[12..16]);
        Assert.Equal(new byte[] { 255, 0, 0, 146 }, px[16..20]);
        Assert.Equal(new byte[] { 0, 0, 255, 109 }, px[20..24]);
        Assert.Equal(new byte[] { 170, 0, 85, 73 }, px[24..28]);
        Assert.Equal(new byte[] { 85, 0, 170, 36 }, px[28..32]);
    }

    [Fact]
    public void DecodeDxt5_interpolates_alpha_when_a0_not_greater_than_a1()
    {
        var block = new byte[16];
        block[0] = 0;   // a0 = 0 <= a1 = 255 → уровни: 0, 255, 51, 102, 153, 204, 0, 255
        block[1] = 255;
        block[2] = 0x40; // индекс пикселя 0 = 2 → floor(255/5) = 51; у остальных 0
        block[8] = 0x00; block[9] = 0xF8;   // c0 = 0xF800
        block[10] = 0xF8; block[11] = 0x00; // c1 = 0xF800 (плоский цвет)

        var px = DdsCodec.DecodeDxt5(block, 0, 4, 4);

        Assert.Equal(new byte[] { 255, 0, 0, 51 }, px[0..4]);
        Assert.Equal(new byte[] { 255, 0, 0, 0 }, px[4..8]);
    }

    [Fact]
    public void DecodeBc4_expands_interpolated_levels()
    {
        // a0 = 255 > a1 = 0 → уровни 255, 0, 219, 182, 146, 109, 73, 36; индексы 0..7 дважды
        var data = new byte[] { 255, 0, 0x05, 0x39, 0x77, 0x05, 0x39, 0x77 };

        var px = DdsCodec.DecodeBc4(data, 0, 4, 4);

        Assert.Equal(64, px.Length);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, px[0..4]);
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, px[4..8]);
        Assert.Equal(new byte[] { 219, 219, 219, 255 }, px[8..12]);
        Assert.Equal(new byte[] { 182, 182, 182, 255 }, px[12..16]);
        Assert.Equal(new byte[] { 146, 146, 146, 255 }, px[16..20]);
        Assert.Equal(new byte[] { 36, 36, 36, 255 }, px[28..32]);
    }

    [Fact]
    public void DecodeBc4_honours_offset()
    {
        var data = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0 };

        var px = DdsCodec.DecodeBc4(data, 8, 4, 4);

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, px[0..4]);
    }

    [Fact]
    public void DecodeBc5_reads_two_single_channel_blocks()
    {
        var data = new byte[16];
        data[0] = 0x0F; data[1] = 0x0F; // красный канал: a0 = a1 = 15
        data[8] = 0xF0; data[9] = 0xF0; // зелёный канал: a0 = a1 = 240

        var px = DdsCodec.DecodeBc5(data, 0, 4, 4);

        Assert.Equal(64, px.Length);
        Assert.Equal(new byte[] { 0x0F, 0xF0, 255, 255 }, px[0..4]);
        Assert.Equal(new byte[] { 0x0F, 0xF0, 255, 255 }, px[60..64]);
    }
}
