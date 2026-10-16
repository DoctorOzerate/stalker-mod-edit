using XRayForge.Core.Ltx;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Ui;

/// <summary>Плитка статистики: глиф вкладки, ключ локализации подписи и значение.</summary>
public sealed record SummaryChip(string Glyph, string LabelKey, int Count);

/// <summary>
/// Сводка по сущностям проекта для статусной строки, чипов настроек и списка проектов
/// (порт счётчиков из App.js: чипы «Настройки проекта» и `cnt` в welcome-списке).
/// </summary>
public sealed record ProjectSummary(
    int Dialogues,
    int Characters,
    int Items,
    int Quests,
    int Scripts,
    int Languages,
    int LtxFiles,
    int Textures)
{
    /// <summary>Вкладки, для которых считается список (порядок — как у чипов App.js).</summary>
    public static readonly string[] ChipIds =
    {
        NavigationCatalog.DialoguesId,
        NavigationCatalog.CharactersId,
        NavigationCatalog.ItemsId,
        NavigationCatalog.QuestsId,
        NavigationCatalog.ScriptsId,
        NavigationCatalog.LanguagesId,
        NavigationCatalog.SystemId
    };

    public static readonly ProjectSummary Empty = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Все сущности, кроме текстур (в JS — сумма по чипам без `ltxFiles`+иконок).</summary>
    public int Total => Dialogues + Characters + Items + Quests + Scripts + Languages + LtxFiles;

    /// <summary>Счётчик в списке проектов (`cnt` в welcome: диалоги + персонажи + квесты + скрипты).</summary>
    public int WelcomeCount => Dialogues + Characters + Quests + Scripts;

    /// <summary>Число по id вкладки (неизвестный id → 0).</summary>
    public int Count(string? tabId) => tabId switch
    {
        NavigationCatalog.DialoguesId => Dialogues,
        NavigationCatalog.CharactersId => Characters,
        NavigationCatalog.ItemsId => Items,
        NavigationCatalog.QuestsId => Quests,
        NavigationCatalog.ScriptsId => Scripts,
        NavigationCatalog.LanguagesId => Languages,
        NavigationCatalog.SystemId => LtxFiles,
        NavigationCatalog.IconsId => Textures,
        _ => 0
    };

    /// <summary>Плитки статистики с глифами и ключами локализации из навигации.</summary>
    public IReadOnlyList<SummaryChip> Chips()
    {
        var chips = new List<SummaryChip>(ChipIds.Length);
        foreach (var id in ChipIds)
        {
            var item = NavigationCatalog.Find(id);
            if (item == null) continue;
            chips.Add(new SummaryChip(item.Glyph, item.LabelKey, Count(id)));
        }
        return chips;
    }

    /// <summary>Посчитать сущности проекта (`null` → нули).</summary>
    public static ProjectSummary From(Project? project)
    {
        if (project == null) return Empty;
        return new ProjectSummary(
            Dialogues: project.Dialogues?.Count ?? 0,
            Characters: project.Characters?.Count ?? 0,
            Items: CountItemSections(project),
            Quests: project.Quests?.Count ?? 0,
            Scripts: project.Scripts?.Count ?? 0,
            Languages: project.Languages?.Count ?? 0,
            LtxFiles: project.LtxFiles?.Count ?? 0,
            Textures: project.Textures?.Count ?? 0);
    }

    /// <summary>
    /// Секции редактора предметов: `itemsLtx` плюс уникальные секции `itemFiles`
    /// (в JS — `parsedItems.sections.length`).
    /// </summary>
    public static int CountItemSections(Project? project)
    {
        if (project == null) return 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in LtxParser.Parse(project.ItemsLtx).Sections) names.Add(section.Name);
        if (project.ItemFiles != null)
            foreach (var file in project.ItemFiles.Values)
                foreach (var section in LtxParser.Parse(file).Sections)
                    names.Add(section.Name);
        return names.Count;
    }
}
