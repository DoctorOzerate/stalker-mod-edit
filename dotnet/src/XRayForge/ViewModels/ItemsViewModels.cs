using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Ltx;
using XRayForge.Core.Projects;
using XRayForge.Core.Suggestions;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>Группа левой панели: заголовок `Категория · N` и карточки секций.</summary>
public sealed class ItemGroupViewModel
{
    public ItemGroupViewModel(string header, IReadOnlyList<ItemSectionViewModel> items)
    {
        Header = header;
        Items = items;
    }

    public string Header { get; }
    public IReadOnlyList<ItemSectionViewModel> Items { get; }
}

/// <summary>Карточка LTX-секции в списке предметов (`s.name` + `N полей · :parents`).</summary>
public sealed partial class ItemSectionViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public ItemSectionViewModel(ItemsViewModel owner, LtxSection model)
    {
        _owner = owner;
        Model = model;
        RefreshDerived();
    }

    public LtxSection Model { get; }

    public string Name => Model.Name;

    /// <summary>`classifyItem(section)` — бейдж категории в заголовке редактора.</summary>
    public string Category => ItemCatalog.ClassifyItem(Model);

    /// <summary>`N полей` + ` · :parent1,parent2` — вторая строка карточки.</summary>
    public string Subtitle => Model.Entries.Count + " " + LocalizationSource.Get("itemFields") +
        (Model.Parents.Count > 0 ? " · :" + string.Join(",", Model.Parents) : "");

    /// <summary>Выделение карточки в списке (`selItem` совпадает с секцией редактора).</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Пересчитать производные подписи после правки модели.</summary>
    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private void Select() => _owner.SelectByName(Name);

    [RelayCommand]
    private void Delete() => _owner.DeleteSection(this);
}

/// <summary>Кнопка родительской секции: отсутствующая подсвечивается красным и не нажимается.</summary>
public sealed partial class ItemParentViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public ItemParentViewModel(ItemsViewModel owner, string name, bool exists)
    {
        _owner = owner;
        Name = name;
        Exists = exists;
    }

    public string Name { get; }
    public bool Exists { get; }

    [RelayCommand]
    private void Select() => _owner.SelectByName(Name);
}

/// <summary>Чип «Быстрые поля · категория»: зелёный при наличии поля, `＋` при отсутствии.</summary>
public sealed partial class ItemQuickFieldViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public ItemQuickFieldViewModel(ItemsViewModel owner, string key, bool present)
    {
        _owner = owner;
        Key = key;
        IsPresent = present;
        Tooltip = (ItemCatalog.FieldDescriptions.TryGetValue(key, out var description) ? description : key) +
            (present ? " · " + LocalizationSource.Get("itemFieldPresent") : " · " + LocalizationSource.Get("itemFieldAdd"));
    }

    public string Key { get; }
    public bool IsPresent { get; }
    public string Text => Key + (IsPresent ? " ✓" : " ＋");
    public string Tooltip { get; }

    [RelayCommand]
    private void Add() => _owner.AddQuickField(Key);
}

/// <summary>Строка поля редактора: собственное/унаследованное значение и действия.</summary>
public sealed partial class ItemFieldViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;
    private readonly bool _loading;

    [ObservableProperty] private string _value = "";
    [ObservableProperty] private bool _isOverridden;
    [ObservableProperty] private bool _hasError;

    public ItemFieldViewModel(
        ItemsViewModel owner, MergedEntry entry, bool locked, bool overridden,
        IReadOnlyList<SuggestionEntry> suggestions)
    {
        _owner = owner;
        Entry = entry;
        IsLocked = locked;
        _isOverridden = overridden;
        Suggestions = suggestions;

        _loading = true;
        _value = entry.Value ?? "";
        _loading = false;

        UpdateValidation();
        UpdateTexts();
    }

    public MergedEntry Entry { get; }
    public string Key => Entry.Key;
    public bool IsInherited => Entry.Inherited;
    public bool IsLocked { get; }
    public IReadOnlyList<SuggestionEntry> Suggestions { get; }
    public bool IsEditable => !IsInherited && !IsLocked;
    public bool CanUnlock => IsInherited;
    public bool CanRemove => !IsInherited && !IsLocked;
    public bool ShowLock => !IsInherited && IsLocked;

    public string Label => (IsInherited ? "🔒 " : (IsOverridden ? "✏️ " : "")) + Key +
        (IsInherited && !string.IsNullOrEmpty(Entry.From) ? " ← " + Entry.From : "");

    public string Tooltip => (ItemCatalog.FieldDescriptions.TryGetValue(Key, out var description) ? description : "") +
        (IsInherited && !string.IsNullOrEmpty(Entry.From) ? " · " + LocalizationSource.Get("itemInheritedFrom") + " " + Entry.From : "") +
        (IsOverridden ? " · " + LocalizationSource.Get("itemOverridden") : "");

    partial void OnValueChanged(string value)
    {
        if (_loading) return;
        _owner.SetItemField(this, value);
    }

    /// <summary>Обновить рамку валидации и подписи после правки/смены локали.</summary>
    public void RefreshComputed(bool overridden)
    {
        IsOverridden = overridden;
        UpdateValidation();
        UpdateTexts();
    }

    private void UpdateValidation() => HasError = Key switch
    {
        "inv_grid_x" or "inv_grid_y" => !IconGrid.IsValidGridValue(Value),
        "inv_grid_width" or "inv_grid_height" => !IconGrid.IsValidGridWH(Value),
        _ => false
    };

    private void UpdateTexts()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(CanUnlock));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(ShowLock));
    }
}

/// <summary>Строка дерева наследования: цепочка предков и вложенные дочерние секции.</summary>
public sealed partial class ItemTreeRowViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public ItemTreeRowViewModel(ItemsViewModel owner, string name, int depth, string glyph, bool current)
    {
        _owner = owner;
        Name = name;
        Depth = depth;
        Glyph = glyph;
        IsCurrent = current;
        Margin = new Thickness(depth * 14, 0, 0, 1);
        Text = depth > 0 && glyph == "↳" ? "↳ " + name : name + (current ? " " + LocalizationSource.Get("itemCurrent") : "");
    }

    public string Name { get; }
    public int Depth { get; }
    public string Glyph { get; }
    public bool IsCurrent { get; }
    public Thickness Margin { get; }
    public string Text { get; }
    public bool IsAccent => IsCurrent;

    [RelayCommand]
    private void Select() => _owner.SelectByName(Name);
}

/// <summary>Кнопка секции, ссылающейся на текущую (`immunities_sect` и т.п.).</summary>
public sealed partial class ItemReferenceViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public ItemReferenceViewModel(ItemsViewModel owner, LtxSection section)
    {
        _owner = owner;
        Name = section.Name;
    }

    public string Name { get; }

    [RelayCommand]
    private void Select() => _owner.SelectByName(Name);
}

/// <summary>Ячейка сетки атласа 16×16; клик задаёт `inv_grid_x/y`.</summary>
public sealed partial class AtlasCellViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public AtlasCellViewModel(ItemsViewModel owner, int x, int y, ImageSource? image, bool active)
    {
        _owner = owner;
        X = x;
        Y = y;
        Image = image;
        IsActive = active;
        Tooltip = LocalizationSource.Get("itemAtlasCell") + " " + x + "×" + y +
            (active ? " " + LocalizationSource.Get("itemAtlasCurrent") : "");
    }

    public int X { get; }
    public int Y { get; }
    public ImageSource? Image { get; }
    public bool IsActive { get; }
    public string Tooltip { get; }

    [RelayCommand]
    private void Pick() => _owner.SetGrid(X, Y);
}

/// <summary>
/// Слот экипировки: назначение текущей секции плюс редактируемая геометрия раскладки
/// (координаты/размер/подпись). Геометрия меняется в окне «Настройка слотов» и сохраняется
/// в проект только по «ОК».
/// </summary>
public sealed partial class ItemEquipSlotViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;
    private bool _loading;

    public ItemEquipSlotViewModel(
        ItemsViewModel owner, string key, string label, string name, ImageSource? icon,
        double x, double y, double width, double height)
    {
        _owner = owner;
        Key = key;
        Name = name;
        Icon = icon;
        _loading = true;
        _label = label;
        _x = x;
        _y = y;
        _width = width;
        _height = height;
        _loading = false;
    }

    /// <summary>Ключ слота (`weapon`, `armor`, `helmet`, а также `knife`/`pistol`/…).</summary>
    public string Key { get; }

    public string Name { get; }
    public string DisplayName => string.IsNullOrEmpty(Name) ? LocalizationSource.Get("itemSlotEmpty") : Name;
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon != null;

    /// <summary>Подпись над слотом; правится в окне настройки.</summary>
    [ObservableProperty] private string _label;

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;

    partial void OnLabelChanged(string value) => GeometryChanged();

    partial void OnXChanged(double value) => GeometryChanged();

    partial void OnYChanged(double value) => GeometryChanged();

    partial void OnWidthChanged(double value) => GeometryChanged();

    partial void OnHeightChanged(double value) => GeometryChanged();

    /// <summary>Снимок для сохранения в `equipSlotLayout[]`.</summary>
    public EquipSlotLayout ToLayout() => new()
    {
        Key = Key,
        Label = Label,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height
    };

    private void GeometryChanged()
    {
        if (_loading) return;
        _owner.OnEquipSlotChanged();
    }

    [RelayCommand]
    private void Assign() => _owner.AssignSlot(Key);
}
