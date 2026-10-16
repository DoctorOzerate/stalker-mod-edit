using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Tests;

/// <summary>
/// Шаг 6.7: чистый прогон диалога (`GraphPlaythrough`) — старт, шаги по связям, подписи текущей
/// реплики и вариантов перехода, состояние панели прогона.
/// </summary>
public class GraphPlaythroughTests
{
    private static List<DialogueNode> Nodes() => new()
    {
        new() { Id = "p1", Type = NodeTypes.Phrase, Data = new NodeData { Text = "Привет" } },
        new() { Id = "s", Type = NodeTypes.Start },
        new() { Id = "e", Type = NodeTypes.End },
        new() { Id = "p2", Type = NodeTypes.Phrase, Data = new NodeData { Text = "Пока" } }
    };

    private static List<DialogueEdge> Edges() => new()
    {
        new() { Id = "e1", Source = "s", Target = "p1" },
        new() { Id = "e2", Source = "p1", Target = "p2" },
        new() { Id = "e3", Source = "p1", Target = "e" },
        new() { Id = "e4", Source = "e", Target = "gone" }
    };

    [Fact]
    public void Start_picks_start_node_even_when_it_is_not_first()
    {
        var state = GraphPlaythrough.Start(Nodes());

        Assert.NotNull(state);
        Assert.Equal("s", state!.Current);
        Assert.Equal(new[] { "s" }, state.Path);
    }

    [Fact]
    public void Start_falls_back_to_first_node_and_returns_null_for_empty_graph()
    {
        var phrases = new List<DialogueNode> { new() { Id = "a", Type = NodeTypes.Phrase } };
        Assert.Equal("a", GraphPlaythrough.Start(phrases)!.Current);

        Assert.Null(GraphPlaythrough.Start(null));
        Assert.Null(GraphPlaythrough.Start(new List<DialogueNode>()));
    }

    [Fact]
    public void Step_appends_visited_node_to_path()
    {
        var state = GraphPlaythrough.Start(Nodes())!;
        var next = GraphPlaythrough.Step(state, "p1");
        next = GraphPlaythrough.Step(next, "e");

        Assert.Equal("e", next.Current);
        Assert.Equal(new[] { "s", "p1", "e" }, next.Path);
        // Исходное состояние шаг не меняет (в JS path.concat создаёт новый массив).
        Assert.Single(state.Path);
    }

    [Fact]
    public void Choices_follow_outgoing_edges_in_order_and_skip_missing_targets()
    {
        var choices = GraphPlaythrough.Choices(Nodes(), Edges(), "p1");

        Assert.Equal(new[] { "p2", "e" }, choices.Select(node => node.Id));
        // Связь в неизвестный узел отбрасывается (`filter(Boolean)` в JS).
        Assert.Empty(GraphPlaythrough.Choices(Nodes(), Edges(), "e"));
        Assert.Empty(GraphPlaythrough.Choices(Nodes(), Edges(), null));
    }

    [Fact]
    public void CurrentLabel_uses_type_label_and_id()
    {
        Assert.Equal("Старт · s", GraphPlaythrough.CurrentLabel(GraphPlaythrough.Current(Nodes(), "s")));
        Assert.Equal("Реплика · p1", GraphPlaythrough.CurrentLabel(GraphPlaythrough.Current(Nodes(), "p1")));
        // Пропавший узел (удалён, отменён) рисуется прочерком (`cur ? … : '—'`).
        Assert.Equal("—", GraphPlaythrough.CurrentLabel(GraphPlaythrough.Current(Nodes(), "gone")));
    }

    [Fact]
    public void CurrentText_uses_node_label_and_stops_at_160_chars()
    {
        Assert.Equal("Привет", GraphPlaythrough.CurrentText(GraphPlaythrough.Current(Nodes(), "p1"), null));
        Assert.Equal("", GraphPlaythrough.CurrentText(null, null));

        var longNode = new DialogueNode
        {
            Id = "long",
            Type = NodeTypes.Phrase,
            Data = new NodeData { Text = new string('я', 200) }
        };
        Assert.Equal(160, GraphPlaythrough.CurrentText(longNode, null).Length);
    }

    [Fact]
    public void ChoiceLabel_has_arrow_prefix_and_stops_at_80_chars()
    {
        Assert.Equal("→ Реплика: Пока", GraphPlaythrough.ChoiceLabel(Nodes()[3], null));

        var longNode = new DialogueNode
        {
            Id = "long",
            Type = NodeTypes.Phrase,
            Data = new NodeData { Text = new string('x', 200) }
        };
        var label = GraphPlaythrough.ChoiceLabel(longNode, null);
        Assert.Equal(80, label.Length);
        Assert.StartsWith("→ Реплика: ", label);
    }

    [Fact]
    public void StatusKey_reports_next_lines_end_of_dialogue_and_dead_end()
    {
        var nodes = Nodes();
        var current = GraphPlaythrough.Current(nodes, "p1")!;
        Assert.Equal("graphPlaythroughNext", GraphPlaythrough.StatusKey(new[] { nodes[3] }, current));

        var end = GraphPlaythrough.Current(nodes, "e")!;
        Assert.Equal("graphPlaythroughEnd", GraphPlaythrough.StatusKey(Array.Empty<DialogueNode>(), end));

        // Реплика без связей и пропавший узел — тупик (`cur && cur.type === 'END'` не выполняется).
        Assert.Equal("graphPlaythroughDeadEnd",
            GraphPlaythrough.StatusKey(Array.Empty<DialogueNode>(), current));
        Assert.Equal("graphPlaythroughDeadEnd",
            GraphPlaythrough.StatusKey(Array.Empty<DialogueNode>(), null));
    }
}
