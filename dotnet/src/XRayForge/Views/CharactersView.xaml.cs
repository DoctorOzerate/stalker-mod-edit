using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XRayForge.ViewModels;

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
            CharactersPaneColumn.Width = new GridLength(shell.CharactersPaneWidth);
    }

    /// <summary>Перетаскивание `GridSplitter`: запомнить ширину панели в настройках.</summary>
    private void OnPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        shell.CharactersPaneWidth = CharactersPaneColumn.ActualWidth;
        shell.SavePaneWidths();
    }

    /// <summary>
    /// При появлении вкладки вернуть правую панель к заголовку редактора: WPF может
    /// унаследовать/восстановить прокрутку от предыдущего содержимого `ContentControl`,
    /// из-за чего первые поля оказываются выше видимой области.
    /// </summary>
    private void ResetDetailsScroll() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(DetailsScroll.ScrollToTop));
}
