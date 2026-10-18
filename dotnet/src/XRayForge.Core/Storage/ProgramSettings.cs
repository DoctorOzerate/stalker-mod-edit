using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRayForge.Core.Storage;

/// <summary>Действие горячей клавиши: id, подпись и комбинация по умолчанию.</summary>
public sealed class HotkeyAction
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Default { get; set; } = "";
}

/// <summary>
/// Настройки программы (отдельно от проекта). Порт `loadProgramSettings` из App.js:
/// тема, акцент, плотность, локаль, автосейв, горячие клавиши, размер шрифта.
/// </summary>
public sealed class ProgramSettings
{
    [JsonPropertyName("theme")] public string Theme { get; set; } = "dark";

    /// <summary>Интервал автосохранения в секундах; `null` — автосейв выключен (в JS — `NaN`).</summary>
    [JsonPropertyName("autosaveInterval")] public double? AutosaveInterval { get; set; } = 60;

    [JsonPropertyName("showNodeText")] public bool ShowNodeText { get; set; } = true;
    [JsonPropertyName("accent")] public string Accent { get; set; } = "green";
    [JsonPropertyName("density")] public string Density { get; set; } = "normal";
    [JsonPropertyName("locale")] public string Locale { get; set; } = "ru";
    [JsonPropertyName("syncUrl")] public string SyncUrl { get; set; } = "";
    [JsonPropertyName("editorFontSize")] public double EditorFontSize { get; set; } = 13;
    [JsonPropertyName("hotkeys")] public Dictionary<string, string> Hotkeys { get; set; } = ProgramSettingsCatalog.DefaultHotkeys();

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Справочники настроек программы: пресеты акцентов, плотности, реестр горячих клавиш.</summary>
public static class ProgramSettingsCatalog
{
    /// <summary>Пресеты акцентного цвета: [основной, hover].</summary>
    public static readonly Dictionary<string, string[]> AccentPresets = new(StringComparer.Ordinal)
    {
        ["green"] = new[] { "#16a34a", "#15803d" },
        ["blue"] = new[] { "#2563eb", "#1d4ed8" },
        ["orange"] = new[] { "#f97316", "#ea580c" },
        ["purple"] = new[] { "#7c3aed", "#6d28d9" },
        ["red"] = new[] { "#dc2626", "#b91c1c" },
        ["amber"] = new[] { "#FFB000", "#D88F00" },
        ["teal"] = new[] { "#2AAE93", "#1F8F78" },
        ["silver"] = new[] { "#C3CBD5", "#9FA9B5" }
    };

    /// <summary>Допустимая плотность интерфейса.</summary>
    public static readonly string[] Densities = { "compact", "normal", "roomy" };

    /// <summary>`HOTKEY_ACTIONS` — реестр горячих клавиш (mod = Ctrl/Cmd).</summary>
    public static readonly HotkeyAction[] HotkeyActions =
    {
        new() { Id = "save", Label = "Сохранить", Default = "mod+s" },
        new() { Id = "exportXml", Label = "XML редактора", Default = "mod+e" },
        new() { Id = "globalSearch", Label = "Глобальный поиск", Default = "mod+k" },
        new() { Id = "undo", Label = "Отменить", Default = "mod+z" },
        new() { Id = "redo", Label = "Повторить", Default = "mod+y" },
        new() { Id = "addStart", Label = "Добавить Старт", Default = "1" },
        new() { Id = "addPhrase", Label = "Добавить Реплику", Default = "2" },
        new() { Id = "addRandom", Label = "Добавить Случайный", Default = "3" },
        new() { Id = "addEnd", Label = "Добавить Конец", Default = "4" },
        new() { Id = "deleteSelected", Label = "Удалить выбранное", Default = "Delete" },
        new() { Id = "escape", Label = "Отмена/закрыть", Default = "Escape" }
    };

    /// <summary>`defaultHotkeys()` — id → комбинация по умолчанию.</summary>
    public static Dictionary<string, string> DefaultHotkeys()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var action in HotkeyActions) map[action.Id] = action.Default;
        return map;
    }

    /// <summary>`fallback` из `loadProgramSettings` — базовые настройки.</summary>
    public static ProgramSettings Fallback() => new();

    /// <summary>Порт `loadProgramSettings`: вне диапазонов и неизвестные пресеты заменяются.</summary>
    public static ProgramSettings Normalize(ProgramSettings? raw)
    {
        var source = raw ?? new ProgramSettings();
        var hotkeys = DefaultHotkeys();
        foreach (var pair in source.Hotkeys) hotkeys[pair.Key] = pair.Value;

        var fontSize = double.IsFinite(source.EditorFontSize) ? source.EditorFontSize : 13;
        return new ProgramSettings
        {
            Theme = IsTruthy(source.Theme) ? source.Theme : "dark",
            AutosaveInterval = source.AutosaveInterval is { } interval && double.IsFinite(interval) ? interval : 0,
            ShowNodeText = source.ShowNodeText,
            Accent = AccentPresets.ContainsKey(source.Accent) ? source.Accent : "green",
            Density = Array.IndexOf(Densities, source.Density) >= 0 ? source.Density : "normal",
            Locale = source.Locale == "en" ? "en" : "ru",
            SyncUrl = source.SyncUrl is { Length: > 0 } url ? url : "",
            EditorFontSize = Math.Min(24, Math.Max(10, fontSize)),
            Hotkeys = hotkeys,
            Extra = source.Extra
        };
    }

    /// <summary>Как `loadProgramSettings`: JSON → настройки; ошибка разбора → значения по умолчанию.</summary>
    public static ProgramSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Fallback();
        try
        {
            var parsed = JsonSerializer.Deserialize<ProgramSettings>(json, Projects.ProjectSerializer.Options);
            return Normalize(parsed);
        }
        catch (JsonException) { return Fallback(); }
    }

    /// <summary>Проверка комбинации против нажатия (порт `comboMatches`).</summary>
    public static bool ComboMatches(string? combo, bool ctrlOrCmd, bool shift, bool alt, string? key)
    {
        var parts = (combo ?? "").ToLowerInvariant().Split('+').Select(p => p.Trim()).ToArray();
        if (parts.Length == 0 || parts[^1].Length == 0) return false;
        if ((parts.Contains("mod") || parts.Contains("ctrl") || parts.Contains("cmd")) != ctrlOrCmd) return false;
        if (parts.Contains("shift") != shift) return false;
        if (parts.Contains("alt") != alt) return false;
        return string.Equals((key ?? "").ToLowerInvariant(), parts[^1], StringComparison.Ordinal);
    }

    private static bool IsTruthy(string? value) => !string.IsNullOrEmpty(value);
}