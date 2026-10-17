namespace XRayForge.Core.Suggestions;

/// <summary>
/// Тип подсказки. Порядок значений используется как вторичный критерий сортировки:
/// сначала идут секции и скрипты, затем более общие справочники.
/// </summary>
public enum SuggestionKind
{
    Item = 0,
    Section,
    Class,
    Field,
    Script,
    ScriptFunction,
    ScriptBinding,
    LuaApi,
    TextId,
    Dialogue,
    Character,
    Community,
    Visual,
    Icon,
    Rank,
    Reputation,
    InfoPortion,
    Quest,
    SpawnSection,
    Monster,
    NpcProfile,
    Squad,
    Texture,
    Value
}

/// <summary>
/// Одна подсказка: значение для вставки, тип и пояснение для строки выпадающего списка.
/// Пояснение намеренно короткое — оно показывается в одну строку рядом со значением.
/// </summary>
public sealed class SuggestionEntry
{
    public SuggestionEntry(string? value, SuggestionKind kind, string? detail = null)
    {
        Value = (value ?? "").Trim();
        Kind = kind;
        Detail = CleanDetail(detail);
        LowerValue = Value.ToLowerInvariant();
    }

    /// <summary>Значение, которое вставляется в текстовое поле.</summary>
    public string Value { get; }

    public SuggestionKind Kind { get; }

    /// <summary>Источник/категория/название для второй колонки списка.</summary>
    public string Detail { get; }

    /// <summary>Значение в нижнем регистре — для быстрого поиска без повторной нормализации.</summary>
    public string LowerValue { get; }

    /// <summary>Короткий глиф типа подсказки (в списке заменяет отдельную колонку «тип»).</summary>
    public string Icon => Kind switch
    {
        SuggestionKind.Item => "▣",
        SuggestionKind.Section => "▦",
        SuggestionKind.Class => "◆",
        SuggestionKind.Field => "≡",
        SuggestionKind.Script => "📜",
        SuggestionKind.ScriptFunction => "ƒ",
        SuggestionKind.ScriptBinding => "⇥",
        SuggestionKind.LuaApi => "λ",
        SuggestionKind.TextId => "T",
        SuggestionKind.Dialogue => "💬",
        SuggestionKind.Character => "👤",
        SuggestionKind.Community => "⚑",
        SuggestionKind.Visual => "◈",
        SuggestionKind.Icon => "▧",
        SuggestionKind.Rank => "★",
        SuggestionKind.Reputation => "☆",
        SuggestionKind.InfoPortion => "ⓘ",
        SuggestionKind.Quest => "✓",
        SuggestionKind.SpawnSection => "⊕",
        SuggestionKind.Monster => "☠",
        SuggestionKind.NpcProfile => "⚙",
        SuggestionKind.Squad => "⚔",
        SuggestionKind.Texture => "▨",
        _ => "="
    };

    public override string ToString() => Value;

    private static string CleanDetail(string? detail)
    {
        var text = (detail ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text.Length > 120 ? text[..117] + "..." : text;
    }
}
