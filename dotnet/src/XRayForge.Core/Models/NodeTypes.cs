namespace XRayForge.Core.Models;

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

    public static readonly HashSet<string> All = new()
    {
        Start, Phrase, Condition, QuestCheck, ItemCheck, Random, Action, End
    };

    public static readonly string[] MetaKeys =
    {
        "has_info", "dont_has_info", "precondition", "give_info", "disable_info", "action"
    };

    public static bool IsKnown(string? type) => type != null && All.Contains(type);
}
