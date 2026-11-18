namespace XRayForge.Core.Ui;

/// <summary>
/// Действие верхней панели раздела: id (оболочка сопоставляет его команде), глиф и ключ
/// локализации подписи.
/// </summary>
public sealed record ToolbarAction(string Id, string Glyph, string LabelKey);

/// <summary>
/// Контекстные действия панели инструментов по активному разделу: каждому редактору —
/// только относящиеся к нему команды. «Диалоги» получают экспорт реплик в игру, проверку
/// графа и (в Debug) XML редактора, «Обзор» — сборку игрового архива и самопроверку,
/// остальные разделы — сохранение. Без открытого проекта действий нет: стартовый экран
/// занимается выбором проекта, а настройки, справка и поиск живут в правом углу панели.
/// </summary>
public static class ToolbarCatalog
{
    /// <summary>Сохранение проекта (есть во всех разделах).</summary>
    public const string SaveId = "save";

    /// <summary>Экспорт диалогов в игру (`game_dialogs`).</summary>
    public const string ExportGameDialogsId = "exportGameDialogs";

    /// <summary>Проверка целостности графа диалога.</summary>
    public const string CheckGraphId = "checkGraph";

    /// <summary>Экспорт XML редактора (Debug-сборка, вкладка «Диалоги»).</summary>
    public const string ExportXmlId = "exportXml";

    /// <summary>Сборка игрового ZIP (`gamedata`).</summary>
    public const string ExportGameZipId = "exportGameZip";

    /// <summary>Встроенная самопроверка проекта.</summary>
    public const string SelfCheckId = "selfCheck";

    private static readonly ToolbarAction Save = new(SaveId, "\U0001F4BE", "save");

    private static readonly ToolbarAction ExportGameDialogs =
        new(ExportGameDialogsId, "\U0001F4E4", "gameExportDialogs");

    private static readonly ToolbarAction CheckGraph =
        new(CheckGraphId, "\U0001F9EA", "checkGraph");

    private static readonly ToolbarAction ExportXml =
        new(ExportXmlId, "\U0001F9FE", "exportXml");

    private static readonly ToolbarAction ExportGameZip =
        new(ExportGameZipId, "\U0001F4E6", "exportGameZip");

    private static readonly ToolbarAction SelfCheck =
        new(SelfCheckId, "\U00002714", "selfCheck");

    /// <summary>
    /// Действия для раздела: `null` — проект не открыт (пустой набор), неизвестный раздел —
    /// только сохранение, чтобы панель не показывала чужие команды после смены навигации.
    /// </summary>
    public static IReadOnlyList<ToolbarAction> Actions(string? sectionId, bool debug = false)
    {
        if (sectionId == null) return Array.Empty<ToolbarAction>();

        var actions = new List<ToolbarAction> { Save };
        switch (sectionId)
        {
            case NavigationCatalog.DialoguesId:
                actions.Add(ExportGameDialogs);
                actions.Add(CheckGraph);
                if (debug) actions.Add(ExportXml);
                break;

            case NavigationCatalog.OverviewId:
                actions.Add(ExportGameZip);
                actions.Add(SelfCheck);
                if (debug) actions.Add(ExportXml);
                break;
        }

        return actions;
    }

    /// <summary>Действие по id (для проверок и маппинга команд оболочкой).</summary>
    public static ToolbarAction? Find(string? id) =>
        All.FirstOrDefault(action => action.Id == id);

    /// <summary>Все известные действия (порядок — как в разделах).</summary>
    public static IReadOnlyList<ToolbarAction> All { get; } = new[]
    {
        Save, ExportGameDialogs, CheckGraph, ExportXml, ExportGameZip, SelfCheck
    };
}
