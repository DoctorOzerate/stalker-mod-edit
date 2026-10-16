using System.Buffers.Binary;
using XRayForge.Core.Imaging;

namespace XRayForge.Tests;

/// <summary>Разбор контейнера DDS и несжатые форматы (`decodeDDS`, `decodeUncompressed`).</summary>
public class DdsContainerTests
{
    [Fact]
    public void DecodeUncompressed_reads_a8r8g8b8()
    {
        var data = new byte[] { 50, 100, 200, 128 }; // B, G, R, A

        var px = DdsCodec.DecodeUncompressed(
            data, 0, 1, 1, 32, 0x00FF0000u, 0x0000FF00u, 0x000000FFu, 0xFF000000u);

        Assert.Equal(new byte[] { 200, 100, 50, 128 }, px);
    }

    [Fact]
    public void DecodeUncompressed_scales_narrow_masks_and_defaults_missing_alpha()
    {
        var data = new byte[] { 0x41, 0x08, 0x00, 0x00 }; // 0x00000841 = R1 G2 B1

        var px = DdsCodec.DecodeUncompressed(data, 0, 1, 1, 32, 0xF800u, 0x07E0u, 0x001Fu, 0u);

        Assert.Equal(new byte[] { 8, 8, 8, 255 }, px);
    }

    [Fact]
    public void DecodeUncompressed_reads_24_bit_pixels()
    {
        var data = new byte[] { 50, 100, 200 }; // B, G, R

        var px = DdsCodec.DecodeUncompressed(
            data, 0, 1, 1, 24, 0x00FF0000u, 0x0000FF00u, 0x000000FFu, 0u);

        Assert.Equal(new byte[] { 200, 100, 50, 255 }, px);
    }

    [Fact]
    public void DecodeUncompressed_rejects_other_bit_depths()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DdsCodec.DecodeUncompressed(new byte[4], 0, 1, 1, 16, 0xF800u, 0x07E0u, 0x001Fu, 0u));

        Assert.Equal("Неподдерживаемая битность: 16", ex.Message);
    }

    [Fact]
    public void DecodeDds_rejects_short_buffer()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.DecodeDds(new byte[127]));

        Assert.Equal("Файл слишком мал для DDS.", ex.Message);
    }

    [Fact]
    public void DecodeDds_rejects_bad_signature()
    {
        var data = Header(4, 4, "DXT1");
        data[0] = 0;

        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.DecodeDds(data));

        Assert.Equal("Неверная сигнатура DDS.", ex.Message);
    }

    [Theory]
    [InlineData(0, 4, "Некорректные размеры DDS: 0x4")]
    [InlineData(4, 0, "Некорректные размеры DDS: 4x0")]
    [InlineData(8193, 4, "Некорректные размеры DDS: 8193x4")]
    [InlineData(4, 8193, "Некорректные размеры DDS: 4x8193")]
    public void DecodeDds_rejects_out_of_range_dimensions(int width, int height, string message)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DdsCodec.DecodeDds(Header(width, height, "DXT1")));

        Assert.Equal(message, ex.Message);
    }

    [Theory]
    [InlineData("DXT1", 4, "Обрезанный DXT1.")]
    [InlineData("DXT3", 8, "Обрезанный DXT3.")]
    [InlineData("DXT5", 8, "Обрезанный DXT5.")]
    [InlineData("ATI1", 4, "Обрезанный BC4.")]
    [InlineData("ATI2", 8, "Обрезанный BC5.")]
    public void DecodeDds_rejects_truncated_payload(string fourCc, int payloadSize, string message)
    {
        var data = Append(Header(4, 4, fourCc), new byte[payloadSize]);

        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.DecodeDds(data));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void DecodeDds_rejects_unsupported_format()
    {
        var data = Append(Header(4, 4, "DX10"), new byte[32]);

        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.DecodeDds(data));

        Assert.Equal("Неподдерживаемый формат DDS: DX10", ex.Message);
    }

    [Fact]
    public void DecodeDds_reports_missing_four_cc_like_js()
    {
        // В JS String.fromCharCode(0, 0, 0, 0) непуста, поэтому в сообщение попадают NUL-символы.
        var data = Append(Header(4, 4, "\0\0\0\0", pfFlags: 0u), new byte[8]);

        var ex = Assert.Throws<InvalidOperationException>(() => DdsCodec.DecodeDds(data));

        Assert.Equal("Неподдерживаемый формат DDS: \0\0\0\0", ex.Message);
    }

    [Theory]
    [InlineData("BC4U")]
    [InlineData("BC4S")]
    [InlineData("ATI1")]
    public void DecodeDds_routes_bc4_four_cc_to_single_channel(string fourCc)
    {
        var data = Append(Header(4, 4, fourCc), new byte[] { 255, 0, 0, 0, 0, 0, 0, 0 });

        var image = DdsCodec.DecodeDds(data);

        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, image.Pixels[0..4]);
    }

    [Theory]
    [InlineData("BC5U")]
    [InlineData("BC5S")]
    [InlineData("ATI2")]
    public void DecodeDds_routes_bc5_four_cc_to_two_channels(string fourCc)
    {
        var payload = new byte[16];
        payload[0] = 255; payload[1] = 0; // красный канал = 255
        payload[8] = 0; payload[9] = 255; // зелёный канал = 0

        var image = DdsCodec.DecodeDds(Append(Header(4, 4, fourCc), payload));

        Assert.Equal(new byte[] { 255, 0, 255, 255 }, image.Pixels[0..4]);
    }

    [Fact]
    public void DecodeDds_reads_uncompressed_container()
    {
        var header = Header(1, 1, "\0\0\0\0", bitCount: 32, pfFlags: 0x40u,
            rMask: 0x00FF0000u, gMask: 0x0000FF00u, bMask: 0x000000FFu, aMask: 0xFF000000u);

        var image = DdsCodec.DecodeDds(Append(header, new byte[] { 50, 100, 200, 128 }));

        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(new byte[] { 200, 100, 50, 128 }, image.Pixels);
    }

    [Fact]
    public void DecodeDds_reads_dxt1_payload_of_non_multiple_of_four_size()
    {
        var payload = new byte[16];
        payload[0] = 0xFF; payload[1] = 0xFF; // блок 0 — белый, блок 1 — чёрный

        var image = DdsCodec.DecodeDds(Append(Header(5, 3, "DXT1"), payload));

        Assert.Equal(5, image.Width);
        Assert.Equal(3, image.Height);
        Assert.Equal(5 * 3 * 4, image.Pixels.Length);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, image.Pixels[0..4]);  // (0,0)
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, image.Pixels[16..20]);      // (4,0)
    }

    private static byte[] Header(
        int width, int height, string fourCc, int bitCount = 0, uint pfFlags = 0x4u,
        uint rMask = 0u, uint gMask = 0u, uint bMask = 0u, uint aMask = 0u)
    {
        var header = new byte[128];
        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, 0x20534444u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 124u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], 0x1007u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(span[76..], 32u);
        BinaryPrimitives.WriteUInt32LittleEndian(span[80..], pfFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(span[84..], FourCc(fourCc));
        BinaryPrimitives.WriteUInt32LittleEndian(span[88..], (uint)bitCount);
        BinaryPrimitives.WriteUInt32LittleEndian(span[92..], rMask);
        BinaryPrimitives.WriteUInt32LittleEndian(span[96..], gMask);
        BinaryPrimitives.WriteUInt32LittleEndian(span[100..], bMask);
        BinaryPrimitives.WriteUInt32LittleEndian(span[104..], aMask);
        return header;
    }

    private static uint FourCc(string s) =>
        (uint)(s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24));

    private static byte[] Append(byte[] header, byte[] payload)
    {
        var result = new byte[header.Length + payload.Length];
        header.CopyTo(result, 0);
        payload.CopyTo(result, header.Length);
        return result;
    }
}
