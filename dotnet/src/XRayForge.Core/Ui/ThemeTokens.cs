namespace XRayForge.Core.Ui;

/// <summary>
/// Семантические токены темы (этап 5). Значения — `#RRGGBB`; имена свойств
/// совпадают с ключами ресурсов WPF: `SurfacePanel` → `{DynamicResource SurfacePanel}`.
/// Источник значений — `docs/ui-references/tokens/00-palette.tokens.md`.
/// </summary>
public sealed record ThemeTokens(
    string SurfaceBase,
    string SurfacePanel,
    string SurfaceRaised,
    string SurfaceSunken,
    string SurfaceDisplay,
    string BorderBevel,
    string BorderStrong,
    string TextPrimary,
    string TextMuted,
    string TextInverted,
    string TextAccent,
    string AccentMain,
    string AccentHover,
    string AccentGlow,
    string AccentDim,
    string StateWarn,
    string StateDanger,
    string StateOk)
{
    /// <summary>Имена ключей ресурсов в порядке следования (совпадают со свойствами записи).</summary>
    public static readonly string[] Keys =
    {
        nameof(SurfaceBase), nameof(SurfacePanel), nameof(SurfaceRaised), nameof(SurfaceSunken), nameof(SurfaceDisplay),
        nameof(BorderBevel), nameof(BorderStrong),
        nameof(TextPrimary), nameof(TextMuted), nameof(TextInverted), nameof(TextAccent),
        nameof(AccentMain), nameof(AccentHover), nameof(AccentGlow), nameof(AccentDim),
        nameof(StateWarn), nameof(StateDanger), nameof(StateOk)
    };

    /// <summary>Значения токенов в том же порядке, что и <see cref="Keys"/>.</summary>
    public IReadOnlyList<string> Values => new[]
    {
        SurfaceBase, SurfacePanel, SurfaceRaised, SurfaceSunken, SurfaceDisplay,
        BorderBevel, BorderStrong,
        TextPrimary, TextMuted, TextInverted, TextAccent,
        AccentMain, AccentHover, AccentGlow, AccentDim,
        StateWarn, StateDanger, StateOk
    };

    /// <summary>Токены как словарь ключ → HEX (для `ThemeService` и тестов).</summary>
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < Keys.Length; i++) map[Keys[i]] = Values[i];
        return map;
    }

    /// <summary>Все значения — корректные `#RRGGBB`.</summary>
    public bool IsValid()
    {
        foreach (var value in Values)
            if (!IsHex(value)) return false;
        return true;
    }

    /// <summary>Копия токенов с другим акцентом (glow — осветлённый main, dim — затемнённый).</summary>
    public ThemeTokens WithAccent(string main, string hover) => this with
    {
        AccentMain = main,
        AccentHover = hover,
        AccentGlow = Mix(main, "#FFFFFF", 0.25),
        AccentDim = Mix(main, SurfaceBase, 0.65)
    };

    /// <summary>Копия токенов с заменённым одиночным токеном (акцентные оверрайды вью).</summary>
    public ThemeTokens With(string key, string value)
    {
        var map = new Dictionary<string, string>(ToDictionary(), StringComparer.Ordinal) { [key] = value };
        return FromMap(map, this);
    }

    /// <summary>Собрать токены из словаря; отсутствующие ключи берутся из `fallback` (по умолчанию Steel).</summary>
    public static ThemeTokens FromMap(IReadOnlyDictionary<string, string> map, ThemeTokens? fallback = null)
    {
        var source = fallback ?? ThemeCatalog.Steel;
        var values = source.Values;
        var keys = Keys;

        string Get(string key)
        {
            if (map.TryGetValue(key, out var value) && IsHex(value)) return value;
            var index = Array.IndexOf(keys, key);
            return index < 0 ? values[0] : values[index];
        }

        return new ThemeTokens(
            Get(nameof(SurfaceBase)), Get(nameof(SurfacePanel)), Get(nameof(SurfaceRaised)),
            Get(nameof(SurfaceSunken)), Get(nameof(SurfaceDisplay)),
            Get(nameof(BorderBevel)), Get(nameof(BorderStrong)),
            Get(nameof(TextPrimary)), Get(nameof(TextMuted)), Get(nameof(TextInverted)), Get(nameof(TextAccent)),
            Get(nameof(AccentMain)), Get(nameof(AccentHover)), Get(nameof(AccentGlow)), Get(nameof(AccentDim)),
            Get(nameof(StateWarn)), Get(nameof(StateDanger)), Get(nameof(StateOk)));
    }

    /// <summary>Проверка `#RRGGBB`.</summary>
    public static bool IsHex(string? value)
    {
        if (value == null || value.Length != 7 || value[0] != '#') return false;
        for (var i = 1; i < value.Length; i++)
            if (!Uri.IsHexDigit(value[i])) return false;
        return true;
    }

    /// <summary>Относительная яркость по WCAG (0…1) — для проверок контраста.</summary>
    public static double RelativeLuminance(string hex)
    {
        if (!IsHex(hex)) return 0;
        var r = Linear(Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0);
        var g = Linear(Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0);
        var b = Linear(Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;

        static double Linear(double channel) =>
            channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    /// <summary>Коэффициент контраста WCAG между двумя цветами (1…21).</summary>
    public static double ContrastRatio(string a, string b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var light = Math.Max(la, lb);
        var dark = Math.Min(la, lb);
        return (light + 0.05) / (dark + 0.05);
    }

    /// <summary>Линейное смешение двух `#RRGGBB` (`t` = 0 → `a`, 1 → `b`).</summary>
    public static string Mix(string a, string b, double t)
    {
        if (!IsHex(a) || !IsHex(b)) return IsHex(a) ? a : "#000000";
        var ratio = Math.Clamp(t, 0, 1);
        var channels = new int[3];
        for (var i = 0; i < 3; i++)
        {
            var ca = Convert.ToInt32(a.Substring(1 + i * 2, 2), 16);
            var cb = Convert.ToInt32(b.Substring(1 + i * 2, 2), 16);
            channels[i] = (int)Math.Round(ca + (cb - ca) * ratio);
        }

        return $"#{channels[0]:X2}{channels[1]:X2}{channels[2]:X2}";
    }
}

