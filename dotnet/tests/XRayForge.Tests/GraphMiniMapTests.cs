using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Tests;

/// <summary>
/// Шаг 6.6: чистая геометрия мини-карты холста диалогов (`GraphMiniMap`) — границы содержимого,
/// вписывание в панель 176×112, рамка видимой области и переход по клику.
/// </summary>
public class GraphMiniMapTests
{
    [Fact]
    public void Build_returns_null_without_nodes()
    {
        Assert.Null(GraphMiniMap.Build(null, 1, 0, 0, 800, 600));
        Assert.Null(GraphMiniMap.Build(new List<DialogueNode>(), 1, 0, 0, 800, 600));
    }

    [Fact]
    public void Build_pads_bounds_by_twenty_and_keeps_aspect_ratio()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "a", Type = NodeTypes.Start, X = 0, Y = 0 },
            new() { Id = "b", Type = NodeTypes.Phrase, X = 480, Y = 0 }
        };

        var layout = GraphMiniMap.Build(nodes, 1, 0, 0, 800, 600);

        Assert.NotNull(layout);
        // Границы: X — от −20 до 480 + 210 + 20 = 710 (730), Y — от −20 до 0 + 100 + 20 = 120 (140).
        Assert.Equal(-20, layout!.MinX);
        Assert.Equal(-20, layout.MinY);
        // Масштаб ограничивает ширина (730 против 140): вписываем по X, по Y остаются поля.
        Assert.Equal(176d / 730d, layout.Scale, 10);
        Assert.Equal(0, layout.OffsetX, 10);
        Assert.Equal((GraphMiniMap.Height - 140 * layout.Scale) / 2, layout.OffsetY, 10);
    }

    [Fact]
    public void Build_scales_nodes_and_keeps_types_rectangles_inside_panel()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "a", Type = NodeTypes.Start, X = 0, Y = 0 },
            new() { Id = "b", Type = NodeTypes.Phrase, X = 0, Y = 300 }
        };

        var layout = GraphMiniMap.Build(nodes, 1, 0, 0, 800, 600)!;

        // Содержимое по Y: от −20 до 300 + 100 + 20 = 420 (440) — масштаб ограничивает высота.
        Assert.Equal(112d / 440d, layout.Scale, 10);
        Assert.Equal(0, layout.OffsetY, 10);
        Assert.Equal(2, layout.Nodes.Count);

        var first = layout.Nodes[0];
        Assert.Equal("a", first.Id);
        Assert.Equal(layout.OffsetX + 20 * layout.Scale, first.Rect.X, 10);
        Assert.Equal(20 * layout.Scale, first.Rect.Y, 10);
        Assert.Equal(GraphMiniMap.NodeWidth * layout.Scale, first.Rect.Width, 10);
        Assert.Equal(GraphMiniMap.NodeHeight * layout.Scale, first.Rect.Height, 10);
        // Рамка узла не выходит за панель по вертикали.
        Assert.True(first.Rect.Y >= 0 && first.Rect.Y + first.Rect.Height <= GraphMiniMap.Height + 0.001);
    }

    [Fact]
    public void Build_viewport_covers_visible_area_in_graph_coordinates()
    {
        var nodes = new List<DialogueNode> { new() { Id = "a", Type = NodeTypes.Phrase, X = 0, Y = 0 } };

        var layout = GraphMiniMap.Build(nodes, 1, -50, -30, 800, 600)!;

        // Видимая область в координатах графа: −pan / zoom … размером холст / zoom.
        Assert.Equal(layout.OffsetX + (50 - layout.MinX) * layout.Scale, layout.Viewport.X, 10);
        Assert.Equal(layout.OffsetY + (30 - layout.MinY) * layout.Scale, layout.Viewport.Y, 10);
        Assert.Equal(800 * layout.Scale, layout.Viewport.Width, 10);
        Assert.Equal(600 * layout.Scale, layout.Viewport.Height, 10);

        // Зум 2 уменьшает рамку вдвое и сдвигает её начало к центру содержимого.
        var zoomed = GraphMiniMap.Build(nodes, 2, -50, -30, 800, 600)!;
        Assert.Equal(400 * zoomed.Scale, zoomed.Viewport.Width, 10);
        Assert.Equal(zoomed.OffsetX + (25 - zoomed.MinX) * zoomed.Scale, zoomed.Viewport.X, 10);
    }

    [Fact]
    public void ToGraph_inverts_layout_scaling()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "a", Type = NodeTypes.Start, X = 40, Y = 60 },
            new() { Id = "b", Type = NodeTypes.End, X = 400, Y = 260 }
        };

        var layout = GraphMiniMap.Build(nodes, 1, 0, 0, 800, 600)!;
        var node = layout.Nodes[1];

        // Левый верхний угол прямоугольника узла переводится обратно в его координаты графа.
        var (x, y) = GraphMiniMap.ToGraph(layout, node.Rect.X, node.Rect.Y);
        Assert.Equal(400, x, 6);
        Assert.Equal(260, y, 6);
    }

    [Fact]
    public void CenterPan_puts_graph_point_into_viewport_center()
    {
        var (x, y) = GraphMiniMap.CenterPan(120, 80, 1, 800, 600);
        Assert.Equal(280, x, 6);
        Assert.Equal(220, y, 6);

        // При зуме точка графа занимает в экранных пикселях zoom × координату.
        var (zx, zy) = GraphMiniMap.CenterPan(120, 80, 1.5, 800, 600);
        Assert.Equal(400 - 180, zx, 6);
        Assert.Equal(300 - 120, zy, 6);
    }
}
