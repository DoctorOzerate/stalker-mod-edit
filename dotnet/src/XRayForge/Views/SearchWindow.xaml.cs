using System.Windows;
using System.Windows.Input;
using XRayForge.Core.Ui;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Окно глобального поиска (`globalSearch`, `Ctrl+K`): фильтр по всем сущностям проекта
/// (`ProjectSearch`), переход к результату — `ShellViewModel.OpenHit`.
/// </summary>
public partial class SearchWindow : Window
{
    private readonly ShellViewModel? _shell;

    public SearchWindow() => InitializeComponent();

    public SearchWindow(ShellViewModel shell)
    {
        InitializeComponent();

        _shell = shell;
        DataContext = shell;
        Loaded += (_, _) => Query.Focus();
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }

    private void OnHitDoubleClick(object sender, MouseButtonEventArgs e) => Open(Hits.SelectedItem as ProjectSearchHit);

    private void OnOpenSelected(object sender, RoutedEventArgs e) => Open(Hits.SelectedItem as ProjectSearchHit);

    /// <summary>Перейти к выбранному результату и закрыть окно.</summary>
    private void Open(ProjectSearchHit? hit)
    {
        if (hit == null || _shell == null) return;

        _shell.OpenHitCommand.Execute(hit);
        Close();
    }
}
