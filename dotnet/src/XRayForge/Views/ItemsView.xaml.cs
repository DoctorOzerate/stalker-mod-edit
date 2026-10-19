using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Вкладка «Предметы» (этап 7.2). Порт `renderItemsTab` из `ItemsTab.js`:
/// список LTX-секций с фильтрами и группами, редактор секции с наследованием,
/// атласом, быстрыми полями, деревом ссылок и слотами экипировки.
/// Логика — в `ItemsViewModel`.
/// </summary>
public partial class ItemsView : UserControl
{
    public ItemsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RestorePaneWidth();
            ResetDetailsScroll();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is not true) return;

            RestorePaneWidth();
            ResetDetailsScroll();
        };
    }

    /// <summary>Восстановить сохранённую ширину левой панели списка.</summary>
    private void RestorePaneWidth()
    {
        if (DataContext is ShellViewModel shell)
            ItemsPaneColumn.Width = new GridLength(shell.ItemsPaneWidth);
    }

    /// <summary>Перетаскивание `GridSplitter`: запомнить ширину панели в настройках.</summary>
    private void OnPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        shell.ItemsPaneWidth = ItemsPaneColumn.ActualWidth;
        shell.SavePaneWidths();
    }

    /// <summary>
    /// При появлении вкладки вернуть правую панель к заголовку редактора: WPF может
    /// унаследовать/восстановить прокрутку от предыдущего содержимого `ContentControl`,
    /// из-за чего карточки начинаются выше видимой области.
    /// </summary>
    private void ResetDetailsScroll() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(DetailsScroll.ScrollToTop));
}
