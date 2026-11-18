using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Lua;
using XRayForge.Core.Projects;
using XRayForge.Core.Scripts;
using XRayForge.Core.Suggestions;
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

    /// <summary>Индекс автодополнения Lua: API, глобалы, функции скриптов и info-порции.</summary>
    private SuggestionLibrary _suggestions = SuggestionLibrary.Empty;

    /// <summary>Текущий список подсказок для Lua-редактора (см. <see cref="Completions"/>).</summary>
    private IReadOnlyList<SuggestionEntry> _completions = Array.Empty<SuggestionEntry>();

    /// <summary>Набор файлов изменился — индекс подсказок нужно пересобрать.</summary>
    private bool _suggestionsDirty = true;

    /// <summary>Список файлов проекта; `null` для ручного JSON заменяется пустым.</summary>
    private List<ScriptFile> Files => _project.Scripts ??= new List<ScriptFile>();

    public ScriptsViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;

        Scripts = new ObservableCollection<ScriptItemViewModel>();
        OpenScripts = new ObservableCollection<ScriptItemViewModel>();
        GameScripts = new ObservableCollection<ScriptItemViewModel>();
        LocalScripts = new ObservableCollection<ScriptItemViewModel>();
        Diagnostics = new ObservableCollection<ScriptDiagnosticViewModel>();
        Edges = new ObservableCollection<ScriptEdgeViewModel>();
        Functions = new ObservableCollection<ScriptFunctionViewModel>();
    }

    // --- коллекции ---

    /// <summary>Отфильтрованный список карточек скриптов (полный набор).</summary>
    public ObservableCollection<ScriptItemViewModel> Scripts { get; }

    /// <summary>Открытые вкладки редактора (файлы, которые пользователь открыл в этой сессии).</summary>
    public ObservableCollection<ScriptItemViewModel> OpenScripts { get; }

    /// <summary>Игровые скрипты — файлы из `gamedata/scripts`.</summary>
    public ObservableCollection<ScriptItemViewModel> GameScripts { get; }

    /// <summary>Локационные скрипты — всё, что лежит вне `gamedata/scripts`.</summary>
    public ObservableCollection<ScriptItemViewModel> LocalScripts { get; }

    public ObservableCollection<ScriptDiagnosticViewModel> Diagnostics { get; }
    public ObservableCollection<ScriptEdgeViewModel> Edges { get; }
    public ObservableCollection<ScriptFunctionViewModel> Functions { get; }

    /// <summary>Справочник API движка для вставки в курсор (`LUA_API`).</summary>
    public IReadOnlyList<string> EngineApi => LuaCatalog.Api;

    /// <summary>Готовые STALKER-сниппеты (`LUA_SNIPPETS`).</summary>
    public IReadOnlyList<LuaSnippet> Snippets => LuaCatalog.Snippets;

    /// <summary>
    /// Подсказки Lua-редактора: API и глобалы движка, функции/файлы скриптов проекта,
    /// info-порции и ключевые слова. Пересобираются при смене набора файлов.
    /// </summary>
    public IReadOnlyList<SuggestionEntry> Completions
    {
        get => _completions;
        private set
        {
            _completions = value;
            OnPropertyChanged();
        }
    }

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
    public bool HasGameScripts => GameScripts.Count > 0;
    public bool HasLocalScripts => LocalScripts.Count > 0;
    public bool HasOpenScripts => OpenScripts.Count > 0;
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
        // Вкладки открытых файлов принадлежат сессии: с новым проектом они закрываются.
        OpenScripts.Clear();
        OnPropertyChanged(nameof(HasOpenScripts));
        Selected = null;

        foreach (var file in Files)
        {
            // Снапшот без `original` (старые проекты) — исходником считаем текущий текст.
            if (string.IsNullOrEmpty(file.Original) && !string.IsNullOrEmpty(file.Text)) file.Original = file.Text;
            _items[file] = new ScriptItemViewModel(this, file);
        }

        _suggestionsDirty = true;
        RefreshList();
        RefreshLocalization();
    }

    /// <summary>Пересобрать индекс подсказок Lua (API, функции скриптов, info-порции).</summary>
    private void RebuildSuggestions()
    {
        _suggestions = SuggestionLibrary.Build(_project);
        Completions = _suggestions.ForLua();
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
        GameScripts.Clear();
        LocalScripts.Clear();
        foreach (var file in filtered)
        {
            if (!_items.TryGetValue(file, out var item)) continue;

            Scripts.Add(item);
            // Список делится на игровые (`gamedata/scripts`) и локационные (всё остальное).
            if (ScriptsCatalog.IsGameScript(file)) GameScripts.Add(item);
            else LocalScripts.Add(item);
        }

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasVisibleScripts));
        OnPropertyChanged(nameof(HasGameScripts));
        OnPropertyChanged(nameof(HasLocalScripts));
        OnPropertyChanged(nameof(NoScriptsText));

        if (_suggestionsDirty)
        {
            _suggestionsDirty = false;
            RebuildSuggestions();
        }
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

    /// <summary>Выбор карточки (клик по списку или команда элемента): файл открывается во вкладке.</summary>
    public void SelectItem(ScriptItemViewModel? item)
    {
        if (item != null && !Scripts.Contains(item))
        {
            // Переход к определению/создание может вывести карточку за пределы фильтра.
            Search = "";
            RefreshList();
        }

        if (item != null && !OpenScripts.Contains(item))
        {
            OpenScripts.Add(item);
            OnPropertyChanged(nameof(HasOpenScripts));
        }

        Selected = item;
    }

    /// <summary>
    /// Закрыть вкладку открытого файла: сам файл остаётся в проекте, редактор переключается
    /// на соседнюю вкладку (как вкладки редактора).
    /// </summary>
    [RelayCommand]
    internal void CloseTab(ScriptItemViewModel? item)
    {
        item ??= Selected;
        if (item == null) return;

        var index = OpenScripts.IndexOf(item);
        if (index < 0) return;

        OpenScripts.RemoveAt(index);
        OnPropertyChanged(nameof(HasOpenScripts));
        if (!ReferenceEquals(Selected, item)) return;

        Selected = OpenScripts.Count > 0 ? OpenScripts[Math.Min(index, OpenScripts.Count - 1)] : null;
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
        // Проверка/граф считаются только для открытых вкладок: файл, выбранный в списке,
        // но не открытый, не анализируется.
        if (item == null || !OpenScripts.Contains(item))
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

        _suggestionsDirty = true;
        Search = "";
        RefreshList();
        SelectItem(item);
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
        _suggestionsDirty = true;
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
        var tabIndex = OpenScripts.IndexOf(item);
        if (tabIndex >= 0) OpenScripts.RemoveAt(tabIndex);
        OnPropertyChanged(nameof(HasOpenScripts));

        // Следующая вкладка — соседняя справа (или последняя), сам список уже без файла.
        var next = OpenScripts.Count == 0
            ? null
            : OpenScripts[Math.Clamp(tabIndex < 0 ? index : tabIndex, 0, OpenScripts.Count - 1)];
        SelectItem(next);
        _suggestionsDirty = true;
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
        SelectItem(_items[Files[^1]]);
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
        if (added > 0) SelectItem(_items[Files[^1]]);
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
            var normalized = ScriptsCatalog.PathKey(fullPath);
            if (Files.Any(file =>
                    string.Equals(ScriptsCatalog.PathKey(file.Path), normalized, StringComparison.OrdinalIgnoreCase)))
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
            _suggestionsDirty = true;
            LastOpenedFile = fullPath;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _shell.StatusText = LocalizationSource.Get("scriptOpenFailed") + ": " + e.Message;
            return false;
        }
    }

    // --- ссылки в тексте (Ctrl+ЛКМ по `#include`/`dofile`/имени файла) ---

    /// <summary>
    /// Открыть файл по ссылке под курсором (`#include "x"`, `dofile("x")`, `require "x"`,
    /// просто `x.lua`). Возвращает `true`, если ссылка распознана и обработана.
    /// </summary>
    public bool OpenFileLinkAt(int offset)
    {
        var link = ScriptLinks.At(Selected?.Text, offset);
        if (link == null) return false;

        // `level.actor()` — ссылка на функцию внутри скрипта `level`.
        if (link.IsCall) return OpenCallLink(link);

        // Вкладка проекта: ищем по полному пути, хвосту пути и имени файла.
        var item = FindByLink(link.Target);
        if (item != null)
        {
            OpenLinked(item);
            return true;
        }

        var extension = Path.GetExtension(link.Target).ToLowerInvariant();
        if (extension.Length > 0 && extension != ".lua" && extension != ".script")
        {
            // .ltx/.xml и прочее — не скрипты, открывать в редакторе нечего.
            _shell.StatusText = LocalizationSource.Get("scriptLinkNotScript") + ": " + link.Target;
            return false;
        }

        var path = ResolveLinkedPath(link.Target);
        if (path != null)
        {
            if (AddFile(path))
            {
                // Ссылка может вести в файл, скрытый фильтром поиска: сбрасываем фильтр,
                // чтобы открытая карточка была видна в списке.
                if (Search.Length > 0) Search = "";
                RefreshList();
                OpenLinked(_items[Files[^1]]);
                _shell.OnSectionEdited();
                return true;
            }

            // Файл уже в проекте, но не нашёлся по имени ссылки (например, скрыт фильтром
            // или путь карточки записан иначе) — сверяем по ключу пути.
            var existing = FindByPath(path);
            if (existing != null)
            {
                OpenLinked(existing);
                return true;
            }
        }

        _shell.StatusText = LocalizationSource.Get("scriptLinkMissing") + ": " + link.Target;
        return false;
    }

    /// <summary>
    /// Переход по вызову `скрипт.функция(` (`level.actor()`): открыть файл скрипта
    /// (`level.script`/`level.lua`) и поставить каретку на строку `function actor(`.
    /// </summary>
    private bool OpenCallLink(ScriptLink link)
    {
        var file = ScriptsCatalog.FindScript(Files, link.Script);
        var item = file != null && _items.TryGetValue(file, out var found) ? found : null;

        if (item == null)
        {
            // Скрипт ещё не в проекте — пробуем достать его из gamedata по имени.
            var path = ResolveLinkedPath(link.Script);
            if (path != null && AddFile(path))
            {
                if (Search.Length > 0) Search = "";
                RefreshList();
                item = _items[Files[^1]];
            }
        }

        if (item == null)
        {
            _shell.StatusText = LocalizationSource.Get("scriptLinkMissing") + ": " + link.Target;
            return false;
        }

        SelectItem(item);
        var line = ScriptsCatalog.DefinitionLine(item.Text, link.Member);
        if (line < 0)
        {
            RaiseCaret(item, 0, 0, "");
            _shell.StatusText = LocalizationSource.Get("scriptDefinitionMissing") + ": " +
                link.Member + " · " + item.Name;
            return true;
        }

        var offset = ScriptsCatalog.LineOffset(item.Text, line);
        var lineText = (item.Text ?? "").Split('\n')[line];
        var column = lineText.IndexOf(link.Member, StringComparison.Ordinal);
        if (column >= 0) offset += column;

        RaiseCaret(item, line, offset, link.Member);
        _shell.StatusText = LocalizationSource.Get("scriptDefinition") + ": " +
            link.Member + " · " + item.Name + ":" + (line + 1).ToString(CultureInfo.CurrentCulture);
        return true;
    }

    /// <summary>Карточка скрипта с тем же файлом на диске (сравнение развёрнутых путей).</summary>
    private ScriptItemViewModel? FindByPath(string path)
    {
        var wanted = ScriptsCatalog.ResolveFullPath(path, _project.GamedataPath) ?? path;
        wanted = wanted.Replace('\\', '/').TrimStart('/');

        foreach (var file in Files)
        {
            var full = ScriptsCatalog.ResolveFullPath(file.Path, _project.GamedataPath);
            if (full == null) continue;
            if (!full.Replace('\\', '/').TrimStart('/').Equals(wanted, StringComparison.OrdinalIgnoreCase))
                continue;
            if (_items.TryGetValue(file, out var item)) return item;
        }

        return null;
    }

    /// <summary>Открыть вкладку скрипта и поставить каретку в начало файла.</summary>
    private void OpenLinked(ScriptItemViewModel item)
    {
        SelectItem(item);
        RaiseCaret(item, 0, 0, "");
        _shell.StatusText = LocalizationSource.Get("scriptLinkOpened") + ": " + item.Name;
    }

    /// <summary>
    /// Найти карточку скрипта по тексту ссылки: полный путь, хвост пути (`scripts/x.lua`),
    /// имя файла, а для ссылок без расширения — имя без расширения (`require "x"`).
    /// Просматриваются все файлы проекта: фильтр поиска не должен ломать переход по ссылке.
    /// </summary>
    private ScriptItemViewModel? FindByLink(string? target)
    {
        var wanted = NormalizeLink(target);
        if (wanted.Length == 0) return null;

        var baseName = Path.GetFileName(wanted);
        var stem = Path.GetFileNameWithoutExtension(baseName);
        var hasExtension = Path.GetExtension(wanted).Length > 0;

        foreach (var file in Files)
        {
            var path = NormalizeLink(file.Path);
            var matched = path.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                          path.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase) ||
                          Path.GetFileName(path).Equals(baseName, StringComparison.OrdinalIgnoreCase);
            if (!matched && !hasExtension)
                matched = Path.GetFileNameWithoutExtension(path).Equals(stem, StringComparison.OrdinalIgnoreCase);

            if (matched && _items.TryGetValue(file, out var item)) return item;
        }

        return null;
    }

    /// <summary>
    /// Найти файл ссылки на диске: рядом с текущим скриптом, в `gamedata` проекта и её
    /// подпапке `scripts`; для ссылок без расширения пробуются `.lua` и `.script`.
    /// </summary>
    private string? ResolveLinkedPath(string? target)
    {
        var wanted = NormalizeLink(target);
        if (wanted.Length == 0) return null;

        var candidates = new List<string>();
        if (Path.IsPathRooted(target)) candidates.Add(target!);

        var currentDir = Path.GetDirectoryName(
            ScriptsCatalog.ResolveFullPath(Selected?.File.Path, _project.GamedataPath) ?? "");
        if (!string.IsNullOrEmpty(currentDir)) candidates.Add(Path.Combine(currentDir, wanted));

        var root = _project.GamedataPath?.TrimEnd('\\', '/');
        if (!string.IsNullOrEmpty(root))
        {
            // `gamedata/scripts/x.lua` → `x.lua` относительно самой папки gamedata.
            var relative = wanted.StartsWith("gamedata/", StringComparison.OrdinalIgnoreCase)
                ? wanted["gamedata/".Length..]
                : wanted;
            candidates.Add(Path.Combine(root, relative));
            candidates.Add(Path.Combine(root, "scripts", relative));

            var parent = Path.GetDirectoryName(root);
            if (!string.IsNullOrEmpty(parent)) candidates.Add(Path.Combine(parent, wanted));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate)) return candidate;
            if (Path.GetExtension(candidate).Length > 0) continue;

            foreach (var extension in new[] { ".lua", ".script" })
                if (File.Exists(candidate + extension)) return candidate + extension;
        }

        return null;
    }

    /// <summary>Прямые слеши без ведущего разделителя — для сравнения путей со ссылками.</summary>
    private static string NormalizeLink(string? value) =>
        (value ?? "").Replace('\\', '/').Trim().TrimStart('/');
}

