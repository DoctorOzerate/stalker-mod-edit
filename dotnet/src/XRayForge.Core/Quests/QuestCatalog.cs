using System.Text.RegularExpressions;
using XRayForge.Core.Models;

namespace XRayForge.Core.Quests;

public sealed class CondlistEntry
{
    public string Cond { get; set; } = "";
    public string Action { get; set; } = "";
}

/// <summary>Категории/шаблоны квестов и утилиты записи condlist (#59).</summary>
public static class QuestCatalog
{
    public static readonly string[] Categories = { "Сюжет", "Побочный", "Повторимый", "Скрытый" };

    public static readonly QuestTemplate[] Templates =
    {
        new() { Label = "Принести предмет", Prefix = "fetch", Prior = "10" },
        new() { Label = "Убить цель", Prefix = "kill", Prior = "15" },
        new() { Label = "Сходить на локацию", Prefix = "goto", Prior = "5" }
    };

    /// <summary>Привести произвольные цели к каноническому виду.</summary>
    public static List<QuestObjective> NormalizeObjectives(IEnumerable<QuestObjective?>? objectives)
    {
        var result = new List<QuestObjective>();
        foreach (var o in objectives ?? Enumerable.Empty<QuestObjective?>())
        {
            if (o != null)
            {
                result.Add(new QuestObjective
                {
                    Text = o.Text ?? "",
                    Icon = o.Icon ?? "",
                    Infoportion = o.Infoportion ?? "",
                    Article = o.Article ?? ""
                });
            }
            else
            {
                result.Add(new QuestObjective());
            }
        }
        return result;
    }

    /// <summary>Разбор/сборка записи condlist вида «{+info} complete» (#59).</summary>
    public static CondlistEntry ParseCondlistEntry(string? str)
    {
        var s = str ?? string.Empty;
        var m = Regex.Match(s, @"^\s*(\{[^}]*\})\s*(.*)\z");
        return m.Success
            ? new CondlistEntry { Cond = m.Groups[1].Value.Trim(), Action = m.Groups[2].Value.Trim() }
            : new CondlistEntry { Cond = "", Action = s.Trim() };
    }

    public static string CondlistEntryToStr(string? cond, string? action) =>
        ((cond ?? string.Empty).Trim() + " " + (action ?? string.Empty).Trim()).Trim();
}