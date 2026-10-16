using System.Globalization;
using XRayForge.Core.Text;

namespace XRayForge.Core.Ltx;

/// <summary>Результат resolveIncludes: путь инклюда, наличие файла, содержимое и цикличность.</summary>
public sealed class ResolvedInclude
{
    public string Path { get; set; } = "";
    public bool Found { get; set; }
    public string Content { get; set; } = "";
    public int Line { get; set; }
    public bool Cyclic { get; set; }
}

/// <summary>Строка дерева инклюдов (includeTreeRows).</summary>
public sealed class IncludeTreeRow
{
    public string Path { get; set; } = "";
    public int Depth { get; set; }
    public bool Cyclic { get; set; }
    public bool Found { get; set; }
}

/// <summary>
/// Работа с деревом `#include` — порт `src/lib/ltx.js`: resolveIncludes,
/// isFileLoaded, findIncludeSuggestion, includeTreeRows, includeGraphSvg.
/// </summary>
public static class LtxIncludes
{
    private const string Nl = "\n";

    /// <summary>resolveIncludes: рекурсивно собрать инклюды (содержимое из карты файлов).</summary>
    public static List<ResolvedInclude> ResolveIncludes(
        string? text, IDictionary<string, string>? files, int? maxDepth = null)
    {
        var results = new List<ResolvedInclude>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var map = files ?? new Dictionary<string, string>();
        var depthMax = maxDepth ?? 20;

        void Walk(string? t, int depth)
        {
            if (depth > depthMax) return;
            foreach (var inc in LtxParser.Parse(t).Includes)
            {
                var norm = Paths.Normalize(inc.Path);
                var found = map.ContainsKey(norm);
                results.Add(new ResolvedInclude
                {
                    Path = norm,
                    Found = found,
                    Content = found ? map[norm] : "",
                    Line = inc.Line,
                    Cyclic = visited.Contains(norm)
                });
                if (!visited.Add(norm)) continue;
                if (found) Walk(map[norm], depth + 1);
            }
        }

        Walk(text, 0);
        return results;
    }

    /// <summary>isFileLoaded: есть ли инклюд среди загруженных файлов (по пути или имени).</summary>
    public static bool IsFileLoaded(string? includePath, IDictionary<string, string>? ltxFiles)
    {
        var norm = Paths.Normalize(includePath).ToLowerInvariant();
        if (norm.Length == 0) return false;

        var keys = (ltxFiles ?? new Dictionary<string, string>()).Keys.ToList();
        if (keys.Any(k => k.ToLowerInvariant() == norm)) return true;

        var baseName = norm.Split('/').Last();
        return keys.Any(k =>
        {
            var lk = k.ToLowerInvariant();
            return lk == baseName || lk.EndsWith("/" + baseName, StringComparison.Ordinal);
        });
    }

    /// <summary>findIncludeSuggestion: последний загруженный файл с таким же именем (иначе null).</summary>
    public static string? FindIncludeSuggestion(string? includePath, IDictionary<string, string>? ltxFiles)
    {
        var baseName = Paths.Normalize(includePath).ToLowerInvariant().Split('/').Last();
        string? best = null;
        foreach (var k in (ltxFiles ?? new Dictionary<string, string>()).Keys)
        {
            var kb = k.ToLowerInvariant().Replace('\\', '/').Split('/').Last();
            if (kb == baseName) best = k;
        }
        return best;
    }

    /// <summary>includeTreeRows: строки дерева инклюдов (глубина ≤ 6) в плоский список.</summary>
    public static void IncludeTreeRows(
        string? text, IDictionary<string, string>? files, int depth,
        HashSet<string>? visited, List<IncludeTreeRow> outRows)
    {
        if (string.IsNullOrEmpty(text) || depth > 6) return;
        var map = files ?? new Dictionary<string, string>();
        var seen = visited ?? new HashSet<string>(StringComparer.Ordinal);

        foreach (var inc in LtxParser.Parse(text).Includes)
        {
            var norm = Paths.Normalize(inc.Path);
            var key = map.Keys.FirstOrDefault(k =>
                k.ToLowerInvariant() == norm.ToLowerInvariant() ||
                k.ToLowerInvariant().EndsWith("/" + norm.ToLowerInvariant(), StringComparison.Ordinal));
            var found = key != null;

            outRows.Add(new IncludeTreeRow
            {
                Path = norm,
                Depth = depth,
                Cyclic = seen.Contains(norm),
                Found = found
            });

            if (found && !seen.Contains(norm))
            {
                var next = new HashSet<string>(seen, StringComparer.Ordinal) { norm };
                IncludeTreeRows(map[key!], map, depth + 1, next, outRows);
            }
        }
    }

    /// <summary>includeGraphSvg: SVG-схема дерева инклюдов (пустая строка, если инклюдов нет).</summary>
    public static string IncludeGraphSvg(string? text, IDictionary<string, string>? files)
    {
        var rows = new List<IncludeTreeRow>();
        IncludeTreeRows(text, files ?? new Dictionary<string, string>(), 0, new HashSet<string>(StringComparer.Ordinal), rows);
        if (rows.Count == 0) return "";

        const int W = 190;
        const int H = 36;
        const int Pad = 24;
        var maxDepth = rows.Max(r => r.Depth);
        var width = (maxDepth + 2) * W;
        var height = (rows.Count + 1) * H + Pad;
        int NodeY(int i) => (i + 1) * H + Pad;

        var sb = new System.Text.StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(width)
          .Append("\" height=\"").Append(height)
          .Append("\" viewBox=\"0 0 ").Append(width).Append(' ').Append(height).Append("\">");
        sb.Append('\n').Append("<rect x=\"0\" y=\"").Append(NodeY(-1))
          .Append("\" width=\"").Append(W - 14).Append("\" height=\"").Append(H - 10)
          .Append("\" rx=\"6\" fill=\"#18181b\" stroke=\"#52525b\"/>");
        sb.Append('\n').Append("<text x=\"8\" y=\"").Append(NodeY(-1) + 20)
          .Append("\" fill=\"#e4e4e7\" font-size=\"11\" font-weight=\"bold\">system.ltx</text>");

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var x = (r.Depth + 1) * W;
            var y = NodeY(i);
            var color = r.Cyclic ? "#7f1d1d" : r.Found ? "#1e293b" : "#78350f";
            sb.Append('\n').Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y)
              .Append("\" width=\"").Append(W - 14).Append("\" height=\"").Append(H - 10)
              .Append("\" rx=\"6\" fill=\"").Append(color).Append("\" stroke=\"#52525b\"/>");
            sb.Append('\n').Append("<text x=\"").Append(x + 8).Append("\" y=\"").Append(y + 20)
              .Append("\" fill=\"#e4e4e7\" font-size=\"11\">")
              .Append(XmlEscaping.EscapeAttribute(r.Path.Split('/').Last() + (r.Found ? "" : " ✗")))
              .Append("</text>");
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int? parent = null;
            for (var j = i - 1; j >= 0; j--)
            {
                if (rows[j].Depth == r.Depth - 1) { parent = j; break; }
            }

            var px = parent == null ? 0 : (rows[parent.Value].Depth + 1) * W;
            var py = parent == null ? NodeY(-1) : NodeY(parent.Value);
            var x1 = px + (W - 14);
            var y1 = py + (H - 10) / 2;
            var x2 = (r.Depth + 1) * W;
            var y2 = NodeY(i) + (H - 10) / 2;
            var mx = Num((x1 + x2) / 2.0);

            sb.Append('\n').Append("<path d=\"M ").Append(x1).Append(' ').Append(y1)
              .Append(" C ").Append(mx).Append(' ').Append(y1)
              .Append(", ").Append(mx).Append(' ').Append(y2)
              .Append(", ").Append(x2).Append(' ').Append(y2)
              .Append("\" fill=\"none\" stroke=\"#52525b\" stroke-width=\"1.5\"/>");
        }

        sb.Append('\n').Append("</svg>");
        return sb.ToString();
    }

    private static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);
}