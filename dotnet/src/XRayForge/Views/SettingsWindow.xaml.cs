using System.Windows;
using System.Windows.Input;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Окно «Настройки программы» (`programSettings`): тема, акцент, плотность, язык,
/// автосейв, шрифт, синхронизация, плагины и горячие клавиши.
/// «Сохранить» пишет профиль, «Отмена» возвращает сохранённые значения.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel? _model;

    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(ShellViewModel shell)
    {
        InitializeComponent();

        _model = new SettingsViewModel(shell);
        DataContext = _model;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _model?.Save();
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _model?.Cancel();
        Close();
    }
}
