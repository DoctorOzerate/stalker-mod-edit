using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>Валидация графа диалога: типы узлов, дубли ID, битые связи, циклы.</summary>
public static class GraphValidator
{
    public static List<GraphIssue> Validate(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var knownIds = new Dictionary<string, DialogueNode>();
        var outMap = new Dictionary<string, List<string>>();
        var issues = new List<GraphIssue>();

        foreach (var n in nodes ?? Array.Empty<DialogueNode>())
        {
            if (!NodeTypes.IsKnown(n.Type))
                issues.Add(new GraphIssue { Severity = "error", Code = "type" });
            else if (knownIds.ContainsKey(n.Id))
                issues.Add(new GraphIssue { Severity = "error", Code = "dup_id" });
            else
                knownIds[n.Id] = n;
        }

        foreach (var e in edges ?? Array.Empty<DialogueEdge>())
        {
            if (e.Broken || !knownIds.ContainsKey(e.Source) || !knownIds.ContainsKey(e.Target))
            {
                issues.Add(new GraphIssue { Severity = "error", Code = "broken_edge" });
                continue;
            }

            if (!outMap.TryGetValue(e.Source, out var list))
                outMap[e.Source] = list = new List<string>();
            list.Add(e.Target);
        }

        // Итеративный DFS с трёхцветной раскраской (1 — в стеке, 2 — готово) для поиска циклов.
        var color = new Dictionary<string, int>();
        foreach (var id in knownIds.Keys)
        {
            if (color.ContainsKey(id)) continue;
            color[id] = 1;
            var stack = new Stack<(string V, int Index)>();
            stack.Push((id, 0));

            while (stack.Count > 0)
            {
                var (v, index) = stack.Pop();
                IReadOnlyList<string> neighbors = outMap.TryGetValue(v, out var adj) ? adj : Array.Empty<string>();

                if (index < neighbors.Count)
                {
                    stack.Push((v, index + 1));
                    var w = neighbors[index];
                    if (color.TryGetValue(w, out var cw))
                    {
                        if (cw == 1)
                            issues.Add(new GraphIssue { Severity = "warning", Code = "imported_cycle", Message = $"Игровой цикл: {v} → {w}" });
                    }
                    else
                    {
                        color[w] = 1;
                        stack.Push((w, 0));
                    }
                }
                else
                {
                    color[v] = 2;
                }
            }
        }

        return issues;
    }
}
