namespace XRayForge.Core.Ui;

/// <summary>Тема интерфейса: id, подпись, референс-скриншот и токены.</summary>
public sealed record ThemeDefinition(string Id, string Name, string Reference, bool IsAccentState, ThemeTokens Tokens);

/// <summary>Метрики плотности интерфейса (`compact`/`normal`/`roomy`).</summary>
public sealed record DensityMetrics(
    string Id,
    double RowHeight,
    double PanelGap,
    double PanelPadding,
    double ControlPaddingX,
    double ControlPaddingY,
    double FontSize,
    double SmallFontSize,
    double TitleFontSize,
    double Radius);

/// <summary>Итог разбора настроек: тема + акцент + плотность, готовые к применению.</summary>
public sealed record ResolvedTheme(string ThemeId, string AccentId, string DensityId, ThemeTokens Tokens, DensityMetrics Metrics)
{
    /// <summary>
    /// Ресурсы для WPF: HEX-токены (ключи `ThemeTokens.Keys`) плюс короткие алиасы
    /// (`Accent` = `AccentMain`). Метрики плотности добавляет `ThemeService`.
    /// </summary>
    public IReadOnlyDictionary<string, string> ColorResources()
    {
        var map = new Dictionary<string, string>(Tokens.ToDictionary(), StringComparer.Ordinal)
        {
            ["Accent"] = Tokens.AccentMain
        };
        return map;
    }
}

/// <summary>
/// Каталог тем (этап 5): основная — Steel по `work.png`; Olive и Hazard —
/// акцентные состояния для плотных редакторов и экрана проверок
/// (см. `docs/ui-references/tokens/00-palette.tokens.md`).
/// </summary>
public static class ThemeCatalog
{
    public const string SteelId = "steel";
    public const string OliveId = "olive";
    public const string HazardId = "hazard";

    /// <summary>Алиасы схемы проекта (`theme: dark|light|auto`) → актуальные темы.</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dark"] = SteelId,
        ["light"] = SteelId,
        ["auto"] = SteelId,
        ["steel"] = SteelId,
        ["olive"] = OliveId,
        ["hazard"] = HazardId
    };

    public static readonly ThemeTokens Steel = new(
        SurfaceBase: "#101010",
        SurfacePanel: "#1B1B1B",
        SurfaceRaised: "#303030",
        SurfaceSunken: "#0B0B0B",
        SurfaceDisplay: "#0A1A0E",
        BorderBevel: "#404040",
        BorderStrong: "#5A5A5A",
        TextPrimary: "#C8C8C8",
        TextMuted: "#7A7A7A",
        TextInverted: "#0A0A0A",
        TextAccent: "#39FF14",
        AccentMain: "#2FE06A",
        AccentHover: "#25B455",
        AccentGlow: "#39FF14",
        AccentDim: "#1E5B32",
        StateWarn: "#FFB020",
        StateDanger: "#E03A3A",
        StateOk: "#2FE06A");

    public static readonly ThemeTokens Olive = new(
        SurfaceBase: "#0A0F0A",
        SurfacePanel: "#16210F",
        SurfaceRaised: "#303820",
        SurfaceSunken: "#080C08",
        SurfaceDisplay: "#0B1A0C",
        BorderBevel: "#404830",
        BorderStrong: "#5A6440",
        TextPrimary: "#C6D2A0",
        TextMuted: "#7E8A5E",
        TextInverted: "#0A0F0A",
        TextAccent: "#7CFF7C",
        AccentMain: "#4CE04C",
        AccentHover: "#3CB43C",
        AccentGlow: "#7CFF7C",
        AccentDim: "#21481F",
        StateWarn: "#FFB020",
        StateDanger: "#E03A3A",
        StateOk: "#4CE04C");

    public static readonly ThemeTokens Hazard = new(
        SurfaceBase: "#0A0A0A",
        SurfacePanel: "#1A0E0E",
        SurfaceRaised: "#302020",
        SurfaceSunken: "#140808",
        SurfaceDisplay: "#1A0505",
        BorderBevel: "#402020",
        BorderStrong: "#5A2A2A",
        TextPrimary: "#C8A8A8",
        TextMuted: "#806060",
        TextInverted: "#0A0A0A",
        TextAccent: "#FF5A3A",
        AccentMain: "#FF2A2A",
        AccentHover: "#D01A1A",
        AccentGlow: "#FF5A3A",
        AccentDim: "#4A1010",
        StateWarn: "#FFB020",
        StateDanger: "#FF2A2A",
        StateOk: "#4CE04C");

    public static readonly IReadOnlyList<ThemeDefinition> Themes = new[]
    {
        new ThemeDefinition(SteelId, "Steel + Phosphor", "work.png", false, Steel),
        new ThemeDefinition(OliveId, "Olive Panel", "good-sborka.png", true, Olive),
        new ThemeDefinition(HazardId, "Hazard Red", "fail-sborka.png", true, Hazard)
    };

    /// <summary>
    /// Акцентные пресеты (`ProgramSettingsCatalog.AccentPresets`) в палитре референсов:
    /// `[основной, hover]`. `green` совпадает с фосфорным акцентом Steel.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Accents = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["green"] = new[] { "#2FE06A", "#25B455" },
        ["blue"] = new[] { "#4C8CE0", "#2F6FC0" },
        ["orange"] = new[] { "#FFB020", "#D98A10" },
        ["purple"] = new[] { "#A86FE0", "#8A50C0" },
        ["red"] = new[] { "#FF2A2A", "#D01A1A" }
    };

    /// <summary>Акцент по умолчанию для темы (Steel/Olive — фосфор, Hazard — алый).</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultAccent = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SteelId] = "green",
        [OliveId] = "green",
        [HazardId] = "red"
    };

    public static readonly IReadOnlyDictionary<string, DensityMetrics> DensityPresets = new Dictionary<string, DensityMetrics>(StringComparer.Ordinal)
    {
        ["compact"] = new DensityMetrics("compact", 26, 6, 10, 8, 3, 12, 10.5, 11, 4),
        ["normal"] = new DensityMetrics("normal", 30, 10, 14, 12, 5, 13, 11, 12, 6),
        ["roomy"] = new DensityMetrics("roomy", 36, 14, 18, 16, 7, 14, 12, 13, 8)
    };

    /// <summary>`theme` из настроек → актуальный id (`dark`/`light`/`auto` → `steel`).</summary>
    public static string NormalizeThemeId(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return SteelId;
        return Aliases.TryGetValue(themeId.Trim(), out var id) ? id : SteelId;
    }

    /// <summary>Описание темы по id (неизвестный id → Steel).</summary>
    public static ThemeDefinition Theme(string? themeId) =>
        Themes.FirstOrDefault(t => t.Id == NormalizeThemeId(themeId)) ?? Themes[0];

    /// <summary>Метрики плотности по id (`compact`/`normal`/`roomy`; иначе `normal`).</summary>
    public static DensityMetrics Density(string? densityId) =>
        densityId != null && DensityPresets.TryGetValue(densityId, out var metrics) ? metrics : DensityPresets["normal"];

    /// <summary>Пара `[основной, hover]` для акцента; неизвестный id → акцент темы.</summary>
    public static string[] AccentPair(string? themeId, string? accentId)
    {
        var theme = Theme(themeId);
        var id = accentId != null && Accents.ContainsKey(accentId) ? accentId : DefaultAccent[theme.Id];
        if (id == DefaultAccent[theme.Id]) return new[] { theme.Tokens.AccentMain, theme.Tokens.AccentHover };
        return Accents[id];
    }

    /// <summary>Акцентный id, который будет применён (для UI-подсветки выбранного пресета).</summary>
    public static string NormalizeAccentId(string? themeId, string? accentId)
    {
        var theme = Theme(themeId);
        return accentId != null && Accents.ContainsKey(accentId) ? accentId : DefaultAccent[theme.Id];
    }

    /// <summary>Разобрать настройки (+ плотность из проекта) в готовую тему интерфейса.</summary>
    public static ResolvedTheme Resolve(string? themeId, string? accentId, string? densityId)
    {
        var theme = Theme(themeId);
        var accent = NormalizeAccentId(theme.Id, accentId);
        var pair = AccentPair(theme.Id, accent);
        var metrics = Density(densityId);
        var tokens = accent == DefaultAccent[theme.Id]
            ? theme.Tokens
            : theme.Tokens.WithAccent(pair[0], pair[1]);
        return new ResolvedTheme(theme.Id, accent, metrics.Id, tokens, metrics);
    }
}
