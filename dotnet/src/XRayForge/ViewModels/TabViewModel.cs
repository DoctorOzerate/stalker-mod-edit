using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Ui;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка левой навигации: запись `NavigationCatalog` (id, глиф, ключ локализации, акцент)
/// плюс счётчик сущностей и признак активности.
/// </summary>
public sealed partial class TabViewModel : ObservableObject
{
    public TabViewModel(NavigationItem item)
    {
        Id = item.Id;
        Glyph = item.Glyph;
        LabelKey = item.LabelKey;
        Accent = item.Accent;
        IsAccentState = item.IsAccentState;
        IsOverview = item.Id == NavigationCatalog.OverviewId;
    }

    public string Id { get; }

    /// <summary>Глиф вкладки (`TABS[].icon` в App.js).</summary>
    public string Glyph { get; }

    /// <summary>Ключ локализации подписи (`tabDialogues`, …).</summary>
    public string LabelKey { get; }

    /// <summary>HEX-акцент темы или акцентного состояния (Olive/Hazard).</summary>
    public string Accent { get; }

    /// <summary>Акцентное состояние раздела (`olive`/`hazard` по референсам).</summary>
    public bool IsAccentState { get; }

    /// <summary>Вкладка «Обзор» — единственная наполненная на этапе 5.</summary>
    public bool IsOverview { get; }

    /// <summary>Раздел-заглушка (наполняется на этапах 6–7).</summary>
    public bool IsStub => !IsOverview;

    /// <summary>Подпись вкладки на текущей локали (обновляет `ShellViewModel.RefreshLocalization`).</summary>
    [ObservableProperty]
    private string _label = "";

    /// <summary>Комбинация перехода на вкладку (`Ctrl+1…9`), подсказка в навигации.</summary>
    public string Hotkey { get; init; } = "";

    /// <summary>HEX-акцент глифа: акцент темы или акцентное состояние (Olive/Hazard).</summary>
    public string AccentHex => ThemeCatalog.Theme(Accent).Tokens.AccentMain;

    /// <summary>Счётчик сущностей раздела.</summary>
    [ObservableProperty]
    private int _count;

    /// <summary>Вкладка активна (подсветка в навигации).</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Подпись счётчика (пусто при нуле — в App.js счётчик не рисуется).</summary>
    public string CountText => Count > 0 ? Count.ToString() : "";

    partial void OnCountChanged(int value) => OnPropertyChanged(nameof(CountText));
}
