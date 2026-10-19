using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Lua;
using XRayForge.Core.Projects;
using XRayForge.Core.Scripts;
using XRayForge.Core.Text;
using XRayForge.Localization;
using XRayForge.Services;

namespace XRayForge.ViewModels;

/// <summary>Аргументы перехода к определению (курсор в редакторе ставит View).</summary>
public sealed class ScriptJumpEventArgs : EventArgs
{
    public ScriptJumpEventArgs(ScriptItemViewModel item, int line, int offset, string name)
    {
        Item = item;
        Line = line;
        Offset = offset;
        Name = name;
    }

    public ScriptItemViewModel Item { get; }
    public int Line { get; }
    public int Offset { get; }
    public string Name { get; }
}

/// <summary>
/// Вкладка «Скрипты» (этап 7.4, `ScriptsTab.js`): список .script/.lua с поиском,
/// редактирование с подсветкой прямо во время ввода, сниппеты API,
/// замена по всем файлам, проверка Lua, граф вызовов, diff и экспорт файла.
/// </summary>
public sealed partial class ScriptsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly Dictionary<ScriptFile, ScriptItemViewModel> _items = new();

    private Project _project = ProjectMigration.Empty();

    /// <summary>Список файлов проекта; `null` для ручного JSON заменяется пустым.</summary>
    private List<ScriptFile> Files => _project.Scripts ??= new List<ScriptFile>();

    public ScriptsViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;

        Scripts = new ObservableCollection<ScriptItemViewModel>();
        Diagnostics = new ObservableCollection<ScriptDiagnosticViewModel>();
        Edges = new ObservableCollection<ScriptEdgeViewModel>();
        Functions = new ObservableCollection<ScriptFunctionViewModel>();
    }

    // --- коллекции ---

    /// <summary>Отфильтрованный список карточек скриптов.</summary>
    public ObservableCollection<ScriptItemViewModel> Scripts { get; }

    public ObservableCollection<ScriptDiagnosticViewModel> Diagnostics { get; }
    public ObservableCollection<ScriptEdgeViewModel> Edges { get; }
    public ObservableCollection<ScriptFunctionViewModel> Functions { get; }

    /// <summary>Справочник API движка для вставки в курсор (`LUA_API`).</summary>
    public IReadOnlyList<string> EngineApi => LuaCatalog.Api;

    /// <summary>Готовые STALKER-сниппеты (`LUA_SNIPPETS`).</summary>
    public IReadOnlyList<LuaSnippet> Snippets => LuaCatalog.Snippets;

    // --- состояние ---

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _replaceText = "";
    [ObservableProperty] private bool _sortByName;
    [ObservableProperty] private ScriptItemViewModel? _selected;

    /// <summary>`scriptDiff`: две колонки «оригинал / текущая».</summary>
    [ObservableProperty] private bool _showDiff;

    /// <summary>Показывать панель диагностики Lua (ошибки/предупреждения можно свернуть).</summary>
    [ObservableProperty] private bool _showDiagnostics = true;

    /// <summary>Текст для поиска API (поле с автодополнением; вставка по Enter/выбору).</summary>
    [ObservableProperty] private string _apiQuery = "";
    [ObservableProperty] private string? _selectedApi;
    [ObservableProperty] private LuaSnippet? _selectedSnippet;
    [ObservableProperty] private string? _lastOpenedFile;

    /// <summary>Позиция каретки, обновляется View (`TextBox.SelectionStart`).</summary>
    public int CaretIndex { get; set; }

    /// <summary>Длина выделения, обновляется View (`TextBox.SelectionLength`).</summary>
    public int SelectionLength { get; set; }

    /// <summary>Запрос установить каретку после перехода/вставки; обрабатывает View.</summary>
    public event EventHandler<ScriptJumpEventArgs>? CaretRequested;

    // --- вычисляемые подписи ---

    public int TotalCount => _project.Scripts?.Count ?? 0;
    public bool HasScripts => TotalCount > 0;
    public bool HasVisibleScripts => Scripts.Count > 0;
    public bool HasSelection => Selected != null;
    public string CountText => LocalizationSource.Get("scriptFiles") + ": " +
        TotalCount.ToString(CultureInfo.CurrentCulture) +
        (string.IsNullOrWhiteSpace(Search) ? "" :
            " · " + LocalizationSource.Get("scriptFound") + ": " + Scripts.Count.ToString(CultureInfo.CurrentCulture));
    public string NoScriptsText => HasScripts
        ? LocalizationSource.Get("scriptNoMatches")
        : LocalizationSource.Get("scriptEmpty");
    public bool HasDiagnostics => Diagnostics.Count > 0;
    public bool ShowDiagnosticsPanel => HasDiagnostics && ShowDiagnostics;
    public string DiagnosticsTitle => LocalizationSource.Get("scriptCheckLua") + " (" +
        Diagnostics.Count.ToString(CultureInfo.CurrentCulture) + ")";
    public string DiagnosticsToggleText => DiagnosticsTitle +
        (HasDiagnostics ? (ShowDiagnostics ? " ▾" : " ▸") : "");
    public bool HasCallGraph => Functions.Count > 0 || Edges.Count > 0 || UnusedText.Length > 0;
    public string CallGraphTitle => LocalizationSource.Get("scriptCallGraph") + " · " +
        Functions.Count.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("scriptFunctionsShort");

    [ObservableProperty] private string _unusedText = "";
    [ObservableProperty] private string _bracketsText = "";
    [ObservableProperty] private bool _bracketsBalanced = true;
    public string FunctionsCaption => LocalizationSource.Get("scriptFunctions");
    public string SourceCaption => LocalizationSource.Get("scriptSource");
    public string CurrentCaption => LocalizationSource.Get("scriptCurrent");
    public bool CanShowDiff => Selected?.HasOriginal == true;

    /// <summary>Принять проект (`Adopt`): пересобрать карточки, выбор сбросить.</summary>
    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _project.Scripts ??= new List<ScriptFile>();
        _items.Clear();
        Selected = null;

        foreach (var file in Files)
        {
            // Снапшот без `original` (старые проекты) — исходником считаем текущий текст.
            if (string.IsNullOrEmpty(file.Original) && !string.IsNullOrEmpty(file.Text)) file.Original = file.Text;
            _items[file] = new ScriptItemViewModel(this, file);
        }

        RefreshList();
        RefreshLocalization();
    }

    /// <summary>Смена локали: подписи и заглушки пересобираются.</summary>
    public void RefreshLocalization()
    {
        foreach (var item in _items.Values) item.RaiseMeta();
        RefreshDerived();
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(NoScriptsText));
        OnPropertyChanged(nameof(DiagnosticsTitle));
        OnPropertyChanged(nameof(DiagnosticsToggleText));
        OnPropertyChanged(nameof(CallGraphTitle));
        OnPropertyChanged(nameof(FunctionsCaption));
        OnPropertyChanged(nameof(SourceCaption));
        OnPropertyChanged(nameof(CurrentCaption));
    }

    private void RefreshList()
    {
        var filtered = ScriptsCatalog.Filter(Files, Search, SortByName);
        Scripts.Clear();
        foreach (var file in filtered)
            if (_items.TryGetValue(file, out var item)) Scripts.Add(item);

        if (Selected == null || !Scripts.Contains(Selected)) Selected = Scripts.FirstOrDefault();

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasVisibleScripts));
        OnPropertyChanged(nameof(NoScriptsText));
    }

    partial void OnSearchChanged(string value) => RefreshList();

    partial void OnSortByNameChanged(bool value) => RefreshList();

    partial void OnSelectedChanged(ScriptItemViewModel? oldValue, ScriptItemViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;

        ShowDiff = false;
        RefreshDerived();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanShowDiff));
    }

    partial void OnShowDiffChanged(bool value) => OnPropertyChanged(nameof(CanShowDiff));

    partial void OnShowDiagnosticsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDiagnosticsPanel));
        OnPropertyChanged(nameof(DiagnosticsToggleText));
    }

    /// <summary>Выбор карточки (клик по списку или команда элемента).</summary>
    public void SelectItem(ScriptItemViewModel? item)
    {
        if (item != null && !Scripts.Contains(item))
        {
            // Переход к определению/создание может вывести карточку за пределы фильтра.
            Search = "";
            RefreshList();
        }

        Selected = item;
    }

    /// <summary>Выбрать скрипт по имени файла (переход из проверки `script_binding`).</summary>
    public bool SelectByName(string? fileName)
    {
        var wanted = (fileName ?? "").Trim();
        if (wanted.Length == 0) return false;
        var baseName = Path.GetFileName(wanted);
        var item = Scripts.FirstOrDefault(x =>
            string.Equals(x.Name, wanted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(x.Name), baseName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(x.Path), baseName, StringComparison.OrdinalIgnoreCase));
        if (item == null) return false;

        Search = "";
        RefreshList();
        SelectItem(item);
        return true;
    }

    /// <summary>Правка в `TextBox`: проект уже обновлён карточкой, пересчитать анализ.</summary>
    internal void OnScriptTextChanged(ScriptItemViewModel item)
    {
        if (!ReferenceEquals(Selected, item)) return;

        RefreshDerived();
        _shell.OnSectionEdited();
    }

    /// <summary>Пересчитать проверку, граф, функции, скобки и предпросмотр по текущему тексту.</summary>
    private void RefreshDerived()
    {
        Diagnostics.Clear();
        Edges.Clear();
        Functions.Clear();
        UnusedText = "";

        var item = Selected;
        if (item == null)
        {
            BracketsText = "";
            BracketsBalanced = true;
            NotifyDerived();
            return;
        }

        var text = item.Text;
        foreach (var issue in LuaAnalyzer.Check(text)) Diagnostics.Add(new ScriptDiagnosticViewModel(issue));
        foreach (var issue in LuaAnalyzer.UndeclaredGlobals(text)) Diagnostics.Add(new ScriptDiagnosticViewModel(issue));
        foreach (var duplicate in LuaAnalyzer.DuplicateFunctions(text))
            Diagnostics.Add(new ScriptDiagnosticViewModel(
                LocalizationSource.Get("scriptDuplicateFunction") + ": " + duplicate.Name +
                " (" + duplicate.Count.ToString(CultureInfo.CurrentCulture) + ")"));

        var graph = LuaAnalyzer.BuildCallGraph(text);
        foreach (var edge in graph.Edges) Edges.Add(new ScriptEdgeViewModel(edge));
        foreach (var name in graph.Functions)
        {
            var calls = Math.Max(0, ScriptsCatalog.CountOccurrences(text, name) - 1);
            Functions.Add(new ScriptFunctionViewModel(name, calls));
        }

        var unused = LuaAnalyzer.Unused(text);
        UnusedText = unused.Count > 0
            ? LocalizationSource.Get("scriptUnused") + ": " + string.Join(", ", unused)
            : "";

        var brackets = LuaTokenizer.CountBrackets(text);
        BracketsBalanced = brackets.Open == brackets.Close;
        BracketsText = LocalizationSource.Get("scriptBrackets") + ": " +
            (BracketsBalanced ? "✓" : "✗ " + brackets.Open.ToString(CultureInfo.CurrentCulture) +
                "/" + brackets.Close.ToString(CultureInfo.CurrentCulture));

        NotifyDerived();
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(ShowDiagnosticsPanel));
        OnPropertyChanged(nameof(DiagnosticsTitle));
        OnPropertyChanged(nameof(DiagnosticsToggleText));
        OnPropertyChanged(nameof(HasCallGraph));
        OnPropertyChanged(nameof(CallGraphTitle));
        OnPropertyChanged(nameof(CanShowDiff));
    }

    // --- операции со списком (newScript / renameScript / deleteScript) ---

    [RelayCommand]
    private void CreateScript()
    {
        var name = UniqueName(ScriptsCatalog.DefaultName);
        var file = ScriptsCatalog.Create(name);
        Files.Add(file);
        var item = new ScriptItemViewModel(this, file);
        _items[file] = item;

        Search = "";
        RefreshList();
        Selected = item;
        _shell.StatusText = LocalizationSource.Get("scriptCreated") + ": " + file.Name;
        _shell.OnSectionEdited();
    }

    [RelayCommand]
    internal void RenameScript(ScriptItemViewModel? item)
    {
        item ??= Selected;
        if (item == null) return;

        var name = _dialogs.Prompt(LocalizationSource.Get("scriptRename"),
            LocalizationSource.Get("scriptFileName"), item.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        var normalized = ScriptsCatalog.NormalizeFileName(name, item.Name);
        if (Files.Any(other => !ReferenceEquals(other, item.File) &&
                string.Equals(ScriptsCatalog.Name(other), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            _shell.StatusText = LocalizationSource.Get("scriptNameExists") + ": " + normalized;
            return;
        }

        if (!ScriptsCatalog.Rename(item.File, normalized)) return;

        // В проекте `name` и `path` переименовываются вместе (`renameScript`).
        item.File.Path = normalized;
        item.RaiseMeta();
        RefreshList();
        _shell.StatusText = LocalizationSource.Get("scriptRenamed") + ": " + normalized;
        _shell.OnSectionEdited();
    }

    [RelayCommand]
    internal void DeleteScript(ScriptItemViewModel? item)
    {
        item ??= Selected;
        if (item == null) return;

        var index = Scripts.IndexOf(item);
        if (!ScriptsCatalog.Remove(Files, item.File)) return;
        _items.Remove(item.File);
        Scripts.Remove(item);
        Selected = Scripts.Count == 0 ? null : Scripts[Math.Min(index, Scripts.Count - 1)];
        RefreshList();
        _shell.StatusText = LocalizationSource.Get("scriptDeleted") + ": " + item.Name;
        _shell.OnSectionEdited();
    }

    private string UniqueName(string name)
    {
        var existing = Files.Select(ScriptsCatalog.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(name)) return name;

        var extension = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        for (var index = 2; ; index++)
        {
            var candidate = stem + "_" + index.ToString(CultureInfo.InvariantCulture) + extension;
            if (!existing.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Замена подстроки во всех скриптах (`replaceAllInScripts`).</summary>
    [RelayCommand]
    private void ReplaceAll()
    {
        if (string.IsNullOrEmpty(Search)) return;

        var count = ScriptsCatalog.ReplaceAll(Files, Search, ReplaceText);
        foreach (var item in _items.Values) item.LoadText(item.File.Text);

        RefreshList();
        RefreshDerived();
        if (count == 0)
        {
            _shell.StatusText = LocalizationSource.Get("scriptNothingReplaced");
            return;
        }

        var message = LocalizationSource.Get("scriptReplaced") + ": " + count.ToString(CultureInfo.CurrentCulture);
        _shell.StatusText = message;
        _shell.OnSectionEdited(message);
    }

    /// <summary>Переключить сравнение «оригинал / текущая» (`toggleDiff` в JS).</summary>
    [RelayCommand]
    private void ToggleDiff()
    {
        if (!CanShowDiff) return;
        ShowDiff = !ShowDiff;
    }

    /// <summary>Свернуть/развернуть панель диагностики Lua; анализ продолжает работать.</summary>
    [RelayCommand]
    private void ToggleDiagnostics()
    {
        if (!HasDiagnostics) return;
        ShowDiagnostics = !ShowDiagnostics;
    }

    // --- форматирование и вставка (formatCurrentScript / insertLuaAtCursor) ---

    [RelayCommand]
    private void FormatScript()
    {
        if (Selected == null) return;

        Selected.LoadText(ScriptsCatalog.Format(Selected.Text));
        RaiseCaret(Selected, ScriptsCatalog.LineAtOffset(Selected.Text, CaretIndex), 0, "");
        _shell.StatusText = LocalizationSource.Get("scriptFormatted") + ": " + Selected.Name;
        _shell.OnSectionEdited();
    }

    /// <summary>Вставить элемент справочника API в позицию каретки (`insertLuaAtCursor`).</summary>
    [RelayCommand]
    private void InsertApi(string? api)
    {
        var body = !string.IsNullOrWhiteSpace(api) ? api : SelectedApi;
        if (string.IsNullOrWhiteSpace(body)) body = ApiQuery;
        InsertText(body);
    }

    [RelayCommand]
    private void InsertSnippet()
    {
        if (SelectedSnippet != null) InsertText(SelectedSnippet.Text);
    }

    private void InsertText(string? body)
    {
        if (Selected == null || string.IsNullOrEmpty(body)) return;

        var result = ScriptsCatalog.InsertAtCursor(Selected.Text, body, CaretIndex, SelectionLength);
        Selected.LoadText(result.Text);
        RaiseCaret(Selected, ScriptsCatalog.LineAtOffset(result.Text, result.Caret), result.Caret, "");
        _shell.StatusText = LocalizationSource.Get("scriptInserted") + ": " + Selected.Name;
        _shell.OnSectionEdited();
    }

    /// <summary>Перейти к определению функции (`jumpToDefinition`), в т.ч. в другом файле.</summary>
    [RelayCommand]
    private void JumpFunction(string? name)
    {
        var function = ScriptsCatalog.TrimFunctionName(name);
        if (function.Length == 0) return;

        var current = Selected?.File;
        var currentIndex = current != null ? Files.IndexOf(current) : -1;
        var definition = ScriptsCatalog.FindDefinition(Files, function, currentIndex);
        if (definition == null)
        {
            _shell.StatusText = LocalizationSource.Get("scriptDefinitionMissing") + ": " + function;
            return;
        }

        if (!_items.TryGetValue(definition.File, out var item))
        {
            item = new ScriptItemViewModel(this, definition.File);
            _items[definition.File] = item;
        }

        SelectItem(item);

        var offset = ScriptsCatalog.LineOffset(item.Text, definition.Line);
        var lineText = (item.Text ?? "").Split('\n')[definition.Line];
        var column = lineText.IndexOf(function, StringComparison.Ordinal);
        if (column >= 0) offset += column;

        RaiseCaret(item, definition.Line, offset, function);
        _shell.StatusText = LocalizationSource.Get("scriptDefinition") + ": " +
            function + " · " + item.Name + ":" + (definition.Line + 1).ToString(CultureInfo.CurrentCulture);
    }

    private void RaiseCaret(ScriptItemViewModel item, int line, int offset, string name) =>
        CaretRequested?.Invoke(this, new ScriptJumpEventArgs(item, line, offset, name));

    // --- файлы (open/scan/download) ---

    /// <summary>Открыть .lua/.script с диска и добавить в проект (`openScriptFiles`).</summary>
    [RelayCommand]
    private void OpenFile()
    {
        var path = _dialogs.PickOpenFile(LocalizationSource.Get("scriptOpen"),
            "Lua (*.lua;*.script)|*.lua;*.script|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!AddFile(path!))
        {
            _shell.StatusText = LocalizationSource.Get("scriptAlreadyAdded") + ": " + Path.GetFileName(path);
            return;
        }

        RefreshList();
        Selected = _items[Files[^1]];
        _shell.StatusText = LocalizationSource.Get("scriptOpened") + ": " + Path.GetFileName(path);
        _shell.OnSectionEdited();
    }

    /// <summary>Сканировать выбранные файлы (`scanScripts` через множественный выбор).</summary>
    [RelayCommand]
    private void ScanFiles()
    {
        var paths = _dialogs.PickOpenFiles(LocalizationSource.Get("scriptScan"),
            "Lua (*.lua;*.script)|*.lua;*.script|*.*|*.*", _shell.SavesDirectory);
        if (paths.Count == 0) return;

        var added = 0;
        foreach (var path in paths) if (AddFile(path)) added++;

        RefreshList();
        if (added > 0) Selected = _items[Files[^1]];
        _shell.StatusText = LocalizationSource.Get("scriptScanned") + ": " +
            added.ToString(CultureInfo.CurrentCulture) + " / " +
            paths.Count.ToString(CultureInfo.CurrentCulture);
        if (added > 0) _shell.OnSectionEdited();
    }

    /// <summary>Сохранить текущий скрипт на диск (`downloadScript`).</summary>
    [RelayCommand]
    private void Download()
    {
        if (Selected == null) return;

        var path = _dialogs.PickSaveFile(LocalizationSource.Get("scriptDownload"), Selected.Name,
            "Lua (*.lua;*.script)|*.lua;*.script|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            File.WriteAllBytes(path!, GameEncoding.Encode(Selected.Text, _project.Settings.ExportEncoding));
            _shell.StatusText = LocalizationSource.Get("scriptSaved") + ": " + path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _shell.StatusText = LocalizationSource.Get("scriptSaveFailed") + ": " + e.Message;
        }
    }

    private bool AddFile(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var normalized = Paths.Normalize(fullPath);
            if (Files.Any(file =>
                    string.Equals(Paths.Normalize(file.Path), normalized, StringComparison.OrdinalIgnoreCase)))
                return false;

            var text = File.ReadAllText(fullPath);
            var file = new ScriptFile
            {
                Name = Path.GetFileName(fullPath),
                Path = fullPath,
                Text = text,
                Original = text
            };
            Files.Add(file);
            _items[file] = new ScriptItemViewModel(this, file);
            LastOpenedFile = fullPath;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _shell.StatusText = LocalizationSource.Get("scriptOpenFailed") + ": " + e.Message;
            return false;
        }
    }
}

