using System.Text.Json.Serialization;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Projects;

/// <summary>
/// Слот экипировки в раскладке инвентаря (`equipSlotLayout[]`): ключ, подпись и прямоугольник
/// на виртуальном поле. Геометрия задана в пикселях макета — предпросмотр масштабирует всё
/// поле целиком, поэтому пропорции слотов сохраняются.
/// </summary>
public sealed class EquipSlotLayout
{
    /// <summary>Ключ слота: по нему предмет назначается в <see cref="Project.EquipSlots"/>.</summary>
    [JsonPropertyName("key")] public string Key { get; set; } = "";

    /// <summary>Подпись слота. Пустая строка — подпись по ключу (например, «Нож»).</summary>
    [JsonPropertyName("label")] public string Label { get; set; } = "";

    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }

    /// <summary>Независимая копия для временного состояния редактора/снимка.</summary>
    public EquipSlotLayout Clone() => new()
    {
        Key = Key,
        Label = Label,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height
    };
}

/// <summary>
/// Раскладки слотов экипировки по сериям игры. Геометрия снята с референсов
/// `docs/ui-references/screenshots/slots-for-{SoC,CS,CoP}.png`; количество, подписи,
/// позиции и размеры можно менять в окне «Настройка слотов».
/// </summary>
public static class EquipSlotCatalog
{
    /// <summary>Минимальный размер слота, чтобы он оставался кликабельным и подпись читалась.</summary>
    public const double MinSlotSize = 24;

    /// <summary>Поле вокруг раскладки (справа/снизу добавляется к габаритам).</summary>
    public const double CanvasPadding = 10;

    /// <summary>Стартовый размер пустого поля.</summary>
    public const double EmptyCanvasWidth = 240;

    /// <summary>Стартовая высота пустого поля.</summary>
    public const double EmptyCanvasHeight = 120;

    /// <summary>
    /// Раскладка по умолчанию для серии. Координаты повторяют референсы:
    /// SoC — нож/пистолет/оружие в ряд, CS — КПК/броня/пистолет слева и оружие справа,
    /// CoP — пистолет слева, шлем/броня/детектор по центру, оружие справа.
    /// </summary>
    public static List<EquipSlotLayout> DefaultLayout(string? series)
    {
        var normalized = GameSeriesCatalog.Normalize(series);
        return normalized switch
        {
            "cs" => new List<EquipSlotLayout>
            {
                Slot("device", 10, 10, 112, 64),
                Slot("armor", 10, 82, 112, 150),
                Slot("pistol", 10, 240, 112, 60),
                Slot("weapon", 130, 10, 98, 290)
            },
            "cop" => new List<EquipSlotLayout>
            {
                Slot("pistol", 8, 174, 54, 64),
                Slot("helmet", 98, 14, 76, 70),
                Slot("armor", 86, 94, 100, 122),
                Slot("detector", 100, 296, 72, 64),
                Slot("weapon", 198, 10, 114, 360)
            },
            _ => new List<EquipSlotLayout>
            {
                Slot("knife", 16, 16, 160, 160),
                Slot("pistol", 216, 16, 160, 160),
                Slot("weapon", 416, 16, 488, 160)
            }
        };
    }

    private static EquipSlotLayout Slot(string key, double x, double y, double width, double height) =>
        new() { Key = key, X = x, Y = y, Width = width, Height = height };

    /// <summary>Глубокая копия раскладки; `null` превращается в пустой список.</summary>
    public static List<EquipSlotLayout> Clone(IEnumerable<EquipSlotLayout>? slots) =>
        slots?.Where(slot => slot != null).Select(slot => slot.Clone()).ToList() ?? new List<EquipSlotLayout>();

    /// <summary>
    /// Нормализовать раскладку после ручной правки: уникальные ключи, непустые подписи
    /// (пустая — значение по ключу), неотрицательные координаты и минимальный размер.
    /// Явно пустой список остаётся пустым — пользователь мог удалить все слоты.
    /// </summary>
    public static List<EquipSlotLayout> Normalize(IEnumerable<EquipSlotLayout>? slots, string? series)
    {
        var source = slots == null
            ? DefaultLayout(series)
            : slots.Where(slot => slot != null).ToList();
        var result = new List<EquipSlotLayout>(source.Count);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < source.Count; index++)
        {
            var slot = source[index];
            var key = (slot.Key ?? "").Trim();
            if (key.Length == 0) key = "slot_" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            key = UniqueKey(key, keys);

            result.Add(new EquipSlotLayout
            {
                Key = key,
                Label = (slot.Label ?? "").Trim(),
                X = Math.Max(0, Sanitize(slot.X, 0)),
                Y = Math.Max(0, Sanitize(slot.Y, 0)),
                Width = Math.Max(MinSlotSize, Sanitize(slot.Width, 80)),
                Height = Math.Max(MinSlotSize, Sanitize(slot.Height, 80))
            });
        }

        return result;
    }

    /// <summary>Следующий свободный ключ для кнопки «Добавить слот».</summary>
    public static string NextKey(IEnumerable<string>? keys)
    {
        var used = (keys ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; ; index++)
        {
            var key = "slot_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!used.Contains(key)) return key;
        }
    }

    /// <summary>
    /// Размер виртуального поля: правый/нижний край всех слотов плюс отступ.
    /// Пустая раскладка получает минимальную площадку, чтобы окно редактора не схлопывалось.
    /// </summary>
    public static (double Width, double Height) CanvasSize(IEnumerable<EquipSlotLayout>? slots)
    {
        var list = slots?.Where(slot => slot != null).ToList() ?? new List<EquipSlotLayout>();
        if (list.Count == 0) return (EmptyCanvasWidth, EmptyCanvasHeight);

        var width = list.Max(slot => Math.Max(0, slot.X) + Math.Max(MinSlotSize, slot.Width));
        var height = list.Max(slot => Math.Max(0, slot.Y) + Math.Max(MinSlotSize, slot.Height));
        return (Math.Ceiling(width + CanvasPadding), Math.Ceiling(height + CanvasPadding));
    }

    private static double Sanitize(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    private static string UniqueKey(string key, ISet<string> used)
    {
        if (used.Add(key)) return key;
        for (var index = 2; ; index++)
        {
            var candidate = key + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (used.Add(candidate)) return candidate;
        }
    }
}
