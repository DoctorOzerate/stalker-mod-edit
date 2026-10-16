using XRayForge.Core.Imaging;

namespace XRayForge.Tests;

/// <summary>Примитивы `src/lib/dds.js`: `rgb565`, `rgbTo565`, `maskInfo`.</summary>
public class DdsPrimitivesTests
{
    [Theory]
    [InlineData(0xFFFF, 255, 255, 255, 255)]
    [InlineData(0x0000, 0, 0, 0, 255)]
    [InlineData(0xF800, 255, 0, 0, 255)]
    [InlineData(0x07E0, 0, 255, 0, 255)]
    [InlineData(0x001F, 0, 0, 255, 255)]
    [InlineData(0x0841, 8, 8, 8, 255)]
    [InlineData(0xCB26, 206, 101, 49, 255)]
    public void Rgb565_unpacks_channels(int packed, int r, int g, int b, int a)
    {
        Assert.Equal(new byte[] { (byte)r, (byte)g, (byte)b, (byte)a }, DdsCodec.Rgb565((ushort)packed));
    }

    [Theory]
    [InlineData(0, 0, 0, 0x0000)]
    [InlineData(255, 255, 255, 0xFFFF)]
    [InlineData(255, 0, 0, 0xF800)]
    [InlineData(0, 255, 0, 0x07E0)]
    [InlineData(0, 0, 255, 0x001F)]
    [InlineData(200, 100, 50, 0xCB26)]
    public void RgbTo565_packs_channels(int r, int g, int b, int packed)
    {
        Assert.Equal((ushort)packed, DdsCodec.RgbTo565(r, g, b));
    }

    [Fact]
    public void RgbTo565_then_Rgb565_quantizes_to_five_six_five()
    {
        // 200 → 5 бит (25) → 206; 100 → 6 бит (25) → 101; 50 → 5 бит (6) → 49
        Assert.Equal(new byte[] { 206, 101, 49, 255 }, DdsCodec.Rgb565(DdsCodec.RgbTo565(200, 100, 50)));
    }

    [Theory]
    [InlineData(0x00000000u, 0, 0)]
    [InlineData(0x000000FFu, 0, 8)]
    [InlineData(0x0000FF00u, 8, 8)]
    [InlineData(0x00FF0000u, 16, 8)]
    [InlineData(0xFF000000u, 24, 8)] // отклонение от JS: там знаковый сдвиг не завершает цикл
    [InlineData(0x0000F800u, 11, 5)]
    [InlineData(0x000007E0u, 5, 6)]
    [InlineData(0x0000001Fu, 0, 5)]
    [InlineData(0x00000F00u, 8, 4)]
    public void MaskInfo_reports_shift_and_bits(uint mask, int shift, int bits)
    {
        var info = DdsCodec.MaskInfo(mask);

        Assert.Equal(shift, info.Shift);
        Assert.Equal(bits, info.Bits);
    }
}
