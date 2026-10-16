using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>Состояние прогона диалога (`playthrough` в App.js): текущий узел и пройденный путь.</summary>
public sealed record PlaythroughState(string Current, IReadOnlyList<string> Path);

/// <summary>
/// Пошаговый прогон диалога (шаг 6.7): блок `playthrough` из `DialoguesTab.js` и обработчики
/// `startPlaythrough`/`stepPlaythrough`/`exitPlaythrough` из App.js. Чистые функции — выбор
/// стартовой ноды, переход по связи, подписи текущей реплики и вариантов ответа; состояние
/// держит ViewModel вкладки.
/// </summary>
public static class GraphPlaythrough
{
    /// <summary>Предел текста текущей реплики в панели прогона (`slice(0, 160)`).</summary>
    public const int TextLimit = 160;

    /// <summary>Предел подписи варианта перехода (`slice(0, 80)`).</summary>
    public const int ChoiceLimit = 80;

    /// <summary>
    /// `startPlaythrough`: стартовая нода — первый START, иначе первый узел графа; `null`, если
    /// узлов нет (в JS `say('Нет стартовой ноды')`). Путь сразу содержит стартовую ноду.
    /// </summary>
    public static PlaythroughState? Start(IEnumerable<DialogueNode>? nodes)
    {
        var list = (nodes ?? Enumerable.Empty<DialogueNode>()).Where(node => node != null).ToList();
        if (list.Count == 0) return null;

        var start = list.FirstOrDefault(node => node.Type == NodeTypes.Start) ?? list[0];
        return new PlaythroughState(start.Id, new[] { start.Id });
    }

    /// <summary>`stepPlaythrough(nextId)`: путь дополняется выбранным узлом.</summary>
    public static PlaythroughState Step(PlaythroughState state, string nextId) =>
        new(nextId, state.Path.Concat(new[] { nextId }).ToList());

    /// <summary>Текущий узел прогона (`nodes.find(n => n.id === playthrough.current)`).</summary>
    public static DialogueNode? Current(IEnumerable<DialogueNode>? nodes, string? id) =>
        string.IsNullOrEmpty(id)
            ? null
            : (nodes ?? Enumerable.Empty<DialogueNode>())
                .FirstOrDefault(node => node != null && string.Equals(node.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// `edges.filter(e => e.source === playthrough.current).map(e => nodes.find(...)).filter(Boolean)`:
    /// узлы, куда ведут связи из текущего, в порядке связей; пропавшие цели пропускаются.
    /// </summary>
    public static List<DialogueNode> Choices(
        IEnumerable<DialogueNode>? nodes, IEnumerable<DialogueEdge>? edges, string? current)
    {
        var result = new List<DialogueNode>();
        if (string.IsNullOrEmpty(current)) return result;

        foreach (var edge in edges ?? Enumerable.Empty<DialogueEdge>())
        {
            if (edge == null || !string.Equals(edge.Source, current, StringComparison.Ordinal)) continue;

            var target = Current(nodes, edge.Target);
            if (target != null) result.Add(target);
        }

        return result;
    }

    /// <summary>Подпись текущего узла: `cfg.label + ' · ' + id`, «—» без выбранного узла.</summary>
    public static string CurrentLabel(DialogueNode? node) =>
        node == null ? "—" : NodeTypes.InfoFor(node.Type).Label + " · " + node.Id;

    /// <summary>Текст текущей реплики (`nodeLabel`, обрезка до <see cref="TextLimit"/>).</summary>
    public static string CurrentText(
        DialogueNode? node, IReadOnlyList<Language>? languages, bool showText = true) =>
        node == null ? string.Empty : Slice(GraphLayout.NodeLabel(node, languages, showText), TextLimit);

    /// <summary>
    /// Подпись варианта перехода: `'→ ' + label + ': ' + nodeLabel`, обрезка до
    /// <see cref="ChoiceLimit"/> (в JS — `slice(0, 80)`).
    /// </summary>
    public static string ChoiceLabel(
        DialogueNode node, IReadOnlyList<Language>? languages, bool showText = true)
    {
        var text = GraphLayout.NodeLabel(node, languages, showText);
        if (text.Length == 0) text = node.Id;
        return Slice("→ " + NodeTypes.InfoFor(node.Type).Label + ": " + text, ChoiceLimit);
    }

    /// <summary>
    /// Ключ строки состояния прогона: есть варианты — «Следующие реплики», иначе у END —
    /// «Диалог завершён», у прочих (и у пропавшего узла) — «Тупик».
    /// </summary>
    public static string StatusKey(IReadOnlyList<DialogueNode>? choices, DialogueNode? current) =>
        choices is { Count: > 0 } ? "graphPlaythroughNext"
        : current != null && current.Type == NodeTypes.End ? "graphPlaythroughEnd"
        : "graphPlaythroughDeadEnd";

    /// <summary>Обрезка строки по пределу (`String.slice(0, limit)`).</summary>
    private static string Slice(string value, int limit) => value.Length > limit ? value[..limit] : value;
}
