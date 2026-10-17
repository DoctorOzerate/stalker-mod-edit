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

/// <summary>
/// Положение иконки предмета в атласе. Координаты и размер берутся из `inv_grid_*`
/// с учётом наследования LTX, поэтому дочерняя секция использует значения родителя.
/// </summary>
public sealed class ItemIconPlacement
{
    /// <summary>`inv_grid_x` или -1, если поле отсутствует/некорректно.</summary>
    public int X { get; init; } = -1;

    /// <summary>`inv_grid_y` или -1, если поле отсутствует/некорректно.</summary>
    public int Y { get; init; } = -1;

    /// <summary>`inv_grid_width`, минимум 1 ячейка.</summary>
    public int Width { get; init; } = 1;

    /// <summary>`inv_grid_height`, минимум 1 ячейка.</summary>
    public int Height { get; init; } = 1;

    /// <summary>Позиция задана полностью — иконку можно вырезать из атласа.</summary>
    public bool HasPosition => X >= 0 && Y >= 0;
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

    /// <summary>
    /// `inv_grid_x/y/width/height` секции вместе с унаследованными значениями.
    /// Собственные поля перекрывают родительские, как в `mergedItemEntries`.
    /// </summary>
    public static ItemIconPlacement ResolvePlacement(
        LtxSection? section, IDictionary<string, LtxSection>? sectionsMap)
    {
        var entries = LtxInheritance.MergedEntries(section, sectionsMap);
        return new ItemIconPlacement
        {
            X = GridValue(entries, "inv_grid_x"),
            Y = GridValue(entries, "inv_grid_y"),
            Width = GridSize(entries, "inv_grid_width"),
            Height = GridSize(entries, "inv_grid_height")
        };
    }

    private static int GridValue(IReadOnlyList<MergedEntry> entries, string key)
    {
        var entry = FindEntry(entries, key);
        return entry != null && IsValidGridValue(entry.Value) ? ParseInt(entry.Value) : -1;
    }

    private static int GridSize(IReadOnlyList<MergedEntry> entries, string key)
    {
        var entry = FindEntry(entries, key);
        return entry != null && IsValidGridWH(entry.Value) ? Math.Max(1, ParseInt(entry.Value)) : 1;
    }

    private static MergedEntry? FindEntry(IReadOnlyList<MergedEntry> entries, string key) =>
        entries.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));

    private static int ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : -1;

    /// <summary>JS-приведение унарным «+»: пустое → 0, нечисло → NaN.</summary>
    internal static double JsNumber(string? v)
    {
        var s = (v ?? "").Trim();
        if (s.Length == 0) return 0;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN;
    }
}