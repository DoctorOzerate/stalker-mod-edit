using XRayForge.Core.Ltx;
using XRayForge.Core.Models;

namespace XRayForge.Core.Quests;

/// <summary>Разбор task_manager.ltx (ЗП): секция = квест (#37).</summary>
public static class TaskManagerParser
{
    public static List<Quest> Parse(string? text)
    {
        var parsed = LtxParser.Parse(text);
        var result = new List<Quest>();

        foreach (var s in parsed.Sections)
        {
            if (s.Name.Length == 0 || s.Name.ToLowerInvariant() == "task_manager") continue;

            string Get(string key) => s.Entries.FirstOrDefault(x => x.Key == key)?.Value ?? "";

            var condlist = s.Entries
                .Where(x => x.Key.StartsWith("condlist_", StringComparison.Ordinal))
                .Select(x => x.Value)
                .ToList();

            var reward = Get("reward_money").Length > 0 ? "money:" + Get("reward_money") : Get("reward_item");

            result.Add(new Quest
            {
                Id = s.Name,
                Prior = Get("prior"),
                Title = Get("title"),
                Text = Get("descr").Length > 0 ? Get("descr") : Get("description"),
                Objectives = new List<QuestObjective> { new() { Text = Get("target") } },
                Icon = Get("icon"),
                Reward = reward,
                Format = "task_manager",
                Condlist = condlist,
                Type = Get("type"),
                Storyline = Get("storyline"),
                RewardMoney = Get("reward_money"),
                RewardItem = Get("reward_item"),
                RewardTreasure = Get("reward_treasure")
            });
        }

        return result;
    }
}