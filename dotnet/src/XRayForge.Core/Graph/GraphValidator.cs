using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>
/// Валидация графа диалога (порт validateGraph из graph.js): типы узлов, дубли ID,
/// битые связи, достижимость, START/END, пустые реплики, тупики, импортированные циклы.
/// Обход циклов — итеративный (без риска переполнения стека на больших графах).
/// </summary>
public static class GraphValidator
{
    private static readonly List<string> NoNeighbors = new();

    public static List<GraphIssue> Validate(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var issues = new List<GraphIssue>();
        var ids = new Dictionary<string, DialogueNode>();
        var order = new List<string>();
        var outMap = new Dictionary<string, List<string>>();

        var nodeList = nodes ?? Array.Empty<DialogueNode>();
        for (var index = 0; index < nodeList.Count; index++)
        {
            var node = nodeList[index];
            if (node == null || !NodeTypes.IsKnown(node.Type))
            {
                issues.Add(new GraphIssue { Severity = "error", Code = "type", Message = "Неизвестный тип узла #" + (index + 1) });
            }
            else if (node.Id.Length == 0 || ids.ContainsKey(node.Id))
            {
                issues.Add(new GraphIssue
                {
                    Severity = "error",
                    Code = "dup_id",
                    Message = "Дублирующийся или пустой ID узла: " + (node.Id.Length > 0 ? node.Id : "(пусто)")
                });
            }
            else
            {
                ids[node.Id] = node;
                order.Add(node.Id);
            }
        }

        foreach (var edge in edges ?? Array.Empty<DialogueEdge>())
        {
            if (edge == null || edge.Broken || !ids.ContainsKey(edge.Source) || !ids.ContainsKey(edge.Target))
            {
                issues.Add(new GraphIssue
                {
                    Severity = "error",
                    Code = "broken_edge",
                    Message = "Битая связь " + (edge != null && edge.Source.Length > 0 ? edge.Source : "?")
                              + " → " + (edge != null && edge.Target.Length > 0 ? edge.Target : "?")
                });
                continue;
            }

            if (!outMap.TryGetValue(edge.Source, out var targets)) outMap[edge.Source] = targets = new List<string>();
            targets.Add(edge.Target);
        }

        var starts = order.Where(id => ids[id].Type == NodeTypes.Start).ToList();
        var ends = order.Where(id => ids[id].Type == NodeTypes.End).ToList();

        if (starts.Count > 0)
        {
            var reachable = new HashSet<string>();
            var queue = new Queue<string>(starts);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!reachable.Add(id)) continue;
                if (!outMap.TryGetValue(id, out var targets)) continue;
                foreach (var t in targets) queue.Enqueue(t);
            }

            foreach (var id in order)
                if (!reachable.Contains(id))
                    issues.Add(new GraphIssue { Severity = "warning", Code = "unreachable", Message = "Недостижимый узел: " + id });
        }

        if (starts.Count == 0) issues.Add(new GraphIssue { Severity = "error", Code = "missing_start", Message = "Отсутствует START" });
        if (ends.Count == 0) issues.Add(new GraphIssue { Severity = "error", Code = "missing_end", Message = "Отсутствует END" });

        foreach (var id in order)
        {
            var node = ids[id];
            if (node.Type != NodeTypes.Phrase) continue;

            var data = GraphData.SafeData(node.Data);
            foreach (var iss in GraphData.ValidateMetadataSyntax(data.Metadata))
                issues.Add(new GraphIssue { Severity = iss.Severity, Code = iss.Code, Message = id + ": " + iss.Message });

            var hasMeta = data.Metadata.Values.Any(v => v.Any(x => !string.IsNullOrWhiteSpace(x)));
            var hasNext = outMap.TryGetValue(id, out var next) && next.Count > 0;
            if (string.IsNullOrWhiteSpace(data.Text) && string.IsNullOrWhiteSpace(data.TextId) && !hasMeta && !hasNext)
                issues.Add(new GraphIssue { Severity = "error", Code = "empty_text", Message = "Пустой текст реплики: " + id });
            if (!hasNext)
                issues.Add(new GraphIssue { Severity = "warning", Code = "dead_end", Message = "Реплика без продолжения (нет next/END): " + id });
        }

        // Итеративный DFS с трёхцветной раскраской (1 — в стеке, 2 — готово) для поиска циклов.
        var color = new Dictionary<string, int>();
        foreach (var root in order)
        {
            if (color.ContainsKey(root)) continue;
            color[root] = 1;
            var stack = new Stack<(string V, int Index)>();
            stack.Push((root, 0));

            while (stack.Count > 0)
            {
                var (v, index) = stack.Pop();
                var neighbors = outMap.TryGetValue(v, out var adj) ? adj : NoNeighbors;

                if (index < neighbors.Count)
                {
                    stack.Push((v, index + 1));
                    var w = neighbors[index];
                    if (color.TryGetValue(w, out var cw))
                    {
                        if (cw == 1)
                            issues.Add(new GraphIssue { Severity = "warning", Code = "imported_cycle", Message = "Цикл: " + v + " → " + w });
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
