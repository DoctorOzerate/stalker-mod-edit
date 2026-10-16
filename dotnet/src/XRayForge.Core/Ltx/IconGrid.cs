using System.Globalization;
using System.Text.RegularExpressions;

namespace XRayForge.Core.Ltx;

/// <summary>Пресет атласа иконок (label/columns/cellW/cellH) — порт ATLAS_PRESETS.</summary>
public sealed class AtlasPreset
{
    public string Label { get; set; } = "";
    public int Columns { get; set; }
    public int CellW { get; set; }
    public int CellH { get; set; }
}

/// <summary>Сетка иконок предметов в атласе — порт `isValidGridValue`, `isValidGridWH`, `iconCoords`, `ATLAS_PRESETS`.</summary>
public static class IconGrid
{
    public static readonly List<AtlasPreset> AtlasPresets = new()
    {
        new() { Label = "50×50 · 5 колонок", Columns = 5, CellW = 50, CellH = 50 },
        new() { Label = "50×50 · 10 колонок", Columns = 10, CellW = 50, CellH = 50 },
        new() { Label = "32×32 · 8 колонок", Columns = 8, CellW = 32, CellH = 32 },
        new() { Label = "64×64 · 4 колонки", Columns = 4, CellW = 64, CellH = 64 }
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