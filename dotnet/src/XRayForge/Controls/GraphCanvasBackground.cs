using System.Windows;
using System.Windows.Media;
using XRayForge.Core.Graph;

namespace XRayForge.Controls;

/// <summary>
/// Бесконечный фон графа диалогов: фон + точечная сетка, которая сдвигается вместе с
/// <c>pan</c> и масштабируется вместе с <c>zoom</c>. Элемент растянут на весь viewport,
/// поэтому панорамирование работает даже если все карточки/связи уехали за экран —
/// в отличие от прежней квадратной подложки 20000×20000.
/// </summary>
public sealed class GraphCanvasBackground : FrameworkElement
{
    public static readonly DependencyProperty SurfaceBrushProperty = DependencyProperty.Register(
        nameof(SurfaceBrush), typeof(Brush), typeof(GraphCanvasBackground),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush), typeof(Brush), typeof(GraphCanvasBackground),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(GraphCanvasBackground),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PanXProperty = DependencyProperty.Register(
        nameof(PanX), typeof(double), typeof(GraphCanvasBackground),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PanYProperty = DependencyProperty.Register(
        nameof(PanY), typeof(double), typeof(GraphCanvasBackground),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Фон холста (`SurfaceDisplay`).</summary>
    public Brush? SurfaceBrush
    {
        get => (Brush?)GetValue(SurfaceBrushProperty);
        set => SetValue(SurfaceBrushProperty, value);
    }

    /// <summary>Цвет точек сетки (`GridLine`).</summary>
    public Brush? DotBrush
    {
        get => (Brush?)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    /// <summary>Текущий масштаб графа (`DialogueGraph.Zoom`).</summary>
    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double PanX
    {
        get => (double)GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => (double)GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(RenderSize);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        drawingContext.DrawRectangle(SurfaceBrush ?? Brushes.Transparent, null, bounds);

        var dot = DotBrush;
        if (dot == null) return;

        var zoom = Zoom > 0 && double.IsFinite(Zoom) ? Zoom : 1;
        var step = GraphEditing.GridStep * zoom;
        if (step < 4) step = 4;

        // Сетка «прибита» к координатам графа: сдвиг — остаток pan по модулю шага.
        var startX = PositiveModulo(PanX, step);
        var startY = PositiveModulo(PanY, step);
        for (var x = startX; x < bounds.Width; x += step)
            for (var y = startY; y < bounds.Height; y += step)
                drawingContext.DrawEllipse(dot, null, new Point(x, y), 1, 1);
    }

    private static double PositiveModulo(double value, double step)
    {
        if (!double.IsFinite(value)) return 0;
        var result = value % step;
        return result < 0 ? result + step : result;
    }
}
