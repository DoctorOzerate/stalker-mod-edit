using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Вкладка «Скрипты» (этап 7.4). Порт `ScriptsTab.js`: список `.script`/`.lua` с поиском
/// и заменой, редактор с нумерацией строк, режим просмотра с подсветкой и свёрткой функций,
/// справочник API и сниппеты, проверка Lua, граф вызовов, diff и экспорт файла.
/// Логика — в <see cref="ScriptsViewModel"/>, View синхронизирует каретку и прокрутку.
/// </summary>
public partial class ScriptsView : UserControl
{
    private ScriptsViewModel? _scripts;

    public ScriptsView()
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
        DataContextChanged += (_, _) => AttachScripts();

        EditorBox.AddHandler(ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(OnEditorScrollChanged));
    }

    /// <summary>Подписаться на запросы каретки после перехода/вставки (`jumpToDefinition`).</summary>
    private void AttachScripts()
    {
        if (_scripts != null) _scripts.CaretRequested -= OnCaretRequested;

        _scripts = (DataContext as ShellViewModel)?.Scripts;
        if (_scripts != null) _scripts.CaretRequested += OnCaretRequested;

        RestorePaneWidth();
    }

    /// <summary>Восстановить сохранённую ширину левой панели списка.</summary>
    private void RestorePaneWidth()
    {
        if (DataContext is ShellViewModel shell)
            ScriptsPaneColumn.Width = new GridLength(shell.ScriptsPaneWidth);
    }

    /// <summary>Перетаскивание `GridSplitter`: запомнить ширину панели в настройках.</summary>
    private void OnPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        shell.ScriptsPaneWidth = ScriptsPaneColumn.ActualWidth;
        shell.SavePaneWidths();
    }

    /// <summary>После переключения вкладки вернуть правую панель к началу редактора.</summary>
    private void ResetDetailsScroll() =>
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(DetailsScroll.ScrollToTop));

    /// <summary>Каретка в редакторе (`selStart`) — для вставки API/сниппета в позицию курсора.</summary>
    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_scripts?.Selected == null) return;

        _scripts.CaretIndex = EditorBox.CaretOffset;
        _scripts.SelectionLength = EditorBox.SelectionLength;
    }

    /// <summary>Нумерация строк прокручивается вместе с редактором (внутренний `ScrollViewer`).</summary>
    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e) =>
        LineNumbersScroll.ScrollToVerticalOffset(e.VerticalOffset);

    /// <summary>Запомнить каретку после правки, чтобы вставка API/сниппета шла в неё.</summary>
    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_scripts?.Selected == null) return;

        _scripts.CaretIndex = EditorBox.CaretOffset;
        _scripts.SelectionLength = EditorBox.SelectionLength;
    }

    /// <summary>Переход к определению функции: показать редактор и поставить каретку.</summary>
    private void OnCaretRequested(object? sender, ScriptJumpEventArgs e)
    {
        if (_scripts == null || !ReferenceEquals(e.Item, _scripts.Selected)) return;

        // Ждём, пока привязка обновит `TextBox` после `LoadText` (формат/вставка).
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            EditorBox.Focus();
            EditorBox.SelectRange(e.Offset, 0);
            EditorBox.ScrollToEditorLine(Math.Max(0, e.Line));
        }));
    }
}
