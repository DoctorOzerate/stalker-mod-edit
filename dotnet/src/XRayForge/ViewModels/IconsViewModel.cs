using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.History;
using XRayForge.Core.Imaging;
using XRayForge.Core.Ltx;
using XRayForge.Core.Projects;
using XRayForge.Localization;
using XRayForge.Services;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «Иконки» (этап 7.3, `IconsTab.js`): загрузка DDS, просмотр атласа с сеткой,
/// выделением, зумом и сравнением, лупа, пресеты, коллекции закреплённых иконок,
/// поиск дубликатов, пиксельные операции и экспорт PNG/DDS/спрайт-листа.
/// </summary>
public sealed partial class IconsViewModel : ObservableObject
{
    /// <summary>Снимок выделения для undo/redo (`iconsHistory`).</summary>
    public readonly record struct SelectionState(int X, int Y, int W, int H);

    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly History<SelectionState> _history = new(100);
    private HistoryState<SelectionState> _historyState = HistoryState<SelectionState>.Empty;

    private Project _project = ProjectMigration.Empty();
    private AtlasGrid _grid = AtlasGrid.Create(0, 0, 50, 50, 5);
    private RgbaImage? _atlasPixels;
    private RgbaImage? _clipboard;
    private BitmapSource? _atlasBitmap;
    private bool _loading;

    public IconsViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;
        Pinned = new ObservableCollection<PinnedIconViewModel>();
        Duplicates = new ObservableCollection<AtlasDuplicateCellViewModel>();
        Collections = new ObservableCollection<string>();
    }

    /// <summary>`ATLAS_PRESETS` с пустым первым пунктом «Выбрать…» (как `&lt;option value=""&gt;` в JS).</summary>
    public IReadOnlyList<AtlasPreset> PresetOptions { get; } = new[]
    {
        new AtlasPreset { Label = LocalizationSource.Get("iconChoosePreset"), Columns = 0, CellW = 0, CellH = 0 }
    }.Concat(IconGrid.AtlasPresets).ToList();

    public ObservableCollection<PinnedIconViewModel> Pinned { get; }
    public ObservableCollection<AtlasDuplicateCellViewModel> Duplicates { get; }
    public ObservableCollection<string> Collections { get; }

    // --- состояние атласа ---

    [ObservableProperty] private ImageSource? _atlasImage;
    [ObservableProperty] private ImageSource? _overlayImage;
    [ObservableProperty] private bool _hasAtlas;
    [ObservableProperty] private bool _hasOverlay;
    [ObservableProperty] private bool _hasAtlasError;
    [ObservableProperty] private string _atlasError = "";
    [ObservableProperty] private string _atlasInfo = "";
    [ObservableProperty] private string _selectionText = "";
    [ObservableProperty] private string _hoverText = "";
    [ObservableProperty] private ImageSource? _magnifierImage;
    [ObservableProperty] private double _magnifierWidth;
    [ObservableProperty] private double _magnifierHeight;

    /// <summary>Масштаб атласа (`atlasZoom`), 0.25…3 с шагом 0.25.</summary>
    [ObservableProperty] private double _zoom = 1;

    /// <summary>Прозрачность второго атласа при сравнении (`compareOpacity`).</summary>
    [ObservableProperty] private double _compareOpacity = 0.5;

    /// <summary>Число колонок из `project.atlas.columns`.</summary>
    [ObservableProperty] private double _atlasColumns = 5;

    /// <summary>Ширина ячейки (`atlas.cellSize`).</summary>
    [ObservableProperty] private double _cellWidth = 50;

    /// <summary>Высота ячейки (`atlas.cellHeight || atlas.cellSize`).</summary>
    [ObservableProperty] private double _cellHeight = 50;

    [ObservableProperty] private int _selectionX;
    [ObservableProperty] private int _selectionY;
    [ObservableProperty] private int _selectionWidth = 1;
    [ObservableProperty] private int _selectionHeight = 1;

    [ObservableProperty] private bool _hasHover;
    [ObservableProperty] private int _hoverX;
    [ObservableProperty] private int _hoverY;

    /// <summary>Выбранный пресет атласа (`ATLAS_PRESETS`).</summary>
    [ObservableProperty] private AtlasPreset? _selectedPreset;

    /// <summary>Имя активной коллекции закреплённых иконок (`pinColName`).</summary>
    [ObservableProperty] private string _collectionName = "default";

    [ObservableProperty] private bool _hasPinned;
    [ObservableProperty] private string _pinnedTitle = "";
    [ObservableProperty] private bool _hasDuplicates;
    [ObservableProperty] private string _duplicatesTitle = "";

    /// <summary>Строка состояния сетки: имя файла, размер и число ячеек.</summary>
    public string GridInfo => AtlasInfo;

    /// <summary>Целое число колонок для `AtlasCanvas`.</summary>
    public int GridColumns => Math.Max(1, (int)Math.Round(AtlasColumns));

    /// <summary>Вычисленное число рядов для `AtlasCanvas`.</summary>
    public int GridRows => Math.Max(1, _grid.Rows);

    /// <summary>Показать подсказку «загрузите DDS», когда атласа ещё нет и ошибки нет.</summary>
    public bool IsEmptyAtlas => !HasAtlas && !HasAtlasError;

    /// <summary>`N ячеек` в подписи выделения.</summary>
    public string SelectionPixels =>
        (SelectionX * _grid.CellWidth) + "," + (SelectionY * _grid.CellHeight) + " px";

    public bool CanUndo => _historyState.Pos > 0;
    public bool CanRedo => _historyState.Pos >= 0 && _historyState.Pos < _historyState.Hist.Length - 1;

    // --- подключение проекта ---

    /// <summary>Пересобрать вкладку из проекта (старт, открытие, скан gamedata).</summary>
    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _loading = true;
        AtlasColumns = _project.Atlas.Columns > 0 ? _project.Atlas.Columns : 5;
        CellWidth = _project.Atlas.CellSize > 0 ? _project.Atlas.CellSize : 50;
        CellHeight = _project.Atlas.CellHeight > 0 ? _project.Atlas.CellHeight : CellWidth;
        Zoom = 1;
        CompareOpacity = 0.5;
        SelectedPreset = PresetOptions.Count > 0 ? PresetOptions[0] : null;
        SelectionX = 0;
        SelectionY = 0;
        SelectionWidth = 1;
        SelectionHeight = 1;
        HasHover = false;
        Duplicates.Clear();
        HasDuplicates = false;
        DuplicatesTitle = "";
        _historyState = HistoryState<SelectionState>.Empty;
        _clipboard = null;
        _loading = false;

        RefreshCollections();
        ReloadAtlas();
        RefreshPinned();
        NotifyHistory();
    }

    private void RefreshCollections()
    {
        Collections.Clear();
        var names = (_project.PinCollections ?? new Dictionary<string, List<PinnedIcon>>()).Keys
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        if (names.Count == 0) names.Add("default");
        foreach (var name in names) Collections.Add(name);

        var current = string.IsNullOrWhiteSpace(CollectionName) ? "" : CollectionName;
        CollectionName = names.Contains(current) ? current : names[0];
        OnPropertyChanged(nameof(HasCollections));
    }

    public bool HasCollections => Collections.Count > 0;

    /// <summary>Первый файл текстур (`textures[0]` в JS) — атлас, второй — сравнение.</summary>
    private void ReloadAtlas()
    {
        _atlasBitmap = null;
        _atlasPixels = null;
        _grid = AtlasGrid.Create(0, 0, CellWidth, CellHeight, AtlasColumns);
        AtlasImage = null;
        OverlayImage = null;
        HasOverlay = false;
        HasAtlas = false;
        HasAtlasError = false;
        AtlasError = "";
        AtlasInfo = "";
        UpdateSelectionText();
        UpdateMagnifier();

        var textures = _project.Textures ?? new List<ProjectTexture>();
        var texture = textures.FirstOrDefault();
        if (texture == null)
        {
            OnPropertyChanged(nameof(GridInfo));
            return;
        }

        if (!string.IsNullOrWhiteSpace(texture.Error))
        {
            SetAtlasError(texture.Error);
            return;
        }

        var bitmap = TexturePreview.Load(texture.DataUrl);
        var pixels = RgbaImageCodec.FromBitmap(bitmap);
        if (bitmap == null || pixels == null || pixels.IsEmpty)
        {
            SetAtlasError(LocalizationSource.Get("iconDecodeFailed"));
            return;
        }

        _atlasBitmap = bitmap;
        _atlasPixels = pixels;
        AtlasImage = bitmap;
        HasAtlas = true;
        HasAtlasError = false;
        AutoFitDefaultColumns(pixels.Width);

        var compare = textures.Skip(1).FirstOrDefault();
        if (compare != null && string.IsNullOrWhiteSpace(compare.Error))
        {
            OverlayImage = TexturePreview.Load(compare.DataUrl);
            HasOverlay = OverlayImage != null;
        }

        RefreshLayout(texture.Name);
        LoadOverlay();
    }

    private void LoadOverlay()
    {
        var compare = (_project.Textures ?? new List<ProjectTexture>()).Skip(1).FirstOrDefault();
        OverlayImage = compare != null && string.IsNullOrWhiteSpace(compare.Error)
            ? TexturePreview.Load(compare.DataUrl)
            : null;
        HasOverlay = OverlayImage != null;
    }

    private void SetAtlasError(string message)
    {
        HasAtlas = false;
        HasAtlasError = true;
        HasOverlay = false;
        AtlasError = message;
        AtlasInfo = "";
        OnPropertyChanged(nameof(GridInfo));
    }

    /// <summary>Пересчитать сетку, строку состояния и ограничить выделение.</summary>
    private void RefreshLayout(string? name = null)
    {
        if (_atlasPixels == null || _atlasPixels.IsEmpty)
        {
            OnPropertyChanged(nameof(GridInfo));
            return;
        }

        _grid = AtlasGrid.Create(_atlasPixels.Width, _atlasPixels.Height, CellWidth, CellHeight, AtlasColumns);
        var selection = _grid.ClampSelection(new AtlasRegion(
            SelectionX, SelectionY,
            SelectionWidth <= 0 ? 1 : SelectionWidth,
            SelectionHeight <= 0 ? 1 : SelectionHeight));
        ApplySelection(selection.X, selection.Y, selection.W, selection.H);
        UpdateSelectionText();

        var fileName = name ?? (_project.Textures?.FirstOrDefault()?.Name ?? "");
        AtlasInfo = fileName + " · " + _atlasPixels.Width + "×" + _atlasPixels.Height +
            " · " + LocalizationSource.Get("iconGrid") + " " + _grid.Columns + "×" + _grid.Rows +
            " " + LocalizationSource.Get("iconCells");
        OnPropertyChanged(nameof(GridInfo));
        OnPropertyChanged(nameof(GridColumns));
        OnPropertyChanged(nameof(GridRows));
        OnPropertyChanged(nameof(SelectionPixels));
        UpdateMagnifier();
    }

    /// <summary>
    /// Для дефолтной сетки 50×50 (5 колонок) подобрать колонки по ширине атласа:
    /// у `ui_icon_equipment.dds` (1024×2048) это 20 ячеек, иначе выделение было бы
    /// ограничено левыми пятью.
    /// </summary>
    private void AutoFitDefaultColumns(int imageWidth)
    {
        if (Math.Abs(AtlasColumns - 5) > 0.001 || Math.Abs(CellWidth - 50) > 0.001) return;
        var actual = Math.Max(1, imageWidth / Math.Max(1, (int)Math.Round(CellWidth)));
        if (actual <= AtlasColumns) return;

        _loading = true;
        AtlasColumns = actual;
        _project.Atlas.Columns = actual;
        _loading = false;
    }

    private void ApplySelection(int x, int y, int w, int h)
    {
        SelectionX = x;
        SelectionY = y;
        SelectionWidth = Math.Max(1, w);
        SelectionHeight = Math.Max(1, h);
    }

    partial void OnSelectionXChanged(int value) => UpdateSelectionText();
    partial void OnSelectionYChanged(int value) => UpdateSelectionText();
    partial void OnSelectionWidthChanged(int value) => UpdateSelectionText();
    partial void OnSelectionHeightChanged(int value) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        SelectionText = SelectionX + "×" + SelectionY + " · " +
            SelectionWidth + "×" + SelectionHeight + " " + LocalizationSource.Get("iconCells") +
            " (" + (SelectionX * _grid.CellWidth) + "," + (SelectionY * _grid.CellHeight) + " px)";
        OnPropertyChanged(nameof(SelectionPixels));
    }

    partial void OnHasAtlasChanged(bool value) => OnPropertyChanged(nameof(IsEmptyAtlas));
    partial void OnHasAtlasErrorChanged(bool value) => OnPropertyChanged(nameof(IsEmptyAtlas));

    partial void OnHoverXChanged(int value) => UpdateMagnifier();
    partial void OnHoverYChanged(int value) => UpdateMagnifier();
    partial void OnHasHoverChanged(bool value) => UpdateMagnifier();

    private void UpdateMagnifier()
    {
        if (!HasHover || _atlasBitmap == null)
        {
            MagnifierImage = null;
            MagnifierWidth = 0;
            MagnifierHeight = 0;
            HoverText = "";
            return;
        }

        var cw = Math.Max(1, _grid.CellWidth);
        var ch = Math.Max(1, _grid.CellHeight);
        MagnifierImage = TexturePreview.Crop(
            _atlasBitmap, HoverX * cw, HoverY * ch, cw, ch,
            _atlasBitmap.PixelWidth, _atlasBitmap.PixelHeight);
        MagnifierWidth = cw * 4;
        MagnifierHeight = ch * 4;
        HoverText = LocalizationSource.Get("iconMagnifier") + " · " + HoverX + "×" + HoverY;
    }

    partial void OnZoomChanged(double value)
    {
        var clamped = Math.Round(Math.Clamp(value, 0.25, 3.0), 2);
        if (Math.Abs(clamped - value) > 0.0001) Zoom = clamped;
    }

    partial void OnAtlasColumnsChanged(double value)
    {
        if (_loading) return;
        if (value < 1)
        {
            _loading = true;
            AtlasColumns = 1;
            _loading = false;
            value = 1;
        }

        _project.Atlas.Columns = value;
        RefreshLayout();
        _shell.OnSectionEdited();
    }

    partial void OnCellWidthChanged(double value)
    {
        if (_loading) return;
        if (value < 1)
        {
            _loading = true;
            CellWidth = 1;
            _loading = false;
            value = 1;
        }

        _project.Atlas.CellSize = value;
        if (_project.Atlas.CellHeight <= 0) _project.Atlas.CellHeight = value;
        RefreshLayout();
        _shell.OnSectionEdited();
    }

    partial void OnCellHeightChanged(double value)
    {
        if (_loading) return;
        if (value < 1)
        {
            _loading = true;
            CellHeight = 1;
            _loading = false;
            value = 1;
        }

        _project.Atlas.CellHeight = value;
        RefreshLayout();
        _shell.OnSectionEdited();
    }

    partial void OnSelectedPresetChanged(AtlasPreset? value)
    {
        if (_loading || value == null || value.Columns <= 0) return;

        _loading = true;
        AtlasColumns = Math.Max(1, value.Columns);
        CellWidth = Math.Max(1, value.CellW);
        CellHeight = Math.Max(1, value.CellH);
        _project.Atlas.Columns = AtlasColumns;
        _project.Atlas.CellSize = CellWidth;
        _project.Atlas.CellHeight = CellHeight;
        _loading = false;
        SelectedPreset = PresetOptions.Count > 0 ? PresetOptions[0] : null;
        RefreshLayout();
        _shell.OnSectionEdited(LocalizationSource.Get("iconPresetApplied") + ": " + value.Label);
    }

    partial void OnCollectionNameChanged(string value) => RefreshPinned();

    private void RefreshPinned()
    {
        Pinned.Clear();
        if (_project.PinCollections != null &&
            _project.PinCollections.TryGetValue(CollectionName ?? "", out var icons))
        {
            foreach (var icon in icons) Pinned.Add(new PinnedIconViewModel(this, icon));
        }

        HasPinned = Pinned.Count > 0;
        PinnedTitle = LocalizationSource.Get("iconPinned") + " · " + CollectionName +
            " (" + Pinned.Count.ToString(CultureInfo.CurrentCulture) + ")";
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // --- выделение, панорама и зум ---

    /// <summary>Выделение drag'ом на холсте (`onSelectRegion`).</summary>
    public void SetSelectionFromCanvas(int x, int y, int width, int height)
    {
        var selection = _grid.ClampSelection(new AtlasRegion(x, y, width, height));
        ApplySelection(selection.X, selection.Y, selection.W, selection.H);
    }

    /// <summary>Конец drag'а: снимок выделения в историю (`pushHist`).</summary>
    public void CommitSelection()
    {
        var snapshot = new SelectionState(SelectionX, SelectionY, SelectionWidth, SelectionHeight);
        if (_historyState.Pos >= 0 && _historyState.Pos < _historyState.Hist.Length &&
            _historyState.Hist[_historyState.Pos] == snapshot)
            return;
        _historyState = _history.Push(_historyState, snapshot);
        NotifyHistory();
    }

    /// <summary>`hoverCell`: подсветка ячейки и лупа.</summary>
    public void SetHoverFromCanvas(bool hasCell, int x, int y)
    {
        HasHover = hasCell;
        HoverX = Math.Max(0, x);
        HoverY = Math.Max(0, y);
    }

    /// <summary>`onZoom` (Ctrl+колесо): шаг 0.25 в диапазоне 0.25…3.</summary>
    public void ZoomByWheel(int delta)
    {
        if (delta == 0) return;
        Zoom = Math.Round(Math.Clamp(Zoom + (delta > 0 ? 0.25 : -0.25), 0.25, 3.0), 2);
    }

    public void SelectCell(int x, int y)
    {
        var selection = _grid.ClampSelection(new AtlasRegion(x, y, 1, 1));
        ApplySelection(selection.X, selection.Y, selection.W, selection.H);
    }

    public void SelectPinned(PinnedIconViewModel item)
    {
        var icon = item.Model;
        SetSelectionFromCanvas((int)Math.Round(icon.X), (int)Math.Round(icon.Y),
            Math.Max(1, (int)Math.Round(icon.W)), Math.Max(1, (int)Math.Round(icon.H)));
    }

    [RelayCommand]
    private void Undo()
    {
        _historyState = _history.Move(_historyState, -1, state => ApplySelection(state.X, state.Y, state.W, state.H));
        NotifyHistory();
    }

    [RelayCommand]
    private void Redo()
    {
        _historyState = _history.Move(_historyState, 1, state => ApplySelection(state.X, state.Y, state.W, state.H));
        NotifyHistory();
    }

    // --- загрузка DDS и сравнение ---

    private string? InitialTextureFolder =>
        Directory.Exists(_project.GamedataPath) ? _project.GamedataPath : _shell.SavesDirectory;

    [RelayCommand]
    private void LoadDds()
    {
        var path = _dialogs.PickOpenFile(
            LocalizationSource.Get("iconLoadDds"), "DDS (*.dds)|*.dds|*.*|*.*", InitialTextureFolder);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var image = DecodeDds(path);
            ApplyAtlasImage(image, Path.GetFileName(path), replaceAll: true,
                LocalizationSource.Get("iconLoaded") + ": " + Path.GetFileName(path) +
                " (" + image.Width + "x" + image.Height + ")");
        }
        catch (Exception e)
        {
            ShowError(LocalizationSource.Get("iconDdsError") + e.Message);
        }
    }

    [RelayCommand]
    private void LoadCompare()
    {
        if (!HasAtlas) return;
        var path = _dialogs.PickOpenFile(
            LocalizationSource.Get("iconLoadCompare"), "DDS (*.dds)|*.dds|*.*|*.*", InitialTextureFolder);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var image = DecodeDds(path);
            var textures = (_project.Textures ?? new List<ProjectTexture>()).Take(1).ToList();
            textures.Add(CreateTexture(image, Path.GetFileName(path)));
            _project.Textures = textures;
            LoadOverlay();
            _shell.OnSectionEdited(LocalizationSource.Get("iconCompareLoaded") + ": " + Path.GetFileName(path));
        }
        catch (Exception e)
        {
            ShowError(LocalizationSource.Get("iconDdsError") + e.Message);
        }
    }

    [RelayCommand]
    private void RemoveCompare()
    {
        if (!HasOverlay) return;
        _project.Textures = (_project.Textures ?? new List<ProjectTexture>()).Take(1).ToList();
        LoadOverlay();
        _shell.OnSectionEdited(LocalizationSource.Get("iconCompareRemoved"));
    }

    private static RgbaImage DecodeDds(string path)
    {
        var decoded = DdsCodec.DecodeDds(File.ReadAllBytes(path));
        return new RgbaImage(decoded.Width, decoded.Height, decoded.Pixels);
    }

    private static ProjectTexture CreateTexture(RgbaImage image, string name) => new()
    {
        Name = name,
        DataUrl = PngDataUrlEncoder.FromRgba(image.Pixels, image.Width, image.Height),
        Width = image.Width,
        Height = image.Height
    };

    private void ApplyAtlasImage(RgbaImage image, string name, bool replaceAll, string status)
    {
        if (image.IsEmpty)
        {
            ShowError(LocalizationSource.Get("iconDecodeFailed"));
            return;
        }

        var texture = CreateTexture(image, name);
        var textures = (_project.Textures ?? new List<ProjectTexture>()).ToList();
        if (replaceAll || textures.Count == 0)
            textures = new List<ProjectTexture> { texture };
        else
            textures[0] = texture;
        _project.Textures = textures;

        _atlasPixels = image;
        _atlasBitmap = RgbaImageCodec.ToBitmap(image);
        AtlasImage = _atlasBitmap;
        HasAtlas = true;
        HasAtlasError = false;
        AtlasError = "";
        if (replaceAll) AutoFitDefaultColumns(image.Width);
        RefreshLayout(name);
        LoadOverlay();
        _shell.OnSectionEdited(status);
    }

    private void ShowError(string message)
    {
        _shell.StatusText = message;
        _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), message);
    }

    // --- экспорт PNG/DDS ---

    [RelayCommand]
    private void ExportPng()
    {
        if (_atlasPixels == null) return;
        ExportPngImage(_atlasPixels, LocalizationSource.Get("iconExportPng"),
            (Path.GetFileNameWithoutExtension(_project.Textures?.FirstOrDefault()?.Name ?? "atlas")) + ".png");
    }

    [RelayCommand]
    private void ExportSelected()
    {
        if (_atlasPixels == null) return;
        var cropped = AtlasPixels.Crop(_atlasPixels, CurrentPixelRegion());
        if (cropped.IsEmpty) return;
        ExportPngImage(cropped, LocalizationSource.Get("iconExportSelected"),
            "icon_" + SelectionX + "_" + SelectionY + ".png");
    }

    private void ExportPngImage(RgbaImage image, string title, string suggestedName)
    {
        var path = _dialogs.PickSaveFile(title, suggestedName, "PNG (*.png)|*.png|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var bytes = RgbaImageCodec.ToPngBytes(image);
            if (bytes == null) throw new InvalidOperationException(LocalizationSource.Get("iconDecodeFailed"));
            File.WriteAllBytes(path, bytes);
            _shell.StatusText = LocalizationSource.Get("iconExported");
            _shell.StatusDetail = path;
        }
        catch (Exception e)
        {
            ShowError(LocalizationSource.Get("iconExportFailed") + ": " + e.Message);
        }
    }

    [RelayCommand]
    private void ExportDxt1() => ExportDds(DdsFormat.Dxt1, false);

    [RelayCommand]
    private void ExportDxt3() => ExportDds(DdsFormat.Dxt3, false);

    [RelayCommand]
    private void ExportDxt5() => ExportDds(DdsFormat.Dxt5, false);

    [RelayCommand]
    private void ExportDxt1Mip() => ExportDds(DdsFormat.Dxt1, true);

    private void ExportDds(DdsFormat format, bool mipmaps)
    {
        if (_atlasPixels == null) return;
        var suffix = format.ToString().ToLowerInvariant() + (mipmaps ? "_mip" : "");
        var path = _dialogs.PickSaveFile(
            LocalizationSource.Get("iconExportDds"), "atlas_" + suffix + ".dds",
            "DDS (*.dds)|*.dds|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var bytes = DdsCodec.EncodeDds(
                _atlasPixels.Pixels, _atlasPixels.Width, _atlasPixels.Height, format, mipmaps);
            File.WriteAllBytes(path, bytes);
            _shell.StatusText = LocalizationSource.Get("iconExported") + " (" +
                format.ToString().ToUpperInvariant() + (mipmaps ? ", mip" : "") + ")";
            _shell.StatusDetail = path;
        }
        catch (Exception e)
        {
            ShowError(LocalizationSource.Get("iconExportFailed") + ": " + e.Message);
        }
    }

    private AtlasRegion CurrentPixelRegion() =>
        _grid.PixelRegion(new AtlasRegion(SelectionX, SelectionY, SelectionWidth, SelectionHeight));

    // --- пиксельный редактор выделения ---

    [RelayCommand]
    private void Copy()
    {
        if (_atlasPixels == null) return;
        _clipboard = AtlasPixels.Crop(_atlasPixels, CurrentPixelRegion());
        _shell.StatusText = LocalizationSource.Get("iconCopied") + " (" +
            SelectionWidth + "×" + SelectionHeight + ")";
    }

    [RelayCommand]
    private void Paste()
    {
        if (_atlasPixels == null || _clipboard == null || _clipboard.IsEmpty) return;
        AtlasPixels.Paste(_atlasPixels, _clipboard, SelectionX * _grid.CellWidth, SelectionY * _grid.CellHeight);
        ApplyAtlasImage(_atlasPixels, CurrentName(), replaceAll: false,
            LocalizationSource.Get("iconPasted") + " " + SelectionX + "×" + SelectionY);
    }

    [RelayCommand]
    private void NewAtlas()
    {
        var columns = ParseInt(_dialogs.Prompt(
            LocalizationSource.Get("iconNewAtlas"), LocalizationSource.Get("iconColumnsPrompt"), "5"), 5);
        var rows = ParseInt(_dialogs.Prompt(
            LocalizationSource.Get("iconNewAtlas"), LocalizationSource.Get("iconRowsPrompt"), "5"), 5);
        var image = AtlasPixels.Transparent(
            (int)(Math.Max(1, columns) * _grid.CellWidth),
            (int)(Math.Max(1, rows) * _grid.CellHeight));
        ApplyAtlasImage(image, "new_atlas.png", replaceAll: true,
            LocalizationSource.Get("iconNewCreated") + ": " + columns + "×" + rows);
    }

    [RelayCommand]
    private void Crop()
    {
        if (_atlasPixels == null) return;
        var cropped = AtlasPixels.Crop(_atlasPixels, CurrentPixelRegion());
        if (cropped.IsEmpty) return;
        ApplyAtlasImage(cropped, "crop_" + cropped.Width + "x" + cropped.Height + ".png",
            replaceAll: true, LocalizationSource.Get("iconCropped") + " (" + cropped.Width + "×" + cropped.Height + ")");
    }

    [RelayCommand]
    private void Fill()
    {
        if (_atlasPixels == null) return;
        var raw = _dialogs.Prompt(
            LocalizationSource.Get("iconFill"), LocalizationSource.Get("iconFillPrompt"), "#000000");
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (!RgbaColor.TryParseHex(raw, out var color))
        {
            ShowError(LocalizationSource.Get("iconColorInvalid") + ": " + raw);
            return;
        }

        AtlasPixels.Fill(_atlasPixels, CurrentPixelRegion(), color);
        ApplyAtlasImage(_atlasPixels, CurrentName(), replaceAll: false,
            LocalizationSource.Get("iconFilled") + ": " + raw);
    }

    [RelayCommand]
    private void Alpha()
    {
        if (_atlasPixels == null) return;
        var raw = _dialogs.Prompt(
            LocalizationSource.Get("iconAlpha"), LocalizationSource.Get("iconAlphaPrompt"), "128");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var alpha = Math.Clamp(ParseInt(raw, 128), 0, 255);
        AtlasPixels.SetAlpha(_atlasPixels, CurrentPixelRegion(), (byte)alpha);
        ApplyAtlasImage(_atlasPixels, CurrentName(), replaceAll: false,
            LocalizationSource.Get("iconAlphaSet") + ": " + alpha);
    }

    [RelayCommand]
    private void FlipX() => Flip(true);

    [RelayCommand]
    private void FlipY() => Flip(false);

    private void Flip(bool horizontal)
    {
        if (_atlasPixels == null) return;
        AtlasPixels.Flip(_atlasPixels, CurrentPixelRegion(), horizontal);
        ApplyAtlasImage(_atlasPixels, CurrentName(), replaceAll: false,
            LocalizationSource.Get(horizontal ? "iconFlippedX" : "iconFlippedY"));
    }

    [RelayCommand]
    private void RotateClockwise() => Rotate(true);

    [RelayCommand]
    private void RotateCounterClockwise() => Rotate(false);

    private void Rotate(bool clockwise)
    {
        if (_atlasPixels == null) return;
        var region = CurrentPixelRegion();
        if (region.W != region.H)
        {
            _shell.StatusText = LocalizationSource.Get("iconRotateSquare") + " (" +
                SelectionWidth + "×" + SelectionHeight + ")";
            return;
        }

        AtlasPixels.Rotate90(_atlasPixels, region, clockwise);
        ApplyAtlasImage(_atlasPixels, CurrentName(), replaceAll: false,
            LocalizationSource.Get(clockwise ? "iconRotatedClockwise" : "iconRotatedCounterClockwise"));
    }

    private string CurrentName() => _project.Textures?.FirstOrDefault()?.Name ?? "atlas.png";

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;

    // --- спрайт-лист, дубликаты и коллекции ---

    [RelayCommand]
    private void ExportSpritesheet()
    {
        if (_atlasPixels == null) return;
        if (_project.PinCollections == null ||
            !_project.PinCollections.TryGetValue(CollectionName ?? "", out var icons) ||
            icons.Count == 0)
        {
            _shell.StatusText = LocalizationSource.Get("iconNoPins");
            return;
        }

        var sprites = icons.Select(icon => new AtlasSprite(
            Math.Max(0, (int)Math.Round(icon.X)),
            Math.Max(0, (int)Math.Round(icon.Y)),
            Math.Max(1, (int)Math.Round(icon.W)),
            Math.Max(1, (int)Math.Round(icon.H)))).ToList();
        var sheet = AtlasPixels.BuildSpritesheet(
            _atlasPixels, sprites, _grid.CellWidth, _grid.CellHeight);
        if (sheet.IsEmpty) return;
        ExportPngImage(sheet, LocalizationSource.Get("iconExportSheet"), "icons_sheet.png");
    }

    [RelayCommand]
    private void FindDuplicates()
    {
        if (_atlasPixels == null) return;
        Duplicates.Clear();
        var cells = AtlasPixels.FindDuplicateCells(
            _atlasPixels, _grid.CellWidth, _grid.CellHeight);
        foreach (var cell in cells) Duplicates.Add(new AtlasDuplicateCellViewModel(this, cell.X, cell.Y));

        HasDuplicates = Duplicates.Count > 0;
        DuplicatesTitle = LocalizationSource.Get("iconDuplicates") + " (" +
            Duplicates.Count.ToString(CultureInfo.CurrentCulture) + ")";
        _shell.StatusText = HasDuplicates
            ? LocalizationSource.Get("iconDuplicatesFound") + ": " + Duplicates.Count
            : LocalizationSource.Get("iconNoDuplicates");
    }

    [RelayCommand]
    private void Pin()
    {
        if (!HasAtlas) return;
        var icon = new PinnedIcon
        {
            X = SelectionX,
            Y = SelectionY,
            W = SelectionWidth,
            H = SelectionHeight,
            Name = "icon_" + SelectionX + "_" + SelectionY
        };
        var collections = CopyCollections();
        var name = string.IsNullOrWhiteSpace(CollectionName) ? "default" : CollectionName;
        if (!collections.TryGetValue(name, out var list))
        {
            list = new List<PinnedIcon>();
            collections[name] = list;
        }

        list.Add(icon);
        _project.PinCollections = collections;
        RefreshPinned();
        if (!Collections.Contains(name)) Collections.Add(name);
        _shell.OnSectionEdited(LocalizationSource.Get("iconPinnedDone") + ": " + name);
    }

    [RelayCommand]
    private void AddCollection()
    {
        var initial = "collection_" + ((_project.PinCollections?.Count ?? 0) + 1);
        var raw = _dialogs.Prompt(
            LocalizationSource.Get("iconCollectionTitle"), LocalizationSource.Get("iconCollectionPrompt"), initial);
        if (string.IsNullOrWhiteSpace(raw)) return;

        var name = raw.Trim();
        var collections = CopyCollections();
        if (!collections.ContainsKey(name)) collections[name] = new List<PinnedIcon>();
        _project.PinCollections = collections;
        RefreshCollections();
        CollectionName = name;
        RefreshPinned();
        _shell.OnSectionEdited(LocalizationSource.Get("iconCollectionCreated") + ": " + name);
    }

    public void RemovePinned(PinnedIconViewModel item)
    {
        if (_project.PinCollections == null) return;
        var name = string.IsNullOrWhiteSpace(CollectionName) ? "default" : CollectionName;
        if (!_project.PinCollections.TryGetValue(name, out var icons)) return;

        var index = icons.IndexOf(item.Model);
        if (index < 0) return;
        icons.RemoveAt(index);
        RefreshPinned();
        _shell.OnSectionEdited(LocalizationSource.Get("iconUnpinned"));
    }

    private Dictionary<string, List<PinnedIcon>> CopyCollections()
    {
        var source = _project.PinCollections ?? new Dictionary<string, List<PinnedIcon>>();
        return source.ToDictionary(pair => pair.Key, pair => new List<PinnedIcon>(pair.Value),
            StringComparer.Ordinal);
    }
}
