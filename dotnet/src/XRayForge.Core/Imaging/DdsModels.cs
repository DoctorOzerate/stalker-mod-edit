namespace XRayForge.Core.Imaging;

/// <summary>Формат сжатия DDS, поддерживаемый кодировщиком (`encodeDds`).</summary>
public enum DdsFormat
{
    Dxt1,
    Dxt3,
    Dxt5
}

/// <summary>Декодированное DDS-изображение: RGBA-пиксели, 4 байта на пиксель (`decodeDDS`).</summary>
public sealed class DdsImage
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Pixels { get; set; } = Array.Empty<byte>();
}

/// <summary>Уровень mip-цепочки (`buildMipChain`).</summary>
public sealed class DdsMipLevel
{
    public byte[] Pixels { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>Позиция и ширина канала в битовой маске (`maskInfo`): `{ shift, bits }`.</summary>
public readonly struct ChannelMaskInfo
{
    public ChannelMaskInfo(int shift, int bits)
    {
        Shift = shift;
        Bits = bits;
    }

    public int Shift { get; }

    public int Bits { get; }
}