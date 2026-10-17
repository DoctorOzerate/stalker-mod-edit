using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace XRayForge.Views;

/// <summary>
/// Вкладка «Персонажи» (этап 7.1). Порт `renderCharactersTab` из `CharactersTab.js`:
/// список с поиском и группами, редактор `specific_character`, карточка `game_relations`,
/// связанные диалоги, diff с `original` и исходный XML. Логика — в `CharactersViewModel`.
/// </summary>
public partial class CharactersView : UserControl
{
    public CharactersView()
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
    /// из-за чего первые поля оказываются выше видимой области.
    /// </summary>
    private void ResetDetailsScroll() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(DetailsScroll.ScrollToTop));
}
