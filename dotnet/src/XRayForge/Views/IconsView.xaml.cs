using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XRayForge.Controls;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Вкладка «Иконки» (этап 7.3). Порт `renderIconsTab` из `IconsTab.js`:
/// DDS-атлас с сеткой, выделением, зумом/панорамой, сравнением, лупой и коллекциями.
/// Логика — в <see cref="IconsViewModel"/>.
/// </summary>
public partial class IconsView : UserControl
{
    public IconsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RestorePaneWidth();
            ResetScroll();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is not true) return;

            RestorePaneWidth();
            ResetScroll();
        };
    }

    private IconsViewModel? Model => (DataContext as ShellViewModel)?.Icons;

    /// <summary>Восстановить сохранённую ширину левой панели списка.</summary>
    private void RestorePaneWidth()
    {
        if (DataContext is ShellViewModel shell)
            IconsPaneColumn.Width = new GridLength(shell.IconsPaneWidth);
    }

    /// <summary>Перетаскивание `GridSplitter`: запомнить ширину панели в настройках.</summary>
    private void OnPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        shell.IconsPaneWidth = IconsPaneColumn.ActualWidth;
        shell.SavePaneWidths();
    }

    /// <summary>`onSelectRegion`: обновить прямоугольник выделения.</summary>
    private void OnAtlasSelectionChanged(object sender, AtlasSelectionEventArgs e) =>
        Model?.SetSelectionFromCanvas(e.X, e.Y, e.Width, e.Height);

    /// <summary>`onSelectEnd`: зафиксировать выделение в истории undo/redo.</summary>
    private void OnAtlasSelectionCommitted(object sender, AtlasSelectionEventArgs e) =>
        Model?.CommitSelection();

    /// <summary>`onHover`: ячейка под курсором и лупа.</summary>
    private void OnAtlasHoverChanged(object sender, AtlasHoverEventArgs e) =>
        Model?.SetHoverFromCanvas(e.HasCell, e.X, e.Y);

    /// <summary>`onZoom`: Ctrl+колесо меняет масштаб на 0.25.</summary>
    private void OnAtlasZoomRequested(object sender, AtlasZoomEventArgs e) =>
        Model?.ZoomByWheel(e.Delta);

    /// <summary>
    /// При появлении вкладки вернуть холст к левому верхнему углу: WPF может
    /// восстановить прокрутку от предыдущего содержимого `ContentControl`.
    /// </summary>
    private void ResetScroll() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            AtlasScroll.ScrollToHome();
            AtlasViewer?.Focus();
        }));
}
