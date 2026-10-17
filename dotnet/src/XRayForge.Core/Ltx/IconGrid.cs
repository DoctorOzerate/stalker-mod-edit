using System.Globalization;
using System.Text.RegularExpressions;

namespace XRayForge.Core.Ltx;

/// <summary>Пресет атласа иконок: только размер ячейки, без фиксированного числа колонок.</summary>
public sealed class AtlasPreset
{
    public string Label { get; set; } = "";
    public int CellW { get; set; }
    public int CellH { get; set; }

    /// <summary>WPF `ComboBox` в кастомном шаблоне показывает элемент через `ToString`.</summary>
    public override string ToString() => Label;
}

/// <summary>Сетка иконок предметов в атласе — порт `isValidGridValue`, `isValidGridWH`, `iconCoords`, `ATLAS_PRESETS`.</summary>
public static class IconGrid
{
    public static readonly List<AtlasPreset> AtlasPresets = new()
    {
        new() { Label = "Ячейка 50×50", CellW = 50, CellH = 50 },
        new() { Label = "Ячейка 32×32", CellW = 32, CellH = 32 },
        new() { Label = "Ячейка 64×64", CellW = 64, CellH = 64 },
        new() { Label = "Ячейка 128×128", CellW = 128, CellH = 128 }
    };

    /// <summary>isValidGridValue: целое неотрицательное (позиция иконки).</summary>
    public static bool IsValidGridValue(string? v) => Regex.IsMatch((v ?? "").Trim(), @"^\d+$");

    /// <summary>isValidGridWH: положительное целое (размер иконки).</summary>
    public static bool IsValidGridWH(string? v) => Regex.IsMatch((v ?? "").Trim(), @"^[1-9]\d*$");

    /// <summary>iconCoords: координаты иконки в атласе; нечисловое значение → NaN, как унарный «+» в JS.</summary>
    public static (double X, double Y) IconCoords(string? gx, string? gy, double cellSize) =>
        (JsNumber(gx) * cellSize, JsNumber(gy) * cellSize);

    /// <summary>JS-приведение унарным «+»: пустое → 0, нечисло → NaN.</summary>
    internal static double JsNumber(string? v)
    {
        var s = (v ?? "").Trim();
        if (s.Length == 0) return 0;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
    }
}