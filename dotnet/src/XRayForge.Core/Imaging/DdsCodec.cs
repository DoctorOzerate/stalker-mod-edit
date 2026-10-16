namespace XRayForge.Core.Imaging;

/// <summary>
/// Декодирование и кодирование DDS-текстур — порт `src/lib/dds.js`
/// (DXT1/DXT3/DXT5, BC4/BC5, несжатые 24/32-бит).
/// </summary>
/// <remarks>
/// Сознательные отклонения от JS-оригинала:
/// <list type="number">
/// <item>
///   <c>maskInfo</c> работает с <see cref="uint"/> (беззнаковые сдвиги). В JS сдвиг <c>&gt;&gt;</c>
///   знаковый: для маски со старшим битом (например <c>aMask = 0xFF000000</c>) цикл подсчёта
///   ширины канала не завершается вовсе. Для масок без старшего бита результат совпадает с JS.
/// </item>
/// <item>
///   <c>encodeDxt5</c> накапливает 3-битные индексы альфы в <see cref="ulong"/> (48 бит). В JS
///   <c>best &lt;&lt; (i * 3)</c> переполняет int32 и маскируется по mod 32, из-за чего индексы
///   пикселей 11–15 попадают в младшие биты и портят индексы пикселей 0–4. Наш вариант
///   корректно кодирует все 16 индексов; подробности — в MIGRATION.md (этап 3).
/// </item>
/// <item>
///   48-битные индексы альфы BC4/BC5/DXT5 (в JS — BigInt): энкодер выгружает их младшими байтами
///   вперёд, а декодер собирает старшим байтом вперёд со сдвигом <c>45 - i * 3</c>. Эта
///   рассинхронизация унаследована из JS и сохранена ради побайтового паритета: при разных
///   индексах round-trip альфы переставляет пиксели внутри блока (проверяется тестами
///   DecodeDxt5_expands_alpha_ramp и EncodeDxt5_keeps_alpha_index_of_high_pixels).
/// </item>
/// <item><c>pixelsToDataURL</c> не переносится: в Core нет canvas — конвертация в <c>BitmapSource</c> живёт в WPF-слое.</item>
/// </list>
/// </remarks>
public static partial class DdsCodec
{
    /// <summary>rgb565: распаковать 16-битный RGB565 в RGBA (альфа = 255).</summary>
    public static byte[] Rgb565(ushort v) => new[]
    {
        (byte)JsRound(((v >> 11) & 31) * 255 / 31.0),
        (byte)JsRound(((v >> 5) & 63) * 255 / 63.0),
        (byte)JsRound((v & 31) * 255 / 31.0),
        (byte)255
    };

    /// <summary>maskInfo: позиция и ширина канала в битовой маске (`{ shift, bits }`).</summary>
    public static ChannelMaskInfo MaskInfo(uint mask)
    {
        if (mask == 0) return new ChannelMaskInfo(0, 0);

        var shift = 0;
        while (((mask >> shift) & 1u) == 0) shift++;

        var m = mask >> shift;
        var bits = 0;
        while ((m & 1u) != 0)
        {
            bits++;
            m >>= 1;
        }

        return new ChannelMaskInfo(shift, bits);
    }

    /// <summary>rgbTo565: упаковать RGB-компоненты в 16-битный RGB565.</summary>
    public static ushort RgbTo565(int r, int g, int b) =>
        (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));

    /// <summary>
    /// Точный аналог JS <c>Math.round</c> (полуцелые — вверх): C# <c>Math.Round</c> по умолчанию
    /// использует банковское округление и дал бы расхождения (`Math.Round(2.5) = 2` против `3`).
    /// </summary>
    internal static int JsRound(double x) => (int)Math.Floor(x + 0.5);
}
