using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Export;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>
/// Строка списка «Файлы диалогов»: группа диалогов проекта и её имя `<c>dialogs_&lt;имя&gt;.xml</c>`.
/// Правка имени сразу пересобирает план пакета (через колбэк окна).
/// </summary>
public sealed partial class GamePackageFileViewModel : ObservableObject
{
    private readonly DialogueFileGroup _group;
    private readonly Action _changed;

    public GamePackageFileViewModel(DialogueFileGroup group, Action changed)
    {
        _group = group;
        _changed = changed;
        _fileName = group.FileName;
    }

    /// <summary>Id диалогов группы — по ним имя применяется в <see cref="DialogueGameExport.Groups"/>.</summary>
    public IReadOnlyList<string> DialogueIds => _group.Dialogues
        .Select(dialogue => string.IsNullOrEmpty(dialogue.Id) ? dialogue.Name : dialogue.Id)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .ToList();

    /// <summary>Подпись группы: перечисление id диалогов через запятую.</summary>
    public string Dialogues => string.Join(", ", DialogueIds);

    /// <summary>Имя группы без префикса и расширения (`bar` → `dialogs_bar.xml`).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileNamePreview))]
    private string _fileName;

    /// <summary>Предпросмотр итогового имени файла (очищенного <c>SafeName</c>).</summary>
    public string FileNamePreview => "dialogs_" + DialogueFiles.SafeName(FileName) + ".xml";

    partial void OnFileNameChanged(string value) => _changed();
}

/// <summary>Локаль пакета: код `rus`/`eng`, имя и флажок «включать string_table».</summary>
public sealed partial class GamePackageLocaleViewModel : ObservableObject
{
    private readonly Action _changed;

    public GamePackageLocaleViewModel(string locale, string name, Action changed)
    {
        Locale = locale;
        Name = name;
        _changed = changed;
    }

    /// <summary>Код локали (`rus`, `eng`, ...) — сегмент пути `text/&lt;locale&gt;/st_*.xml`.</summary>
    public string Locale { get; }

    /// <summary>Отображаемое имя локали из проекта.</summary>
    public string Name { get; }

    /// <summary>Подпись для чекбокса: имя и код.</summary>
    public string Label => string.IsNullOrWhiteSpace(Name) ? Locale : Name + " (" + Locale + ")";

    /// <summary>Включать `string_table` этой локали в пакет.</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    partial void OnIsSelectedChanged(bool value) => _changed();
}

/// <summary>Строка списка «Файлы пакета»: путь, категория и размер в кодировке экспорта.</summary>
public sealed class GamePackageEntryViewModel
{
    public GamePackageEntryViewModel(GamePackageEntry entry) => Entry = entry;

    /// <summary>Запись плана (путь, категория, байты).</summary>
    public GamePackageEntry Entry { get; }

    /// <summary>Путь в архиве (`gamedata/...`).</summary>
    public string Path => Entry.Path;

    /// <summary>Локализованная подпись категории (`gamePackageKind*`).</summary>
    public string Kind => LocalizationSource.Get("gamePackageKind" + (Entry.Kind switch
    {
        GamePackageKind.Dialogues => "Dialogues",
        GamePackageKind.Localization => "Localization",
        GamePackageKind.Characters => "Characters",
        GamePackageKind.Items => "Items",
        GamePackageKind.Quests => "Quests",
        GamePackageKind.SystemLtx => "System",
        GamePackageKind.Scripts => "Scripts",
        _ => "Other"
    }));

    /// <summary>Размер файла: байты или килобайты (локаль интерфейса).</summary>
    public string Size => GamePackageViewModel.FormatBytes(Entry.Bytes);
}

/// <summary>
/// Модель окна «Игровой пакет gamedata»: имена `dialogs_*.xml`, набор локалей, манифест
/// и предпросмотр файлов. План пересобирается на каждое изменение, поэтому размеры
/// и список файлов в окне всегда совпадают с тем, что попадёт в ZIP.
/// </summary>
public sealed partial class GamePackageViewModel : ObservableObject
{
    private readonly Project _project;
    private bool _refreshing;

    public GamePackageViewModel(Project project)
    {
        _project = project;

        DialogueFiles = new ObservableCollection<GamePackageFileViewModel>(
            DialogueGameExport.Groups(project)
                .Select(group => new GamePackageFileViewModel(group, Refresh)));

        var languages = project.Languages.Count > 0
            ? project.Languages
            : new List<Language> { new() { Locale = "rus", Name = "Русский" } };

        Locales = new ObservableCollection<GamePackageLocaleViewModel>(
            languages
                .Where(language => !string.IsNullOrWhiteSpace(language.Locale))
                .Select(language => new GamePackageLocaleViewModel(
                    language.Locale, language.Name, Refresh)));

        Plan = new GamePackagePlan(
            new Dictionary<string, string>(StringComparer.Ordinal),
            project.Settings.ExportEncoding ?? "windows-1251");

        Refresh();
    }

    /// <summary>Группы диалогов с редактируемыми именами файлов.</summary>
    public ObservableCollection<GamePackageFileViewModel> DialogueFiles { get; }

    /// <summary>Локали `string_table` с флажками.</summary>
    public ObservableCollection<GamePackageLocaleViewModel> Locales { get; }

    /// <summary>Файлы пакета для предпросмотра.</summary>
    public ObservableCollection<GamePackageEntryViewModel> Entries { get; } = new();

    /// <summary>Текущий план: его и записывает оболочка после «Собрать ZIP».</summary>
    public GamePackagePlan Plan { get; private set; }

    /// <summary>Добавить `xrayforge-package.json` в архив.</summary>
    [ObservableProperty]
    private bool _includeManifest = true;

    /// <summary>Сводка «Итого: N · размер».</summary>
    [ObservableProperty]
    private string _summary = "";

    /// <summary>Кодировка экспорта проекта (подпись в шапке списка файлов).</summary>
    [ObservableProperty]
    private string _encoding = "";

    /// <summary>Ошибка сборки плана (битые связи графа), иначе пусто.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(CanBuild))]
    private string _error = "";

    /// <summary>Есть ли ошибка, блокирующая сборку пакета.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>В пакете есть файлы — список не пуст.</summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>В пакете есть файлы и нет ошибок — кнопка «Собрать ZIP» активна.</summary>
    public bool CanBuild => Entries.Count > 0 && !HasError;

    partial void OnIncludeManifestChanged(bool value) => Refresh();

    /// <summary>Пересобрать план и список файлов по текущим настройкам окна.</summary>
    public void Refresh()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var options = new GamePackageOptions
            {
                DialogueFileNames = BuildFileNames(),
                Locales = Locales.Where(locale => locale.IsSelected)
                    .Select(locale => locale.Locale)
                    .ToList(),
                IncludeManifest = IncludeManifest
            };

            try
            {
                Plan = GamePackage.Plan(_project, options);
                Error = "";
            }
            catch (InvalidOperationException e)
            {
                // Паритет с legacy: диалог с битыми связями пакет не собирает.
                Plan = new GamePackagePlan(
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    _project.Settings.ExportEncoding ?? "windows-1251");
                Error = e.Message;
            }

            Entries.Clear();
            foreach (var entry in Plan.Entries) Entries.Add(new GamePackageEntryViewModel(entry));

            Encoding = Plan.Encoding;
            Summary = LocalizationSource.Get("gamePackageTotal") + ": " +
                      Entries.Count.ToString(CultureInfo.CurrentCulture) + " · " +
                      FormatBytes(Plan.TotalBytes);

            OnPropertyChanged(nameof(CanBuild));
            OnPropertyChanged(nameof(HasEntries));
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>Размер с единицей измерения в текущей локали (`Б`/`КБ`, `B`/`KB`).</summary>
    public static string FormatBytes(int bytes) => bytes < 1024
        ? bytes.ToString("N0", CultureInfo.CurrentCulture) + " " +
          LocalizationSource.Get("gamePackageBytes")
        : (bytes / 1024.0).ToString("N1", CultureInfo.CurrentCulture) + " " +
          LocalizationSource.Get("gamePackageKilobytes");

    /// <summary>Карта «id диалога → имя группы» для <see cref="GamePackageOptions.DialogueFileNames"/>.</summary>
    private IReadOnlyDictionary<string, string>? BuildFileNames()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in DialogueFiles)
        foreach (var id in group.DialogueIds)
            if (!string.IsNullOrWhiteSpace(group.FileName))
                map[id] = group.FileName;

        return map.Count > 0 ? map : null;
    }
}
