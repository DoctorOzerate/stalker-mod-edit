using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Projects;

namespace XRayForge.ViewModels;

/// <summary>Закреплённая иконка коллекции (`pinCollections[name][]`).</summary>
public sealed partial class PinnedIconViewModel : ObservableObject
{
    private readonly IconsViewModel _owner;

    public PinnedIconViewModel(IconsViewModel owner, PinnedIcon model)
    {
        _owner = owner;
        Model = model;
    }

    public PinnedIcon Model { get; }

    public string Display =>
        Model.Name + " · " + Model.X.ToString("0") + "×" + Model.Y.ToString("0") +
        " (" + Model.W.ToString("0") + "×" + Model.H.ToString("0") + ")";

    [RelayCommand]
    private void Select() => _owner.SelectPinned(this);

    [RelayCommand]
    private void Remove() => _owner.RemovePinned(this);
}

/// <summary>Ячейка-дубликат атласа: кнопка переводит выделение на неё.</summary>
public sealed partial class AtlasDuplicateCellViewModel : ObservableObject
{
    private readonly IconsViewModel _owner;

    public AtlasDuplicateCellViewModel(IconsViewModel owner, int x, int y)
    {
        _owner = owner;
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }
    public string Display => X + "×" + Y;

    [RelayCommand]
    private void Select() => _owner.SelectCell(X, Y);
}
