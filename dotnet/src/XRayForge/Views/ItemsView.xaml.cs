using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
        Loaded += (_, _) => ResetDetailsScroll();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) ResetDetailsScroll();
        };
    }

    /// <summary>
    /// При появлении вкладки вернуть правую панель к заголовку редактора: WPF может
    /// унаследовать/восстановить прокрутку от предыдущего содержимого `ContentControl`,
    /// из-за чего карточки начинаются выше видимой области.
    /// </summary>
    private void ResetDetailsScroll() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(DetailsScroll.ScrollToTop));
}
