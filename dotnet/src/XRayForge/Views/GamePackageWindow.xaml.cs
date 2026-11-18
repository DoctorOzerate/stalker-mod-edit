using System.Windows;
using System.Windows.Input;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Окно «Игровой пакет gamedata»: имена `dialogs_*.xml`, набор локалей, манифест
/// и предпросмотр файлов перед записью ZIP. «Собрать ZIP» возвращает `DialogResult=true`,
/// запись выполняет оболочка по готовому <see cref="GamePackageViewModel.Plan"/>.
/// </summary>
public partial class GamePackageWindow : Window
{
    private readonly GamePackageViewModel? _model;

    public GamePackageWindow() => InitializeComponent();

    public GamePackageWindow(GamePackageViewModel model)
    {
        InitializeComponent();

        _model = model;
        DataContext = model;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }

    private void OnBuild(object sender, RoutedEventArgs e)
    {
        if (_model == null || !_model.CanBuild) return;
        DialogResult = true;
    }
}
