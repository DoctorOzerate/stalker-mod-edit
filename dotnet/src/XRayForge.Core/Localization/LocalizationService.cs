namespace XRayForge.Core.Localization;

/// <summary>
/// Локализация интерфейса программы (хром: заголовок, вкладки, настройки, модальные окна).
/// Доменные строки редактора (реплики, поля LTX) остаются на языке мода/данных.
/// </summary>
public static class LocalizationService
{
    public static readonly IReadOnlyDictionary<string, string> LocaleNames =
        new Dictionary<string, string> { { "ru", "Русский" }, { "en", "English" } };

    private static readonly IReadOnlyDictionary<string, string> Ru = new Dictionary<string, string>
    {
        ["appTitle"] = "X-Ray Forge",
        ["projects"] = "Проекты",
        ["save"] = "Сохранить",
        ["projectSettings"] = "Проект",
        ["programSettings"] = "Программа",
        ["help"] = "Справка",
        ["unsaved"] = "Есть несохранённые изменения",
        ["tabDialogues"] = "Диалоги",
        ["tabCharacters"] = "Персонажи",
        ["tabItems"] = "Предметы",
        ["tabIcons"] = "Иконки",
        ["tabScripts"] = "Скрипты",
        ["tabSystem"] = "system.ltx",
        ["tabLanguages"] = "Языки",
        ["tabQuests"] = "Квесты",
        ["tabOverview"] = "Обзор",
        ["continue"] = "Продолжить редактирование",
        ["createProject"] = "Создать проект",
        ["importProject"] = "Импортировать проект (.json)",
        ["demoGraph"] = "Демо-граф",
        ["savedProjects"] = "Сохранённые проекты",
        ["newProject"] = "Новый проект",
        ["programSettingsTitle"] = "Настройки программы",
        ["programSettingsSub"] = "Глобальные параметры интерфейса.",
        ["theme"] = "Тема",
        ["accentColor"] = "Акцентный цвет",
        ["density"] = "Плотность интерфейса",
        ["compact"] = "Компактная",
        ["normal"] = "Обычная",
        ["roomy"] = "Просторная",
        ["nodesDisplay"] = "Узлы диалогов",
        ["autosave"] = "Автосохранение (сек, 0 — выкл)",
        ["hotkeys"] = "Горячие клавиши",
        ["language"] = "Язык интерфейса",
        ["syncSection"] = "Синхронизация",
        ["syncUrl"] = "URL сервера синхронизации",
        ["syncNow"] = "Синхронизировать",
        ["syncLoad"] = "Загрузить с сервера",
        ["pluginsSection"] = "Плагины (экспортёры)",
        ["addPlugin"] = "Добавить плагин",
        ["pluginName"] = "Имя",
        ["pluginCode"] = "Код (function(project){ return [{name, text}] })",
        ["overviewTitle"] = "Обзор проекта",
        ["overviewSub"] = "Сводка по сущностям и быстрые переходы.",
        ["checkGraph"] = "Проверить граф",
        ["globalSearch"] = "Глобальный поиск",
        ["close"] = "Закрыть",
        ["helpTitle"] = "Справка",
        ["exportSettings"] = "Экспорт настроек",
        ["importSettings"] = "Импорт настроек",
        ["resetData"] = "Сбросить данные",
        ["done"] = "Готово",
        ["runPlugins"] = "Экспорт через плагин",
    };

    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["appTitle"] = "X-Ray Forge",
        ["projects"] = "Projects",
        ["save"] = "Save",
        ["projectSettings"] = "Project",
        ["programSettings"] = "Program",
        ["help"] = "Help",
        ["unsaved"] = "Unsaved changes",
        ["tabDialogues"] = "Dialogues",
        ["tabCharacters"] = "Characters",
        ["tabItems"] = "Items",
        ["tabIcons"] = "Icons",
        ["tabScripts"] = "Scripts",
        ["tabSystem"] = "system.ltx",
        ["tabLanguages"] = "Languages",
        ["tabQuests"] = "Quests",
        ["tabOverview"] = "Overview",
        ["continue"] = "Continue editing",
        ["createProject"] = "Create project",
        ["importProject"] = "Import project (.json)",
        ["demoGraph"] = "Demo graph",
        ["savedProjects"] = "Saved projects",
        ["newProject"] = "New project",
        ["programSettingsTitle"] = "Program settings",
        ["programSettingsSub"] = "Global UI settings.",
        ["theme"] = "Theme",
        ["accentColor"] = "Accent color",
        ["density"] = "UI density",
        ["compact"] = "Compact",
        ["normal"] = "Normal",
        ["roomy"] = "Roomy",
        ["nodesDisplay"] = "Dialogue nodes",
        ["autosave"] = "Autosave (sec, 0 — off)",
        ["hotkeys"] = "Hotkeys",
        ["language"] = "Interface language",
        ["syncSection"] = "Synchronization",
        ["syncUrl"] = "Sync server URL",
        ["syncNow"] = "Sync now",
        ["syncLoad"] = "Load from server",
        ["pluginsSection"] = "Plugins (exporters)",
        ["addPlugin"] = "Add plugin",
        ["pluginName"] = "Name",
        ["pluginCode"] = "Code (function(project){ return [{name, text}] })",
        ["overviewTitle"] = "Project overview",
        ["overviewSub"] = "Entity summary and quick navigation.",
        ["checkGraph"] = "Check graph",
        ["globalSearch"] = "Global search",
        ["close"] = "Close",
        ["helpTitle"] = "Help",
        ["exportSettings"] = "Export settings",
        ["importSettings"] = "Import settings",
        ["resetData"] = "Reset data",
        ["done"] = "Done",
        ["runPlugins"] = "Export via plugin",
    };

    public static string Tr(string? locale, string key, string? fallback = null)
    {
        var d = locale == "en" ? En : Ru;
        if (d.TryGetValue(key, out var value)) return value;
        if (fallback != null) return fallback;
        return Ru.TryGetValue(key, out var ruValue) ? ruValue : key;
    }
}
