using System.Globalization;
using XRayForge.Core.Models;
using XRayForge.Core.Text;

namespace XRayForge.Core.Graph;

/// <summary>Рендер графа диалога в SVG (порт graphToSvg из graph.js).</summary>
public static class GraphSvg
{
    public static string GraphToSvg(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var list = nodes ?? Array.Empty<DialogueNode>();
        if (list.Count == 0) return "";

        var minX = list.Min(n => n.X) - 20;
        var minY = list.Min(n => n.Y) - 20;
        var maxX = list.Max(n => n.X + 210) + 20;
        var maxY = list.Max(n => n.Y + 100) + 20;
        var w = maxX - minX;
        var h = maxY - minY;

        var byId = new Dictionary<string, DialogueNode>();
        foreach (var n in list) byId[n.Id] = n;

        var lines = new List<string>
        {
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" + Num(w) + "\" height=\"" + Num(h)
                + "\" viewBox=\"" + Num(minX) + " " + Num(minY) + " " + Num(w) + " " + Num(h) + "\">",
            "<rect x=\"" + Num(minX) + "\" y=\"" + Num(minY) + "\" width=\"" + Num(w) + "\" height=\"" + Num(h) + "\" fill=\"#18181b\"/>"
        };

        foreach (var e in edges ?? Array.Empty<DialogueEdge>())
        {
            if (!byId.TryGetValue(e.Source, out var a) || !byId.TryGetValue(e.Target, out var b)) continue;

            var x1 = a.X + 210;
            var y1 = a.Y + 45;
            var x2 = b.X;
            var y2 = b.Y + 45;
            var mx = (x1 + x2) / 2;
            lines.Add("<path d=\"M " + Num(x1) + " " + Num(y1) + " C " + Num(mx) + " " + Num(y1) + ", "
                      + Num(mx) + " " + Num(y2) + ", " + Num(x2) + " " + Num(y2)
                      + "\" fill=\"none\" stroke=\"#71717a\" stroke-width=\"2\"/>");
        }

        foreach (var n in list)
        {
            var cfg = NodeTypes.InfoFor(n.Type);
            var label = GraphLayout.NodeLabel(n, null, true);
            if (label.Length == 0) label = n.Id;
            var txt = XmlEscaping.EscapeAttribute(label.Length > 60 ? label.Substring(0, 60) : label);

            lines.Add("<rect x=\"" + Num(n.X) + "\" y=\"" + Num(n.Y) + "\" width=\"210\" height=\"90\" rx=\"8\" fill=\"" + cfg.Color + "\"/>");
            lines.Add("<text x=\"" + Num(n.X + 12) + "\" y=\"" + Num(n.Y + 24)
                      + "\" fill=\"#fff\" font-size=\"12\" font-weight=\"bold\">"
                      + XmlEscaping.EscapeAttribute(cfg.Icon + " " + cfg.Label) + "</text>");
            lines.Add("<text x=\"" + Num(n.X + 12) + "\" y=\"" + Num(n.Y + 50)
                      + "\" fill=\"#fff\" font-size=\"11\">" + txt + "</text>");
        }

        lines.Add("</svg>");
        return string.Join('\n', lines);
    }

    private static string Num(double d) => d.ToString(CultureInfo.InvariantCulture);
}