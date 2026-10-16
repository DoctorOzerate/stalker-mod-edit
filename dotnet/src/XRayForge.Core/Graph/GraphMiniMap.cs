using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>Прямоугольник в пикселях мини-карты (панель 176×112).</summary>
public sealed record MiniMapRect(double X, double Y, double Width, double Height);

/// <summary>Узел графа на мини-карте: id для сопоставления с карточкой холста и её прямоугольник.</summary>
public sealed record MiniMapNode(string Id, MiniMapRect Rect);

/// <summary>
/// Раскладка мини-карты (блок `&lt;svg viewBox&gt;` из `DialoguesTab.js`): координаты графа
/// пересчитаны в пиксели панели. `Scale`/`Offset*` — вписывание содержимого целиком и
/// центрирование (в SVG это `preserveAspectRatio: xMidYMid meet` по умолчанию),
/// <see cref="Viewport"/> — рамка видимой области холста в тех же пикселях.
/// </summary>
public sealed class MiniMapLayout
{
    internal MiniMapLayout(double minX, double minY, double scale, double offsetX, double offsetY,
        MiniMapRect viewport, IReadOnlyList<MiniMapNode> nodes)
    {
        MinX = minX;
        MinY = minY;
        Scale = scale;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Viewport = viewport;
        Nodes = nodes;
    }

    /// <summary>Левый край содержимого графа (`min(x) − 20` в JS).</summary>
    public double MinX { get; }

    /// <summary>Верхний край содержимого графа (`min(y) − 20`).</summary>
    public double MinY { get; }

    /// <summary>Масштаб «координаты графа → пиксели мини-карты».</summary>
    public double Scale { get; }

    /// <summary>Смещение содержимого по X внутри панели (письмо «meet» вписывает его целиком).</summary>
    public double OffsetX { get; }

    /// <summary>Смещение содержимого по Y внутри панели.</summary>
    public double OffsetY { get; }

    /// <summary>Рамка видимой области холста (`rect` со `stroke #22c55e` в JS).</summary>
    public MiniMapRect Viewport { get; }

    /// <summary>Прямоугольники узлов 210×90, приведённые к пикселям мини-карты.</summary>
    public IReadOnlyList<MiniMapNode> Nodes { get; }
}

/// <summary>
/// Чистая геометрия мини-карты холста диалогов (шаг 6.6): границы содержимого, пересчёт в панель
/// `w-44 h-28` (176×112), рамка видимой области и переход по клику. Экранная точка графа —
/// `pan + zoom × точка графа` (transform-слой `translate(pan) scale(zoom)`), поэтому видимая
/// область в координатах графа начинается в `−pan / zoom` и имеет размер `холст / zoom`.
/// </summary>
public static class GraphMiniMap
{
    /// <summary>Ширина панели мини-карты (CSS `w-44` = 176px).</summary>
    public const double Width = 176;

    /// <summary>Высота панели мини-карты (CSS `h-28` = 112px).</summary>
    public const double Height = 112;

    /// <summary>Ширина прямоугольника узла на мини-карте — как у карточки (`210`).</summary>
    public const double NodeWidth = GraphEditing.NodeWidth;

    /// <summary>Высота прямоугольника узла на мини-карте (`rect height 90` в JS).</summary>
    public const double NodeHeight = 90;

    /// <summary>Отступ границ содержимого (`−20` и `+20` вокруг узлов в JS).</summary>
    public const double BoundsPadding = 20;

    /// <summary>
    /// Раскладка мини-карты по узлам графа и текущему виду холста. `null` — в графе нет узлов:
    /// в JS блок мини-карты не рендерится вовсе (`if (!nodes.length) return null`).
    /// </summary>
    public static MiniMapLayout? Build(
        IEnumerable<DialogueNode>? nodes, double zoom, double panX, double panY,
        double viewportWidth, double viewportHeight)
    {
        var list = (nodes ?? Enumerable.Empty<DialogueNode>()).Where(node => node != null).ToList();
        if (list.Count == 0) return null;

        var minX = list.Min(node => node.X) - BoundsPadding;
        var minY = list.Min(node => node.Y) - BoundsPadding;
        // Границы считаются по карточке 210×100 (`n.x + 210` / `n.y + 100` в JS), а прямоугольник
        // узла на мини-карте — 210×90: как и в оригинале.
        var maxX = list.Max(node => node.X + GraphEditing.NodeWidth) + BoundsPadding;
        var maxY = list.Max(node => node.Y + GraphEditing.NodeHitHeight) + BoundsPadding;
        var contentWidth = Math.Max(1, maxX - minX);
        var contentHeight = Math.Max(1, maxY - minY);

        var scale = Math.Min(Width / contentWidth, Height / contentHeight);
        var offsetX = (Width - contentWidth * scale) / 2;
        var offsetY = (Height - contentHeight * scale) / 2;

        var z = zoom > 0 ? zoom : 1;
        var viewport = new MiniMapRect(
            offsetX + (-panX / z - minX) * scale,
            offsetY + (-panY / z - minY) * scale,
            viewportWidth / z * scale,
            viewportHeight / z * scale);

        var items = new List<MiniMapNode>(list.Count);
        foreach (var node in list)
            items.Add(new MiniMapNode(node.Id, new MiniMapRect(
                offsetX + (node.X - minX) * scale,
                offsetY + (node.Y - minY) * scale,
                NodeWidth * scale,
                NodeHeight * scale)));

        return new MiniMapLayout(minX, minY, scale, offsetX, offsetY, viewport, items);
    }

    /// <summary>
    /// Точка графа по точке внутри мини-карты — обратный пересчёт `Scale`/`Offset*`
    /// (в JS клик по `svg` переводится той же линейной формулой `minX + (clientX − left) / width`).
    /// </summary>
    public static (double X, double Y) ToGraph(MiniMapLayout layout, double pointX, double pointY) =>
        (layout.MinX + (pointX - layout.OffsetX) / layout.Scale,
         layout.MinY + (pointY - layout.OffsetY) / layout.Scale);

    /// <summary>
    /// Сдвиг холста, при котором точка графа встаёт в центр видимой области
    /// (`setPan({ x: cvW / 2 − gx, y: cvH / 2 − gy })` из обработчика клика в JS).
    /// </summary>
    public static (double X, double Y) CenterPan(
        double graphX, double graphY, double zoom, double viewportWidth, double viewportHeight)
    {
        var z = zoom > 0 ? zoom : 1;
        return (viewportWidth / 2 - z * graphX, viewportHeight / 2 - z * graphY);
    }
}
