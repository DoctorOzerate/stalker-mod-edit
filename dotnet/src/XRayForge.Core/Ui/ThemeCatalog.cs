namespace XRayForge.Core.Ui;

/// <summary>Тема интерфейса: id, подпись, референс-скриншот, токены и ключ описания.</summary>
public sealed record ThemeDefinition(
    string Id,
    string Name,
    string Reference,
    bool IsAccentState,
    ThemeTokens Tokens,
    string DescriptionKey = "");

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
    public const string AmberId = "amber";
    public const string CobaltId = "cobalt";
    public const string ArcticId = "arctic";
    public const string GraphiteId = "graphite";

    /// <summary>Алиасы схемы проекта (`theme: dark|light|auto`) → актуальные темы.</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dark"] = SteelId,
        ["light"] = ArcticId,
        ["auto"] = SteelId,
        ["steel"] = SteelId,
        ["olive"] = OliveId,
        ["hazard"] = HazardId,
        ["amber"] = AmberId,
        ["cobalt"] = CobaltId,
        ["arctic"] = ArcticId,
        ["graphite"] = GraphiteId
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
        StateOk: "#2FE06A",
        ChromeBackdrop: "#202020",
        GridLine: "#27272A");

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
        StateOk: "#4CE04C",
        ChromeBackdrop: "#1B2414",
        GridLine: "#2A3320");

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
        StateOk: "#4CE04C",
        ChromeBackdrop: "#241414",
        GridLine: "#331C1C");

    /// <summary>Тёплый янтарный CRT: тёмно-коричневые поверхности и янтарный люминофор.</summary>
    public static readonly ThemeTokens Amber = new(
        SurfaceBase: "#14100A",
        SurfacePanel: "#211A10",
        SurfaceRaised: "#3A2E1C",
        SurfaceSunken: "#0D0A06",
        SurfaceDisplay: "#1A1206",
        BorderBevel: "#4A3A22",
        BorderStrong: "#66502C",
        TextPrimary: "#E8D8B0",
        TextMuted: "#A08A60",
        TextInverted: "#1A1206",
        TextAccent: "#FFC24A",
        AccentMain: "#FFB000",
        AccentHover: "#D88F00",
        AccentGlow: "#FFD166",
        AccentDim: "#4A3308",
        StateWarn: "#FFB020",
        StateDanger: "#E03A3A",
        StateOk: "#5BD75B",
        ChromeBackdrop: "#1B150C",
        GridLine: "#332714");

    /// <summary>Холодная сине-стальная тема для ночной работы.</summary>
    public static readonly ThemeTokens Cobalt = new(
        SurfaceBase: "#0A1018",
        SurfacePanel: "#111C2A",
        SurfaceRaised: "#1F3046",
        SurfaceSunken: "#070B11",
        SurfaceDisplay: "#071823",
        BorderBevel: "#29405C",
        BorderStrong: "#3B5A7E",
        TextPrimary: "#C8D8EA",
        TextMuted: "#7890A8",
        TextInverted: "#06101A",
        TextAccent: "#7CC4FF",
        AccentMain: "#4C9AFF",
        AccentHover: "#2F7AD8",
        AccentGlow: "#8FC8FF",
        AccentDim: "#1B3A5A",
        StateWarn: "#FFB020",
        StateDanger: "#FF5A5A",
        StateOk: "#4CE04C",
        ChromeBackdrop: "#0D1620",
        GridLine: "#1C2A3A");

    /// <summary>Светлая ледяная тема: высокий контраст и приглушённый бирюзовый акцент.</summary>
    public static readonly ThemeTokens Arctic = new(
        SurfaceBase: "#E8EEF2",
        SurfacePanel: "#F4F7FA",
        SurfaceRaised: "#DCE5EC",
        SurfaceSunken: "#D2DDE6",
        SurfaceDisplay: "#EAF7F5",
        BorderBevel: "#A8B8C4",
        BorderStrong: "#7E94A4",
        TextPrimary: "#17242E",
        TextMuted: "#526775",
        TextInverted: "#0B1A18",
        TextAccent: "#0B6B5D",
        AccentMain: "#2AAE93",
        AccentHover: "#1F8F78",
        AccentGlow: "#7EE0C9",
        AccentDim: "#C8EDE6",
        StateWarn: "#A15C00",
        StateDanger: "#C62828",
        StateOk: "#1F8A5B",
        ChromeBackdrop: "#DCE5EC",
        GridLine: "#C4D1DB");

    /// <summary>Монохромный графит с серебристым акцентом — минималистичная тема.</summary>
    public static readonly ThemeTokens Graphite = new(
        SurfaceBase: "#101113",
        SurfacePanel: "#17191C",
        SurfaceRaised: "#26292D",
        SurfaceSunken: "#0B0C0E",
        SurfaceDisplay: "#0E1114",
        BorderBevel: "#34383D",
        BorderStrong: "#4A5058",
        TextPrimary: "#D7DBE0",
        TextMuted: "#858D97",
        TextInverted: "#0B0D0F",
        TextAccent: "#EAF0F6",
        AccentMain: "#C3CBD5",
        AccentHover: "#9FA9B5",
        AccentGlow: "#F2F6FA",
        AccentDim: "#3A424B",
        StateWarn: "#FFB020",
        StateDanger: "#E05A5A",
        StateOk: "#6BD96B",
        ChromeBackdrop: "#15171A",
        GridLine: "#23262A");

    public static readonly IReadOnlyList<ThemeDefinition> Themes = new[]
    {
        new ThemeDefinition(SteelId, "Steel + Phosphor", "work.png", false, Steel, "themeSteelSub"),
        new ThemeDefinition(OliveId, "Olive Panel", "good-sborka.png", true, Olive, "themeOliveSub"),
        new ThemeDefinition(HazardId, "Hazard Red", "fail-sborka.png", true, Hazard, "themeHazardSub"),
        new ThemeDefinition(AmberId, "Amber CRT", "", false, Amber, "themeAmberSub"),
        new ThemeDefinition(CobaltId, "Cobalt Night", "", false, Cobalt, "themeCobaltSub"),
        new ThemeDefinition(ArcticId, "Arctic Light", "", false, Arctic, "themeArcticSub"),
        new ThemeDefinition(GraphiteId, "Graphite", "", false, Graphite, "themeGraphiteSub")
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
        ["red"] = new[] { "#FF2A2A", "#D01A1A" },
        ["amber"] = new[] { "#FFB000", "#D88F00" },
        ["teal"] = new[] { "#2AAE93", "#1F8F78" },
        ["silver"] = new[] { "#C3CBD5", "#9FA9B5" }
    };

    /// <summary>Акцент по умолчанию для темы (Steel/Olive — фосфор, Hazard — алый).</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultAccent = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SteelId] = "green",
        [OliveId] = "green",
        [HazardId] = "red",
        [AmberId] = "amber",
        [CobaltId] = "blue",
        [ArcticId] = "teal",
        [GraphiteId] = "silver"
    };

    public static readonly IReadOnlyDictionary<string, DensityMetrics> DensityPresets = new Dictionary<string, DensityMetrics>(StringComparer.Ordinal)
    {
        ["compact"] = new DensityMetrics("compact", 26, 6, 10, 8, 3, 12, 10.5, 11, 4),
        ["normal"] = new DensityMetrics("normal", 30, 10, 14, 12, 5, 13, 11, 12, 6),
        ["roomy"] = new DensityMetrics("roomy", 36, 14, 18, 16, 7, 14, 12, 13, 8)
    };

    /// <summary>`theme` из настроек → актуальный id (`dark`/`auto` → `steel`, `light` → `arctic`).</summary>
    public static string NormalizeThemeId(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return SteelId;
        return Aliases.TryGetValue(themeId.Trim(), out var id) ? id : SteelId;
    }

    /// <summary>Акцент по умолчанию для темы; для новой темы без записи — `green`.</summary>
    public static string DefaultAccentId(string? themeId)
    {
        var theme = Theme(themeId);
        return DefaultAccent.TryGetValue(theme.Id, out var id) ? id : "green";
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
        var defaultAccent = DefaultAccentId(theme.Id);
        var id = accentId != null && Accents.ContainsKey(accentId) ? accentId : defaultAccent;
        if (id == defaultAccent) return new[] { theme.Tokens.AccentMain, theme.Tokens.AccentHover };
        return Accents[id];
    }

    /// <summary>Акцентный id, который будет применён (для UI-подсветки выбранного пресета).</summary>
    public static string NormalizeAccentId(string? themeId, string? accentId)
    {
        var theme = Theme(themeId);
        return accentId != null && Accents.ContainsKey(accentId) ? accentId : DefaultAccentId(theme.Id);
    }

    /// <summary>Разобрать настройки (+ плотность из проекта) в готовую тему интерфейса.</summary>
    public static ResolvedTheme Resolve(string? themeId, string? accentId, string? densityId)
    {
        var theme = Theme(themeId);
        var accent = NormalizeAccentId(theme.Id, accentId);
        var pair = AccentPair(theme.Id, accent);
        var metrics = Density(densityId);
        var tokens = accent == DefaultAccentId(theme.Id)
            ? theme.Tokens
            : theme.Tokens.WithAccent(pair[0], pair[1]);
        return new ResolvedTheme(theme.Id, accent, metrics.Id, tokens, metrics);
    }
}
