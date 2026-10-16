namespace XRayForge.Core.Ui;

/// <summary>Вкладка основной навигации: id, глиф, ключ локализации и акцентное состояние.</summary>
public sealed record NavigationItem(string Id, string Glyph, string LabelKey, string Accent)
{
    /// <summary>Акцент темы или акцентное состояние вью (`steel`/`olive`/`hazard`).</summary>
    public bool IsAccentState => Accent is ThemeCatalog.OliveId or ThemeCatalog.HazardId;
}

/// <summary>
/// Реестр вкладок окна (`TABS` из App.js): порядок задаёт `Ctrl+1…9`.
/// Диалоги → Персонажи → Предметы → Иконки → Скрипты → system.ltx → Языки → Квесты → Обзор.
/// </summary>
public static class NavigationCatalog
{
    public const string DialoguesId = "dialogues";
    public const string CharactersId = "characters";
    public const string ItemsId = "items";
    public const string IconsId = "icons";
    public const string ScriptsId = "scripts";
    public const string SystemId = "system";
    public const string LanguagesId = "languages";
    public const string QuestsId = "quests";
    public const string OverviewId = "overview";

    public const string DefaultTabId = DialoguesId;

    public static readonly IReadOnlyList<NavigationItem> Items = new[]
    {
        new NavigationItem(DialoguesId, "\U0001F4AC", "tabDialogues", ThemeCatalog.SteelId),
        new NavigationItem(CharactersId, "\U0001F9CD", "tabCharacters", ThemeCatalog.SteelId),
        new NavigationItem(ItemsId, "\U0001F392", "tabItems", ThemeCatalog.OliveId),
        new NavigationItem(IconsId, "\U0001F5BC", "tabIcons", ThemeCatalog.OliveId),
        new NavigationItem(ScriptsId, "\U0001F4DC", "tabScripts", ThemeCatalog.OliveId),
        new NavigationItem(SystemId, "\U0001F9FE", "tabSystem", ThemeCatalog.SteelId),
        new NavigationItem(LanguagesId, "\U0001F310", "tabLanguages", ThemeCatalog.SteelId),
        new NavigationItem(QuestsId, "\U0001F3AF", "tabQuests", ThemeCatalog.SteelId),
        new NavigationItem(OverviewId, "\U0001F4CA", "tabOverview", ThemeCatalog.SteelId)
    };

    /// <summary>Вкладка по id; `null` (и `overview` как акцент) — если id неизвестен.</summary>
    public static NavigationItem? Find(string? id) =>
        id == null ? null : Items.FirstOrDefault(item => item.Id == id);

    /// <summary>Индекс вкладки (0…8) или −1.</summary>
    public static int IndexOf(string? id)
    {
        if (id == null) return -1;
        for (var i = 0; i < Items.Count; i++)
            if (Items[i].Id == id) return i;
        return -1;
    }

    /// <summary>Вкладка по номеру горячей клавиши `Ctrl+1…9`.</summary>
    public static NavigationItem? ByHotkeyIndex(int number) =>
        number >= 1 && number <= Items.Count ? Items[number - 1] : null;
}
