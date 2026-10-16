using System.Globalization;
using System.Text;
using XRayForge.Core.Models;
using XRayForge.Core.Text;

namespace XRayForge.Core.Quests;

/// <summary>SVG-схема цепочек квестов (#64).</summary>
public static class QuestChainSvg
{
    private const int NodeW = 170;
    private const int NodeH = 54;
    private const int GapX = 70;
    private const int GapY = 46;
    private const int Pad = 30;

    public static string Render(IEnumerable<IEnumerable<string>?>? chains, IEnumerable<Quest?>? quests)
    {
        var list = chains?.Where(c => c != null).Select(c => c!.ToList()).ToList() ?? new List<List<string>>();
        var byId = new Dictionary<string, Quest>(StringComparer.Ordinal);
        foreach (var q in quests ?? Enumerable.Empty<Quest?>())
            if (q != null) byId[q.Id] = q;

        if (list.Count == 0) return "";

        var pos = new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
        var order = new List<string>();
        for (var ci = 0; ci < list.Count; ci++)
        {
            var chain = list[ci];
            for (var depth = 0; depth < chain.Count; depth++)
            {
                var id = chain[depth];
                if (pos.ContainsKey(id)) continue;
                pos[id] = (Pad + depth * (NodeW + GapX), Pad + ci * (NodeH + GapY));
                order.Add(id);
            }
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<(string A, string B)>();
        foreach (var chain in list)
        {
            for (var i = 1; i < chain.Count; i++)
            {
                var a = chain[i - 1];
                var b = chain[i];
                var key = a + ">" + b;
                if (seen.Add(key) && pos.ContainsKey(a) && pos.ContainsKey(b))
                    edges.Add((a, b));
            }
        }

        if (order.Count == 0) return "";

        var minX = order.Min(id => pos[id].X) - Pad;
        var maxX = order.Max(id => pos[id].X + NodeW) + Pad;
        var minY = order.Min(id => pos[id].Y) - Pad;
        var maxY = order.Max(id => pos[id].Y + NodeH) + Pad;
        var w = maxX - minX;
        var h = maxY - minY;

        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(w)
          .Append("\" height=\"").Append(h)
          .Append("\" viewBox=\"").Append(minX).Append(' ').Append(minY).Append(' ').Append(w).Append(' ').Append(h).Append("\">");
        sb.Append('\n').Append("<rect x=\"").Append(minX).Append("\" y=\"").Append(minY)
          .Append("\" width=\"").Append(w).Append("\" height=\"").Append(h).Append("\" fill=\"#18181b\"/>");

        foreach (var (a, b) in edges)
        {
            var pa = pos[a];
            var pb = pos[b];
            var x1 = pa.X + NodeW;
            var y1 = pa.Y + NodeH / 2;
            var x2 = pb.X;
            var y2 = pb.Y + NodeH / 2;
            var mx = Num((x1 + x2) / 2.0);

            sb.Append('\n').Append("<path d=\"M ").Append(x1).Append(' ').Append(y1)
              .Append(" C ").Append(mx).Append(' ').Append(y1)
              .Append(", ").Append(mx).Append(' ').Append(y2)
              .Append(", ").Append(x2).Append(' ').Append(y2)
              .Append("\" fill=\"none\" stroke=\"#71717a\" stroke-width=\"2\"/>");
            sb.Append('\n').Append("<polygon points=\"").Append(x2).Append(',').Append(y2)
              .Append(' ').Append(x2 - 8).Append(',').Append(y2 - 5)
              .Append(' ').Append(x2 - 8).Append(',').Append(y2 + 5)
              .Append("\" fill=\"#71717a\"/>");
        }

        foreach (var id in order)
        {
            var p = pos[id];
            byId.TryGetValue(id, out var q);
            sb.Append('\n').Append("<rect x=\"").Append(p.X).Append("\" y=\"").Append(p.Y)
              .Append("\" width=\"").Append(NodeW).Append("\" height=\"").Append(NodeH)
              .Append("\" rx=\"8\" fill=\"#1f2937\" stroke=\"#3f3f46\"/>");
            sb.Append('\n').Append("<text x=\"").Append(p.X + 12).Append("\" y=\"").Append(p.Y + 20)
              .Append("\" fill=\"#fff\" font-size=\"12\" font-weight=\"bold\">")
              .Append(XmlEscaping.EscapeAttribute(Slice(id, 28))).Append("</text>");
            if (q != null && q.Category.Length > 0)
                sb.Append('\n').Append("<text x=\"").Append(p.X + 12).Append("\" y=\"").Append(p.Y + 38)
                  .Append("\" fill=\"#a1a1aa\" font-size=\"10\">")
                  .Append(XmlEscaping.EscapeAttribute(q.Category)).Append("</text>");
        }

        sb.Append('\n').Append("</svg>");
        return sb.ToString();
    }

    private static string Slice(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

    private static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);
}