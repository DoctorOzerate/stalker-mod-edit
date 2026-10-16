namespace XRayForge.Core.Models;

public sealed class QuestObjective
{
    public string Text { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Infoportion { get; set; } = "";
    public string Article { get; set; } = "";
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
    public string Id { get; set; } = "";
    public string Prior { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = new();
    public string InfoportionComplete { get; set; } = "";
    public string InfoportionFail { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Location { get; set; } = "";
    public string Article { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Requires { get; set; } = new();
    public string Reward { get; set; } = "";

    /// <summary>"game_tasks" (ТЧ/ЧН) либо "task_manager" (ЗП).</summary>
    public string Format { get; set; } = "game_tasks";

    public List<string> Condlist { get; set; } = new();
    public string Type { get; set; } = "";
    public string Storyline { get; set; } = "";
    public string RewardMoney { get; set; } = "";
    public string RewardItem { get; set; } = "";
    public string RewardTreasure { get; set; } = "";
}