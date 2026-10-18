using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Gamedata;
using XRayForge.Core.Graph;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Storage;
using XRayForge.Core.Ui;
using XRayForge.Localization;
using XRayForge.Services;
using XRayForge.Views;

namespace XRayForge.ViewModels;

/// <summary>
/// Оболочка окна (этап 5): проект, список проектов, вкладки, статусная строка, welcome-экран,
/// глобальный поиск и горячие клавиши. Логика портирована из App.js (`loadProjects`,
/// `seedDialogue`, `saveProject`, `render`), но поверх Core-сервисов:
/// <see cref="AppDataService"/>, <see cref="ProjectSerializer"/>, <see cref="GamedataScanner"/>.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogs;
    private readonly AppDataService _store;
    private readonly ThemeService _theme;
    private readonly AutosaveService _autosave;
    private readonly Dispatcher _dispatcher;

    private Project _project = ProjectMigration.Empty();
    private ProgramSettings _settings;
    private ProjectSummary _summary = ProjectSummary.Empty;
    private bool _disposed;

    public ShellViewModel(IDialogService dialogs, ThemeService theme, AppDataService? store = null)
    {
        _dialogs = dialogs;
        _theme = theme;
        _store = store ?? new AppDataService();
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _settings = _store.LoadProgramSettings();
        LocalizationSource.Instance.Locale = _settings.Locale;
        _theme.ApplySettings(_settings);

        // Ctrl+1…Ctrl+9 переключают вкладки (в JS — обработчик `keydown`).
        Tabs = new ObservableCollection<TabViewModel>(NavigationCatalog.Items.Select((item, index) => new TabViewModel(item)
        {
            Hotkey = "Ctrl+" + (index + 1).ToString(CultureInfo.InvariantCulture)
        }));
        Projects = new ObservableCollection<ProjectItemViewModel>();
        Chips = new ObservableCollection<StatusChipViewModel>();
        OverviewChips = new ObservableCollection<StatusChipViewModel>();
        Issues = new ObservableCollection<ProjectIntegrityIssue>();
        Rows = new ObservableCollection<SectionRow>();
        SearchHits = new ObservableCollection<ProjectSearchHit>();
        Backups = new ObservableCollection<BackupItemViewModel>();
        DialogueGraph = new DialogueGraphViewModel(this, _dialogs);
        Characters = new CharactersViewModel(this);
        Items = new ItemsViewModel(this, _dialogs);
        Icons = new IconsViewModel(this, _dialogs);

        _autosave = new AutosaveService(
            _store,
            // Без открытого проекта таймер не должен перезаписывать current.project.json пустышкой.
            () => IsProjectOpen ? _project : null,
            () => _settings,
            project =>
            {
                // Автосохранение вместе с проектом пишет снапшот gamedata (`gamedataOf`).
                _store.SaveGamedataSnapshot(project.ProjectName, AppDataService.ToSnapshot(project));
                return true;
            });
        _autosave.Saved += OnAutosaveSaved;
        _autosave.ApplySettings();

        // Старт всегда пустой: сохранённый проект восстанавливается только кнопкой
        // «Продолжить» или открытием из списка, а не молча при запуске.
        _project = ProjectMigration.Empty();
        _statusText = LocalizationSource.Get("ready");
        _activeTab = Tabs[0];
        _activeTab.IsActive = true;

        ProjectName = "";
        SeriesLabel = "";
        GamedataPath = "";
        RefreshLocalization();

        LoadWelcomeList();
        LoadBackups();
        PublishSearch();
        // Вкладки строятся из пустого проекта; содержимое появится после Adopt.
        DialogueGraph.Attach(_project);
        Characters.Attach(_project);
        Items.Attach(_project);
        Icons.Attach(_project);

        RefreshProjectView();
    }

    // --- коллекции ---

    /// <summary>Вкладки навигации (`NavigationCatalog.Items`).</summary>
    public ObservableCollection<TabViewModel> Tabs { get; }

    /// <summary>Сохранённые проекты профиля (welcome-экран).</summary>
    public ObservableCollection<ProjectItemViewModel> Projects { get; }

    /// <summary>Плитки статусной строки.</summary>
    public ObservableCollection<StatusChipViewModel> Chips { get; }

    /// <summary>Плитки панели «Обзор» (статусные плюс иконки).</summary>
    public ObservableCollection<StatusChipViewModel> OverviewChips { get; }

    /// <summary>Проблемы последней проверки графа.</summary>
    public ObservableCollection<ProjectIntegrityIssue> Issues { get; }

    /// <summary>Строки активного раздела (персонажи, LTX-секции, скрипты, языки, квесты).</summary>
    public ObservableCollection<SectionRow> Rows { get; }

    /// <summary>Результаты глобального поиска.</summary>
    public ObservableCollection<ProjectSearchHit> SearchHits { get; }

    /// <summary>История точек сохранения («Копии»: восстановление и удаление снимков).</summary>
    public ObservableCollection<BackupItemViewModel> Backups { get; }

    /// <summary>Текущий проект (запросы ViewModels просмотров).</summary>
    /// <summary>Вкладка «Диалоги» (этап 6): редактор графа диалога (`DialoguesTab.js`).</summary>
    public DialogueGraphViewModel DialogueGraph { get; }

    /// <summary>Вкладка «Персонажи» (этап 7.1): редактор `specific_character` (`CharactersTab.js`).</summary>
    public CharactersViewModel Characters { get; }

    /// <summary>Вкладка «Предметы» (этап 7.2): редактор `items.ltx` (`ItemsTab.js`).</summary>
    public ItemsViewModel Items { get; }

    /// <summary>Вкладка «Иконки» (этап 7.3): DDS-атлас (`IconsTab.js`).</summary>
    public IconsViewModel Icons { get; }

    public Project Project => _project;

    /// <summary>Окно-владелец модальных диалогов (ставит `MainWindow`).</summary>
    public Window? ActiveWindow { get; set; }

    // --- наблюдаемые свойства ---

    /// <summary>Активная вкладка навигации.</summary>
    [ObservableProperty]
    private TabViewModel _activeTab;

    /// <summary>Выбранный в списке проект (открытие/удаление на welcome-экране).</summary>
    [ObservableProperty]
    private ProjectItemViewModel? _selectedProject;

    /// <summary>Запрос глобального поиска.</summary>
    [ObservableProperty]
    private string _searchQuery = "";

    /// <summary>Открыт ли welcome-экран поверх работы.</summary>
    [ObservableProperty]
    private bool _isWelcomeOpen = true;

    /// <summary>
    /// В профиле есть `current.project.json`, и проект из него всё ещё присутствует
    /// в списке сохранённых. Удалённый проект не должен предлагаться к продолжению.
    /// </summary>
    [ObservableProperty]
    private bool _hasCurrentProject;

    /// <summary>
    /// Есть открытый проект. До открытия/создания/импорта вкладки и их горячие клавиши
    /// недоступны, а автосохранение не пишет пустой проект.
    /// </summary>
    [ObservableProperty]
    private bool _isProjectOpen;

    /// <summary>Длительная операция (сканирование gamedata) — индикатор занятости.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Подпись индикатора занятости.</summary>
    [ObservableProperty]
    private string _busyText = "";

    /// <summary>Левая часть статусной строки (`statusText` в App.js).</summary>
    [ObservableProperty]
    private string _statusText;

    /// <summary>Правая часть статусной строки (прогресс, статистика, подсказка).</summary>
    [ObservableProperty]
    private string _statusDetail = "";

    /// <summary>Есть несохранённые изменения.</summary>
    [ObservableProperty]
    private bool _dirty;

    /// <summary>Имя текущего проекта.</summary>
    [ObservableProperty]
    private string _projectName;

    /// <summary>Подпись последнего сохранения.</summary>
    [ObservableProperty]
    private string _lastSavedText = "";

    /// <summary>Подпись последнего автосохранения.</summary>
    [ObservableProperty]
    private string _lastAutosaveText = "";

    /// <summary>Серия игры текущего проекта (подпись чипа).</summary>
    [ObservableProperty]
    private string _seriesLabel = "";

    /// <summary>Путь к gamedata текущего проекта.</summary>
    [ObservableProperty]
    private string _gamedataPath = "";
    // --- вычисляемые свойства ---

    /// <summary>Заголовок окна.</summary>
    public string Title =>
        LocalizationSource.Get("appTitle") +
        (string.IsNullOrWhiteSpace(ProjectName) ? "" : " — " + ProjectName) +
        (Dirty ? " •" : "");

    /// <summary>Активна вкладка «Обзор» (панель обзора вместо списка раздела).</summary>
    public bool IsOverviewActive => ActiveTab?.IsOverview == true;

    /// <summary>В профиле есть сохранённые проекты.</summary>
    public bool HasProjects => Projects.Count > 0;

    /// <summary>В проекте есть данные (иначе — подсказка `noData`).</summary>
    public bool HasData => _summary.Total > 0;

    /// <summary>Есть проблемы проверки графа.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Суммарное число сущностей проекта (чип `total`).</summary>
    public string TotalText => _summary.Total.ToString(CultureInfo.CurrentCulture);

    /// <summary>Подпись темы (`Steel`/`Olive`/`Hazard`).</summary>
    public string ThemeLabel => ThemeCatalog.Theme(_settings.Theme).Name;

    /// <summary>Подпись плотности интерфейса.</summary>
    public string DensityLabel => LocalizationSource.Get(_theme.Current.DensityId);

    /// <summary>Сводка поиска для окна глобального поиска.</summary>
    public string SearchSummary => LocalizationSource.Get("globalSearch") + ": " +
        SearchHits.Count.ToString(CultureInfo.CurrentCulture);

    partial void OnDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    partial void OnProjectNameChanged(string value) => OnPropertyChanged(nameof(Title));

    partial void OnSearchQueryChanged(string value) => PublishSearch();

    partial void OnActiveTabChanged(TabViewModel value)
    {
        foreach (var tab in Tabs) tab.IsActive = ReferenceEquals(tab, value);
        OnPropertyChanged(nameof(IsOverviewActive));
        RefreshRows();
    }


    // --- проект и welcome-экран ---

    /// <summary>`loadProjects()`: перечитать список сохранённых проектов профиля.</summary>
    public void LoadWelcomeList()
    {
        Projects.Clear();
        foreach (var stored in _store.ListProjects().Take(50))
            Projects.Add(new ProjectItemViewModel(stored).WithData(stored.Data));
        OnPropertyChanged(nameof(HasProjects));

        // LoadResumableProject заодно вычищает current.project.json удалённого проекта.
        HasCurrentProject = _store.LoadResumableProject() != null;
    }

    /// <summary>Открыть welcome-экран (кнопка «Проекты»).</summary>
    [RelayCommand]
    private void ShowWelcome()
    {
        LoadWelcomeList();
        IsWelcomeOpen = true;
    }

    /// <summary>Закрыть welcome-экран и вернуться к текущему проекту (только если он открыт).</summary>
    [RelayCommand]
    private void CloseWelcome()
    {
        if (IsProjectOpen) IsWelcomeOpen = false;
    }

    /// <summary>`continueProject`: продолжить сохранённый текущий проект.</summary>
    [RelayCommand]
    private void ContinueProject()
    {
        var project = _store.LoadResumableProject();
        if (project == null)
        {
            LoadWelcomeList();
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), LocalizationSource.Get("noProjects"));
            return;
        }

        Adopt(RestoreGamedata(project), dirty: false);
        StatusText = LocalizationSource.Get("continue");
    }

    /// <summary>`createProject`: новый проект с указанным именем.</summary>
    [RelayCommand]
    private void NewProject()
    {
        var name = _dialogs.Prompt(
            LocalizationSource.Get("newProject"),
            LocalizationSource.Get("projectName"),
            ProjectSchema.DefaultProjectName);
        if (string.IsNullOrWhiteSpace(name)) return;

        var project = ProjectMigration.Empty();
        project.ProjectName = name.Trim();
        project.DialogueId = name.Trim();
        Adopt(project, dirty: true);
        Save();
    }


    // --- проекты: открытие, импорт, удаление, демо-граф, сохранение, копии ---

    /// <summary>
    /// Каталог точек сохранения профиля (`saves`): общий стартовый каталог для файловых
    /// диалогов вкладок (экспорт XML/SVG, импорт `game_dialogs`).
    /// </summary>
    public string SavesDirectory => _store.SavesDirectory;

    /// <summary>`openProject(name)`: открыть проект профиля вместе со снапшотом gamedata.</summary>
    [RelayCommand]
    private void OpenProject(ProjectItemViewModel? item)
    {
        var name = item?.Name ?? SelectedProject?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;

        var stored = _store.ListProjects().FirstOrDefault(project => project.Name == name);
        if (stored == null)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), LocalizationSource.Get("openFailed"));
            LoadWelcomeList();
            return;
        }

        Adopt(RestoreGamedata(stored.Data), dirty: false);
        StatusText = LocalizationSource.Get("continue");
    }

    /// <summary>`importProject`: чтение `.project.json` с диска (в JS — `input[type=file]` + `loadProject`).</summary>
    [RelayCommand]
    private void ImportProject()
    {
        var path = _dialogs.PickOpenJson(LocalizationSource.Get("importProject"), _store.SavesDirectory);
        if (string.IsNullOrEmpty(path)) return;

        var project = ProjectSerializer.Load(path);
        if (project == null)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), LocalizationSource.Get("openFailed"));
            StatusText = LocalizationSource.Get("openFailed");
            return;
        }

        Adopt(project, dirty: true);
        StatusText = LocalizationSource.Get("importProject");
    }

    /// <summary>`removeProject`: удалить проект профиля (с подтверждением, как в JS).</summary>
    [RelayCommand]
    private void DeleteProject(ProjectItemViewModel? item)
    {
        var name = item?.Name ?? SelectedProject?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!_dialogs.Confirm(name + " — " + LocalizationSource.Get("deleteProject") + "?")) return;

        _store.RemoveProject(name);
        _store.RemoveGamedataSnapshot(name);
        if (IsProjectOpen && string.Equals(_project.ProjectName, name, StringComparison.Ordinal))
            CloseCurrentProject();

        LoadWelcomeList();
        StatusText = LocalizationSource.Get("deleteProject");
    }

    /// <summary>`seedDialogue`: демонстрационный граф торговца (`trader_demo`, 8 узлов / 8 связей).</summary>
    [RelayCommand]
    private void LoadDemoGraph()
    {
        var (nodes, edges) = DemoGraph.Seed();
        var project = ProjectMigration.Empty();
        project.ProjectName = DemoGraph.DialogueId;
        project.DialogueId = DemoGraph.DialogueId;
        project.Series = GameSeriesCatalog.DefaultSeries;
        project.Nodes = nodes;
        project.Edges = edges;

        Adopt(project, dirty: true);
        ActivateTabById(NavigationCatalog.DialoguesId);
        Save();
        StatusText = LocalizationSource.Get("demoGraph");
    }

    /// <summary>`saveProject()`: текущий проект, `saves/&lt;имя&gt;.project.json` и снапшот gamedata.</summary>
    [RelayCommand]
    private void Save()
    {
        if (!IsProjectOpen) return;
        if (string.IsNullOrWhiteSpace(_project.ProjectName)) _project.ProjectName = ProjectSchema.DefaultProjectName;
        if (string.IsNullOrWhiteSpace(_project.DialogueId)) _project.DialogueId = _project.ProjectName;

        _store.SaveCurrentProject(_project);
        _store.SaveProject(_project);
        _store.SaveGamedataSnapshot(_project.ProjectName, AppDataService.ToSnapshot(_project));

        SetDirty(false);
        LastSavedText = DateTime.Now.ToString("t", CultureInfo.CurrentCulture);
        StatusText = LocalizationSource.Get("save");
        ProjectName = _project.ProjectName;
        LoadWelcomeList();
    }

    /// <summary>`saveBackups`: снимок проекта в историю (профиль хранит `ProjectSchema.BackupsLimit`).</summary>
    [RelayCommand]
    private void AddBackup()
    {
        _store.AddBackup(_project);
        LoadBackups();
        StatusText = LocalizationSource.Get("backups") + ": " + Backups.Count.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>Восстановление резервной копии (в JS — кнопка «Восстановить» у исторического снимка).</summary>
    [RelayCommand]
    private void RestoreBackup(BackupItemViewModel? item)
    {
        if (item == null) return;
        if (!_dialogs.Confirm(item.Text + " — " + LocalizationSource.Get("restore") + "?")) return;

        Adopt(item.Backup.Data, dirty: true);
        StatusText = LocalizationSource.Get("restore");
    }

    /// <summary>Окно резервных копий: ревью истории и восстановление.</summary>
    [RelayCommand]
    private void OpenBackups()
    {
        LoadBackups();
        var window = new BackupsWindow(this) { Owner = ActiveWindow };
        window.ShowDialog();
    }

    // --- проект: принятие, вкладки, обзор, строки разделов ---

    /// <summary>
    /// Вернуть в проект «тяжёлые» поля из снапшота gamedata (`loadGamedataSnapshot` +
    /// `Object.assign` на старте/продолжении/открытии проекта).
    /// </summary>
    private Project RestoreGamedata(Project? project)
    {
        if (project == null) return ProjectMigration.Empty();
        _store.LoadGamedataSnapshot(project.ProjectName)?.ApplyTo(project);
        return project;
    }

    /// <summary>
    /// Принять проект как текущий (`openProject`/`createProject`/`importProject`):
    /// нормализация имени и серии, заголовок, обзор, закрытие welcome-экрана и флаг изменений.
    /// </summary>
    public void Adopt(Project project, bool dirty)
    {
        _project = project ?? ProjectMigration.Empty();
        _project.Series = GameSeriesCatalog.Normalize(_project.Series);
        if (string.IsNullOrWhiteSpace(_project.ProjectName)) _project.ProjectName = ProjectSchema.DefaultProjectName;
        if (string.IsNullOrWhiteSpace(_project.DialogueId)) _project.DialogueId = _project.ProjectName;
        IsProjectOpen = true;

        ProjectName = _project.ProjectName;
        SeriesLabel = GameSeriesCatalog.Label(_project.Series);
        GamedataPath = _project.GamedataPath;
        IsWelcomeOpen = false;
        SetDirty(dirty);
        // Граф вкладки «Диалоги» пересобирается из принятого проекта (как `patch` в JS).
        DialogueGraph.Attach(_project);
        // Персонажи пересобираются вместе с проектом (выбор сбрасывается, как `setSelChar(null)`).
        Characters.Attach(_project);
        // Предметы пересобираются вместе с проектом (выбор и история сбрасываются).
        Items.Attach(_project);
        // Атласы и коллекции иконок пересобираются вместе с проектом.
        Icons.Attach(_project);

        RefreshProjectView();
    }

    /// <summary>
    /// Закрыть текущий проект и вернуть стартовое пустое состояние (удаление текущего
    /// проекта, сброс профиля). Вкладки снова блокируются, welcome-экран открывается.
    /// </summary>
    private void CloseCurrentProject()
    {
        _project = ProjectMigration.Empty();
        ProjectName = "";
        SeriesLabel = "";
        GamedataPath = "";
        IsProjectOpen = false;
        IsWelcomeOpen = true;
        SetDirty(false);

        DialogueGraph.Attach(_project);
        Characters.Attach(_project);
        Items.Attach(_project);
        Icons.Attach(_project);
        RefreshProjectView();
    }

    /// <summary>Активировать вкладку по идентификатору (навигация, `Ctrl+1…9`, переход из поиска).</summary>
    public void ActivateTabById(string? id)
    {
        // До открытия проекта вкладки недоступны (навигация и Ctrl+1…9 одинаково).
        if (!IsProjectOpen) return;
        var tab = Tabs.FirstOrDefault(item => item.Id == id);
        if (tab != null) ActiveTab = tab;
    }

    /// <summary>Активировать вкладку (левая навигация; команда для `ListBox`).</summary>
    [RelayCommand]
    private void ActivateTab(TabViewModel? tab) => ActivateTabById(tab?.Id);

    /// <summary>
    /// Пересчитать обзор проекта: сводка, плитки статусной строки и панели «Обзор»
    /// (статусные чипы плюс иконки), счётчики вкладок, строки раздела и проблемы.
    /// </summary>
    public void RefreshProjectView()
    {
        _summary = ProjectSummary.From(_project);

        Chips.Clear();
        OverviewChips.Clear();
        foreach (var chip in _summary.Chips())
        {
            Chips.Add(new StatusChipViewModel(chip));
            OverviewChips.Add(new StatusChipViewModel(chip));
        }

        // Чип иконок в App.js строится отдельно (не входит в `ChipIds`).
        if (NavigationCatalog.Find(NavigationCatalog.IconsId) is { } icons)
            OverviewChips.Add(new StatusChipViewModel(icons.Glyph, icons.LabelKey, _summary.Textures));

        foreach (var tab in Tabs) tab.Count = _summary.Count(tab.Id);

        StatusDetail = HasData
            ? _summary.Total.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("entities")
            : LocalizationSource.Get("noData");

        RefreshRows();
        RefreshIssues();

        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(TotalText));
    }

    /// <summary>`checkProject`/`validateGraph`: целостность проекта и проверка графа диалога.</summary>
    public void RefreshIssues()
    {
        Issues.Clear();
        foreach (var issue in ProjectIntegrity.Validate(_project)) Issues.Add(issue);

        var where = LocalizationSource.Get("tabDialogues");
        foreach (var issue in GraphValidator.Validate(_project.Nodes, _project.Edges))
            Issues.Add(new ProjectIntegrityIssue
            {
                Where = where,
                Message = string.IsNullOrWhiteSpace(issue.Message) ? issue.Code : issue.Message!
            });

        OnPropertyChanged(nameof(HasIssues));
    }

    /// <summary>
    /// Строки активного раздела: пока только чтение данных проекта — редактирование
    /// приходит в редакторах этапов 6–7.
    /// </summary>
    public void RefreshRows()
    {
        Rows.Clear();

        switch (ActiveTab?.Id)
        {
            case NavigationCatalog.DialoguesId:
                foreach (var dialogue in _project.Dialogues)
                    Rows.Add(new SectionRow("💬", Text(dialogue.Name, dialogue.Id),
                        dialogue.Nodes.Count.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("nodes") +
                        " · " + dialogue.Edges.Count.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("edges")));
                break;

            case NavigationCatalog.CharactersId:
                foreach (var character in _project.Characters)
                    Rows.Add(new SectionRow("🧍", Text(character.Name, character.Id), Joined(character.Community, character.Class)));
                break;

            case NavigationCatalog.ItemsId:
                foreach (var section in ItemSections())
                    Rows.Add(new SectionRow("📦", section.Name, section.Parents.Count > 0
                        ? string.Join(", ", section.Parents)
                        : section.Entries.Count.ToString(CultureInfo.CurrentCulture)));
                break;

            case NavigationCatalog.IconsId:
                foreach (var texture in _project.Textures ?? Enumerable.Empty<ProjectTexture>())
                    Rows.Add(new SectionRow("🖼", texture.Name, texture.Error ?? Size(texture.Width, texture.Height)));
                break;

            case NavigationCatalog.ScriptsId:
                foreach (var script in _project.Scripts ?? Enumerable.Empty<ScriptFile>())
                    Rows.Add(new SectionRow("📜", Text(script.Name, script.Path), script.Path));
                break;

            case NavigationCatalog.SystemId:
                foreach (var include in LtxParser.Parse(_project.SystemLtx).Includes)
                    Rows.Add(new SectionRow("🔗", include.Path, include.Line.ToString(CultureInfo.CurrentCulture)));
                break;

            case NavigationCatalog.LanguagesId:
                foreach (var language in _project.Languages)
                    Rows.Add(new SectionRow("🌐", Text(language.Name, language.Locale),
                        language.Entries.Count.ToString(CultureInfo.CurrentCulture)));
                break;

            case NavigationCatalog.QuestsId:
                foreach (var quest in _project.Quests)
                    Rows.Add(new SectionRow("❕", Text(quest.Title, quest.Id),
                        quest.Objectives.Count.ToString(CultureInfo.CurrentCulture)));
                break;
        }

        OnPropertyChanged(nameof(HasRows));
    }

    /// <summary>Смена локали: подписи вкладок, серия и подписи настроек обновляются на месте.</summary>
    public void RefreshLocalization()
    {
        foreach (var tab in Tabs) tab.Label = LocalizationSource.Get(tab.LabelKey);
        Characters.RefreshLocalization();
        Items.RefreshLocalization();

        // До открытия проекта серия не показывается (иначе в пустом старте висел бы «SoC · …»).
        SeriesLabel = IsProjectOpen ? GameSeriesCatalog.Label(_project.Series) : "";
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(DensityLabel));
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>Подпись строки: имя, иначе идентификатор (в JS — `x.name || x.id`).</summary>
    private static string Text(string? name, string? id) =>
        !string.IsNullOrWhiteSpace(name) ? name! : id ?? "";

    /// <summary>Склейка непустых частей подписи (`community · class`).</summary>
    private static string Joined(params string?[] parts) =>
        string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>Размер текстуры (`W×H`) либо пустая строка, если размер неизвестен.</summary>
    private static string Size(int? width, int? height) =>
        width.HasValue && height.HasValue
            ? width.Value.ToString(CultureInfo.CurrentCulture) + "×" + height.Value.ToString(CultureInfo.CurrentCulture)
            : "";

    /// <summary>Секции редактора предметов: `itemsLtx` и уникальные секции `itemFiles`.</summary>
    private IEnumerable<LtxSection> ItemSections()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in LtxParser.Parse(_project.ItemsLtx).Sections)
            if (seen.Add(section.Name)) yield return section;

        if (_project.ItemFiles == null) yield break;
        foreach (var file in _project.ItemFiles.Values)
            foreach (var section in LtxParser.Parse(file).Sections)
                if (seen.Add(section.Name)) yield return section;
    }



    // --- настройки, справка, проверка, глобальный поиск ---

    /// <summary>Настройки программы (окно настроек правит их на месте).</summary>
    public ProgramSettings Settings => _settings;

    /// <summary>Настройки применены (тема/локаль/плотность) — окно перечитывает ресурсы.</summary>
    public event EventHandler? SettingsApplied;

    /// <summary>Применить настройки к оболочке: локаль, тема, автосейв, подписи вкладок.</summary>
    public void ApplyProgramSettings()
    {
        LocalizationSource.Instance.Locale = _settings.Locale;
        _theme.ApplySettings(_settings);
        _autosave.ApplySettings();
        RefreshLocalization();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Перечитать настройки профиля (кнопка «Отмена» в окне настроек).</summary>
    public void ReloadProgramSettings()
    {
        _settings = _store.LoadProgramSettings();
        ApplyProgramSettings();
    }

    /// <summary>`saveProgramSettings`: настройки в `settings.json` профиля.</summary>
    public void SaveProgramSettings()
    {
        _store.SaveProgramSettings(_settings);
        ApplyProgramSettings();
        StatusText = LocalizationSource.Get("programSettings");
        StatusDetail = LocalizationSource.Get("savedAt") + " " + DateTime.Now.ToString("t", CultureInfo.CurrentCulture);
    }

    /// <summary>Окно настроек программы (`programSettings`): тема, акцент, плотность, автосейв, клавиши.</summary>
    [RelayCommand]
    private void OpenProgramSettings()
    {
        var window = new SettingsWindow(this) { Owner = ActiveWindow };
        window.ShowDialog();
    }

    /// <summary>Окно настроек проекта (`projectSettings`): имя, серия, кодировки, gamedata.</summary>
    [RelayCommand]
    private void OpenProjectSettings()
    {
        var window = new ProjectSettingsWindow(new ProjectSettingsViewModel(this)) { Owner = ActiveWindow };
        if (window.ShowDialog() != true) return;

        ProjectName = _project.ProjectName;
        SeriesLabel = GameSeriesCatalog.Label(_project.Series);
        GamedataPath = _project.GamedataPath;
        SetDirty(true);
        RefreshProjectView();
        StatusText = LocalizationSource.Get("projectSettings");
    }

    /// <summary>Справка (`help`): разделы «Проекты», «Вкладки», «Gamedata», «Тема».</summary>
    [RelayCommand]
    private void OpenHelp()
    {
        var window = new HelpWindow { Owner = ActiveWindow };
        window.ShowDialog();
    }

    /// <summary>Выбор каталога (`chooseFolder` в настройках проекта).</summary>
    public string? SelectFolder(string? initialDirectory) => _dialogs.PickFolder(initialDirectory);

    /// <summary>`checkGraph` (кнопка «Проверить»): проблемы проекта и графа в статусной строке.</summary>
    [RelayCommand]
    private void CheckGraph()
    {
        RefreshIssues();
        StatusText = LocalizationSource.Get("checkGraph");
        StatusDetail = HasIssues
            ? Issues.Count.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("issues")
            : LocalizationSource.Get("noIssues");
    }

    /// <summary>`globalSearch`: результаты поиска по всем сущностям проекта.</summary>
    public void PublishSearch()
    {
        SearchHits.Clear();
        foreach (var hit in ProjectSearch.Search(_project, SearchQuery)) SearchHits.Add(hit);

        OnPropertyChanged(nameof(HasSearchHits));
        OnPropertyChanged(nameof(SearchSummary));
    }

    /// <summary>Окно глобального поиска (`globalSearch`, `Ctrl+K`).</summary>
    [RelayCommand]
    private void OpenSearch()
    {
        var window = new SearchWindow(this) { Owner = ActiveWindow };
        window.ShowDialog();
    }

    /// <summary>Переход к результату поиска: вкладка и подсказка (редактор — этапы 6–7).</summary>
    [RelayCommand]
    private void OpenHit(ProjectSearchHit? hit)
    {
        if (hit == null) return;

        ActivateTabById(hit.TabId);
        IsWelcomeOpen = false;
        StatusText = hit.Label;
        StatusDetail = hit.Snippet;
    }

    /// <summary>
    /// `processGamedataMap` (кнопка «Сканировать gamedata»): чтение каталога в фоне,
    /// скан, наложение `patch()` на проект, снапшот и отчёт с пропусками.
    /// </summary>
    [RelayCommand]
    private async Task ScanGamedataAsync()
    {
        var folder = _dialogs.PickFolder(string.IsNullOrWhiteSpace(GamedataPath) ? _project.GamedataPath : GamedataPath);
        if (string.IsNullOrWhiteSpace(folder)) return;

        IsBusy = true;
        BusyText = LocalizationSource.Get("scanning");
        StatusText = LocalizationSource.Get("scanGamedata");
        StatusDetail = "";

        try
        {
            var read = await Task.Run(() => FileSystemService.ReadDirectory(folder));

            // Скан с welcome-экрана создаёт проект: имя берём из папки, серию — по пути.
            // Для открытого проекта сканируем в него же, как и раньше.
            var startingNewProject = !IsProjectOpen;
            var project = startingNewProject ? ProjectMigration.Empty() : _project;
            if (startingNewProject)
            {
                var name = SuggestedProjectName(folder, read.TopName);
                project.ProjectName = name;
                project.DialogueId = name;
                project.Series = DetectSeries(folder, read.TopName);
            }

            var result = await Task.Run(() => GamedataScanner.Scan(
                project,
                read.TopName,
                read.Files,
                new GamedataScanOptions
                {
                    ImportEncoding = project.Settings.ImportEncoding,
                    PngEncoder = PngDataUrlEncoder.FromRgba,
                    Progress = (done, total) => _dispatcher.BeginInvoke(new Action(() =>
                        StatusDetail = done.ToString(CultureInfo.CurrentCulture) + "/" +
                                       total.ToString(CultureInfo.CurrentCulture)))
                }));

            result.Patch.Apply(project);
            GamedataPath = string.IsNullOrWhiteSpace(project.GamedataPath) ? read.TopName : project.GamedataPath;
            project.GamedataPath = GamedataPath;

            var skipped = result.Skipped.Concat(read.Skipped).ToList();
            if (startingNewProject)
            {
                // Adopt закрывает welcome, подключает вкладки и делает проект текущим;
                // Save фиксирует его в профиле, чтобы список проектов сразу обновился.
                Adopt(project, dirty: true);
                Save();
                StatusText = LocalizationSource.Get("scanProjectCreated");
            }
            else
            {
                _store.SaveGamedataSnapshot(project.ProjectName, AppDataService.ToSnapshot(project));
                SetDirty(true);
                // Скан заменяет персонажей/диалоги/предметы и справочники — вкладки и подсказки
                // пересобираются сразу, иначе до перезапуска показывался старый проект.
                DialogueGraph.Attach(project);
                Characters.Attach(project);
                Items.Attach(project);
                Icons.Attach(project);
                RefreshProjectView();
            }
            PublishSearch();

            StatusDetail = startingNewProject
                ? LocalizationSource.Get("scanProjectCreated") + " · " + project.ProjectName
                : project.ProjectName + " · " + LocalizationSource.Get("scanGamedata");
            _dialogs.ShowMessage(LocalizationSource.Get("scanGamedata"), ScanReport(result.Stats, skipped));
        }
        catch (Exception e)
        {
            StatusText = LocalizationSource.Get("scanFailed");
            StatusDetail = e.Message;
            _dialogs.ShowMessage(LocalizationSource.Get("scanGamedata"), e.Message);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    /// <summary>
    /// Имя проекта для скана без открытого проекта: корневая папка, а для типового
    /// `gamedata` — имя родительского каталога (например, «STALKER ТЧ»).
    /// </summary>
    private static string SuggestedProjectName(string folder, string? topName)
    {
        var name = (topName ?? "").Trim();
        if (name.Length == 0 || IsGenericGamedataName(name))
        {
            var trimmed = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(trimmed);
            var parentName = string.IsNullOrEmpty(parent) ? "" : Path.GetFileName(parent).Trim();
            if (parentName.Length > 0) name = parentName;
        }

        return name.Length == 0 ? ProjectSchema.DefaultProjectName : name;
    }

    /// <summary>Типовое имя папки данных (`gamedata`, `gamedata_*`) не подходит как имя проекта.</summary>
    private static bool IsGenericGamedataName(string name) =>
        name.Equals("gamedata", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("gamedata_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Серия по подсказкам в пути; иначе SoC (пользователь может сменить в настройках проекта).</summary>
    private static string DetectSeries(string folder, string? topName)
    {
        var hint = (folder + " " + topName).ToLowerInvariant();
        if (hint.Contains("зов припяти") || hint.Contains("call of pripyat") ||
            hint.Contains("pripyat") || hint.Contains("cop"))
            return "cop";
        if (hint.Contains("чистое небо") || hint.Contains("clear sky"))
            return "cs";
        return GameSeriesCatalog.DefaultSeries;
    }

    /// <summary>`exportXml`: внутренний XML диалога в файл (`InternalXml`).</summary>
    [RelayCommand]
    private void ExportXml()
    {
        var name = string.IsNullOrWhiteSpace(_project.ProjectName) ? ProjectSchema.DefaultProjectName : _project.ProjectName;
        var path = _dialogs.PickSaveFile(LocalizationSource.Get("exportXml"), name + ".xml",
            "XML (*.xml)|*.xml|*.*|*.*", _store.SavesDirectory);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllText(path, InternalXml.ToXml(_project), new UTF8Encoding(false));
        }
        catch (IOException e)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }
        catch (UnauthorizedAccessException e)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }

        StatusText = LocalizationSource.Get("exportXml");
        StatusDetail = path;
    }

    /// <summary>`exportSettings`: настройки программы в выбранный JSON-файл.</summary>
    [RelayCommand]
    private void ExportSettings()
    {
        var path = _dialogs.PickSaveJson(LocalizationSource.Get("exportSettings"), "xray-forge-settings.json", _store.Root);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(_settings, ProjectSerializer.Options), new UTF8Encoding(false));
        }
        catch (IOException e)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }
        catch (UnauthorizedAccessException e)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }

        StatusText = LocalizationSource.Get("exportSettings");
        StatusDetail = path;
    }

    /// <summary>`importSettings`: настройки программы из JSON-файла с сохранением в профиль.</summary>
    [RelayCommand]
    private void ImportSettings()
    {
        var path = _dialogs.PickOpenJson(LocalizationSource.Get("importSettings"), _store.Root);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            _settings = ProgramSettingsCatalog.FromJson(File.ReadAllText(path));
        }
        catch (IOException e)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }
        catch (UnauthorizedAccessException e)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }

        SaveProgramSettings();
        StatusText = LocalizationSource.Get("importSettings");
    }

    /// <summary>`resetStorage`: очистить профиль и начать с пустого проекта.</summary>
    [RelayCommand]
    private void ResetData()
    {
        if (!_dialogs.Confirm(LocalizationSource.Get("resetData") + "?")) return;

        _autosave.Stop();
        _store.Reset();
        _settings = _store.LoadProgramSettings();
        ApplyProgramSettings();

        LoadWelcomeList();
        LoadBackups();
        CloseCurrentProject();
        StatusText = LocalizationSource.Get("resetData");
    }

    /// <summary>`runPlugins`: экспорт через плагин (движок плагинов — вне каркаса этапа 5).</summary>
    [RelayCommand]
    private void RunPlugins()
    {
        StatusText = LocalizationSource.Get("runPlugins");
        StatusDetail = LocalizationSource.Get("tabStubHint");
    }

    /// <summary>`syncNow`: состояние синхронизации по сохранённому URL.</summary>
    [RelayCommand]
    private void SyncNow()
    {
        StatusText = LocalizationSource.Get("syncNow");
        StatusDetail = string.IsNullOrWhiteSpace(_settings.SyncUrl)
            ? LocalizationSource.Get("syncUrl")
            : _settings.SyncUrl;
    }

    /// <summary>`syncLoad`: запросить URL сервера синхронизации и сохранить настройки.</summary>
    [RelayCommand]
    private void SyncLoad()
    {
        var url = _dialogs.Prompt(LocalizationSource.Get("syncSection"), LocalizationSource.Get("syncUrl"), _settings.SyncUrl);
        if (string.IsNullOrWhiteSpace(url)) return;

        _settings.SyncUrl = url.Trim();
        _store.SaveProgramSettings(_settings);
        StatusText = LocalizationSource.Get("syncLoad");
        StatusDetail = _settings.SyncUrl;
    }

    /// <summary>Отчёт сканирования для диалога: счётчики разделов и первые пропущенные файлы.</summary>
    private static string ScanReport(GamedataScanStats stats, IReadOnlyList<SkippedFile> skipped)
    {
        var report = new StringBuilder();
        Line(report, "total", stats.Files);
        Line(report, "tabDialogues", stats.Dialogues);
        Line(report, "tabCharacters", stats.Characters);
        Line(report, "tabItems", stats.Items);
        Line(report, "tabIcons", stats.Textures);
        Line(report, "tabScripts", stats.Scripts);
        Line(report, "tabLanguages", stats.Languages);
        Line(report, "tabQuests", stats.Quests);

        if (skipped.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(LocalizationSource.Get("issues") + ": " + skipped.Count.ToString(CultureInfo.CurrentCulture));
            foreach (var file in skipped.Take(10))
                report.AppendLine("⚠ " + file.Path + " — " + file.Error);
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>Строка отчёта «подпись: значение».</summary>
    private static void Line(StringBuilder report, string key, int count) =>
        report.AppendLine(LocalizationSource.Get(key) + ": " + count.ToString(CultureInfo.CurrentCulture));







    // --- служебное: автосохранение, флаги, копии, горячие клавиши, освобождение ---

    /// <summary>Есть строки в активном разделе.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Есть результаты глобального поиска.</summary>
    public bool HasSearchHits => SearchHits.Count > 0;

    /// <summary>Есть история точек сохранения.</summary>
    public bool HasBackups => Backups.Count > 0;

    /// <summary>Автосохранение выполнено (`AutosaveService.Saved`, фон) — в UI-поток.</summary>
    private void OnAutosaveSaved(object? sender, Project project) =>
        _dispatcher.BeginInvoke(new Action(OnAutosaved));

    /// <summary>Отметить автосохранение в статусной строке и снять флаг изменений.</summary>
    private void OnAutosaved()
    {
        LastAutosaveText = LocalizationSource.Get("autosavedAt") + " " +
            DateTimeOffset.Now.LocalDateTime.ToString("t", CultureInfo.CurrentCulture);
        SetDirty(false);
        StatusText = LocalizationSource.Get("autosavedAt");
        StatusDetail = LastAutosaveText;
    }

    /// <summary>
    /// Граф диалога изменился (`patch` в JS): отметить проект изменённым, пересобрать обзор,
    /// проблемы и строки разделов, показать подпись действия в статусной строке.
    /// </summary>
    public void OnGraphEdited(string? statusText = null, string? statusDetail = null)
    {
        SetDirty(true);
        RefreshProjectView();
        if (statusText != null) StatusText = statusText;
        if (statusDetail != null) StatusDetail = statusDetail;
    }

    /// <summary>
    /// Правка в неграфовой вкладке (персонажи и следующие разделы): тот же путь, что
    /// <see cref="OnGraphEdited"/> — флаг изменений, обзор и строки разделов.
    /// </summary>
    public void OnSectionEdited(string? statusText = null, string? statusDetail = null) =>
        OnGraphEdited(statusText, statusDetail);

    /// <summary>
    /// На вкладке «Иконки» сменились атлас или размер ячеек. «Предметы» держат
    /// производные вырезки (`SelectedItemIcon`, слоты), поэтому их нужно пересобрать,
    /// иначе до повторного выбора предмета показывалась иконка со старым масштабом.
    /// </summary>
    public void OnAtlasChanged() => Items.RefreshAtlasPreview();

    /// <summary>Флаг изменений: оболочка (`dirty`) и автосохранение (`AutosaveService.Dirty`).</summary>
    private void SetDirty(bool value)
    {
        Dirty = value;
        _autosave.Dirty = value;
    }

    /// <summary>`loadBackups()`: история точек сохранения профиля в список окна «Копии».</summary>
    public void LoadBackups()
    {
        Backups.Clear();
        foreach (var backup in _store.LoadBackups()) Backups.Add(new BackupItemViewModel(backup));
        OnPropertyChanged(nameof(HasBackups));
    }

    /// <summary>Удаление точки сохранения из истории профиля.</summary>
    [RelayCommand]
    private void DeleteBackup(BackupItemViewModel? item)
    {
        if (item == null) return;

        var list = _store.LoadBackups();
        var index = list.FindIndex(backup => backup.Timestamp == item.Timestamp);
        if (index < 0) return;

        list.RemoveAt(index);
        _store.SaveBackups(list);
        LoadBackups();
        StatusText = LocalizationSource.Get("delete");
        StatusDetail = item.Text;
    }

    /// <summary>
    /// Выполнить действие горячей клавиши по id (`HotkeyCatalog.MatchAction`).
    /// Действия редакторов этапов 6–7 (узлы, undo/redo, удаление) пока только подсказывают.
    /// </summary>
    public void RunHotkey(string? actionId)
    {
        switch (actionId)
        {
            case "save": Save(); break;
            case "exportXml": ExportXml(); break;
            case "globalSearch": OpenSearch(); break;
            // Действия редактора графа (этап 6) — по id из `HotkeyCatalog` и только на вкладке «Диалоги».
            case "undo": RunGraphCommand(DialogueGraph.UndoCommand); break;
            case "redo": RunGraphCommand(DialogueGraph.RedoCommand); break;
            case "addStart": RunGraphCommand(DialogueGraph.AddStartCommand); break;
            case "addPhrase": RunGraphCommand(DialogueGraph.AddPhraseCommand); break;
            case "addRandom": RunGraphCommand(DialogueGraph.AddRandomCommand); break;
            case "addEnd": RunGraphCommand(DialogueGraph.AddEndCommand); break;
            case "deleteSelected": RunGraphCommand(DialogueGraph.DeleteSelectedCommand); break;
            case "escape":
                // Связь, ожидающая второй порт, отменяется первой (`setConnectFrom(null)` в App.js).
                DialogueGraph.CancelConnect();
                if (IsWelcomeOpen && IsProjectOpen) IsWelcomeOpen = false;
                else CloseHotkeyHint();
                break;
            default:
                StatusDetail = LocalizationSource.Get("tabStubHint");
                break;
        }
    }

    /// <summary>`hotkeys`: сочетания из настроек (окно пересобирает `KeyBinding`).</summary>
    /// <summary>
    /// Горячая клавиша редактора графа: выполняется только на вкладке «Диалоги»
    /// (в JS обработчики `undoActive`/`addNode` зависят от `tab === 'dialogues'`).
    /// </summary>
    private void RunGraphCommand(IRelayCommand command)
    {
        if (ActiveTab?.Id == NavigationCatalog.DialoguesId) command.Execute(null);
        else StatusDetail = LocalizationSource.Get("tabStubHint");
    }

    public IReadOnlyList<HotkeyBinding> Hotkeys => HotkeyCatalog.Bindings(_settings.Hotkeys);

    /// <summary>`hotkeyConflicts`: id действия → конфликтующая комбинация.</summary>
    public IReadOnlyDictionary<string, string> HotkeyDuplicates => HotkeyCatalog.Duplicates(_settings.Hotkeys);

    private void CloseHotkeyHint()
    {
        StatusText = LocalizationSource.Get("close");
        StatusDetail = LocalizationSource.Get("pressKey");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autosave.Saved -= OnAutosaveSaved;
        _autosave.Dispose();
    }






}
