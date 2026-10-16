namespace XRayForge.Core.Graph;

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
}