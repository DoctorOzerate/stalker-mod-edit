using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRayForge.Core.Models;

public sealed class QuestObjective
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
    [JsonPropertyName("infoportion")] public string Infoportion { get; set; } = "";
    [JsonPropertyName("article")] public string Article { get; set; } = "";
}

public sealed class QuestTemplate
{
    public string Label { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Prior { get; set; } = "";
}

public sealed class QuestIssue
{
    public string Quest { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>Квест (game_task / task_manager): поля обеих схем (#37, #64).</summary>
public sealed class Quest
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("prior")] public string Prior { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("objectives")] public List<QuestObjective> Objectives { get; set; } = new();
    [JsonPropertyName("infoportion_complete")] public string InfoportionComplete { get; set; } = "";
    [JsonPropertyName("infoportion_fail")] public string InfoportionFail { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
    [JsonPropertyName("location")] public string Location { get; set; } = "";
    [JsonPropertyName("article")] public string Article { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
    [JsonPropertyName("reward")] public string Reward { get; set; } = "";

    /// <summary>"game_tasks" (ТЧ/ЧН) либо "task_manager" (ЗП).</summary>
    [JsonPropertyName("format")] public string Format { get; set; } = "game_tasks";

    /// <summary>Путь файла-источника (в JS пишется всегда, в том числе пустым).</summary>
    [JsonPropertyName("sourcePath")] public string SourcePath { get; set; } = "";

    /// <summary>Снимок квеста до правок (`original`); в JS может быть `null`.</summary>
    [JsonPropertyName("original")] public JsonElement? Original { get; set; }

    /// <summary>Строки `condlist_*` схемы task_manager (null — ключ не пишется, как в JS для game_tasks).</summary>
    [JsonPropertyName("condlist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<string>? Condlist { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Type { get; set; } = "";

    [JsonPropertyName("storyline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Storyline { get; set; } = "";

    [JsonPropertyName("reward_money")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string RewardMoney { get; set; } = "";

    [JsonPropertyName("reward_item")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string RewardItem { get; set; } = "";

    [JsonPropertyName("reward_treasure")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string RewardTreasure { get; set; } = "";

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}