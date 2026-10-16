using System.Text;
using System.Text.RegularExpressions;

namespace XRayForge.Core.Text;

/// <summary>
/// Декодирование/кодирование игровых текстовых файлов: BOM, XML-декларация encoding,
/// UTF-8 и Windows-1251.
/// </summary>
public static class GameEncoding
{
    private static readonly Encoding Cp1251;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static GameEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp1251 = Encoding.GetEncoding(1251);
    }

    /// <summary>Декодировать байты в текст (аналог decodeGameBytes).</summary>
    public static string Decode(byte[] bytes, string encoding = "auto")
    {
        var enc = encoding;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            enc = "utf-8";
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            enc = "utf-16le";
        else
        {
            var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(300, bytes.Length));
            var m = Regex.Match(head, @"<\?xml[^>]*encoding\s*=\s*[""']([^""']+)", RegexOptions.IgnoreCase);
            if (m.Success)
                enc = m.Groups[1].Value.ToLowerInvariant();
            else if (enc == "auto")
            {
                try { return StrictUtf8.GetString(bytes); }
                catch (DecoderFallbackException) { enc = "windows-1251"; }
            }
        }

        return Resolve(enc).GetString(bytes);
    }

    /// <summary>Кодировать текст в Windows-1251 (непредставимые символы → «?»).</summary>
    public static byte[] EncodeCp1251(string? s) => Cp1251.GetBytes(s ?? string.Empty);

    private static Encoding Resolve(string enc)
    {
        var e = enc.ToLowerInvariant();
        return e switch
        {
            "cp1251" or "windows-1251" or "win-1251" or "1251" => Cp1251,
            "utf-8" or "utf8" => Encoding.UTF8,
            "utf-16" or "utf-16le" or "utf-16-le" or "unicode" => Encoding.Unicode,
            "utf-16be" or "utf-16-be" or "bigendianunicode" => Encoding.BigEndianUnicode,
            _ => throw new NotSupportedException($"Неизвестная кодировка: {enc}")
        };
    }
}
