using System.Buffers.Binary;
using XRayForge.Core.Imaging;

namespace XRayForge.Tests;

/// <summary>Порт случаев из `test/dds.test.mjs` — поведение совпадает с JS побайтово.</summary>
public class DdsTests
{
    [Fact]
    public void DecodeBc4_decodes_white_block()
    {
        var buf = new byte[] { 255, 0, 0, 0, 0, 0, 0, 0 };

        var px = DdsCodec.DecodeBc4(buf, 0, 4, 4);

        Assert.Equal(16 * 4, px.Length);
        Assert.Equal(255, px[0]);
        Assert.Equal(255, px[3]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, px[0..4]);
    }

    [Fact]
    public void DecodeBc5_decodes_red_and_green_channels()
    {
        var buf = new byte[16];
        buf[0] = 255; buf[1] = 0; // красный канал: a0 = 255
        buf[8] = 0; buf[9] = 0;   // зелёный канал: a0 = 0

        var px = DdsCodec.DecodeBc5(buf, 0, 4, 4);

        Assert.Equal(255, px[0]); // R
        Assert.Equal(0, px[1]);   // G
        Assert.Equal(255, px[2]); // B
        Assert.Equal(255, px[3]); // A
        Assert.Equal(new byte[] { 255, 0, 255, 255 }, px[0..4]);
    }

    [Fact]
    public void DownsampleRgba_averages_2x2_block()
    {
        var px = new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 };

        var outBytes = DdsCodec.DownsampleRgba(px, 2, 2);

        Assert.Equal(4, outBytes.Length);
        Assert.Equal(128, outBytes[0]); // (255+0+0+255)/4 = 127.5 → 128
        Assert.Equal(128, outBytes[1]);
        Assert.Equal(128, outBytes[2]);
        Assert.Equal(255, outBytes[3]);
    }

    [Fact]
    public void EncodeDds_with_mipmaps_writes_mip_chain_and_flags()
    {
        const int w = 8, h = 8;
        var pixels = new byte[w * h * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 3] = 255;
        }

        var outBytes = DdsCodec.EncodeDds(pixels, w, h, DdsFormat.Dxt1, true);
        var span = outBytes.AsSpan();

        Assert.Equal(0x20534444u, BinaryPrimitives.ReadUInt32LittleEndian(span));
        Assert.NotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(span[8..]) & 0x20000u);
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(span[28..])); // 8, 4, 2, 1
        Assert.Equal(128 + 32 + 8 + 8 + 8, outBytes.Length);
    }

    [Fact]
    public void EncodeDds_without_mipmaps_keeps_single_level()
    {
        var outBytes = DdsCodec.EncodeDds(new byte[16 * 4], 4, 4, DdsFormat.Dxt1, false);
        var span = outBytes.AsSpan();

        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(span[28..]));
        Assert.Equal(128 + 8, outBytes.Length);
    }
}
