using System.Windows;
using System.Windows.Input;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Окно «Копии» (`backups`/`saveBackups`): история точек сохранения профиля,
/// восстановление и удаление снимка.
/// </summary>
public partial class BackupsWindow : Window
{
    public BackupsWindow() => InitializeComponent();

    public BackupsWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }
}
