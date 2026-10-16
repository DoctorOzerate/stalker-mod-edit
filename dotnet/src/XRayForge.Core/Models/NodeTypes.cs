namespace XRayForge.Core.Models;

/// <summary>Отображение типа узла в UI: подпись, иконка, цвет, описание.</summary>
public sealed class NodeTypeInfo
{
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    public string Desc { get; set; } = "";
}

/// <summary>Типы узлов графа диалога и набор метаданных реплики.</summary>
public static class NodeTypes
{
    public const string Start = "START";
    public const string Phrase = "PHRASE";
    public const string Condition = "CONDITION";
    public const string QuestCheck = "QUEST_CHECK";
    public const string ItemCheck = "ITEM_CHECK";
    public const string Random = "RANDOM";
    public const string Action = "ACTION";
    public const string End = "END";

    /// <summary>Узел неизвестного типа (используется как значение по умолчанию для старых проектов).</summary>
    public const string Unknown = "UNKNOWN";

    public static readonly HashSet<string> All = new()
    {
        Start, Phrase, Condition, QuestCheck, ItemCheck, Random, Action, End
    };

    /// <summary>Типы, скрываемые палитрой по умолчанию (служебные, не реплики).</summary>
    public static readonly string[] PaletteHidden = { Condition, QuestCheck, ItemCheck, Action };

    public static readonly string[] MetaKeys =
    {
        "has_info", "dont_has_info", "precondition", "give_info", "disable_info", "action"
    };

    public static readonly NodeTypeInfo FallbackNode = new()
    {
        Label = "Неизвестный узел", Icon = "?", Color = "#64748b", Desc = "Неизвестный тип"
    };

    public static readonly IReadOnlyDictionary<string, NodeTypeInfo> Info = new Dictionary<string, NodeTypeInfo>
    {
        [Start] = new() { Label = "Старт", Icon = "▶", Color = "#16a34a", Desc = "Начало диалога" },
        [Phrase] = new() { Label = "Реплика", Icon = "💬", Color = "#2563eb", Desc = "Игровая реплика NPC/ГГ/не определено" },
        [Condition] = new() { Label = "Условие", Icon = "❓", Color = "#15803d", Desc = "Проверка выражения" },
        [QuestCheck] = new() { Label = "Проверка квеста", Icon = "📋", Color = "#9333ea", Desc = "Проверка info portion" },
        [ItemCheck] = new() { Label = "Проверка предмета", Icon = "🎒", Color = "#22c55e", Desc = "Наличие предмета" },
        [Random] = new() { Label = "Случайный выбор", Icon = "🎲", Color = "#4f46e5", Desc = "Случайная ветка" },
        [Action] = new() { Label = "Действие", Icon = "⚙", Color = "#dc2626", Desc = "Выдать предмет, вызвать скрипт" },
        [End] = new() { Label = "Конец", Icon = "■", Color = "#64748b", Desc = "Завершение диалога" }
    };

    public static bool IsKnown(string? type) => type != null && All.Contains(type);

    /// <summary>Метаданные типа с подстановкой FALLBACK_NODE для неизвестных значений.</summary>
    public static NodeTypeInfo InfoFor(string? type) =>
        type != null && Info.TryGetValue(type, out var info) ? info : FallbackNode;
}
