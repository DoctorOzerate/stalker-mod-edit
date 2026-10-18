using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.History;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Suggestions;
using XRayForge.Core.Text;
using XRayForge.Localization;
using XRayForge.Services;
using XRayForge.Views;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «Предметы» (этап 7.2, `ItemsTab.js`): список LTX-секций по категориям с поиском,
/// редактор секции с унаследованными полями, быстрыми полями, деревом наследования,
/// сеткой атласа, слотами экипировки, undo/redo и экспортом LTX/CSV.
/// </summary>
public sealed partial class ItemsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly History<string> _history = new(50);
    private readonly HashSet<string> _unlockedRoots = new(StringComparer.Ordinal);

    private Project _project = ProjectMigration.Empty();
    private LtxDocument _document = new();
    private List<LtxSection> _sections = new();
    private Dictionary<string, LtxSection> _itemsByName = new(StringComparer.Ordinal);
    private SuggestionLibrary _suggestions = SuggestionLibrary.Empty;
    private HistoryState<string> _historyState = HistoryState<string>.Empty;
    private LtxSection? _selectedSection;
    private BitmapImage? _atlasBitmap;
    private ProjectTexture? _atlasTexture;
    private List<EquipSlotLayout> _slotLayout = new();

    public ItemsViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;

        Groups = new ObservableCollection<ItemGroupViewModel>();
        Fields = new ObservableCollection<ItemFieldViewModel>();
        QuickFields = new ObservableCollection<ItemQuickFieldViewModel>();
        ParentLinks = new ObservableCollection<ItemParentViewModel>();
        TreeRows = new ObservableCollection<ItemTreeRowViewModel>();
        References = new ObservableCollection<ItemReferenceViewModel>();
        AtlasCells = new ObservableCollection<AtlasCellViewModel>();
        EquipSlots = new ObservableCollection<ItemEquipSlotViewModel>();
        CategoryOptions = new ObservableCollection<string> { ItemEditor.AllCategories };
        foreach (var category in ItemCatalog.Categories) CategoryOptions.Add(category);
    }

    // --- коллекции вкладки ---

    public ObservableCollection<ItemGroupViewModel> Groups { get; }
    public ObservableCollection<ItemFieldViewModel> Fields { get; }
    public ObservableCollection<ItemQuickFieldViewModel> QuickFields { get; }
    public ObservableCollection<ItemParentViewModel> ParentLinks { get; }
    public ObservableCollection<ItemTreeRowViewModel> TreeRows { get; }
    public ObservableCollection<ItemReferenceViewModel> References { get; }
    public ObservableCollection<AtlasCellViewModel> AtlasCells { get; }
    public ObservableCollection<ItemEquipSlotViewModel> EquipSlots { get; }
    public ObservableCollection<string> CategoryOptions { get; }

    // --- состояние ---

    /// <summary>Выбранная секция (`selItem`/`selSection` в JS); null — подсказка редактора.</summary>
    [ObservableProperty] private ItemSectionViewModel? _selected;

    /// <summary>Поиск по имени и полям секции (`itemSearch`).</summary>
    [ObservableProperty] private string _search = "";

    /// <summary>`name` или `count` (`listSort`).</summary>
    [ObservableProperty] private string _sortMode = "name";

    /// <summary>`Все` либо одна из <see cref="ItemCatalog.Categories"/>.</summary>
    [ObservableProperty] private string _categoryFilter = ItemEditor.AllCategories;

    /// <summary>`Секций: N` под кнопкой создания.</summary>
    public string CountText => LocalizationSource.Get("itemSections") + ": " +
        _document.Sections.Count.ToString(CultureInfo.CurrentCulture);

    public bool HasItems => _sections.Any(section => !string.IsNullOrEmpty(section.Name));

    /// <summary>Есть ли карточки после фильтра/поиска (подсказка «ничего не найдено»).</summary>
    public bool HasGroups => Groups.Count > 0;

    /// <summary>Подсказка списка: предметов нет вообще или ничего не найдено по фильтру.</summary>
    public string NoGroupsText => HasItems
        ? LocalizationSource.Get("itemNoMatches")
        : LocalizationSource.Get("itemEmpty");
    public bool HasSelection => _selectedSection != null;
    public bool CanUndo => _historyState.Pos > 0;
    public bool CanRedo => _historyState.Pos >= 0 && _historyState.Pos < _historyState.Hist.Length - 1;
    public bool IsLocked => _selectedSection != null &&
        ItemEditor.IsRoot(_selectedSection) && !_unlockedRoots.Contains(_selectedSection.Name);

    /// <summary>`Наследует от: … · унаследовано полей: N · всего полей: M`.</summary>
    [ObservableProperty] private string _inheritanceSummary = "";

    public bool HasParents => ParentLinks.Count > 0;
    public bool HasBrokenParents => BrokenParentsText.Length > 0;
    [ObservableProperty] private string _brokenParentsText = "";

    public bool HasQuickFields => QuickFields.Count > 0;
    [ObservableProperty] private string _quickFieldsTitle = "";

    public bool HasTree => TreeRows.Count > 0;
    public string TreeTitle => LocalizationSource.Get("itemTreeTitle");
    public string QuickFieldsCaption => LocalizationSource.Get("itemQuickFields");
    public string FieldsCaption => LocalizationSource.Get("itemFieldsTitle");
    public string ReferencesCaption => LocalizationSource.Get("itemReferences");
    public string EquipCaption => LocalizationSource.Get("itemEquipSlots");

    public bool HasReferences => References.Count > 0;
    [ObservableProperty] private string _referencesTitle = "";

    public bool HasAtlas => _atlasTexture != null;

    /// <summary>Полное изображение атласа для предпросмотра (`atlasImg` в JS).</summary>
    public ImageSource? AtlasImage => _atlasBitmap;
    [ObservableProperty] private string _atlasTitle = "";
    [ObservableProperty] private string _gridCoordsText = "";

    /// <summary>Вырезанная иконка выбранного предмета по `inv_grid_*`.</summary>
    [ObservableProperty] private ImageSource? _selectedItemIcon;

    /// <summary>
    /// Размер превью иконки 1:1 с атласом: `inv_grid_width/height` × размер ячейки.
    /// Поэтому 2×1 шире 1×1, а 2×2 больше по обеим осям, без подгонки под квадрат.
    /// </summary>
    [ObservableProperty] private double _iconPreviewWidth = ItemsViewModel.DefaultPreviewCellPx;

    [ObservableProperty] private double _iconPreviewHeight = ItemsViewModel.DefaultPreviewCellPx;

    /// <summary>`inv_grid_x/y` заданы — координаты можно показать рядом с превью.</summary>
    [ObservableProperty] private bool _hasGridCoords;

    /// <summary>Сетка атласа раскрыта кнопкой «Выбрать иконку».</summary>
    [ObservableProperty] private bool _isIconPickerOpen;

    private const double DefaultPreviewCellPx = 50;

    /// <summary>
    /// Размер ячейки из `project.atlas` — единый источник для превью, пикера и слотов.
    /// Пресет «Иконок» пишет сюда `cellSize`/`cellHeight`, поэтому после его смены
    /// вырезки предметов пересчитываются тем же масштабом, что и сетка атласа.
    /// </summary>
    private (double Width, double Height) AtlasCellSize
    {
        get
        {
            var width = _project.Atlas.CellSize > 0 ? _project.Atlas.CellSize : DefaultPreviewCellPx;
            var height = _project.Atlas.CellHeight > 0 ? _project.Atlas.CellHeight : width;
            return (width, height);
        }
    }

    public bool HasSelectedItemIcon => SelectedItemIcon != null;

    partial void OnSelectedItemIconChanged(ImageSource? value) =>
        OnPropertyChanged(nameof(HasSelectedItemIcon));

    partial void OnIsIconPickerOpenChanged(bool value)
    {
        if (value) RefreshAtlasCells(_selectedSection);
    }

    [RelayCommand]
    private void ToggleIconPicker() => IsIconPickerOpen = !IsIconPickerOpen;

    /// <summary>Число колонок сетки атласа — для `UniformGrid` предпросмотра.</summary>
    [ObservableProperty] private int _atlasColumns;

    public bool HasEquipSlots => EquipSlots.Count > 0;

    /// <summary>В редакторе раскладки выбран слот (поля справа доступны).</summary>
    public bool HasSelectedEquipSlot => SelectedEquipSlot != null;

    /// <summary>Размер виртуального поля слотов (общий прямоугольник раскладки).</summary>
    [ObservableProperty] private double _slotCanvasWidth = EquipSlotCatalog.EmptyCanvasWidth;

    [ObservableProperty] private double _slotCanvasHeight = EquipSlotCatalog.EmptyCanvasHeight;

    /// <summary>Слот, выбранный в окне «Настройка слотов».</summary>
    [ObservableProperty] private ItemEquipSlotViewModel? _selectedEquipSlot;

    partial void OnSelectedEquipSlotChanged(ItemEquipSlotViewModel? value) =>
        OnPropertyChanged(nameof(HasSelectedEquipSlot));

    public bool IsCopSeries => string.Equals(_project.Series, "cop", StringComparison.Ordinal);

    // --- проект и список ---

    /// <summary>Принять проект (`openProject`/импорт/скан): история и выбор сбрасываются.</summary>
    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _unlockedRoots.Clear();
        _historyState = HistoryState<string>.Empty;
        _selectedSection = null;
        Selected = null;
        // null — раскладка ещё не менялась: показываем дефолт серии, не записывая его в проект.
        _slotLayout = EquipSlotCatalog.Clone(
            _project.EquipSlotLayout ?? EquipSlotCatalog.DefaultLayout(_project.Series));
        SelectedEquipSlot = null;
        RefreshFromText(null);
    }

    /// <summary>Смена локали: пересобрать производные строки и подписи слотов.</summary>
    public void RefreshLocalization()
    {
        RefreshGroups();
        RefreshDetails();
    }

    partial void OnSearchChanged(string value) => RefreshGroups();

    partial void OnSortModeChanged(string value) => RefreshGroups();

    partial void OnCategoryFilterChanged(string value) => RefreshGroups();

    /// <summary>Выбрать секцию по имени (кнопки списка, дерева, ссылок, родителей).</summary>
    public void SelectByName(string? name)
    {
        if (name != null && _itemsByName.TryGetValue(name.ToLowerInvariant(), out var section))
        {
            _selectedSection = section;
            Selected = new ItemSectionViewModel(this, section);
            IsIconPickerOpen = false;
            UpdateListSelection();
            RefreshDetails();
            return;
        }

        _selectedSection = null;
        Selected = null;
        IsIconPickerOpen = false;
        UpdateListSelection();
        RefreshDetails();
    }

    [RelayCommand]
    private void Select(ItemSectionViewModel? item) => SelectByName(item?.Name);

    /// <summary>Перечитать LTX, пересобрать группы и выбрать секцию (если она уцелела).</summary>
    private void RefreshFromText(string? preferredName)
    {
        _document = LtxParser.Parse(_project.ItemsLtx);
        _sections = _document.Sections;
        _itemsByName = ItemEditor.MapByName(_sections);
        _suggestions = SuggestionLibrary.Build(_project, _sections);
        RefreshGroups();

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(NoGroupsText));

        var name = preferredName ?? Selected?.Name;
        if (!string.IsNullOrEmpty(name) && _itemsByName.TryGetValue(name.ToLowerInvariant(), out var section))
        {
            _selectedSection = section;
            Selected = new ItemSectionViewModel(this, section);
        }
        else
        {
            _selectedSection = null;
            Selected = null;
        }

        UpdateListSelection();
        RefreshDetails();
        RefreshHistory();
    }

    private void RefreshGroups()
    {
        Groups.Clear();
        foreach (var group in ItemEditor.Group(_sections, Search, CategoryFilter, SortMode))
        {
            var items = group.Items
                .Select(section => new ItemSectionViewModel(this, section))
                .ToList();
            Groups.Add(new ItemGroupViewModel(
                group.Category + " · " + items.Count.ToString(CultureInfo.CurrentCulture), items));
        }

        UpdateListSelection();
        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(NoGroupsText));
    }

    /// <summary>Перенести выделение редактора на карточки списка (`selItem`).</summary>
    private void UpdateListSelection()
    {
        foreach (var item in Groups.SelectMany(group => group.Items))
            item.IsSelected = _selectedSection != null &&
                string.Equals(item.Name, _selectedSection.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>
    /// Атлас или размер его ячеек изменился на вкладке «Иконки»: пересчитать вырезанную
    /// иконку и слоты экипировки, не сбрасывая выбранную секцию и историю правок.
    /// </summary>
    public void RefreshAtlasPreview()
    {
        if (_selectedSection == null) return;
        RefreshAtlas(_selectedSection);
        RefreshEquipSlots();
        NotifyDetails();
    }

    /// <summary>Пересчитать правую панель: слияние полей, быстрые поля, дерево, ссылки и слоты.</summary>
    private void RefreshDetails()
    {
        Fields.Clear();
        QuickFields.Clear();
        ParentLinks.Clear();
        TreeRows.Clear();
        References.Clear();
        AtlasCells.Clear();
        EquipSlots.Clear();
        SelectedEquipSlot = null;

        var section = _selectedSection;
        if (section == null)
        {
            InheritanceSummary = "";
            BrokenParentsText = "";
            QuickFieldsTitle = "";
            ReferencesTitle = "";
            AtlasTitle = "";
            GridCoordsText = "";
            HasGridCoords = false;
            IconPreviewWidth = DefaultPreviewCellPx;
            IconPreviewHeight = DefaultPreviewCellPx;
            SelectedItemIcon = null;
            IsIconPickerOpen = false;
            _atlasTexture = null;
            _atlasBitmap = null;
            RefreshSlotCanvas();
            NotifyDetails();
            return;
        }

        var locked = ItemEditor.IsRoot(section) && !_unlockedRoots.Contains(section.Name);
        var merged = ItemEditor.MergedEntries(section, _itemsByName);
        foreach (var entry in merged)
        {
            var overridden = ItemEditor.IsOverridden(section, entry, _itemsByName);
            Fields.Add(new ItemFieldViewModel(
                this, entry, locked, overridden,
                _suggestions.ForItemField(entry.Key, ItemEditor.ValueSuggestions(_sections, entry.Key))));
        }

        var parentLinks = ItemEditor.Parents(section, _itemsByName);
        foreach (var parent in parentLinks)
            ParentLinks.Add(new ItemParentViewModel(this, parent.Name, parent.Exists));

        InheritSummary(section, merged);
        var broken = ItemEditor.BrokenParents(section, _itemsByName);
        BrokenParentsText = broken.Count > 0
            ? "⚠ " + LocalizationSource.Get("itemBrokenParents") + ": " + string.Join(", ", broken)
            : "";

        var mergedKeys = merged.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in ItemCatalog.ItemFieldProfile(section).Distinct(StringComparer.Ordinal))
            QuickFields.Add(new ItemQuickFieldViewModel(this, key, mergedKeys.Contains(key)));
        QuickFieldsTitle = LocalizationSource.Get("itemQuickFields") + " · " + ItemCatalog.ClassifyItem(section);

        RefreshTree(section);
        RefreshReferences(section);
        RefreshAtlas(section);
        RefreshEquipSlots();
        NotifyDetails();
    }

    private void InheritSummary(LtxSection section, IReadOnlyList<MergedEntry> merged)
    {
        if (section.Parents.Count == 0)
        {
            InheritanceSummary = LocalizationSource.Get("itemNoParents");
            return;
        }

        var inherited = merged.Count(entry => entry.Inherited);
        InheritanceSummary = LocalizationSource.Get("itemInheritsFrom") + ": " + string.Join(", ", section.Parents) +
            " · " + LocalizationSource.Get("itemInheritedFields") + ": " + inherited.ToString(CultureInfo.CurrentCulture) +
            " · " + LocalizationSource.Get("itemTotalFields") + ": " + merged.Count.ToString(CultureInfo.CurrentCulture);
    }

    private void NotifyDetails()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(HasParents));
        OnPropertyChanged(nameof(HasBrokenParents));
        OnPropertyChanged(nameof(HasQuickFields));
        OnPropertyChanged(nameof(HasTree));
        OnPropertyChanged(nameof(HasReferences));
        OnPropertyChanged(nameof(HasAtlas));
        OnPropertyChanged(nameof(AtlasImage));
        OnPropertyChanged(nameof(HasEquipSlots));
        OnPropertyChanged(nameof(TreeTitle));
        OnPropertyChanged(nameof(QuickFieldsCaption));
        OnPropertyChanged(nameof(FieldsCaption));
        OnPropertyChanged(nameof(ReferencesCaption));
        OnPropertyChanged(nameof(EquipCaption));
        OnPropertyChanged(nameof(IsCopSeries));
    }

    /// <summary>
    /// Дерево наследования как в JS: цепочка предков (`■`/`↑`) и рекурсивные дочерние
    /// секции (`↳`); циклы обрываются защитным множеством.
    /// </summary>
    private void RefreshTree(LtxSection section)
    {
        var tree = LtxInheritance.InheritanceTree(_project.ItemsLtx);
        var chain = ItemEditor.InheritanceChain(section, _itemsByName);
        var children = ItemEditor.Children(section.Name, tree);
        if (chain.Count <= 1 && children.Count == 0) return;

        for (var i = 0; i < chain.Count; i++)
        {
            var current = string.Equals(chain[i], section.Name, StringComparison.Ordinal);
            TreeRows.Add(new ItemTreeRowViewModel(this, chain[i], i, i == 0 ? "■" : "↑", current));
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { section.Name };
        void AddChildren(string name, int depth)
        {
            if (depth > 50) return;
            foreach (var child in ItemEditor.Children(name, tree))
            {
                if (!visited.Add(child)) continue;
                TreeRows.Add(new ItemTreeRowViewModel(this, child, depth, "↳", false));
                AddChildren(child, depth + 1);
            }
        }

        AddChildren(section.Name, 1);
    }

    /// <summary>`refs`: секции, ссылающиеся на выбранную (`immunities_sect` и т.п.).</summary>
    private void RefreshReferences(LtxSection section)
    {
        var references = ItemEditor.ReferencingSections(_sections, section.Name);
        foreach (var reference in references)
            References.Add(new ItemReferenceViewModel(this, reference));
        ReferencesTitle = LocalizationSource.Get("itemReferencesTitle") + " (" +
            references.Count.ToString(CultureInfo.CurrentCulture) + ")";
    }

    /// <summary>
    /// Иконка предмета: `inv_grid_x/y` + размер `inv_grid_width/height` вырезаются из
    /// первого атласа проекта. Полная сетка строится только при открытии выбора иконки.
    /// </summary>
    private void RefreshAtlas(LtxSection section)
    {
        _atlasTexture = _project.Textures?.FirstOrDefault();
        _atlasBitmap = null;
        AtlasCells.Clear();
        AtlasTitle = "";
        GridCoordsText = "";
        AtlasColumns = 0;
        SelectedItemIcon = null;

        var (cellSize, cellHeight) = AtlasCellSize;
        var placement = IconGrid.ResolvePlacement(section, _itemsByName);
        var iconWidth = Math.Max(1, placement.Width);
        var iconHeight = Math.Max(1, placement.Height);

        HasGridCoords = placement.HasPosition;
        GridCoordsText = placement.HasPosition
            ? "inv_grid_x=" + placement.X + " · inv_grid_y=" + placement.Y +
              " · " + iconWidth + "×" + iconHeight
            : "";

        var preview = IconPreviewSize(iconWidth, iconHeight, cellSize, cellHeight);
        IconPreviewWidth = preview.Width;
        IconPreviewHeight = preview.Height;

        _atlasBitmap = TexturePreview.Load(_atlasTexture?.DataUrl);
        var width = _atlasBitmap?.PixelWidth ?? _atlasTexture?.Width ?? 0;
        var height = _atlasBitmap?.PixelHeight ?? _atlasTexture?.Height ?? 0;
        if (_atlasBitmap == null || width <= 0 || height <= 0)
        {
            IsIconPickerOpen = false;
            return;
        }

        var cols = Math.Max(1, (int)Math.Floor(width / cellSize));
        var rows = Math.Max(1, (int)Math.Floor(height / cellHeight));
        AtlasColumns = cols;
        AtlasTitle = LocalizationSource.Get("itemAtlasTitle") + " · " + _atlasTexture?.Name +
                     " (" + cols + "×" + rows + ")";

        if (placement.HasPosition)
        {
            SelectedItemIcon = TexturePreview.Crop(
                _atlasBitmap, placement.X * cellSize, placement.Y * cellHeight,
                iconWidth * cellSize, iconHeight * cellHeight,
                width, height);
        }

        if (IsIconPickerOpen) RefreshAtlasCells(section);
    }

    /// <summary>
    /// Размер превью 1:1 с атласом: число ячеек × размер ячейки. Пропорции
    /// прямоугольных ячеек (`cellSize` ≠ `cellHeight`) сохраняются. Ограничение
    /// 400 px нужно только для аномально больших иконок, чтобы не раздувать панель.
    /// </summary>
    private static (double Width, double Height) IconPreviewSize(
        int iconWidth, int iconHeight, double cellWidth, double cellHeight)
    {
        const double maxPreview = 400;
        var width = iconWidth * Math.Max(1, cellWidth);
        var height = iconHeight * Math.Max(1, cellHeight);

        var largest = Math.Max(width, height);
        if (largest > maxPreview)
        {
            var fit = maxPreview / largest;
            width *= fit;
            height *= fit;
        }

        return (Math.Round(width, 1), Math.Round(height, 1));
    }

    /// <summary>Построить сетку ячеек для раскрытого выбора иконки.</summary>
    private void RefreshAtlasCells(LtxSection? section)
    {
        AtlasCells.Clear();
        if (section == null || _atlasBitmap == null || AtlasColumns <= 0) return;

        var (cellWidth, cellHeight) = AtlasCellSize;
        var rows = Math.Max(1, (int)Math.Floor(_atlasBitmap.PixelHeight / cellHeight));
        var placement = IconGrid.ResolvePlacement(section, _itemsByName);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < AtlasColumns; x++)
            {
                var image = TexturePreview.Crop(
                    _atlasBitmap, x * cellWidth, y * cellHeight, cellWidth, cellHeight,
                    _atlasBitmap.PixelWidth, _atlasBitmap.PixelHeight);
                var active = placement.HasPosition && x == placement.X && y == placement.Y;
                AtlasCells.Add(new AtlasCellViewModel(this, x, y, image, active));
            }
        }
    }

    /// <summary>
    /// Собрать слоты по пользовательской раскладке (`equipSlotLayout`), с подписями,
    /// назначенными секциями и вырезанными из атласа иконками.
    /// </summary>
    private void RefreshEquipSlots()
    {
        var selectedKey = SelectedEquipSlot?.Key;
        EquipSlots.Clear();

        var (cellSize, cellHeight) = AtlasCellSize;
        foreach (var slot in _slotLayout)
        {
            var key = slot.Key;
            var name = _project.EquipSlots.TryGetValue(key, out var value) ? value : "";
            ImageSource? icon = null;
            if (!string.IsNullOrEmpty(name) && _itemsByName.TryGetValue(name.ToLowerInvariant(), out var section))
            {
                var placement = IconGrid.ResolvePlacement(section, _itemsByName);
                if (_atlasTexture?.DataUrl is { Length: > 0 } dataUrl &&
                    _atlasTexture.Width is > 0 && _atlasTexture.Height is > 0 &&
                    placement.HasPosition)
                {
                    icon = TexturePreview.Crop(
                        dataUrl,
                        placement.X * cellSize, placement.Y * cellHeight,
                        Math.Max(1, placement.Width) * cellSize,
                        Math.Max(1, placement.Height) * cellHeight,
                        _atlasTexture.Width.Value, _atlasTexture.Height.Value);
                }
            }

            var label = string.IsNullOrWhiteSpace(slot.Label) ? DefaultSlotLabel(key) : slot.Label;
            EquipSlots.Add(new ItemEquipSlotViewModel(
                this, key, label, name, icon, slot.X, slot.Y, slot.Width, slot.Height));
        }

        SelectedEquipSlot = selectedKey != null
            ? EquipSlots.FirstOrDefault(slot => string.Equals(slot.Key, selectedKey, StringComparison.Ordinal))
            : null;
        RefreshSlotCanvas();
        OnPropertyChanged(nameof(HasEquipSlots));
    }

    /// <summary>Локализованная подпись для ключа слота по умолчанию.</summary>
    private static string DefaultSlotLabel(string key) => key.ToLowerInvariant() switch
    {
        "knife" => LocalizationSource.Get("itemSlotKnife"),
        "pistol" => LocalizationSource.Get("itemSlotPistol"),
        "weapon" => LocalizationSource.Get("itemSlotWeapon"),
        "armor" => LocalizationSource.Get("itemSlotArmor"),
        "helmet" => LocalizationSource.Get("itemSlotHelmet"),
        "device" => LocalizationSource.Get("itemSlotDevice"),
        "detector" => LocalizationSource.Get("itemSlotDetector"),
        _ => key
    };

    /// <summary>Пересчитать общий прямоугольник раскладки после правки геометрии слота.</summary>
    private void RefreshSlotCanvas()
    {
        var (width, height) = EquipSlotCatalog.CanvasSize(EquipSlots.Select(slot => slot.ToLayout()));
        SlotCanvasWidth = width;
        SlotCanvasHeight = height;
    }

    /// <summary>Слот изменился в окне настройки — обновить габариты общего прямоугольника.</summary>
    internal void OnEquipSlotChanged() => RefreshSlotCanvas();

    // --- правки LTX и история ---

    /// <summary>
    /// `mutateItems`: снимок текущего текста в историю, замена текста и выбор секции.
    /// </summary>
    private void Mutate(string newText, string? selectName, string? status = null)
    {
        if (string.Equals(newText, _project.ItemsLtx, StringComparison.Ordinal)) return;

        _historyState = _history.Push(_historyState, _project.ItemsLtx);
        _project.ItemsLtx = newText;
        RefreshFromText(selectName);
        _shell.OnSectionEdited(status);
    }

    private void ApplyHistory(string text)
    {
        _project.ItemsLtx = text;
        RefreshFromText(Selected?.Name);
        _shell.OnSectionEdited();
    }

    [RelayCommand]
    private void Undo()
    {
        var next = _history.Move(_historyState, -1, ApplyHistory);
        if (next.Pos == _historyState.Pos) return;
        _historyState = next;
        RefreshHistory();
    }

    [RelayCommand]
    private void Redo()
    {
        var next = _history.Move(_historyState, +1, ApplyHistory);
        if (next.Pos == _historyState.Pos) return;
        _historyState = next;
        RefreshHistory();
    }

    [RelayCommand]
    private void Create()
    {
        var name = ItemEditor.NewSectionName(_project.ItemsLtx);
        Mutate(LtxTextTools.AddSection(_project.ItemsLtx, name), name, LocalizationSource.Get("itemCreated"));
    }

    /// <summary>`deleteItem`: удаление секции без подтверждения; отмена — кнопкой undo.</summary>
    public void DeleteSection(ItemSectionViewModel? item)
    {
        var name = item?.Name ?? Selected?.Name;
        if (string.IsNullOrEmpty(name)) return;
        Mutate(LtxTextTools.RemoveSection(_project.ItemsLtx, name), null,
            LocalizationSource.Get("itemDeleted"));
    }

    [RelayCommand]
    private void Delete() => DeleteSection(Selected);

    [RelayCommand]
    private void Rename()
    {
        var section = Selected;
        if (section == null)
        {
            _shell.StatusText = LocalizationSource.Get("itemSelectSection");
            return;
        }

        var name = _dialogs.Prompt(
            LocalizationSource.Get("itemRenameTitle"), LocalizationSource.Get("itemRenameLabel"), section.Name);
        if (name == null || string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        if (string.Equals(name, section.Name, StringComparison.Ordinal)) return;

        var oldName = section.Name;
        Mutate(LtxTextTools.RenameSection(_project.ItemsLtx, oldName, name), name,
            LocalizationSource.Get("itemRenamed") + ": " + oldName + " → " + name);
    }

    [RelayCommand]
    private void ReplaceFieldValues()
    {
        var key = _dialogs.Prompt(
            LocalizationSource.Get("itemReplaceTitle"), LocalizationSource.Get("itemReplaceKey"), "cost");
        if (key == null || string.IsNullOrWhiteSpace(key)) return;

        var from = _dialogs.Prompt(
            LocalizationSource.Get("itemReplaceTitle"), LocalizationSource.Get("itemReplaceFrom"), "");
        if (from == null) return;

        var to = _dialogs.Prompt(
            LocalizationSource.Get("itemReplaceTitle"), LocalizationSource.Get("itemReplaceTo"), "");
        if (to == null) return;

        Mutate(LtxTextTools.ReplaceFieldValues(_project.ItemsLtx, key.Trim(), from, to),
            Selected?.Name, LocalizationSource.Get("itemReplaced") + ": " + key.Trim());
    }

    /// <summary>
    /// `setItemField`: правка строки без записи в историю (как JS) — фокус в поле сохраняется,
    /// список и структура не пересобираются.
    /// </summary>
    public void SetItemField(ItemFieldViewModel field, string value)
    {
        if (_selectedSection == null || field.IsInherited || field.IsLocked) return;

        _project.ItemsLtx = ItemEditor.SetField(_project.ItemsLtx, field.Entry.Line, field.Key, value);
        field.Entry.Value = value ?? "";

        var entry = _selectedSection.Entries.FirstOrDefault(
            item => item.Line == field.Entry.Line && item.Key == field.Key);
        if (entry != null) entry.Value = value ?? "";

        Selected?.RefreshDerived();
        field.RefreshComputed(ItemEditor.IsOverridden(_selectedSection, field.Entry, _itemsByName));
        _shell.OnSectionEdited();
    }

    [RelayCommand]
    private void AddField()
    {
        var section = _selectedSection;
        if (section == null) return;
        Mutate(LtxTextTools.AddFieldToSection(_project.ItemsLtx, section.Name, "new_key", "value"),
            section.Name, LocalizationSource.Get("itemFieldAdded"));
    }

    /// <summary>Чип быстрого поля: отсутствующее поле добавляется с пустым значением.</summary>
    public void AddQuickField(string key)
    {
        var section = _selectedSection;
        if (section == null) return;
        if (ItemEditor.MergedEntries(section, _itemsByName).Any(entry => entry.Key == key)) return;

        Mutate(LtxTextTools.AddFieldToSection(_project.ItemsLtx, section.Name, key, ""),
            section.Name, LocalizationSource.Get("itemFieldAdded") + ": " + key);
    }

    [RelayCommand]
    private void UnlockRoot()
    {
        var section = _selectedSection;
        if (section == null) return;
        if (!_dialogs.Confirm(LocalizationSource.Get("itemUnlockConfirm") + " «" + section.Name + "»?")) return;

        _unlockedRoots.Add(section.Name);
        RefreshDetails();
        _shell.StatusText = LocalizationSource.Get("itemUnlocked") + ": " + section.Name;
    }

    /// <summary>Разблокировать унаследованное поле: добавить собственное значение из родителя.</summary>
    [RelayCommand]
    private void UnlockField(ItemFieldViewModel? field)
    {
        var section = _selectedSection;
        if (section == null || field == null || !field.IsInherited) return;
        if (!_dialogs.Confirm(LocalizationSource.Get("itemUnlockFieldConfirm") + " «" + field.Key + "»?")) return;

        Mutate(LtxTextTools.AddFieldToSection(_project.ItemsLtx, section.Name, field.Key, field.Value),
            section.Name, LocalizationSource.Get("itemFieldOverridden"));
    }

    [RelayCommand]
    private void RemoveField(ItemFieldViewModel? field)
    {
        if (_selectedSection == null || field == null || field.IsInherited || field.IsLocked) return;
        Mutate(LtxTextTools.RemoveFieldByLine(_project.ItemsLtx, field.Entry.Line),
            _selectedSection.Name, LocalizationSource.Get("itemFieldRemoved"));
    }

    /// <summary>Клик по ячейке атласа: `setItemGrid` с добавлением/обновлением inv_grid_x/y.</summary>
    public void SetGrid(int x, int y)
    {
        var section = _selectedSection;
        if (section == null) return;

        var text = ApplyGridField(_project.ItemsLtx, section.Name, "inv_grid_x", x);
        text = ApplyGridField(text, section.Name, "inv_grid_y", y);
        Mutate(text, section.Name, LocalizationSource.Get("itemGridSet"));
        IsIconPickerOpen = false;
    }

    /// <summary>
    /// Записать `inv_grid_x/y` в собственные поля секции. В отличие от JS, после добавления
    /// первого поля текст разбирается заново — сдвиг строк не ломает обновление второго поля.
    /// </summary>
    private static string ApplyGridField(string text, string sectionName, string key, int value)
    {
        var section = LtxParser.Parse(text).Sections.FirstOrDefault(
            item => string.Equals(item.Name, sectionName, StringComparison.OrdinalIgnoreCase));
        var entry = section?.Entries.FirstOrDefault(item => item.Key == key);
        return entry != null
            ? LtxTextTools.UpdateLine(text, entry.Line, key + " = " + value.ToString(CultureInfo.InvariantCulture))
            : LtxTextTools.AddFieldToSection(text, sectionName, key, value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Окно «Настройка слотов»: правки идут во временные view-models; «Отмена» возвращает
    /// снимок раскладки, «ОК» пишет `equipSlotLayout[]` в проект и отмечает его изменённым.
    /// </summary>
    [RelayCommand]
    private void OpenEquipSlotSettings()
    {
        var snapshot = EquipSlotCatalog.Clone(_slotLayout);
        SelectedEquipSlot ??= EquipSlots.FirstOrDefault();

        var window = new EquipSlotsWindow(this) { Owner = _shell.ActiveWindow };
        if (window.ShowDialog() != true)
        {
            _slotLayout = snapshot;
            RefreshEquipSlots();
            return;
        }

        _slotLayout = EquipSlotCatalog.Normalize(EquipSlots.Select(slot => slot.ToLayout()), _project.Series);
        _project.EquipSlotLayout = EquipSlotCatalog.Clone(_slotLayout);

        // Удалённые в редакторе слоты больше не должны хранить назначения предметов.
        // Скрытые/legacy-ключи, которых не было в раскладке, не трогаем.
        var keys = _slotLayout.Select(slot => slot.Key).ToHashSet(StringComparer.Ordinal);
        var removed = snapshot.Select(slot => slot.Key)
            .Where(key => !keys.Contains(key))
            .ToList();
        foreach (var key in removed) _project.EquipSlots.Remove(key);

        RefreshEquipSlots();
        _shell.OnSectionEdited(LocalizationSource.Get("itemSlotSaved"));
    }

    /// <summary>Добавить слот справа от текущей раскладки (дальше двигается в редакторе).</summary>
    [RelayCommand]
    private void AddEquipSlot()
    {
        var key = EquipSlotCatalog.NextKey(EquipSlots.Select(slot => slot.Key));
        var x = EquipSlots.Count == 0 ? 10 : EquipSlots.Max(slot => slot.X + slot.Width) + 10;
        var slot = new ItemEquipSlotViewModel(
            this, key, LocalizationSource.Get("itemSlotNew"), "", null, x, 10, 80, 80);
        EquipSlots.Add(slot);
        SelectedEquipSlot = slot;
        RefreshSlotCanvas();
        OnPropertyChanged(nameof(HasEquipSlots));
    }

    /// <summary>Удалить выбранный слот (назначение убирается при «ОК»).</summary>
    [RelayCommand]
    private void RemoveEquipSlot()
    {
        var slot = SelectedEquipSlot;
        if (slot == null) return;

        var index = EquipSlots.IndexOf(slot);
        EquipSlots.Remove(slot);
        SelectedEquipSlot = EquipSlots.Count == 0
            ? null
            : EquipSlots[Math.Min(index, EquipSlots.Count - 1)];
        RefreshSlotCanvas();
        OnPropertyChanged(nameof(HasEquipSlots));
    }

    /// <summary>`patch({equipSlots})`: назначить выбранную секцию слоту экипировки.</summary>
    public void AssignSlot(string slotKey)
    {
        if (_selectedSection == null) return;
        _project.EquipSlots[slotKey] = _selectedSection.Name;
        RefreshEquipSlots();
        NotifyDetails();
        _shell.OnSectionEdited(LocalizationSource.Get("itemSlotAssigned") + ": " + _selectedSection.Name);
    }

    // --- экспорт (exportItemsLtx / exportItemsCsv из ItemsTab.js) ---

    /// <summary>`exportItemsCsv`: `section;key;value` в UTF-8 с BOM (`download(..., true)`).</summary>
    [RelayCommand]
    private void ExportCsv()
    {
        var path = _dialogs.PickSaveFile(LocalizationSource.Get("itemExportCsv"), "items.csv",
            "CSV (*.csv)|*.csv|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllBytes(path, new UTF8Encoding(true).GetBytes(ItemEditor.BuildCsv(_sections)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }

        _shell.StatusText = LocalizationSource.Get("itemExportCsvDone");
        _shell.StatusDetail = path;
    }

    /// <summary>`exportItemsLtx`: сохранение `items.ltx` в кодировке экспорта проекта.</summary>
    [RelayCommand]
    private void ExportLtx()
    {
        var path = _dialogs.PickSaveFile(LocalizationSource.Get("itemExportLtx"), "items.ltx",
            "LTX (*.ltx)|*.ltx|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllBytes(path, GameEncoding.Encode(_project.ItemsLtx ?? "", _project.Settings.ExportEncoding));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }

        _shell.StatusText = LocalizationSource.Get("itemExportLtxDone");
        _shell.StatusDetail = path;
    }
}
