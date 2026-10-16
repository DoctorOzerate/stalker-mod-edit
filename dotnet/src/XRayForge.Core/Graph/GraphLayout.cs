using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

public sealed class DialogueStats
{
    public int Nodes { get; set; }
    public int Edges { get; set; }
    public int Phrases { get; set; }
    public int Branches { get; set; }
    public int MaxDepth { get; set; }
    public int AvgText { get; set; }
}

/// <summary>Раскладка и представление графа диалога (порт layoutDialogue/nodeLabel/roleColor/dialogueStats).</summary>
public static class GraphLayout
{
    /// <summary>Разложить узлы по уровням слева-направо от START (мутирует X/Y узлов).</summary>
    public static void LayoutDialogue(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var list = nodes ?? Array.Empty<DialogueNode>();
        var adj = BuildAdjacency(edges);

        var level = new Dictionary<string, int>();
        var start = list.FirstOrDefault(n => n.Type == NodeTypes.Start);
        var queue = new List<string>();
        if (start != null)
        {
            queue.Add(start.Id);
            level[start.Id] = 0;
        }

        var visited = new HashSet<string>();
        var qIndex = 0;
        while (qIndex < queue.Count)
        {
            var cur = queue[qIndex++];
            if (!visited.Add(cur)) continue;
            if (!adj.TryGetValue(cur, out var targets)) continue;
            foreach (var t in targets)
            {
                if (!level.ContainsKey(t)) level[t] = (level.TryGetValue(cur, out var curLevel) ? curLevel : 0) + 1;
                queue.Add(t);
            }
        }

        foreach (var n in list)
            if (!level.ContainsKey(n.Id)) level[n.Id] = 0;

        var groups = new Dictionary<int, List<string>>();
        foreach (var n in list)
        {
            var l = level[n.Id];
            if (!groups.TryGetValue(l, out var ids)) groups[l] = ids = new List<string>();
            ids.Add(n.Id);
        }

        const double lx = 60, ly = 80, xs = 320, ys = 110;
        foreach (var l in groups.Keys.OrderBy(k => k))
        {
            var ids = groups[l];
            for (var i = 0; i < ids.Count; i++)
            {
                var n = list.FirstOrDefault(x => x.Id == ids[i]);
                if (n == null) continue;
                n.X = lx + l * xs;
                n.Y = ly + i * ys;
            }
        }
    }

    /// <summary>Текст для узла: перевод text_id или собственный текст реплики.</summary>
    public static string NodeLabel(DialogueNode? n, IReadOnlyList<Language>? languages, bool showText = true)
    {
        var d = n?.Data ?? new NodeData();
        if (d.UseId && d.TextId.Length > 0)
        {
            if (!showText) return "#" + d.TextId;
            foreach (var lang in languages ?? Array.Empty<Language>())
            {
                var e = lang.Entries.FirstOrDefault(x => x.Id == d.TextId);
                if (e != null && e.Text.Length > 0) return e.Text;
            }
            return "#" + d.TextId;
        }

        var text = d.Text.Length > 0 ? d.Text : d.Expression.Length > 0 ? d.Expression : d.ActionType;
        return text.Length > 0 ? text : "Нет содержимого";
    }

    /// <summary>Цвет узла реплики по роли (npc/player/не определено); для остальных типов — null.</summary>
    public static string? RoleColor(DialogueNode? n)
    {
        if (n == null || n.Type != NodeTypes.Phrase) return null;
        var role = n.Data?.Role;
        return role == "npc" ? "#2563eb" : role == "player" ? "#16a34a" : "#64748b";
    }

    /// <summary>Сводная статистика диалога.</summary>
    public static DialogueStats Stats(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var list = nodes ?? Array.Empty<DialogueNode>();
        var edgeList = edges ?? Array.Empty<DialogueEdge>();
        var phrases = list.Where(n => n.Type == NodeTypes.Phrase).ToList();
        var adj = BuildAdjacency(edgeList);

        var depth = new Dictionary<string, int>();
        var queue = new List<string>();
        foreach (var n in list.Where(n => n.Type == NodeTypes.Start))
        {
            depth[n.Id] = 0;
            queue.Add(n.Id);
        }

        var qi = 0;
        while (qi < queue.Count)
        {
            var id = queue[qi++];
            if (!adj.TryGetValue(id, out var targets)) continue;
            foreach (var t in targets)
            {
                if (!depth.ContainsKey(t))
                {
                    depth[t] = depth[id] + 1;
                    queue.Add(t);
                }
            }
        }

        var maxDepth = 0;
        foreach (var v in depth.Values) maxDepth = Math.Max(maxDepth, v);

        var totalText = phrases.Sum(n => (n.Data?.Text ?? string.Empty).Length);

        return new DialogueStats
        {
            Nodes = list.Count,
            Edges = edgeList.Count,
            Phrases = phrases.Count,
            Branches = phrases.Count(n => adj.TryGetValue(n.Id, out var t) && t.Count > 1),
            MaxDepth = maxDepth,
            AvgText = phrases.Count > 0 ? (int)Math.Round((double)totalText / phrases.Count, MidpointRounding.AwayFromZero) : 0
        };
    }

    internal static Dictionary<string, List<string>> BuildAdjacency(IReadOnlyList<DialogueEdge>? edges)
    {
        var adj = new Dictionary<string, List<string>>();
        foreach (var e in edges ?? Array.Empty<DialogueEdge>())
        {
            if (!adj.TryGetValue(e.Source, out var targets)) adj[e.Source] = targets = new List<string>();
            targets.Add(e.Target);
        }
        return adj;
    }
}