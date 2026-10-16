using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>Группа списка «Загруженные диалоги»: подпись группы и вошедшие в неё диалоги.</summary>
public sealed class DialogueBucket
{
    public string Group { get; set; } = "";
    public List<DialogueGraph> Dialogues { get; set; } = new();
}

/// <summary>Группировка диалогов по локации/персонажу/типу (по префиксу id).</summary>
public static class DialogueGroups
{
    public static readonly IReadOnlyDictionary<string, string> Groups = new Dictionary<string, string>
    {
        ["esc"] = "Кордон",
        ["escape"] = "Кордон",
        ["agr"] = "Агропром",
        ["gar"] = "Свалка",
        ["dar"] = "Тёмная долина",
        ["bar"] = "Бар",
        ["mil"] = "Военные склады",
        ["yantar"] = "Янтарь",
        ["rad"] = "Радар",
        ["pri"] = "Припять",
        ["cit"] = "Цитадель",
        ["dm"] = "Служебные",
        ["tm"] = "Торговля",
        ["quest"] = "Квесты",
        ["tutorial"] = "Обучение",
        ["optional"] = "Дополнительные",
        ["general"] = "Общие",
        ["hello"] = "Приветствия",
        ["raid"] = "Рейд",
        ["napad"] = "Нападение",
        ["actor"] = "ГГ (actor)",
        ["barman"] = "Бармен",
        ["volk"] = "Волк",
        ["oso"] = "Осознание",
        ["dolg"] = "Долг",
        ["trader"] = "Торговец",
        ["brodyaga"] = "Бродяга",
        ["vagonchik"] = "Вагончик",
        ["val"] = "Вал",
        ["hlam"] = "Хлам",
        ["stalker"] = "Сталкер"
    };

    /// <summary>Человекочитаемая группа диалога по префиксу id.</summary>
    public static string DialogueGroup(string? id)
    {
        var prefix = (id ?? string.Empty).Split('_')[0].ToLowerInvariant();
        if (Groups.TryGetValue(prefix, out var group)) return group;
        if (prefix.Length == 0) return "Прочее";
        return char.ToUpperInvariant(prefix[0]) + prefix.Substring(1);
    }

    /// <summary>`d.name || d.id` — подпись диалога в списке (пустая строка, если нет ни того, ни другого).</summary>
    public static string Name(DialogueGraph? dialogue) =>
        dialogue == null ? "" : !string.IsNullOrEmpty(dialogue.Name) ? dialogue.Name : dialogue.Id;

    /// <summary>
    /// `groups[dialogueGroup(d.id || d.name)]` + `Object.keys(groups).sort()`: диалоги раскладываются
    /// по группам в порядке появления, группы сортируются по коду символов (как `Array.sort()` в JS).
    /// </summary>
    public static List<DialogueBucket> Grouped(IEnumerable<DialogueGraph?>? dialogues)
    {
        var buckets = new Dictionary<string, DialogueBucket>(StringComparer.Ordinal);
        foreach (var dialogue in dialogues ?? Enumerable.Empty<DialogueGraph?>())
        {
            if (dialogue == null) continue;

            var key = string.IsNullOrEmpty(dialogue.Id) ? dialogue.Name : dialogue.Id;
            var group = DialogueGroup(key);
            if (!buckets.TryGetValue(group, out var bucket))
            {
                bucket = new DialogueBucket { Group = group };
                buckets[group] = bucket;
            }

            bucket.Dialogues.Add(dialogue);
        }

        return buckets
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToList();
    }
}