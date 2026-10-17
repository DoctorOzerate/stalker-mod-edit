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

    /// <summary>Сетка атласа раскрыта кнопкой «Выбрать иконку».</summary>
    [ObservableProperty] private bool _isIconPickerOpen;

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

        var section = _selectedSection;
        if (section == null)
        {
            InheritanceSummary = "";
            BrokenParentsText = "";
            QuickFieldsTitle = "";
            ReferencesTitle = "";
            AtlasTitle = "";
            GridCoordsText = "";
            SelectedItemIcon = null;
            IsIconPickerOpen = false;
            _atlasTexture = null;
            _atlasBitmap = null;
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

        var cellSize = _project.Atlas.CellSize > 0 ? _project.Atlas.CellSize : 50;
        var cellHeight = _project.Atlas.CellHeight > 0 ? _project.Atlas.CellHeight : cellSize;
        var curX = GridValue(section, "inv_grid_x");
        var curY = GridValue(section, "inv_grid_y");
        var iconWidth = GridSize(section, "inv_grid_width");
        var iconHeight = GridSize(section, "inv_grid_height");
        GridCoordsText = "inv_grid_x=" + curX + " · inv_grid_y=" + curY +
                         " · " + iconWidth + "×" + iconHeight;

        if (_atlasTexture == null || _atlasTexture.Width is not > 0 || _atlasTexture.Height is not > 0)
        {
            IsIconPickerOpen = false;
            return;
        }

        var width = _atlasTexture.Width.Value;
        var height = _atlasTexture.Height.Value;
        var cols = Math.Max(1, (int)Math.Floor(width / cellSize));
        var rows = Math.Max(1, (int)Math.Floor(height / cellHeight));
        AtlasColumns = cols;
        AtlasTitle = LocalizationSource.Get("itemAtlasTitle") + " · " + _atlasTexture.Name +
                     " (" + cols + "×" + rows + ")";

        _atlasBitmap = TexturePreview.Load(_atlasTexture.DataUrl);
        if (_atlasBitmap != null && curX >= 0 && curY >= 0)
        {
            SelectedItemIcon = TexturePreview.Crop(
                _atlasBitmap, curX * cellSize, curY * cellHeight,
                iconWidth * cellSize, iconHeight * cellHeight,
                _atlasBitmap.PixelWidth, _atlasBitmap.PixelHeight);
        }

        if (IsIconPickerOpen) RefreshAtlasCells(section);
    }

    /// <summary>Построить сетку ячеек для раскрытого выбора иконки.</summary>
    private void RefreshAtlasCells(LtxSection? section)
    {
        AtlasCells.Clear();
        if (section == null || _atlasBitmap == null || AtlasColumns <= 0) return;

        var cellSize = _project.Atlas.CellSize > 0 ? _project.Atlas.CellSize : 50;
        var cellHeight = _project.Atlas.CellHeight > 0 ? _project.Atlas.CellHeight : cellSize;
        var rows = Math.Max(1, (int)Math.Floor(_atlasBitmap.PixelHeight / cellHeight));
        var cellWidth = _atlasBitmap.PixelWidth / (double)AtlasColumns;
        var cellHeightPx = _atlasBitmap.PixelHeight / (double)rows;
        var curX = GridValue(section, "inv_grid_x");
        var curY = GridValue(section, "inv_grid_y");

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < AtlasColumns; x++)
            {
                var image = TexturePreview.Crop(
                    _atlasBitmap, x * cellWidth, y * cellHeightPx, cellWidth, cellHeightPx,
                    _atlasBitmap.PixelWidth, _atlasBitmap.PixelHeight);
                AtlasCells.Add(new AtlasCellViewModel(this, x, y, image, x == curX && y == curY));
            }
        }
    }

    private static int GridValue(LtxSection section, string key)
    {
        var entry = section.Entries.FirstOrDefault(item => item.Key == key);
        return entry != null && IconGrid.IsValidGridValue(entry.Value) ? (int)ParseGrid(entry.Value) : -1;
    }

    private static int GridSize(LtxSection section, string key)
    {
        var entry = section.Entries.FirstOrDefault(item => item.Key == key);
        return entry != null && IconGrid.IsValidGridWH(entry.Value)
            ? Math.Max(1, (int)ParseGrid(entry.Value))
            : 1;
    }

    /// <summary>Слоты `weapon`/`armor` (+`helmet` для ЗП) с вырезкой иконки из атласа.</summary>
    private void RefreshEquipSlots()
    {
        EquipSlots.Clear();
        var slots = new List<(string Key, string Label)>
        {
            ("weapon", LocalizationSource.Get("itemSlotWeapon")),
            ("armor", LocalizationSource.Get("itemSlotArmor"))
        };
        if (IsCopSeries) slots.Add(("helmet", LocalizationSource.Get("itemSlotHelmet")));

        var cellSize = _project.Atlas.CellSize > 0 ? _project.Atlas.CellSize : 50;
        foreach (var (key, label) in slots)
        {
            var name = _project.EquipSlots.TryGetValue(key, out var value) ? value : "";
            ImageSource? icon = null;
            if (!string.IsNullOrEmpty(name) && _itemsByName.TryGetValue(name.ToLowerInvariant(), out var section))
            {
                var gx = section.Entries.FirstOrDefault(entry => entry.Key == "inv_grid_x");
                var gy = section.Entries.FirstOrDefault(entry => entry.Key == "inv_grid_y");
                if (_atlasTexture?.DataUrl is { Length: > 0 } dataUrl &&
                    _atlasTexture.Width is > 0 && _atlasTexture.Height is > 0 &&
                    gx != null && gy != null &&
                    IconGrid.IsValidGridValue(gx.Value) && IconGrid.IsValidGridValue(gy.Value))
                {
                    var x = ParseGrid(gx.Value) * cellSize;
                    var y = ParseGrid(gy.Value) * cellSize;
                    icon = TexturePreview.Crop(
                        dataUrl, x, y, cellSize, cellSize,
                        _atlasTexture.Width.Value, _atlasTexture.Height.Value);
                }
            }

            EquipSlots.Add(new ItemEquipSlotViewModel(this, key, label, name, icon, cellSize));
        }
    }

    private static double ParseGrid(string value) =>
        double.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : -1;

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
            File.WriteAllBytes(path, EncodeForExport(_project.ItemsLtx ?? "", _project.Settings.ExportEncoding));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowMessage(LocalizationSource.Get("appTitle"), e.Message);
            return;
        }

        _shell.StatusText = LocalizationSource.Get("itemExportLtxDone");
        _shell.StatusDetail = path;
    }

    /// <summary>Кодировка экспорта проекта: UTF-8 без BOM, UTF-16 или CP1251.</summary>
    private static byte[] EncodeForExport(string text, string? encoding) => encoding?.ToLowerInvariant() switch
    {
        "utf-8" or "utf8" => new UTF8Encoding(false).GetBytes(text),
        "utf-16" or "utf-16le" or "utf-16-le" or "unicode" => new UnicodeEncoding(false, false).GetBytes(text),
        "utf-16be" or "utf-16-be" or "bigendianunicode" => new UnicodeEncoding(true, false).GetBytes(text),
        _ => GameEncoding.EncodeCp1251(text)
    };
}
