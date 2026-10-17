using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XRayForge.Core.Imaging;

namespace XRayForge.Controls;

/// <summary>Аргументы изменения прямоугольника выделения атласа (в ячейках).</summary>
public sealed class AtlasSelectionEventArgs : EventArgs
{
    public AtlasSelectionEventArgs(int x, int y, int width, int height, bool committed)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Committed = committed;
    }

    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public bool Committed { get; }
}

/// <summary>Аргументы наведения на ячейку (`hoverCell` в JS).</summary>
public sealed class AtlasHoverEventArgs : EventArgs
{
    public AtlasHoverEventArgs(bool hasCell, int x, int y)
    {
        HasCell = hasCell;
        X = x;
        Y = y;
    }

    public bool HasCell { get; }
    public int X { get; }
    public int Y { get; }
}

/// <summary>Запрос масштаба (Ctrl+колесо): −1 или +1 шаг.</summary>
public sealed class AtlasZoomEventArgs : EventArgs
{
    public AtlasZoomEventArgs(int delta) => Delta = delta;

    public int Delta { get; }
}

/// <summary>
/// Холст атласа (`AtlasViewer` из `App.js`): изображение с nearest-neighbor, сетка,
/// сравнение с прозрачностью, выделение drag'ом и подсветка hover. Панорама — средней
/// кнопкой через родительский `ScrollViewer`, зум — Ctrl+колесо.
/// </summary>
public sealed class AtlasCanvas : FrameworkElement
{
    private static readonly Brush GridBrush = Frozen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)));
    private static readonly Brush HoverBrush = Frozen(new SolidColorBrush(Color.FromArgb(225, 34, 197, 94)));
    private static readonly Brush SelectionBrush = Frozen(new SolidColorBrush(Color.FromRgb(34, 197, 94)));

    private AtlasCell? _anchor;
    private bool _dragging;
    private bool _panning;
    private Point _panPointer;
    private double _panHorizontal;
    private double _panVertical;
    private int _hoverX = -1;
    private int _hoverY = -1;

    public AtlasCanvas()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        SnapsToDevicePixels = true;
        Focusable = true;
    }

    public static readonly DependencyProperty TextureProperty = DependencyProperty.Register(
        nameof(Texture), typeof(ImageSource), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender |
                                            FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty OverlayProperty = DependencyProperty.Register(
        nameof(Overlay), typeof(ImageSource), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayOpacityProperty = DependencyProperty.Register(
        nameof(OverlayOpacity), typeof(double), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender |
                                             FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty CellWidthProperty = DependencyProperty.Register(
        nameof(CellWidth), typeof(double), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(50.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CellHeightProperty = DependencyProperty.Register(
        nameof(CellHeight), typeof(double), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(50.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionXProperty = DependencyProperty.Register(
        nameof(SelectionX), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionYProperty = DependencyProperty.Register(
        nameof(SelectionY), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionWidthProperty = DependencyProperty.Register(
        nameof(SelectionWidth), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionHeightProperty = DependencyProperty.Register(
        nameof(SelectionHeight), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverXProperty = DependencyProperty.Register(
        nameof(HoverX), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverYProperty = DependencyProperty.Register(
        nameof(HoverY), typeof(int), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasHoverProperty = DependencyProperty.Register(
        nameof(HasHover), typeof(bool), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(AtlasCanvas),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public ImageSource? Texture
    {
        get => (ImageSource?)GetValue(TextureProperty);
        set => SetValue(TextureProperty, value);
    }

    public ImageSource? Overlay
    {
        get => (ImageSource?)GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    public double OverlayOpacity
    {
        get => (double)GetValue(OverlayOpacityProperty);
        set => SetValue(OverlayOpacityProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double CellWidth
    {
        get => (double)GetValue(CellWidthProperty);
        set => SetValue(CellWidthProperty, value);
    }

    public double CellHeight
    {
        get => (double)GetValue(CellHeightProperty);
        set => SetValue(CellHeightProperty, value);
    }

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int SelectionX
    {
        get => (int)GetValue(SelectionXProperty);
        set => SetValue(SelectionXProperty, value);
    }

    public int SelectionY
    {
        get => (int)GetValue(SelectionYProperty);
        set => SetValue(SelectionYProperty, value);
    }

    public int SelectionWidth
    {
        get => (int)GetValue(SelectionWidthProperty);
        set => SetValue(SelectionWidthProperty, value);
    }

    public int SelectionHeight
    {
        get => (int)GetValue(SelectionHeightProperty);
        set => SetValue(SelectionHeightProperty, value);
    }

    public int HoverX
    {
        get => (int)GetValue(HoverXProperty);
        set => SetValue(HoverXProperty, value);
    }

    public int HoverY
    {
        get => (int)GetValue(HoverYProperty);
        set => SetValue(HoverYProperty, value);
    }

    public bool HasHover
    {
        get => (bool)GetValue(HasHoverProperty);
        set => SetValue(HasHoverProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public event EventHandler<AtlasSelectionEventArgs>? SelectionChanged;
    public event EventHandler<AtlasSelectionEventArgs>? SelectionCommitted;
    public event EventHandler<AtlasHoverEventArgs>? HoverChanged;
    public event EventHandler<AtlasZoomEventArgs>? ZoomRequested;

    protected override Size MeasureOverride(Size availableSize) =>
        Texture is BitmapSource bitmap && Zoom > 0
            ? new Size(bitmap.PixelWidth * Zoom, bitmap.PixelHeight * Zoom)
            : new Size(0, 0);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Texture == null || ActualWidth <= 0 || ActualHeight <= 0) return;

        var zoom = Zoom > 0 ? Zoom : 1;
        drawingContext.DrawImage(Texture, new Rect(0, 0, ActualWidth, ActualHeight));

        if (Overlay != null)
        {
            drawingContext.PushOpacity(Math.Clamp(OverlayOpacity, 0, 1));
            var overlayWidth = Overlay is BitmapSource overlayBitmap
                ? overlayBitmap.PixelWidth * zoom
                : ActualWidth;
            var overlayHeight = Overlay is BitmapSource overlayPixels
                ? overlayPixels.PixelHeight * zoom
                : ActualHeight;
            drawingContext.DrawImage(Overlay, new Rect(0, 0, overlayWidth, overlayHeight));
            drawingContext.Pop();
        }

        var cw = CellWidth > 0 ? CellWidth * zoom : 50;
        var ch = CellHeight > 0 ? CellHeight * zoom : 50;
        var gridPen = new Pen(GridBrush, 1);
        gridPen.Freeze();

        if (ShowGrid)
        {
            for (var column = 0; column <= Math.Max(0, Columns); column++)
            {
                var x = column * cw + 0.5;
                if (x > ActualWidth) break;
                drawingContext.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
            }

            for (var row = 0; row <= Math.Max(0, Rows); row++)
            {
                var y = row * ch + 0.5;
                if (y > ActualHeight) break;
                drawingContext.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
            }
        }

        if (HasHover)
        {
            var hoverPen = new Pen(HoverBrush, 1);
            hoverPen.Freeze();
            drawingContext.DrawRectangle(null, hoverPen,
                new Rect(HoverX * cw, HoverY * ch, cw, ch));
        }

        var selectionPen = new Pen(SelectionBrush, 2);
        selectionPen.Freeze();
        drawingContext.DrawRectangle(null, selectionPen,
            new Rect(SelectionX * cw, SelectionY * ch,
                Math.Max(1, SelectionWidth) * cw, Math.Max(1, SelectionHeight) * ch));
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Delta != 0)
        {
            e.Handled = true;
            ZoomRequested?.Invoke(this, new AtlasZoomEventArgs(e.Delta > 0 ? 1 : -1));
            return;
        }

        base.OnPreviewMouseWheel(e);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle)
        {
            var scroll = FindScrollViewer();
            if (scroll == null) return;
            _panning = true;
            _panPointer = e.GetPosition(scroll);
            _panHorizontal = scroll.HorizontalOffset;
            _panVertical = scroll.VerticalOffset;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left || Texture == null) return;
        var cell = CellAt(e.GetPosition(this));
        if (cell == null) return;

        _anchor = cell;
        _dragging = true;
        CaptureMouse();
        RaiseSelection(AtlasCellSelection(cell.Value, cell.Value));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panning)
        {
            var scroll = FindScrollViewer();
            if (scroll != null)
            {
                var point = e.GetPosition(scroll);
                scroll.ScrollToHorizontalOffset(_panHorizontal - (point.X - _panPointer.X));
                scroll.ScrollToVerticalOffset(_panVertical - (point.Y - _panPointer.Y));
            }

            return;
        }

        if (_dragging && _anchor != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var cell = ClampCellAt(e.GetPosition(this));
            if (cell == null) return;
            RaiseSelection(AtlasCellSelection(_anchor.Value, cell.Value));
            return;
        }

        SetHover(CellAt(e.GetPosition(this)));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && _panning)
        {
            _panning = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left || !_dragging) return;
        _dragging = false;
        _anchor = null;
        ReleaseMouseCapture();
        SelectionCommitted?.Invoke(this,
            new AtlasSelectionEventArgs(SelectionX, SelectionY, SelectionWidth, SelectionHeight, true));
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging && !_panning) SetHover(null);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragging)
        {
            _dragging = false;
            _anchor = null;
            SelectionCommitted?.Invoke(this,
                new AtlasSelectionEventArgs(SelectionX, SelectionY, SelectionWidth, SelectionHeight, true));
        }

        _panning = false;
    }

    private AtlasCell? CellAt(Point point)
    {
        if (Texture is not BitmapSource bitmap || Zoom <= 0) return null;
        var pixelX = point.X / Zoom;
        var pixelY = point.Y / Zoom;
        if (pixelX < 0 || pixelY < 0 || pixelX >= bitmap.PixelWidth || pixelY >= bitmap.PixelHeight) return null;

        var x = (int)Math.Floor(pixelX / Math.Max(1, CellWidth));
        var y = (int)Math.Floor(pixelY / Math.Max(1, CellHeight));
        if (x < 0 || y < 0 || x >= Math.Max(1, Columns) || y >= Math.Max(1, Rows)) return null;
        return new AtlasCell(x, y);
    }

    private AtlasCell? ClampCellAt(Point point)
    {
        if (Texture is not BitmapSource) return null;
        var scale = Zoom > 0 ? Zoom : 1;
        var x = (int)Math.Floor(point.X / scale / Math.Max(1, CellWidth));
        var y = (int)Math.Floor(point.Y / scale / Math.Max(1, CellHeight));
        return new AtlasCell(
            Math.Clamp(x, 0, Math.Max(0, Columns - 1)),
            Math.Clamp(y, 0, Math.Max(0, Rows - 1)));
    }

    private static AtlasRegion AtlasCellSelection(AtlasCell start, AtlasCell end) => new(
        Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
        Math.Abs(start.X - end.X) + 1, Math.Abs(start.Y - end.Y) + 1);

    private void RaiseSelection(AtlasRegion region) =>
        SelectionChanged?.Invoke(this,
            new AtlasSelectionEventArgs(region.X, region.Y, region.W, region.H, false));

    private void SetHover(AtlasCell? cell)
    {
        var x = cell?.X ?? -1;
        var y = cell?.Y ?? -1;
        if (x == _hoverX && y == _hoverY) return;
        _hoverX = x;
        _hoverY = y;
        HoverChanged?.Invoke(this, new AtlasHoverEventArgs(cell != null, x, y));
    }

    private ScrollViewer? FindScrollViewer()
    {
        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current != null)
        {
            if (current is ScrollViewer scroll) return scroll;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
