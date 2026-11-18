using System.Globalization;

namespace XRayForge.Core.GameUi;

/// <summary>
/// Форматирование значений атрибутов игрового UI: инвариантная культура, целые без `.0`,
/// флаги `1`/`0` и экранирование XML при записи.
/// </summary>
public static class GameUiFormat
{
    /// <summary>Разобрать число (`1`, `1.5`, `-20`); лишние пробелы допускаются.</summary>
    public static bool TryNumber(string? text, out double value)
    {
        value = 0;
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0) return false;
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Число для атрибута: `0`, `16`, `1.5` (без хвостовых нулей).</summary>
    public static string Number(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
        var rounded = Math.Round(value, 3);
        if (Math.Abs(rounded - Math.Round(rounded)) < 0.0005)
            return ((long)Math.Round(rounded)).ToString(CultureInfo.InvariantCulture);
        return rounded.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>Значение флага как в X-Ray: `1` или `0`.</summary>
    public static string Flag(bool value) => value ? "1" : "0";

    /// <summary>Истина для флага: `1`, `true`, `yes` (регистр не важен); пусто/`0`/`false` → `false`.</summary>
    public static bool IsTrue(string? text) => (text ?? "").Trim().ToLowerInvariant() is "1" or "true" or "yes";

    /// <summary>Экранировать значение атрибута (`&amp;`, `&lt;`, `&gt;`, `&quot;`).</summary>
    public static string EscapeAttribute(string? text) => (text ?? "")
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>Экранировать текст элемента (`&amp;`, `&lt;`, `&gt;`).</summary>
    public static string EscapeText(string? text) => (text ?? "")
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
