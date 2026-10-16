using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Graph;
using XRayForge.Core.History;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;
using XRayForge.Core.Xml;
using XRayForge.Localization;
using XRayForge.Services;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «Диалоги» (этап 6): состояние и обработчики редактора графа — порт `DialoguesTab.js`
/// и обработчиков `App.js` (`addNode`, `createDialogue`, `duplicateNode`, `deleteNode`,
/// `autoLayout`, `centerView`, `undoGraph`/`redoGraph`, поиск по графу, `dialogueStats`).
/// Шаг 6.3 добавляет обработчики `dragRef` из App.js: перенос узла и группы, панорамирование
/// холста, рамка выделения (`Shift`+drag) и масштаб колесом с Ctrl.
/// Шаг 6.4 — связи: клик по портам карточки (`portClick` + `connectFrom` с отменой по Esc) и
/// удаление связи кликом по её широкой hit-зоне (`deleteEdge`, `strokeWidth 14`).
/// Правки выполняют чистые функции <see cref="GraphEditing"/>; VM держит коллекции карточек и
/// связей, синхронизирует их с <c>Project.Nodes/Edges</c> (активный редактируемый граф,
/// импортированные диалоги лежат в <c>Project.Dialogues</c>) и отмечает проект изменённым.
/// Шаг 6.7 — прогон диалога (`playthrough` + `GraphPlaythrough`) и экспорт холста в SVG
/// (`graphToSvg` + `download`), шаг 6.8 — импорт диалогов из файла `game_dialogs*.xml`
/// (`DialogueImporter.ImportFile`).
/// </summary>
public sealed partial class DialogueGraphViewModel : ObservableObject
{
    /// <summary>Лимит истории отмены (`createHistory(50)` в App.js).</summary>
    public const int HistoryLimit = 50;

    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly History<GraphSnapshot> _history = new(HistoryLimit);
    private readonly Dictionary<string, GraphNodeViewModel> _index = new(StringComparer.Ordinal);

    private Project _project = ProjectMigration.Empty();
    private HistoryState<GraphSnapshot> _hist = HistoryState<GraphSnapshot>.Empty;
    private string? _selectedId;

    public DialogueGraphViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;

        RefreshPalette();
        foreach (var snippet in GraphPalette.Snippets) MetadataSnippets.Add(new MetadataSnippetViewModel(snippet));
        foreach (var key in NodeTypes.MetaKeys) MetadataFields.Add(new MetadataFieldViewModel(this, key));
    }

    // --- коллекции графа ---

    /// <summary>Карточки узлов активного графа (`project.nodes`).</summary>
    public ObservableCollection<GraphNodeViewModel> Nodes { get; } = new();

    /// <summary>Связи активного графа (`project.edges`).</summary>
    public ObservableCollection<GraphEdgeViewModel> Edges { get; } = new();

    // --- палитра узлов (шаг 6.5) ---

    /// <summary>Фильтр палитры (`paletteFilter`): поиск по типу, подписи и описанию ноды.</summary>
    [ObservableProperty]
    private string _paletteFilter = "";

    /// <summary>Кнопки палитры (`filteredTypes`): служебные типы скрыты, порядок как в `NODE_TYPES`.</summary>
    public ObservableCollection<PaletteItemViewModel> PaletteItems { get; } = new();

    partial void OnPaletteFilterChanged(string value) => RefreshPalette();

    /// <summary>`filteredTypes`: пересобрать список типов палитры по фильтру.</summary>
    private void RefreshPalette()
    {
        PaletteItems.Clear();
        foreach (var item in GraphPalette.Items(PaletteFilter)) PaletteItems.Add(new PaletteItemViewModel(item));
    }

    /// <summary>`addNode(k)` кнопки палитры: узел выбранного типа, сразу выделенный (`select` в JS).</summary>
    [RelayCommand]
    private void PaletteAdd(string? type) => AddNode(string.IsNullOrEmpty(type) ? NodeTypes.Phrase : type!);

    // --- список загруженных диалогов (шаг 6.5) ---

    /// <summary>Диалоги проекта, разложенные по группам (`project.dialogues` + `dialogueGroup`).</summary>
    public ObservableCollection<DialogueBucketViewModel> DialogueBuckets { get; } = new();

    /// <summary>Число загруженных диалогов для заголовка панели.</summary>
    public int DialogueCount => _project.Dialogues.Count;

    /// <summary>`Object.keys(groups).sort().map(...)`: пересобрать группы списка диалогов.</summary>
    private void RefreshDialogueList()
    {
        DialogueBuckets.Clear();
        foreach (var bucket in DialogueGroups.Grouped(_project.Dialogues))
            DialogueBuckets.Add(new DialogueBucketViewModel(bucket));

        OnPropertyChanged(nameof(DialogueCount));
    }

    // --- состояние холста и панели ---

    /// <summary>Поиск по графу (`graphSearch`): совпавшие узлы подсвечиваются, прочие гаснут.</summary>
    [ObservableProperty]
    private string _search = "";

    /// <summary>«Цвет по роли» (`colorByRole`): шапки реплик красятся по роли NPC/ГГ.</summary>
    [ObservableProperty]
    private bool _colorByRole;

    /// <summary>Масштаб холста (`zoom`, 0.5…2).</summary>
    [ObservableProperty]
    private double _zoom = 1;

    /// <summary>Сдвиг содержимого по X (`pan.x`).</summary>
    [ObservableProperty]
    private double _panX;

    /// <summary>Сдвиг содержимого по Y (`pan.y`).</summary>
    [ObservableProperty]
    private double _panY;

    /// <summary>Строка статистики графа (`dialogueStats` и покрытие text_id).</summary>
    [ObservableProperty]
    private string _statsText = "";

    /// <summary>История графа: есть шаг назад (`graphHist.pos > 0`).</summary>
    [ObservableProperty]
    private bool _canUndo;

    /// <summary>История графа: есть шаг вперёд (`pos &lt; hist.length − 1`).</summary>
    [ObservableProperty]
    private bool _canRedo;

    /// <summary>Ширина холста в пикселях (обновляет представление) — для «В центр» и мини-карты.</summary>
    public double ViewportWidth
    {
        get => _viewportWidth;
        set
        {
            if (_viewportWidth.Equals(value)) return;
            _viewportWidth = value;
            RefreshMiniMap();
        }
    }

    /// <summary>Высота холста в пикселях.</summary>
    public double ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            if (_viewportHeight.Equals(value)) return;
            _viewportHeight = value;
            RefreshMiniMap();
        }
    }

    private double _viewportWidth = 800;
    private double _viewportHeight = 600;

    /// <summary>В графе есть узлы (иначе показывается empty state холста).</summary>
    public bool HasNodes => Nodes.Count > 0;

    /// <summary>Есть выделение (`selected`) — управляет затуханием несвязанных узлов и связей.</summary>
    public bool HasSelection => _selectedId != null;

    /// <summary>Выделенный узел: поиск по id, поэтому ссылка переживает перезагрузку графа.</summary>
    public GraphNodeViewModel? SelectedNode =>
        _selectedId != null && _index.TryGetValue(_selectedId, out var node) ? node : null;

    /// <summary>Подпись масштаба (`Math.round(zoom * 100) + '%'`).</summary>
    public string ZoomText =>
        Math.Round(Zoom * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.CurrentCulture) + "%";

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomText));
        RefreshMiniMap();
    }

    /// <summary>Сдвиг холста по X изменился — рамка видимой области мини-карты едет за ним.</summary>
    partial void OnPanXChanged(double value) => RefreshMiniMap();

    /// <summary>Сдвиг холста по Y изменился — рамка видимой области мини-карты едет за ним.</summary>
    partial void OnPanYChanged(double value) => RefreshMiniMap();

    partial void OnSearchChanged(string value) => ApplySearch();

    partial void OnColorByRoleChanged(bool value)
    {
        foreach (var node in Nodes) node.UseRoleColor = value;
    }

    // --- сворачивание панелей и полноэкранный холст (шаг 6.6) ---

    /// <summary>Палитра свёрнута в узкую полосу (`paletteCollapsed`).</summary>
    [ObservableProperty]
    private bool _paletteCollapsed;

    /// <summary>Панель свойств свёрнута в узкую полосу (`propsCollapsed`).</summary>
    [ObservableProperty]
    private bool _propsCollapsed;

    /// <summary>Полноэкранный холст (`canvasFull`): обе боковые панели уходят в узкие полосы.</summary>
    [ObservableProperty]
    private bool _canvasFull;

    /// <summary>Левая панель показана целиком (`!paletteCollapsed &amp;&amp; !canvasFull`).</summary>
    public bool ShowsPalettePanel => !PaletteCollapsed && !CanvasFull;

    /// <summary>Вместо левой панели — узкая полоса с кнопкой разворота.</summary>
    public bool ShowsPaletteRail => !ShowsPalettePanel;

    /// <summary>Панель свойств показана целиком (`!propsCollapsed &amp;&amp; !canvasFull`).</summary>
    public bool ShowsPropsPanel => !PropsCollapsed && !CanvasFull;

    /// <summary>Вместо панели свойств — узкая полоса с кнопкой разворота.</summary>
    public bool ShowsPropsRail => !ShowsPropsPanel;

    /// <summary>Глиф кнопки полноэкранного холста (`canvasFull ? '🗗' : '⛶'` в JS).</summary>
    public string CanvasFullGlyph => CanvasFull ? "🗗" : "⛶";

    partial void OnPaletteCollapsedChanged(bool value) => NotifyPanelState();

    partial void OnPropsCollapsedChanged(bool value) => NotifyPanelState();

    partial void OnCanvasFullChanged(bool value) => NotifyPanelState();

    /// <summary>Состояние панелей изменилось: пересчитать видимость панелей и полос.</summary>
    private void NotifyPanelState()
    {
        OnPropertyChanged(nameof(ShowsPalettePanel));
        OnPropertyChanged(nameof(ShowsPaletteRail));
        OnPropertyChanged(nameof(ShowsPropsPanel));
        OnPropertyChanged(nameof(ShowsPropsRail));
        OnPropertyChanged(nameof(CanvasFullGlyph));
    }

    // --- мини-карта (шаг 6.6) ---

    /// <summary>Мини-карта видна, пока в графе есть узлы (`if (!nodes.length) return null`).</summary>
    [ObservableProperty]
    private bool _showsMiniMap;

    /// <summary>Рамка видимой области на мини-карте: `Canvas.Left`.</summary>
    [ObservableProperty]
    private double _miniMapViewportX;

    /// <summary>Рамка видимой области на мини-карте: `Canvas.Top`.</summary>
    [ObservableProperty]
    private double _miniMapViewportY;

    /// <summary>Ширина рамки видимой области (`холст / zoom × scale`).</summary>
    [ObservableProperty]
    private double _miniMapViewportWidth;

    /// <summary>Высота рамки видимой области.</summary>
    [ObservableProperty]
    private double _miniMapViewportHeight;

    /// <summary>
    /// Пересчёт мини-карты (<see cref="GraphMiniMap.Build"/>): границы содержимого, прямоугольники
    /// узлов и рамка видимой области. Вызывается при правке графа, выделении, сдвиге, масштабе
    /// и смене размера холста.
    /// </summary>
    private void RefreshMiniMap()
    {
        var layout = GraphMiniMap.Build(_project.Nodes, Zoom, PanX, PanY, ViewportWidth, ViewportHeight);
        ShowsMiniMap = layout != null;
        if (layout == null) return;

        MiniMapViewportX = layout.Viewport.X;
        MiniMapViewportY = layout.Viewport.Y;
        MiniMapViewportWidth = layout.Viewport.Width;
        MiniMapViewportHeight = layout.Viewport.Height;

        foreach (var item in layout.Nodes)
            if (_index.TryGetValue(item.Id, out var card)) card.SetMiniMapRect(item.Rect);
    }

    /// <summary>
    /// Клик по мини-карте (`onClick` её `svg`): точка панели переводится в координаты графа,
    /// и холст центрируется на ней.
    /// </summary>
    public void MiniMapClick(double pointX, double pointY)
    {
        var layout = GraphMiniMap.Build(_project.Nodes, Zoom, PanX, PanY, ViewportWidth, ViewportHeight);
        if (layout == null) return;

        var (graphX, graphY) = GraphMiniMap.ToGraph(layout, pointX, pointY);
        var (x, y) = GraphMiniMap.CenterPan(graphX, graphY, Zoom, ViewportWidth, ViewportHeight);
        PanX = x;
        PanY = y;
    }

    // --- прогон диалога (шаг 6.7, `playthrough` в App.js) ---

    /// <summary>Состояние прогона: текущий узел и пройденный путь (`playthrough` в JS).</summary>
    private PlaythroughState? _playthrough;

    /// <summary>Прогон идёт (`playthrough` непустой): панель прогона на холсте и кнопка-переключатель.</summary>
    [ObservableProperty]
    private bool _isPlaythrough;

    /// <summary>Текущий узел прогона («Тип · id» или «—»).</summary>
    [ObservableProperty]
    private string _playthroughNode = "";

    /// <summary>Текст текущей реплики (`nodeLabel`, до 160 символов).</summary>
    [ObservableProperty]
    private string _playthroughText = "";

    /// <summary>Заголовок списка вариантов: «Следующие реплики» / «Диалог завершён» / «Тупик».</summary>
    [ObservableProperty]
    private string _playthroughStatus = "";

    /// <summary>Число пройденных нод (`playthrough.path.length`).</summary>
    [ObservableProperty]
    private int _playthroughPathCount;

    /// <summary>Варианты перехода из текущей ноды (`choices`).</summary>
    public ObservableCollection<PlaythroughChoiceViewModel> PlaythroughChoices { get; } = new();

    /// <summary>Подпись кнопки прогона (`playthrough ? '■ Завершить прогон' : '▶ Прогон диалога'`).</summary>
    public string PlaythroughButtonLabel =>
        LocalizationSource.Get(IsPlaythrough ? "graphPlaythroughStop" : "graphPlaythroughPlay");

    partial void OnIsPlaythroughChanged(bool value) => OnPropertyChanged(nameof(PlaythroughButtonLabel));

    /// <summary>Кнопка «Прогон диалога»: без активного прогона запускает его, с активным — завершает.</summary>
    [RelayCommand]
    private void TogglePlaythrough()
    {
        if (IsPlaythrough) ExitPlaythrough();
        else StartPlaythrough();
    }

    /// <summary>
    /// `startPlaythrough`: старт с первой START-ноды (иначе с первого узла), выделение стартовой
    /// ноды и открытие панели прогона.
    /// </summary>
    private void StartPlaythrough()
    {
        var state = GraphPlaythrough.Start(_project.Nodes);
        if (state == null)
        {
            _shell.OnGraphEdited(LocalizationSource.Get("graphPlaythroughNoStart"));
            return;
        }

        _playthrough = state;
        IsPlaythrough = true;
        Select(state.Current);
        RefreshPlaythrough();
    }

    /// <summary>`exitPlaythrough`: закрыть панель прогона и забыть путь (выделение не сбрасывается).</summary>
    [RelayCommand]
    private void ExitPlaythrough()
    {
        _playthrough = null;
        IsPlaythrough = false;
        PlaythroughNode = "";
        PlaythroughText = "";
        PlaythroughStatus = "";
        PlaythroughPathCount = 0;
        PlaythroughChoices.Clear();
    }

    /// <summary>`stepPlaythrough(nextId)`: переход по выбранной связи с выделением нового узла.</summary>
    [RelayCommand]
    private void StepPlaythrough(string? id)
    {
        if (_playthrough == null || string.IsNullOrEmpty(id)) return;

        _playthrough = GraphPlaythrough.Step(_playthrough, id!);
        Select(id);
        RefreshPlaythrough();
    }

    /// <summary>
    /// Пересборка панели прогона (`playthrough &amp;&amp; …` из DialoguesTab.js): текущая реплика,
    /// варианты перехода и число пройденных нод. Вызывается на каждом шаге и после правок графа.
    /// </summary>
    private void RefreshPlaythrough()
    {
        if (_playthrough == null) return;

        var languages = _project.Languages;
        var showText = _shell.Settings.ShowNodeText;
        var current = GraphPlaythrough.Current(_project.Nodes, _playthrough.Current);
        var choices = GraphPlaythrough.Choices(_project.Nodes, _project.Edges, _playthrough.Current);

        PlaythroughNode = GraphPlaythrough.CurrentLabel(current);
        PlaythroughText = GraphPlaythrough.CurrentText(current, languages, showText);
        PlaythroughStatus = LocalizationSource.Get(GraphPlaythrough.StatusKey(choices, current));
        PlaythroughPathCount = _playthrough.Path.Count;

        PlaythroughChoices.Clear();
        foreach (var node in choices)
            PlaythroughChoices.Add(new PlaythroughChoiceViewModel(
                node.Id, GraphPlaythrough.ChoiceLabel(node, languages, showText)));
    }

    // --- экспорт холста в SVG (шаг 6.7, `graphToSvg` + `download`) ---

    /// <summary>
    /// `graphToSvg` + `download`: разметка текущего графа сохраняется в `.svg` (UTF-8 без BOM —
    /// в JS `download(..., bom = false)`); пустой граф — только статус «Нет нод для экспорта».
    /// </summary>
    [RelayCommand]
    private void ExportSvg()
    {
        var svg = GraphSvg.GraphToSvg(_project.Nodes, _project.Edges);
        if (svg.Length == 0)
        {
            _shell.OnGraphEdited(LocalizationSource.Get("graphNoNodesToExport"));
            return;
        }

        var name = string.IsNullOrWhiteSpace(_project.DialogueId) ? _project.ProjectName : _project.DialogueId;
        if (string.IsNullOrWhiteSpace(name)) name = "dialogue";

        var path = _dialogs.PickSaveFile(LocalizationSource.Get("graphExportSvg"), name + ".svg",
            "SVG (*.svg)|*.svg|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllText(path, svg, new UTF8Encoding(false));
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

        _shell.OnGraphEdited(LocalizationSource.Get("graphExportedSvg"), path);
    }

    // --- импорт диалогов из game_dialogs (шаг 6.8) ---

    /// <summary>
    /// Импорт файла `game_dialogs*.xml` в список диалогов проекта: файл читается в кодировке
    /// импорта проекта, диалоги нормализуются (`DialogueImporter.ImportFile`), уже загруженные
    /// id пропускаются. В JS диалоги попадают в проект только сканированием gamedata.
    /// </summary>
    [RelayCommand]
    private void ImportGameDialogs()
    {
        var path = _dialogs.PickOpenFile(LocalizationSource.Get("graphImportGameDialogs"),
            "XML (*.xml)|*.xml|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrEmpty(path)) return;

        string text;
        try
        {
            text = GameEncoding.Decode(File.ReadAllBytes(path), _project.Settings.ImportEncoding);
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

        DialogueImportResult result;
        try
        {
            result = DialogueImporter.ImportFile(text, DialogueIds(), path, Path.GetFileName(path));
        }
        catch (InvalidOperationException e)
        {
            // Некорректный XML: `XmlParsers.ParseDialogueXml` сообщает об этом исключением.
            _shell.OnGraphEdited(LocalizationSource.Get("graphImportFailed"), e.Message);
            return;
        }

        if (result.Imported.Count == 0)
        {
            _shell.OnGraphEdited(LocalizationSource.Get("graphImportNone"), Path.GetFileName(path));
            return;
        }

        _project.Dialogues.AddRange(result.Imported);
        RefreshDialogueList();

        var detail = result.Imported.Count.ToString(CultureInfo.CurrentCulture);
        if (result.Skipped.Count > 0)
            detail += " · " + LocalizationSource.Get("graphImportSkipped") + " " +
                result.Skipped.Count.ToString(CultureInfo.CurrentCulture);
        _shell.OnGraphEdited(LocalizationSource.Get("graphImportDone"), detail);
    }

    /// <summary>Id диалогов проекта (свой id, иначе имя) — для отсева уже загруженных при импорте.</summary>
    private IEnumerable<string> DialogueIds() =>
        _project.Dialogues.Select(d => d.Id.Length > 0 ? d.Id : d.Name);

    // --- проект и синхронизация ---

    /// <summary>
    /// Принять проект (`openProject`/`createProject`/`importProject`/демо-граф): пересобрать граф
    /// из `project.nodes/edges`, сбросить выделение, поиск, сдвиг холста и историю отмены.
    /// </summary>
    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _hist = HistoryState<GraphSnapshot>.Empty;
        _selectedId = null;
        ExitPlaythrough();
        ResetInteraction();
        Search = "";
        PanX = 0;
        PanY = 0;
        Zoom = 1;
        RefreshDialogueList();
        Reload();
        PushHistory();
    }

    /// <summary>`patch` и отрисовка: синхронизировать карточки узлов и связи с проектом.</summary>
    public void Reload()
    {
        var languages = _project.Languages;
        var showText = _shell.Settings.ShowNodeText;

        _index.Clear();
        Nodes.Clear();
        foreach (var node in _project.Nodes)
        {
            var card = new GraphNodeViewModel(node, languages, showText) { UseRoleColor = ColorByRole };
            _index[node.Id] = card;
            Nodes.Add(card);
        }

        Edges.Clear();
        foreach (var edge in _project.Edges) Edges.Add(new GraphEdgeViewModel(edge));

        // Выделение переживает перезагрузку только если узел ещё существует (`selected` — id в JS).
        if (_selectedId != null && !_index.ContainsKey(_selectedId)) _selectedId = null;
        // Групповое выделение тоже чистится от исчезнувших узлов (удаление, undo, смена диалога).
        _multiSelect.RemoveWhere(id => !_index.ContainsKey(id));
        // Взведённый порт чистится, если его узел исчез (удаление узла, undo, смена диалога).
        if (_connectFrom != null && !_index.ContainsKey(_connectFrom.Id)) CancelConnect();

        RefreshPaths();
        RefreshStats();
        ApplySearch();
        RefreshProps();
        // Панель прогона живёт по тем же данным: правки графа, undo и раскладка её пересобирают.
        RefreshPlaythrough();

        OnPropertyChanged(nameof(SelectedNode));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNodes));
    }

    /// <summary>Пересобрать пути связей (после перезагрузки, раскладки или перетаскивания).</summary>
    public void RefreshPaths()
    {
        foreach (var edge in Edges) edge.RefreshPath(_index);
        ApplyVisuals();
    }

    /// <summary>`dialogueStats` и покрытие text_id в строку панели.</summary>
    private void RefreshStats()
    {
        var stats = GraphLayout.Stats(_project.Nodes, _project.Edges);
        var coverage = TextIdTools.Coverage(_project.Dialogues, _project.Languages);
        StatsText = string.Join(" · ",
            LocalizationSource.Get("graphStatNodes") + " " + stats.Nodes.ToString(CultureInfo.CurrentCulture),
            LocalizationSource.Get("graphStatEdges") + " " + stats.Edges.ToString(CultureInfo.CurrentCulture),
            LocalizationSource.Get("graphStatPhrases") + " " + stats.Phrases.ToString(CultureInfo.CurrentCulture),
            LocalizationSource.Get("graphStatBranches") + " " + stats.Branches.ToString(CultureInfo.CurrentCulture),
            LocalizationSource.Get("graphStatDepth") + " " + stats.MaxDepth.ToString(CultureInfo.CurrentCulture),
            LocalizationSource.Get("graphStatAvgText") + " " + stats.AvgText.ToString(CultureInfo.CurrentCulture),
            LocalizationSource.Get("graphStatCoverage") + " " + coverage.Pct.ToString(CultureInfo.CurrentCulture) + "%");
    }

    // --- выделение и визуальные флаги ---

    /// <summary>`setSelected(id)`: выделить узел и пересчитать затухание и подсветку связей.</summary>
    public void Select(string? id)
    {
        var next = id != null && _index.ContainsKey(id) ? id : null;
        if (next == _selectedId) return;

        _selectedId = next;
        OnPropertyChanged(nameof(SelectedNode));
        OnPropertyChanged(nameof(HasSelection));
        ApplyVisuals();
        RefreshProps();
    }

    /// <summary>`setSelected(null)`: клик по пустому холсту снимает выделение.</summary>
    public void Deselect() => Select(null);

    /// <summary>
    /// Пересчёт затухания и подсветки: карточки вне связи с выделенным узлом гасятся до 0.25,
    /// связи выделенного узла подсвечиваются (в JS — зелёным), остальные связи — до 0.2.
    /// </summary>
    private void ApplyVisuals()
    {
        var selected = _selectedId;
        var connected = new HashSet<string>(StringComparer.Ordinal);
        if (selected != null)
        {
            connected.Add(selected);
            foreach (var edge in _project.Edges)
            {
                if (edge.Source == selected) connected.Add(edge.Target);
                else if (edge.Target == selected) connected.Add(edge.Source);
            }
        }

        var query = (Search ?? "").Trim();
        foreach (var node in Nodes)
        {
            node.IsSelected = selected != null && node.Id == selected;
            node.IsMultiSelected = _multiSelect.Contains(node.Id);
            node.Opacity = query.Length > 0 && !node.IsSearchHit
                ? 0.12
                : selected == null || connected.Contains(node.Id) ? 1 : 0.25;
        }

        foreach (var edge in Edges)
        {
            var highlight = selected != null && (edge.Edge.Source == selected || edge.Edge.Target == selected);
            edge.IsHighlighted = highlight;
            edge.Opacity = selected == null || highlight ? 1 : 0.2;
        }

        // Границы и подсветка узлов мини-карты зависят от тех же данных (позиции, выделение).
        RefreshMiniMap();
    }

    /// <summary>`graphSearch`: отметить совпавшие узлы и пересчитать визуальные флаги.</summary>
    private void ApplySearch()
    {
        var query = (Search ?? "").Trim();
        foreach (var node in Nodes)
            node.IsSearchHit = query.Length > 0 && GraphEditing.Matches(node.Title, node.Id, query);

        ApplyVisuals();
    }

    // --- перетаскивание, панорамирование и рамка выделения (шаг 6.3) ---

    /// <summary>Вид активного перетаскивания (`dragRef.current.kind` в App.js).</summary>
    private GraphDragMode _drag = GraphDragMode.None;
    private string? _dragId;
    private double _dragFromX;
    private double _dragFromY;

    /// <summary>Позиция узла или сдвиг холста на момент нажатия (`d.x`/`d.y` в App.js).</summary>
    private (double X, double Y) _dragOrigin;

    /// <summary>
    /// Позиции узлов группы на момент нажатия (`origins` в App.js): каждое перемещение считается
    /// от них, поэтому ошибка округления не накапливается.
    /// </summary>
    private readonly Dictionary<string, (double X, double Y)> _dragOrigins = new(StringComparer.Ordinal);

    /// <summary>Рамка выделения в координатах графа (`marquee` в App.js).</summary>
    private (double X1, double Y1, double X2, double Y2) _marquee;

    /// <summary>Групповое выделение (`multiSelect`): ids узлов с синей рамкой.</summary>
    private readonly HashSet<string> _multiSelect = new(StringComparer.Ordinal);

    /// <summary>Идёт перетаскивание (узел, группа, панорама или рамка) — холст ведёт mouse-move.</summary>
    public bool IsDragging => _drag != GraphDragMode.None;

    /// <summary>Рамка выделения показана (`marquee` в JS).</summary>
    [ObservableProperty]
    private bool _hasMarquee;

    /// <summary>Левый край рамки выделения в координатах графа.</summary>
    [ObservableProperty]
    private double _marqueeX;

    /// <summary>Верхний край рамки выделения в координатах графа.</summary>
    [ObservableProperty]
    private double _marqueeY;

    /// <summary>Ширина рамки выделения (не меньше 1px, как в JS).</summary>
    [ObservableProperty]
    private double _marqueeWidth;

    /// <summary>Высота рамки выделения.</summary>
    [ObservableProperty]
    private double _marqueeHeight;

    /// <summary>
    /// `onMouseDown` холста (`DialoguesTab.js` + обработчики App.js): карточка — Shift-переключение
    /// группового выделения, перенос группы или одного узла; пустое место — рамка выделения (Shift)
    /// или панорама. Вид перетаскивания выбирает <see cref="GraphEditing.ResolveDragMode"/>.
    /// </summary>
    /// <param name="node">Карточка узла под курсором или <c>null</c> для пустого холста.</param>
    /// <param name="shift">Удерживается Shift.</param>
    /// <param name="screenX">Координата нажатия по X в системе холста (без pan/zoom, `clientX` в JS).</param>
    /// <param name="screenY">Координата нажатия по Y в системе холста (без pan/zoom).</param>
    /// <param name="graphX">Координата нажатия по X в системе графа (нужна рамке выделения).</param>
    /// <param name="graphY">Координата нажатия по Y в системе графа (нужна рамке выделения).</param>
    public void BeginCanvasInteraction(
        GraphNodeViewModel? node, bool shift, double screenX, double screenY, double graphX, double graphY)
    {
        var inGroup = node != null && _multiSelect.Contains(node.Id);
        var mode = GraphEditing.ResolveDragMode(node != null, shift, inGroup, _multiSelect.Count);

        switch (mode)
        {
            case GraphDragMode.None:
                // Shift+клик по карточке: только переключение группового выделения (`setMultiSelect`).
                ToggleMultiSelect(node!.Id);
                Select(node.Id);
                _drag = GraphDragMode.None;
                _dragId = null;
                return;

            case GraphDragMode.Group:
                Select(node!.Id);
                _dragOrigins.Clear();
                foreach (var id in _multiSelect)
                    if (_index.TryGetValue(id, out var card)) _dragOrigins[id] = (card.X, card.Y);

                StartScreenDrag(GraphDragMode.Group, null, screenX, screenY);
                return;

            case GraphDragMode.Node:
                Select(node!.Id);
                // В JS группа сбрасывается только при `length > 1`: одиночный узел остаётся в группе.
                if (_multiSelect.Count > 1) SetMultiSelect(null);

                _dragOrigin = (node.X, node.Y);
                StartScreenDrag(GraphDragMode.Node, node.Id, screenX, screenY);
                return;

            case GraphDragMode.Marquee:
                BeginMarquee(graphX, graphY);
                return;

            default:
                BeginPan(screenX, screenY);
                return;
        }
    }

    /// <summary>
    /// Начало панорамирования: нажатие по пустому холсту или средняя кнопка мыши.
    /// Групповое выделение сбрасывается (`setMultiSelect([])` в JS), а выделение узла — нет.
    /// </summary>
    public void BeginPan(double screenX, double screenY)
    {
        SetMultiSelect(null);
        _dragOrigin = (PanX, PanY);
        StartScreenDrag(GraphDragMode.Pan, null, screenX, screenY);
    }

    /// <summary>
    /// Начало рамки выделения. Координаты приходят в системе графа: слой холста в WPF уже учитывает
    /// pan/zoom — аналог `(clientX − rect.left − pan) / zoom` в JS.
    /// </summary>
    private void BeginMarquee(double graphX, double graphY)
    {
        _drag = GraphDragMode.Marquee;
        _dragId = null;
        _marquee = (graphX, graphY, graphX, graphY);
        UpdateMarqueeVisual();
    }

    /// <summary>`mousemove`: рамка выделения тянется за курсором (координаты графа).</summary>
    private void MarqueeTo(double graphX, double graphY)
    {
        if (_drag != GraphDragMode.Marquee) return;

        _marquee = (_marquee.X1, _marquee.Y1, graphX, graphY);
        UpdateMarqueeVisual();
    }

    /// <summary>
    /// `mousemove`: перетаскивание узла/группы, панорамирование или растягивание рамки. Экранные
    /// координаты приходят в системе холста без pan/zoom (аналог `clientX/clientY`), поэтому сдвиг
    /// узлов делится на зум, а панорама идёт 1:1 за курсором — как в App.js; рамке выделения нужны
    /// координаты слоя графа.
    /// </summary>
    public void DragTo(double screenX, double screenY, double graphX, double graphY)
    {
        switch (_drag)
        {
            case GraphDragMode.Node:
                if (_dragId == null || !_index.TryGetValue(_dragId, out var node)) return;

                var (dx, dy) = GraphEditing.DragDelta(_dragFromX, _dragFromY, screenX, screenY, Zoom);
                node.X = _dragOrigin.X + dx;
                node.Y = _dragOrigin.Y + dy;
                RefreshPaths();
                break;

            case GraphDragMode.Group:
                var (gx, gy) = GraphEditing.DragDelta(_dragFromX, _dragFromY, screenX, screenY, Zoom);
                foreach (var (id, origin) in _dragOrigins)
                    if (_index.TryGetValue(id, out var card))
                    {
                        card.X = origin.X + gx;
                        card.Y = origin.Y + gy;
                    }

                RefreshPaths();
                break;

            case GraphDragMode.Pan:
                var (px, py) = GraphEditing.PanShift(
                    _dragOrigin.X, _dragOrigin.Y, _dragFromX, _dragFromY, screenX, screenY);
                PanX = px;
                PanY = py;
                break;

            case GraphDragMode.Marquee:
                MarqueeTo(graphX, graphY);
                break;
        }
    }

    /// <summary>
    /// `mouseup`: завершение перетаскивания. Рамка выделения отбирает узлы, пересекающие её
    /// (карточка 210×100 — <see cref="GraphEditing.MarqueeIds"/>), и они получают групповое
    /// выделение. Перенос узлов отмечает проект изменённым (`setDirty(true)` в App.js).
    /// </summary>
    public void EndDrag()
    {
        var movedNodes = _drag is GraphDragMode.Node or GraphDragMode.Group;

        if (_drag == GraphDragMode.Marquee)
            SetMultiSelect(GraphEditing.MarqueeIds(_project.Nodes, _marquee.X1, _marquee.Y1, _marquee.X2, _marquee.Y2));

        _drag = GraphDragMode.None;
        _dragId = null;
        _dragOrigins.Clear();
        HasMarquee = false;

        if (movedNodes) _shell.OnGraphEdited();
    }

    /// <summary>Ctrl+колесо: шаг масштаба ±0.1 за щелчок (`onZoom` в App.js), границы 0.5…2.</summary>
    public void ZoomByWheel(int notches) =>
        Zoom = GraphEditing.ZoomBy(Zoom, GraphEditing.ZoomStep * notches);

    /// <summary>Начать перетаскивание в экранной системе координат (`dragRef.current` в JS).</summary>
    private void StartScreenDrag(GraphDragMode kind, string? id, double screenX, double screenY)
    {
        _drag = kind;
        _dragId = id;
        _dragFromX = screenX;
        _dragFromY = screenY;
    }

    /// <summary>`setMultiSelect`: пересобрать групповое выделение и перекрасить рамки карточек.</summary>
    private void SetMultiSelect(IEnumerable<string>? ids)
    {
        _multiSelect.Clear();
        foreach (var id in ids ?? Enumerable.Empty<string>())
            if (!string.IsNullOrEmpty(id) && _index.ContainsKey(id)) _multiSelect.Add(id!);

        ApplyVisuals();
    }

    /// <summary>`setMultiSelect(prev => …)`: переключить узел в групповом выделении (Shift+клик).</summary>
    private void ToggleMultiSelect(string id)
    {
        if (!_multiSelect.Remove(id)) _multiSelect.Add(id);
        ApplyVisuals();
    }

    /// <summary>`setMarquee`: пересчитать рамку выделения для отрисовки в слое холста.</summary>
    private void UpdateMarqueeVisual()
    {
        var (x, y, width, height) = GraphEditing.MarqueeRect(_marquee.X1, _marquee.Y1, _marquee.X2, _marquee.Y2);
        MarqueeX = x;
        MarqueeY = y;
        MarqueeWidth = width;
        MarqueeHeight = height;
        HasMarquee = true;
    }

    /// <summary>
    /// Сброс взаимодействия холста: групповое выделение, перетаскивание, рамка выделения
    /// и взведённый порт связи.
    /// </summary>
    private void ResetInteraction()
    {
        _multiSelect.Clear();
        _drag = GraphDragMode.None;
        _dragId = null;
        _dragOrigins.Clear();
        HasMarquee = false;
        CancelConnect();
    }

    // --- связи: порты и удаление кликом (шаг 6.4) ---

    /// <summary>Взведённый порт: первый клик по порту ждёт второго (`connectFrom` в App.js).</summary>
    private GraphPortRef? _connectFrom;

    /// <summary>Порт взведён: на холсте показывается подсказка «Выберите второй порт. Esc — отмена».</summary>
    [ObservableProperty]
    private bool _isConnecting;

    /// <summary>
    /// `portClick`: клик по порту карточки. Первый взводит порт, второй создаёт связь
    /// `source → target` (дубликаты и самопетли отбрасывает <see cref="GraphEditing.Connect"/>),
    /// повторный клик по тому же или однотипному порту снимает взвод
    /// (<see cref="GraphEditing.ResolvePortClick"/>).
    /// </summary>
    /// <param name="node">Карточка, по порту которой щёлкнули.</param>
    /// <param name="kind">Вид порта: вход (`in`) или выход (`out`).</param>
    public void PortClick(GraphNodeViewModel? node, GraphPortKind kind)
    {
        if (node == null) return;

        var click = GraphEditing.ResolvePortClick(_connectFrom, node.Id, kind);
        if (click.CreatesEdge)
        {
            var edge = GraphEditing.Connect(_project.Edges, click.Source, click.Target);
            if (edge != null)
            {
                PushHistory();
                _project.Edges.Add(edge);
                Reload();
                _shell.OnGraphEdited(LocalizationSource.Get("graphEdgeCreated"));
            }
        }

        // Взвод снимается в любом случае, кроме ожидания второго порта (`setConnectFrom` в JS).
        _connectFrom = click.Pending;
        IsConnecting = _connectFrom != null;
    }

    /// <summary>`Escape`: отмена взведённого порта (`setConnectFrom(null)` в App.js).</summary>
    public void CancelConnect()
    {
        if (_connectFrom == null) return;

        _connectFrom = null;
        IsConnecting = false;
    }

    /// <summary>
    /// `deleteEdge`: связь удаляется кликом по её широкой hit-зоне (`strokeWidth 14` в JS) —
    /// по видимой линии связи клик не проходит (слой рёбер `pointer-events-none`).
    /// </summary>
    [RelayCommand]
    private void DeleteEdge(GraphEdgeViewModel? edge)
    {
        if (edge == null) return;

        PushHistory();
        _project.Edges = GraphEditing.WithoutEdge(_project.Edges, edge.Id);
        Reload();
        _shell.OnGraphEdited(LocalizationSource.Get("graphEdgeDeleted"));
    }

    /// <summary>
    /// `loadDialogueIntoGraph`: загрузить диалог проекта в холст — узлы нормализуются (`safeNode`),
    /// связи копируются, id диалога берётся из `dlg.id` (иначе остаётся текущий). История правок
    /// не трогается — как и в JS, где `patch` не пишет снапшот.
    /// </summary>
    [RelayCommand]
    private void LoadDialogue(DialogueGraph? dialogue)
    {
        if (dialogue == null) return;

        var nodes = new List<DialogueNode>();
        for (var i = 0; i < dialogue.Nodes.Count; i++) nodes.Add(GraphData.SafeNode(dialogue.Nodes[i], i));

        _project.Nodes = nodes;
        _project.Edges = dialogue.Edges.Select(GraphEditing.Clone).ToList();
        _project.DialogueId = !string.IsNullOrEmpty(dialogue.Id)
            ? dialogue.Id
            : !string.IsNullOrEmpty(_project.DialogueId) ? _project.DialogueId : _project.ProjectName;

        Deselect();
        Reload();
        _shell.OnGraphEdited(LocalizationSource.Get("graphDialogueLoaded"), dialogue.Id);
    }

    // --- свойства реплики: действия и условия (шаг 6.5) ---
    /// <summary>Идёт загрузка полей из модели — сеттеры не пишут правки обратно (`patchNode`).</summary>
    private bool _loadingProps;

    /// <summary>Заголовок панели свойств: `id · подпись типа` (`selectedNode.id + ' · ' + label`).</summary>
    [ObservableProperty]
    private string _propsHeader = "";

    /// <summary>Показать поля реплики (`selectedNode.type === 'PHRASE'`).</summary>
    [ObservableProperty]
    private bool _showsPhraseFields;

    /// <summary>Показать поле `expression` (типы CONDITION/QUEST_CHECK/ITEM_CHECK).</summary>
    [ObservableProperty]
    private bool _showsExpressionFields;

    /// <summary>Показать поля действия (`selectedNode.type === 'ACTION'`).</summary>
    [ObservableProperty]
    private bool _showsActionFields;

    /// <summary>Роль реплики (`unknown`/`npc`/`player`); правка ставит `roleSource: 'manual'`.</summary>
    [ObservableProperty]
    private string _phraseRole = "unknown";

    /// <summary>Id реплики (`data.phraseId`).</summary>
    [ObservableProperty]
    private string _phraseId = "";

    /// <summary>Ссылка на строку таблицы переводов (`data.text_id`); правка включает `use_id`.</summary>
    [ObservableProperty]
    private string _phraseTextId = "";

    /// <summary>Fallback-текст реплики (`data.text`).</summary>
    [ObservableProperty]
    private string _phraseText = "";

    /// <summary>Озвучка (`data.voice_id`), необязательна.</summary>
    [ObservableProperty]
    private string _phraseVoiceId = "";

    /// <summary>Выражение условия (`data.expression`) для узлов-проверок.</summary>
    [ObservableProperty]
    private string _phraseExpression = "";

    /// <summary>Тип действия (`data.action_type`): `give_info`, `give_item`, `take_item`, `script_call`.</summary>
    [ObservableProperty]
    private string _phraseActionType = "";

    /// <summary>Параметры действия (`data.params`).</summary>
    [ObservableProperty]
    private string _phraseParams = "";

    /// <summary>Найденный перевод `text_id`: «текст» · локаль (`resolveTextId`).</summary>
    [ObservableProperty]
    private string _resolvedPhraseText = "";

    /// <summary>Активная локаль панели переводов (`activePhraseLocale`).</summary>
    [ObservableProperty]
    private string _activePhraseLocale = "";

    /// <summary>Перевод `text_id` в активной локали (поле `setTranslation`).</summary>
    [ObservableProperty]
    private string _phraseTranslation = "";

    /// <summary>Поля «Действия и условия» — по одному на ключ `META_KEYS`.</summary>
    public ObservableCollection<MetadataFieldViewModel> MetadataFields { get; } = new();

    /// <summary>Кнопки-сниппеты блока «Действия и условия» (`META_SNIPPETS`).</summary>
    public ObservableCollection<MetadataSnippetViewModel> MetadataSnippets { get; } = new();

    /// <summary>Таблицы переводов проекта (ряд локалей `langs.map`).</summary>
    public ObservableCollection<PhraseLanguageViewModel> PhraseLanguages { get; } = new();

    /// <summary>В проекте есть таблицы переводов (`langs.length` в JS).</summary>
    public bool HasPhraseLanguages => PhraseLanguages.Count > 0;

    /// <summary>Перевод показан: есть и таблицы переводов, и `text_id` (`textId && active` в JS).</summary>
    public bool ShowsTranslation => PhraseLanguages.Count > 0 && PhraseTextId.Length > 0;

    /// <summary>Кнопка «Создать и привязать text_id» — пока ссылки нет (`!textId`).</summary>
    public bool ShowsBindTextId => PhraseTextId.Length == 0;

    /// <summary>Кнопка «Создать таблицу языка» — ссылка есть, таблиц ещё нет.</summary>
    public bool ShowsCreateLanguage => PhraseTextId.Length > 0 && PhraseLanguages.Count == 0;

    /// <summary>Подпись поля перевода: «Перевод · rus».</summary>
    public string TranslationLabel => LocalizationSource.Get("graphTranslation") + " · " + ActivePhraseLocale;

    /// <summary>Подсказка поля перевода: «Перевод для st_1».</summary>
    public string TranslationPlaceholder => LocalizationSource.Get("graphTranslationPlaceholder") + " " + PhraseTextId;

    partial void OnActivePhraseLocaleChanged(string value)
    {
        if (_loadingProps) return;

        PhraseTranslation = TextIdTools.TranslationOf(_project.Languages, value, PhraseTextId);
        OnPropertyChanged(nameof(TranslationLabel));
    }

    partial void OnPhraseRoleChanged(string value)
    {
        if (_loadingProps) return;
        PatchData(data =>
        {
            data.Role = value;
            data.RoleSource = "manual";
        });
    }

    partial void OnPhraseIdChanged(string value) => PatchIfReady(data => data.PhraseId = value);

    partial void OnPhraseTextIdChanged(string value)
    {
        if (_loadingProps) return;
        PatchData(data =>
        {
            data.TextId = value;
            data.UseId = true;
        }, refreshText: true);
        OnPropertyChanged(nameof(ShowsTranslation));
        OnPropertyChanged(nameof(ShowsBindTextId));
        OnPropertyChanged(nameof(ShowsCreateLanguage));
    }

    partial void OnPhraseTextChanged(string value) => PatchIfReady(data => data.Text = value);

    partial void OnPhraseVoiceIdChanged(string value) => PatchIfReady(data => data.VoiceId = value);

    partial void OnPhraseExpressionChanged(string value) => PatchIfReady(data => data.Expression = value);

    partial void OnPhraseActionTypeChanged(string value) => PatchIfReady(data => data.ActionType = value);

    partial void OnPhraseParamsChanged(string value) => PatchIfReady(data => data.Params = value);

    partial void OnPhraseTranslationChanged(string value)
    {
        if (_loadingProps) return;
        if (SelectedNode?.Node.Data == null || PhraseTextId.Length == 0) return;

        _project.Languages = TextIdTools.SetTranslation(_project.Languages, ActivePhraseLocale, PhraseTextId, value);
        // Как и в JS, предпросмотр перевода под полем `text_id` пересчитывается сразу после правки.
        RefreshResolvedText();
        _shell.OnGraphEdited();
    }

    /// <summary>`patchNode(id, data)`: правка одного поля реплики с обновлением бейджей карточки.</summary>
    private void PatchData(Action<NodeData> mutate, bool refreshText = false)
    {
        var card = SelectedNode;
        if (card?.Node.Data == null) return;

        mutate(card.Node.Data);
        card.RefreshLabels(_project.Languages, _shell.Settings.ShowNodeText);
        if (refreshText) RefreshResolvedText();
        _shell.OnGraphEdited();
    }

    /// <summary>Правка поля реплики, когда панель уже загружена (сеттеры из модели пропускаются).</summary>
    private void PatchIfReady(Action<NodeData> mutate)
    {
        if (_loadingProps) return;
        PatchData(mutate);
    }

    /// <summary>Показать значения выбранного узла во всех полях панели (`selectedNode` в JS).</summary>
    private void RefreshProps()
    {
        _loadingProps = true;

        var card = SelectedNode;
        var data = card?.Node.Data;

        PropsHeader = card == null ? "" : card.Id + " · " + card.TypeInfo.Label;
        ShowsPhraseFields = card?.Type == NodeTypes.Phrase;
        ShowsExpressionFields = card?.Type is NodeTypes.Condition or NodeTypes.QuestCheck or NodeTypes.ItemCheck;
        ShowsActionFields = card?.Type == NodeTypes.Action;

        PhraseRole = data == null || data.Role.Length == 0 ? "unknown" : data.Role;
        PhraseId = data?.PhraseId ?? "";
        PhraseTextId = data?.TextId ?? "";
        PhraseText = data?.Text ?? "";
        PhraseVoiceId = data?.VoiceId ?? "";
        PhraseExpression = data?.Expression ?? "";
        PhraseActionType = data?.ActionType ?? "";
        PhraseParams = data?.Params ?? "";

        foreach (var field in MetadataFields) field.Load(GraphPalette.Text(data?.Metadata, field.Key));

        RefreshPhraseLanguages();
        RefreshResolvedText();
        OnPropertyChanged(nameof(ShowsTranslation));
        OnPropertyChanged(nameof(ShowsBindTextId));
        OnPropertyChanged(nameof(ShowsCreateLanguage));
        OnPropertyChanged(nameof(TranslationLabel));
        OnPropertyChanged(nameof(TranslationPlaceholder));

        _loadingProps = false;
    }

    /// <summary>Пересобрать список локалей панели и подтянуть перевод активной (`langs.map` в JS).</summary>
    private void RefreshPhraseLanguages()
    {
        PhraseLanguages.Clear();
        foreach (var language in _project.Languages) PhraseLanguages.Add(new PhraseLanguageViewModel(language));

        var active = TextIdTools.ActiveLanguage(_project.Languages, ActivePhraseLocale);
        ActivePhraseLocale = active?.Locale ?? "";
        PhraseTranslation = TextIdTools.TranslationOf(_project.Languages, ActivePhraseLocale, PhraseTextId);
        OnPropertyChanged(nameof(HasPhraseLanguages));
    }

    /// <summary>`resolveTextId`: строка ««перевод» · локаль» под полем `text_id`.</summary>
    private void RefreshResolvedText()
    {
        var resolved = PhraseTextId.Length == 0
            ? null
            : TextIdTools.ResolveTextId(_project.Languages, PhraseTextId);

        ResolvedPhraseText = resolved == null ? "" : "«" + resolved.Text + "» · " + resolved.Locale;
    }

    /// <summary>
    /// Сниппет «Действий и условий»: значение дописывается в конец списка ключа
    /// (`m[key] = (m[key] || []).concat([value])`).
    /// </summary>
    [RelayCommand]
    private void AddMetadataSnippet(MetaSnippet? snippet)
    {
        if (snippet == null || SelectedNode == null) return;

        PatchData(data => data.Metadata = GraphPalette.WithSnippet(data.Metadata, snippet));
        MetadataFields.FirstOrDefault(field => field.Key == snippet.Key)
            ?.Load(GraphPalette.Text(SelectedNode.Node.Data?.Metadata, snippet.Key));
    }

    /// <summary>
    /// `patchNode(id, { metadata })` из многострочного поля: строки поля становятся значениями ключа
    /// (`e.target.value.split(/\r?\n/)`). Вызывается полями <see cref="MetadataFields"/>.
    /// </summary>
    public void SetMetadataText(string key, string text)
    {
        if (_loadingProps) return;
        PatchData(data => data.Metadata = GraphPalette.WithText(data.Metadata, key, text));
    }

    /// <summary>Кнопка «⤵»: `text_id` из `phraseId`/id реплики (`suggestTextId`) с включением ссылки.</summary>
    [RelayCommand]
    private void SuggestTextId()
    {
        var card = SelectedNode;
        if (card == null) return;

        var dialogueId = string.IsNullOrEmpty(_project.DialogueId) ? _project.ProjectName : _project.DialogueId;
        ApplyTextId(GraphData.SuggestTextId(card.Node, dialogueId));
    }

    /// <summary>`bindId`: привязать `text_id` к таблицам переводов (таблицы создаются при необходимости).</summary>
    [RelayCommand]
    private void BindTextId()
    {
        var card = SelectedNode;
        if (card?.Node.Data == null) return;

        var textId = PhraseTextId.Length > 0 ? PhraseTextId : TextIdTools.GeneratedTextId(card.Id);
        _project.Languages = TextIdTools.BindTextId(_project.Languages, textId, card.Node.Data.Text);
        ApplyTextId(textId);
        _shell.OnGraphEdited();
    }

    /// <summary>Записать `text_id` реплики и включить ссылку (`use_id: true` в JS).</summary>
    private void ApplyTextId(string textId)
    {
        if (PhraseTextId != textId)
        {
            // Сеттер поля сам проставит `use_id` и обновит предпросмотр перевода.
            PhraseTextId = textId;
            RefreshPhraseLanguages();
            return;
        }

        PatchData(data =>
        {
            data.TextId = textId;
            data.UseId = true;
        }, refreshText: true);
        RefreshPhraseLanguages();
    }

    // --- история отмены и повтора ---

    /// <summary>`pushGraphHist`: снапшот графа перед правкой (лимит `HistoryLimit`).</summary>
    private void PushHistory()
    {
        _hist = _history.Push(_hist, GraphEditing.Capture(_project.Nodes, _project.Edges));
        UpdateHistoryFlags();
    }

    /// <summary>Доступность кнопок «Отменить»/«Повторить» по позиции курсора истории.</summary>
    private void UpdateHistoryFlags()
    {
        CanUndo = _hist.Pos > 0;
        CanRedo = _hist.Pos >= 0 && _hist.Pos < _hist.Hist.Length - 1;
    }

    /// <summary>`patch({ nodes, edges })` из снапшота истории.</summary>
    private void ApplySnapshot(GraphSnapshot snapshot)
    {
        if (snapshot == null) return;

        _project.Nodes = snapshot.Nodes;
        _project.Edges = snapshot.Edges;
        Reload();
    }

    // --- правки графа (порт обработчиков App.js) ---

    /// <summary>`createDialogue`: новый диалог из стартового узла, id чистится как в JS.</summary>
    [RelayCommand]
    private void CreateDialogue()
    {
        var initial = string.IsNullOrWhiteSpace(_project.DialogueId) ? _project.ProjectName : _project.DialogueId;
        var input = _dialogs.Prompt(LocalizationSource.Get("createDialogue"),
            LocalizationSource.Get("dialogueIdPrompt"), initial);
        if (string.IsNullOrWhiteSpace(input)) return;

        var clean = GraphEditing.SanitizeDialogueId(input);
        PushHistory();
        _project.DialogueId = clean;
        _project.Nodes = new List<DialogueNode> { GraphEditing.CreateStartNode() };
        _project.Edges = new List<DialogueEdge>();
        Reload();
        Deselect();
        _shell.OnGraphEdited(LocalizationSource.Get("dialogueCreated"),
            clean + " · " + LocalizationSource.Get("dialogueCreatedHint"));
    }

    /// <summary>`addNode(type)`: узел с каскадной позицией, сразу выделенный (`select` в JS).</summary>
    private void AddNode(string type)
    {
        var node = GraphEditing.CreateNode(type, _project.Nodes.Count, _project.DialogueId, _project.Nodes);
        PushHistory();
        _project.Nodes.Add(node);
        Reload();
        Select(node.Id);
        _shell.OnGraphEdited(LocalizationSource.Get("graphNodeAdded"), NodeTypes.InfoFor(type).Label);
    }

    /// <summary>Горячая клавиша `addStart`: узел начала диалога.</summary>
    [RelayCommand]
    private void AddStart() => AddNode(NodeTypes.Start);

    /// <summary>Горячая клавиша `addPhrase`: реплика.</summary>
    [RelayCommand]
    private void AddPhrase() => AddNode(NodeTypes.Phrase);

    /// <summary>Горячая клавиша `addRandom`: случайный выбор ветки.</summary>
    [RelayCommand]
    private void AddRandom() => AddNode(NodeTypes.Random);

    /// <summary>Горячая клавиша `addEnd`: завершение диалога.</summary>
    [RelayCommand]
    private void AddEnd() => AddNode(NodeTypes.End);

    /// <summary>`duplicateNode`: копия выделенного узла со сдвигом 40/40.</summary>
    [RelayCommand]
    private void DuplicateSelected()
    {
        var source = SelectedNode;
        if (source == null)
        {
            _shell.OnGraphEdited(LocalizationSource.Get("graphNothingSelected"));
            return;
        }

        var copy = GraphEditing.Duplicate(source.Node);
        PushHistory();
        _project.Nodes.Add(copy);
        Reload();
        Select(copy.Id);
        _shell.OnGraphEdited(LocalizationSource.Get("graphNodeDuplicated"));
    }

    /// <summary>`deleteNode` (`deleteSelected`): узел вместе с его связями.</summary>
    [RelayCommand]
    private void DeleteSelected()
    {
        var node = SelectedNode;
        if (node == null)
        {
            _shell.OnGraphEdited(LocalizationSource.Get("graphNothingSelected"));
            return;
        }

        PushHistory();
        var (nodes, edges) = GraphEditing.WithoutNode(_project.Nodes, _project.Edges, node.Id);
        _project.Nodes = nodes;
        _project.Edges = edges;
        Reload();
        Deselect();
        _shell.OnGraphEdited(LocalizationSource.Get("graphNodeDeleted"), node.Title);
    }

    /// <summary>`autoLayout`: раскладка узлов по уровням от START.</summary>
    [RelayCommand]
    private void AutoLayout()
    {
        if (!HasNodes) return;

        PushHistory();
        GraphLayout.LayoutDialogue(_project.Nodes, _project.Edges);
        foreach (var node in Nodes) node.NotifyPositionChanged();
        RefreshPaths();
        _shell.OnGraphEdited(LocalizationSource.Get("graphLaidOut"));
    }

    /// <summary>`undoGraph`: шаг назад по снапшотам графа.</summary>
    [RelayCommand]
    private void Undo()
    {
        if (!CanUndo) return;

        _hist = _history.Move(_hist, -1, ApplySnapshot);
        UpdateHistoryFlags();
        _shell.OnGraphEdited(LocalizationSource.Get("graphUndo"));
    }

    /// <summary>`redoGraph`: шаг вперёд по снапшотам графа.</summary>
    [RelayCommand]
    private void Redo()
    {
        if (!CanRedo) return;

        _hist = _history.Move(_hist, 1, ApplySnapshot);
        UpdateHistoryFlags();
        _shell.OnGraphEdited(LocalizationSource.Get("graphRedo"));
    }

    /// <summary>Шаг «−» масштаба (`Math.max(0.5, z − 0.1)`); Ctrl+колесо — <see cref="ZoomByWheel"/>.</summary>
    [RelayCommand]
    private void ZoomOut() => Zoom = GraphEditing.ZoomBy(Zoom, -GraphEditing.ZoomStep);

    /// <summary>Шаг «+» масштаба (`Math.min(2, z + 0.1)`).</summary>
    [RelayCommand]
    private void ZoomIn() => Zoom = GraphEditing.ZoomBy(Zoom, GraphEditing.ZoomStep);

    /// <summary>`centerView`: масштаб 1 и сдвиг так, чтобы содержимое легло в центр холста.</summary>
    [RelayCommand]
    private void CenterView()
    {
        var (x, y) = GraphEditing.CenterPan(_project.Nodes, ViewportWidth, ViewportHeight);
        Zoom = 1;
        PanX = x;
        PanY = y;
    }

    // --- команды панелей (шаг 6.6) ---

    /// <summary>`setPaletteCollapsed(true)`: свернуть палитру в узкую полосу.</summary>
    [RelayCommand]
    private void CollapsePalette() => PaletteCollapsed = true;

    /// <summary>`setPaletteCollapsed(false)`: развернуть палитру.</summary>
    [RelayCommand]
    private void ExpandPalette() => PaletteCollapsed = false;

    /// <summary>`setPropsCollapsed(true)`: свернуть панель свойств.</summary>
    [RelayCommand]
    private void CollapseProps() => PropsCollapsed = true;

    /// <summary>`setPropsCollapsed(false)`: развернуть панель свойств.</summary>
    [RelayCommand]
    private void ExpandProps() => PropsCollapsed = false;

    /// <summary>`setCanvasFull(!canvasFull)`: полноэкранный холст — обе панели в узкие полосы.</summary>
    [RelayCommand]
    private void ToggleCanvasFull() => CanvasFull = !CanvasFull;

    /// <summary>`setCanvasFull(false)`: выйти из полноэкранного холста (кнопка на холсте).</summary>
    [RelayCommand]
    private void ExitCanvasFull() => CanvasFull = false;
}


