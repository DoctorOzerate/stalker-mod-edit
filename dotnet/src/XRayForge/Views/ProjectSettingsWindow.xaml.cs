using System.Windows;
using System.Windows.Input;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Окно «Настройки проекта» (`projectSettings`): имя, идентификатор диалога, серия игры,
/// кодировки, показ узлов и путь к gamedata. Правки пишутся в проект при «ОК».
/// </summary>
public partial class ProjectSettingsWindow : Window
{
    private readonly ProjectSettingsViewModel? _model;

    public ProjectSettingsWindow() => InitializeComponent();

    public ProjectSettingsWindow(ProjectSettingsViewModel model)
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

    private void OnOk(object sender, RoutedEventArgs e)
    {
        _model?.Commit();
        DialogResult = true;
    }
}
