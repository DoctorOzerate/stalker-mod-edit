using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Ltx;
using XRayForge.Core.Lua;
using XRayForge.Core.Projects;
using XRayForge.Core.Spawn;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;
using XRayForge.Localization;
using XRayForge.Services;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «system.ltx» (этап 7.5, `SystemTab.js`): редактор файла, список инклюдов
/// с подсветкой битых и автоисправлением, дерево/граф инклюдов, секции с правкой
/// полей, карта секций, проверки LTX/скриптов и просмотр `all.spawn`.
/// </summary>
public sealed partial class SystemLtxViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private Project _project = ProjectMigration.Empty();
    private Dictionary<string, string> LtxFiles => _project.LtxFiles ??= new(StringComparer.Ordinal);
    private bool _loading;
    private bool _suppressDerived;
    private Dictionary<string, List<string>> _sectionMap = new(StringComparer.Ordinal);

    public SystemLtxViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;
        Sections = new ObservableCollection<SystemSectionViewModel>();
        Includes = new ObservableCollection<SystemIncludeViewModel>();
        IncludeRows = new ObservableCollection<IncludeTreeRowViewModel>();
        SectionRows = new ObservableCollection<SectionMapRowViewModel>();
        Issues = new ObservableCollection<SystemIssueViewModel>();
        LtxFileRows = new ObservableCollection<LtxFileViewModel>();
        SpawnSections = new ObservableCollection<string>();
        AvailableLtxPaths = new ObservableCollection<string>();
        Attach(ProjectMigration.Empty());
    }

    public ObservableCollection<SystemSectionViewModel> Sections { get; }
    public ObservableCollection<SystemIncludeViewModel> Includes { get; }
    public ObservableCollection<IncludeTreeRowViewModel> IncludeRows { get; }
    public ObservableCollection<SectionMapRowViewModel> SectionRows { get; }
    public ObservableCollection<SystemIssueViewModel> Issues { get; }
    public ObservableCollection<LtxFileViewModel> LtxFileRows { get; }
    public ObservableCollection<string> SpawnSections { get; }
    public ObservableCollection<string> AvailableLtxPaths { get; }

    /// <summary>Текст `system.ltx`; каждая правка сразу попадает в проект.</summary>
    [ObservableProperty] private string _systemText = "";

    [ObservableProperty] private string _sectionSearch = "";
    [ObservableProperty] private string? _selectedIncludePath;
    [ObservableProperty] private LtxFileViewModel? _selectedLtxFile;
    [ObservableProperty] private SystemIncludeViewModel? _includePreview;

    // --- all.spawn (эпизодический просмотр, как spawnData в JS) ---

    [ObservableProperty] private bool _hasSpawn;
    [ObservableProperty] private string _spawnName = "";
    [ObservableProperty] private string _spawnSummary = "";

    // --- вычисляемые подписи ---

    public bool HasSections => Sections.Count > 0;
    public bool HasIncludes => Includes.Count > 0;
    public bool HasIncludeTree => IncludeRows.Count > 0;
    public bool HasIncludeSvg => IncludeGraphSvg.Length > 0;
    public bool HasSectionRows => SectionRows.Count > 0;
    public bool HasIssues => Issues.Count > 0;
    public bool HasLtxFiles => LtxFileRows.Count > 0;
    public bool HasSelectedLtxFile => SelectedLtxFile != null;
    public bool HasIncludePreview => IncludePreview != null;
    public bool HasSpawnSections => SpawnSections.Count > 0;

    public string IncludeGraphSvg { get; private set; } = "";
    public int BrokenIncludes => Includes.Count(i => !i.Found);
    public string IncludesTitle => LocalizationSource.Get("systemIncludeTitle") + ": " +
        Includes.Count.ToString(CultureInfo.CurrentCulture) + " · " +
        LocalizationSource.Get("systemBroken") + ": " + BrokenIncludes.ToString(CultureInfo.CurrentCulture);
    public string SectionsTitle => LocalizationSource.Get("systemSections") + " · " +
        Sections.Count.ToString(CultureInfo.CurrentCulture);
    public string SectionMapTitle => LocalizationSource.Get("systemSectionsLoaded") + " · " +
        SectionRows.Count.ToString(CultureInfo.CurrentCulture);
    public string LtxFilesTitle => LocalizationSource.Get("systemLtxFiles") + " · " +
        LtxFileRows.Count.ToString(CultureInfo.CurrentCulture);
    public string ChecksTitle => LocalizationSource.Get("systemChecks");

    /// <summary>Принять проект (`Adopt`): сбросить просмотр, пересобрать всё.</summary>
    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _project.LtxFiles ??= new Dictionary<string, string>(StringComparer.Ordinal);
        _project.Scripts ??= new List<ScriptFile>();

        _loading = true;
        try
        {
            SystemText = _project.SystemLtx ?? "";
            IncludePreview = null;
            SelectedLtxFile = null;
            SectionSearch = "";
            HasSpawn = false;
            SpawnName = "";
            SpawnSummary = "";
            SpawnSections.Clear();
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(SystemText));
        OnPropertyChanged(nameof(IncludePreview));
        OnPropertyChanged(nameof(SelectedLtxFile));
        OnPropertyChanged(nameof(SectionSearch));
        RefreshLtxFileList();
        RefreshDerived();
    }

    /// <summary>Смена локали: заголовки пересобираются.</summary>
    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(IncludesTitle));
        OnPropertyChanged(nameof(SectionsTitle));
        OnPropertyChanged(nameof(SectionMapTitle));
        OnPropertyChanged(nameof(LtxFilesTitle));
        OnPropertyChanged(nameof(ChecksTitle));
        foreach (var section in Sections) section.RaiseLabels();
    }

    partial void OnSystemTextChanged(string value)
    {
        if (_loading) return;
        _project.SystemLtx = value ?? "";
        if (!_suppressDerived) RefreshDerived();
        _shell.OnSectionEdited();
    }

    partial void OnSelectedLtxFileChanged(LtxFileViewModel? value)
    {
        foreach (var row in LtxFileRows) row.IsSelected = ReferenceEquals(row, value);
        OnPropertyChanged(nameof(HasSelectedLtxFile));
    }

    partial void OnIncludePreviewChanged(SystemIncludeViewModel? value) =>
        OnPropertyChanged(nameof(HasIncludePreview));

    partial void OnSectionSearchChanged(string value) => RefreshSectionRows();

    partial void OnHasSpawnChanged(bool value) => OnPropertyChanged(nameof(HasSpawnSections));

    private void RefreshLtxFileList()
    {
        LtxFileRows.Clear();
        foreach (var pair in LtxFiles.OrderBy(x => x.Key, StringComparer.Ordinal))
            LtxFileRows.Add(new LtxFileViewModel(this, pair.Key, pair.Value));

        AvailableLtxPaths.Clear();
        foreach (var path in LtxFiles.Keys.OrderBy(x => x, StringComparer.Ordinal))
            AvailableLtxPaths.Add(path);

        if (SelectedLtxFile != null)
        {
            var path = SelectedLtxFile.Path;
            SelectedLtxFile = LtxFileRows.FirstOrDefault(r => r.Path == path);
        }

        OnPropertyChanged(nameof(HasLtxFiles));
        OnPropertyChanged(nameof(LtxFilesTitle));
        OnPropertyChanged(nameof(HasSelectedLtxFile));
    }

    /// <summary>Полный пересчёт: секции, инклюды, дерево, карта секций и проверки.</summary>
    private void RefreshDerived()
    {
        _sectionMap = BuildSectionMap();
        BuildSections();
        BuildIncludes();
        BuildIncludeTree();
        RefreshSectionRows();
        BuildIssues();
        NotifyDerived();
    }

    private Dictionary<string, List<string>> BuildSectionMap()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string? text, string file)
        {
            foreach (var section in LtxParser.Parse(text).Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Name)) continue;
                if (!map.TryGetValue(section.Name, out var files))
                    map[section.Name] = files = new List<string>();
                if (!files.Contains(file)) files.Add(file);
            }
        }

        Add(_project.SystemLtx, "system.ltx");
        Add(_project.ItemsLtx, "items.ltx");
        foreach (var pair in LtxFiles) Add(pair.Value, pair.Key);
        return map;
    }

    private void BuildSections()
    {
        Sections.Clear();
        var parsed = LtxParser.Parse(_project.SystemLtx);
        foreach (var section in parsed.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Name)) continue;
            Sections.Add(new SystemSectionViewModel(this, section.Name, section.Parents, section.Entries));
        }

        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(SectionsTitle));
    }

    private void BuildIncludes()
    {
        Includes.Clear();
        foreach (var include in LtxParser.Parse(_project.SystemLtx).Includes)
        {
            var path = Paths.Normalize(include.Path);
            var found = LtxIncludes.IsFileLoaded(path, LtxFiles);
            var suggestion = found ? null : LtxIncludes.FindIncludeSuggestion(path, LtxFiles);
            Includes.Add(new SystemIncludeViewModel(include.Line, include.Raw, path, found, suggestion));
        }

        OnPropertyChanged(nameof(HasIncludes));
        OnPropertyChanged(nameof(BrokenIncludes));
        OnPropertyChanged(nameof(IncludesTitle));
    }

    private void BuildIncludeTree()
    {
        IncludeRows.Clear();
        var rows = new List<IncludeTreeRow>();
        LtxIncludes.IncludeTreeRows(_project.SystemLtx, LtxFiles, 0,
            new HashSet<string>(StringComparer.Ordinal), rows);
        foreach (var row in rows) IncludeRows.Add(new IncludeTreeRowViewModel(row));

        IncludeGraphSvg = LtxIncludes.IncludeGraphSvg(_project.SystemLtx, LtxFiles);
        OnPropertyChanged(nameof(IncludeGraphSvg));
        OnPropertyChanged(nameof(HasIncludeTree));
        OnPropertyChanged(nameof(HasIncludeSvg));
    }

    private void RefreshSectionRows()
    {
        SectionRows.Clear();
        var search = (SectionSearch ?? "").Trim();
        foreach (var pair in _sectionMap.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (search.Length > 0 && pair.Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            SectionRows.Add(new SectionMapRowViewModel(pair.Key, pair.Value));
        }

        OnPropertyChanged(nameof(HasSectionRows));
        OnPropertyChanged(nameof(SectionMapTitle));
    }

    private void BuildIssues()
    {
        Issues.Clear();
        var combined = (_project.SystemLtx ?? "") + "\n" + (_project.ItemsLtx ?? "");

        foreach (var duplicate in LtxInheritance.FindDuplicateSections(_sectionMap))
        {
            Issues.Add(new SystemIssueViewModel("warning",
                "⚠ " + duplicate.Name + " (" + duplicate.Files.Count.ToString(CultureInfo.CurrentCulture) + ")",
                string.Join(", ", duplicate.Files)));
        }

        foreach (var cycle in LtxInheritance.FindInheritanceCycles(combined))
            Issues.Add(new SystemIssueViewModel("error", "↻ " + cycle.Message, cycle.Section));

        foreach (var issue in LtxInheritance.ValidateRefs(combined, _sectionMap))
            Issues.Add(new SystemIssueViewModel(issue.Severity, issue.Message, issue.Section));

        var allLtx = string.Join("\n", LtxFiles.Values);
        var scripts = (_project.Scripts ?? new List<ScriptFile>())
            .Select(file => new LuaScriptFile { Name = file.Name, Text = file.Text });
        foreach (var issue in LuaAnalyzer.ValidateScriptBindings(allLtx, scripts))
            Issues.Add(new SystemIssueViewModel("error", "🔗 " + issue.Message, issue.Binding));

        OnPropertyChanged(nameof(HasIssues));
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasIncludes));
        OnPropertyChanged(nameof(HasIncludeTree));
        OnPropertyChanged(nameof(HasIncludeSvg));
        OnPropertyChanged(nameof(HasSectionRows));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(BrokenIncludes));
        OnPropertyChanged(nameof(IncludesTitle));
        OnPropertyChanged(nameof(SectionsTitle));
        OnPropertyChanged(nameof(SectionMapTitle));
    }

    // --- инклюды и файлы ---

    [RelayCommand]
    private void AddInclude()
    {
        if (string.IsNullOrWhiteSpace(SelectedIncludePath)) return;
        AppendInclude(SelectedIncludePath!);
    }

    [RelayCommand]
    private void AddIncludePlaceholder() => AppendInclude("your_file.ltx");

    private void AppendInclude(string path)
    {
        var current = (SystemText ?? "").TrimEnd();
        var line = "#include \"" + path + "\"";
        SystemText = (current.Length > 0 ? current + "\n" : "") + line + "\n";
        _shell.StatusText = LocalizationSource.Get("systemIncludeAdded") + ": " + path;
    }

    [RelayCommand]
    private void DownloadSystem()
    {
        var path = _dialogs.PickSaveFile(LocalizationSource.Get("systemDownload"), "system.ltx",
            "LTX (*.ltx)|*.ltx|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;
        WriteText(path, SystemText);
    }

    [RelayCommand]
    private void PreviewInclude(SystemIncludeViewModel? include)
    {
        include ??= IncludePreview;
        if (include == null) return;
        var key = FindLtxKey(include.Path);
        if (key == null) return;
        IncludePreview = new SystemIncludeViewModel(include.Line, include.Raw, key, true, null,
            LtxFiles[key]);
    }

    [RelayCommand]
    private void FixInclude(SystemIncludeViewModel? include)
    {
        if (include?.Suggestion == null) return;
        SystemText = LtxTextTools.UpdateLine(SystemText, include.Line,
            "#include \"" + include.Suggestion + "\"");
        _shell.StatusText = LocalizationSource.Get("systemIncludeFixed") + ": " + include.Suggestion;
    }

    [RelayCommand]
    private void CommentInclude(SystemIncludeViewModel? include)
    {
        if (include == null) return;
        var lines = LtxTextTools.SplitLines(SystemText);
        if (include.Line < 1 || include.Line > lines.Count) return;
        var original = lines[include.Line - 1].Trim();
        var next = original.StartsWith(';') ? original.TrimStart(';').TrimStart() : ";" + original;
        SystemText = LtxTextTools.UpdateLine(SystemText, include.Line, next);
    }

    [RelayCommand]
    private void DownloadLtxFile(LtxFileViewModel? file)
    {
        file ??= SelectedLtxFile;
        if (file == null) return;
        var name = Path.GetFileName(file.Path);
        var path = _dialogs.PickSaveFile(LocalizationSource.Get("systemDownload"),
            string.IsNullOrWhiteSpace(name) ? "file.ltx" : name, "LTX (*.ltx)|*.ltx|*.*|*.*",
            _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;
        WriteText(path, file.Text);
    }

    [RelayCommand]
    private void DeleteLtxFile(LtxFileViewModel? file)
    {
        file ??= SelectedLtxFile;
        if (file == null) return;
        if (!_dialogs.Confirm(LocalizationSource.Get("systemDeleteFile") + ": " + file.Path)) return;

        LtxFiles.Remove(file.Path);
        if (SelectedLtxFile?.Path == file.Path) SelectedLtxFile = null;
        RefreshLtxFileList();
        RefreshDerived();
        _shell.OnSectionEdited(LocalizationSource.Get("systemFileDeleted") + ": " + file.Path);
    }

    private void WriteText(string path, string text)
    {
        try
        {
            File.WriteAllBytes(path, GameEncoding.Encode(text ?? "",
                _project.Settings.ExportEncoding ?? "windows-1251"));
            _shell.StatusText = LocalizationSource.Get("systemDownloaded");
            _shell.StatusDetail = path;
        }
        catch (Exception e)
        {
            _shell.StatusText = LocalizationSource.Get("systemDownloadFailed") + ": " + e.Message;
        }
    }

    private string? FindLtxKey(string path)
    {
        var lowered = (path ?? "").ToLowerInvariant();
        foreach (var key in LtxFiles.Keys)
        {
            var candidate = key.ToLowerInvariant();
            if (candidate == lowered || candidate.EndsWith("/" + lowered, StringComparison.Ordinal))
                return key;
        }
        return null;
    }

    // --- секции и поля ---

    [RelayCommand]
    private void AddSection()
    {
        var name = _dialogs.Prompt(LocalizationSource.Get("systemSectionAdd"),
            LocalizationSource.Get("systemSectionName"), "new_section");
        if (string.IsNullOrWhiteSpace(name)) return;
        ApplyText(LtxTextTools.AddSection(SystemText, name.Trim()), true);
        _shell.StatusText = LocalizationSource.Get("systemSectionCreated") + ": " + name.Trim();
    }

    internal void ToggleSection(SystemSectionViewModel section) => section.IsExpanded = !section.IsExpanded;

    internal void AddEntry(SystemSectionViewModel section) =>
        ApplyText(LtxTextTools.AddFieldToSection(SystemText, section.Name, "new_key", "value"), true);

    internal void RemoveEntry(int line) => ApplyText(LtxTextTools.RemoveFieldByLine(SystemText, line), true);

    internal void UpdateEntry(int line, string key, string value)
    {
        var lines = LtxTextTools.SplitLines(SystemText);
        if (line < 1 || line > lines.Count) return;
        var original = lines[line - 1];
        var commentIndex = original.IndexOf(';');
        var comment = commentIndex >= 0 ? " " + original[commentIndex..] : "";
        ApplyText(LtxTextTools.UpdateLine(SystemText, line, key + " = " + value + comment), false);
    }

    private void ApplyText(string? text, bool rebuild)
    {
        _suppressDerived = !rebuild;
        try
        {
            SystemText = text ?? "";
        }
        finally
        {
            _suppressDerived = false;
        }

        if (rebuild) RefreshDerived();
    }

    // --- all.spawn и переход к скрипту ---

    [RelayCommand]
    private void LoadSpawn()
    {
        var path = _dialogs.PickOpenFile(LocalizationSource.Get("systemLoadSpawn"),
            "Spawn (*.spawn)|*.spawn|*.*|*.*", _shell.Project.GamedataPath);
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var analysis = SpawnAnalyzer.AnalyzeSpawn(File.ReadAllBytes(path));
            var header = analysis.Header;
            var info = new FileInfo(path);
            SpawnName = Path.GetFileName(path);
            SpawnSummary = SpawnName + " · " +
                Math.Round(info.Length / 1024.0).ToString(CultureInfo.CurrentCulture) + " KB" +
                (header != null
                    ? " · v" + header.Version.ToString(CultureInfo.CurrentCulture) +
                      " · objects " + header.SpawnCount.ToString(CultureInfo.CurrentCulture) +
                      " · levels " + header.LevelCount.ToString(CultureInfo.CurrentCulture)
                    : "");
            SpawnSections.Clear();
            foreach (var section in analysis.Sections.Take(500)) SpawnSections.Add(section);
            HasSpawn = true;
            OnPropertyChanged(nameof(HasSpawnSections));
        }
        catch (Exception e)
        {
            _shell.StatusText = LocalizationSource.Get("systemSpawnFailed") + ": " + e.Message;
        }
    }

    [RelayCommand]
    private void JumpToBinding(SystemIssueViewModel? issue) =>
        _shell.JumpToScriptBinding(issue?.JumpTarget);

    internal void OnLtxFileTextChanged(LtxFileViewModel file)
    {
        LtxFiles[file.Path] = file.Text;
        RefreshDerived();
        _shell.OnSectionEdited();
    }
}

