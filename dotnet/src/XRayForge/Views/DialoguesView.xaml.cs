using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XRayForge.Core.Graph;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Вкладка «Диалоги» — редактор графа диалога (этап 6). Порт `renderDialoguesTab` из `DialoguesTab.js`:
/// палитра узлов, канвас со связями, мини-карта, свойства реплики, проверка графа.
/// Шаг 6.2: размер холста для «В центр» и выбор узла кликом (`setSelected`).
/// Шаг 6.3: обработчики нажатия карточки/канваса и `mousemove`/`mouseup` из App.js — перенос узла
/// и группы, панорама, рамка выделения (Shift+drag) и масштаб колесом с Ctrl.
/// Шаг 6.4: клики по портам (`portClick`) и по hit-зоне связи (`deleteEdge`); такие нажатия
/// обрабатывают сами элементы, поэтому холст их не перехватывает.
/// Шаг 6.6: клик по мини-карте (`onClick` её `svg`) центрирует холст на выбранной точке.
/// </summary>
public partial class DialoguesView : UserControl
{
    public DialoguesView() => InitializeComponent();

    /// <summary>Размер холста нужен «В центр»: сдвиг считает `GraphEditing.CenterPan`.</summary>
    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        shell.DialogueGraph.ViewportWidth = e.NewSize.Width;
        shell.DialogueGraph.ViewportHeight = e.NewSize.Height;
    }

    /// <summary>
    /// Нажатие кнопки мыши на холсте (`onMouseDown` карточки и канваса в JS): левая — выделение,
    /// перенос узла/группы, рамка выделения (Shift) или панорама; средняя — всегда панорама.
    /// Мышь захватывается: это аналог window-слушателей `move`/`up`, которые в JS ловят курсор
    /// и за пределами холста.
    /// </summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Middle)) return;
        // Порт узла или hit-зона связи обрабатывают нажатие сами (`portClick`/`deleteEdge` в App.js):
        // холст не начинает перенос/панораму и не перехватывает мышь.
        if (IsInteractiveHit(e.OriginalSource as DependencyObject)) return;

        var graph = shell.DialogueGraph;
        // Экранные координаты холста (без pan/zoom) — аналог `clientX/clientY` в JS; координаты слоя
        // графа нужны рамке выделения (в JS их считает `onMouseDown` канваса).
        var screen = e.GetPosition(CanvasHost);

        if (e.ChangedButton == MouseButton.Middle)
        {
            graph.BeginPan(screen.X, screen.Y);
        }
        else
        {
            var point = e.GetPosition(GraphLayer);
            graph.BeginCanvasInteraction(FindNodeCard(e.OriginalSource as DependencyObject),
                Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), screen.X, screen.Y, point.X, point.Y);
        }

        // Захватываем именно тот слой, по которому нажали: пустая область вне уехавшего
        // графа — это GraphCanvasBackground, а карточки/связи — GraphLayer.
        if (sender is UIElement element) element.CaptureMouse();
        else GraphLayer.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>`mousemove`: перенос узла/группы, панорама холста или растягивание рамки выделения.</summary>
    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        var graph = shell.DialogueGraph;
        if (!graph.IsDragging) return;

        var screen = e.GetPosition(CanvasHost);
        var point = e.GetPosition(GraphLayer);
        graph.DragTo(screen.X, screen.Y, point.X, point.Y);
    }

    /// <summary>
    /// `mouseup`: завершение переноса/панорамы; рамка выделения отбирает накрытые узлы.
    /// Холст не трогает отпускание, если он не начинал перетаскивание и не захватывал мышь:
    /// клик по порту/связи должен дойти до самой кнопки. Иначе `PreviewMouseUp` помечается
    /// обработанным, `Button.Click` не срабатывает, а кнопка остаётся в захвате мыши —
    /// внешне приложение «зависает» и связь не создаётся.
    /// </summary>
    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Middle)) return;
        if (DataContext is not ShellViewModel shell) return;

        var element = sender as UIElement;
        var captured = element?.IsMouseCaptured == true || GraphLayer.IsMouseCaptured;
        if (!shell.DialogueGraph.IsDragging && !captured) return;

        shell.DialogueGraph.EndDrag();
        if (element?.IsMouseCaptured == true) element.ReleaseMouseCapture();
        else if (GraphLayer.IsMouseCaptured) GraphLayer.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>
    /// Захват мыши потерян (переключение окна, захват другим элементом) — перетаскивание
    /// завершается, иначе холст остался бы «прилипшим» к курсору.
    /// </summary>
    private void OnCanvasLostCapture(object sender, MouseEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        shell.DialogueGraph.EndDrag();
    }

    /// <summary>
    /// Ctrl+колесо — масштаб холста (`onZoom` в App.js: ±0.1 за щелчок, границы 0.5…2).
    /// Без Ctrl колесо не перехватывается — жест остаётся системным.
    /// </summary>
    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        if (DataContext is not ShellViewModel shell) return;

        shell.DialogueGraph.ZoomByWheel(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    // --- мини-карта (шаг 6.6) ---

    /// <summary>
    /// Клик по мини-карте (`onClick` её `svg` в `DialoguesTab.js`): точка панели переводится
    /// в координаты графа, и холст центрируется на ней. Обработчик висит на `Canvas` мини-карты
    /// (176×112), поэтому её пиксели совпадают с пикселями раскладки `GraphMiniMap`.
    /// </summary>
    private void OnMiniMapClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;
        if (sender is not IInputElement element) return;

        var point = e.GetPosition(element);
        shell.DialogueGraph.MiniMapClick(point.X, point.Y);
        e.Handled = true;
    }

    // --- связи: порты и удаление кликом (шаг 6.4) ---

    /// <summary>Клик по порту входа (`portClick(n.id, 'in')`).</summary>
    private void OnInputPortClick(object sender, RoutedEventArgs e) =>
        PortClick(sender, GraphPortKind.In);

    /// <summary>Клик по порту выхода (`portClick(n.id, 'out')`).</summary>
    private void OnOutputPortClick(object sender, RoutedEventArgs e) =>
        PortClick(sender, GraphPortKind.Out);

    /// <summary>`portClick` карточки порта: первый клик взводит порт, второй создаёт связь.</summary>
    private void PortClick(object sender, GraphPortKind kind)
    {
        if (sender is not FrameworkElement { DataContext: GraphNodeViewModel node }) return;
        if (DataContext is not ShellViewModel shell) return;

        shell.DialogueGraph.PortClick(node, kind);
    }

    /// <summary>
    /// `deleteEdge`: клик по прозрачной hit-зоне связи (ширина 14, `pointerEvents: 'stroke'` в JS)
    /// удаляет связь вместе со сдвигом истории.
    /// </summary>
    private void OnEdgeDelete(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GraphEdgeViewModel edge }) return;
        if (DataContext is not ShellViewModel shell) return;

        if (shell.DialogueGraph.DeleteEdgeCommand.CanExecute(edge))
        {
            shell.DialogueGraph.DeleteEdgeCommand.Execute(edge);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Нажатие попало в интерактивный элемент холста: порт узла (`Button` в карточке) или
    /// прозрачную hit-зону связи (`Path`). Они обрабатывают мышь сами, поэтому холст их нажатие
    /// не разбирает (в JS порты гасят клик, а hit-зона связи ловит его вместо канваса).
    /// </summary>
    private static bool IsInteractiveHit(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase || source is Path) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
        }

        return false;
    }

    /// <summary>Карточка узла под курсором: подъём по визуальному дереву до DataContext узла.</summary>
    private static GraphNodeViewModel? FindNodeCard(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { DataContext: GraphNodeViewModel node }) return node;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
        }

        return null;
    }
}

