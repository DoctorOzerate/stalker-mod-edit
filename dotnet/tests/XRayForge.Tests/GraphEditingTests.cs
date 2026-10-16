using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Tests;

/// <summary>Этап 6: чистые операции редактора графа (`GraphEditing`) — порт обработчиков App.js.</summary>
public class GraphEditingTests
{
    [Fact]
    public void SanitizeDialogueId_keeps_word_chars_only()
    {
        Assert.Equal("esc_dlg_1", GraphEditing.SanitizeDialogueId("esc_dlg_1"));
        Assert.Equal("esc_dlg_1", GraphEditing.SanitizeDialogueId("  esc_dlg_1  "));
        Assert.Equal("my_dialog_", GraphEditing.SanitizeDialogueId("my dialog!"));
        Assert.Equal("dialogue", GraphEditing.SanitizeDialogueId(""));
        Assert.Equal("dialogue", GraphEditing.SanitizeDialogueId(null));
        Assert.Equal("dialogue", GraphEditing.SanitizeDialogueId("   "));
        // Не-ASCII символы заменяются подчёркиванием (регексп `[^a-zA-Z0-9_]`).
        Assert.Equal("______", GraphEditing.SanitizeDialogueId("диалог"));
    }

    [Fact]
    public void NextPhraseId_returns_first_free_number()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "a", Data = new NodeData { PhraseId = "trader_demo_1" } },
            new() { Id = "b", Data = new NodeData { PhraseId = "trader_demo_2" } }
        };

        Assert.Equal("trader_demo_3", GraphEditing.NextPhraseId(nodes, "trader_demo"));
        Assert.Equal("other_dialog_1", GraphEditing.NextPhraseId(nodes, "other_dialog"));
        Assert.Equal("dialogue_1", GraphEditing.NextPhraseId(null, ""));
        // Некорректный id очищается как в `createDialogue` (пробел и «!» → подчёркивание).
        Assert.Equal("bad_id__1", GraphEditing.NextPhraseId(nodes, "bad id!"));
    }

    [Fact]
    public void PalettePosition_cascades_from_120_120()
    {
        Assert.Equal((120d, 120d), GraphEditing.PalettePosition(0));
        Assert.Equal((192d, 168d), GraphEditing.PalettePosition(3));
        Assert.Equal((120d, 120d), GraphEditing.PalettePosition(-5));
    }

    [Fact]
    public void CreateStartNode_builds_start_node_with_fixed_id()
    {
        var node = GraphEditing.CreateStartNode();
        Assert.Equal(NodeTypes.Start, node.Type);
        Assert.Equal("start", node.Id);
        Assert.Equal(60d, node.X);
        Assert.Equal(180d, node.Y);
        Assert.NotNull(node.Data);
    }

    [Fact]
    public void CreateNode_sets_phrase_id_for_phrase_and_cascade_position()
    {
        var existing = new List<DialogueNode> { new() { Id = "p", Data = new NodeData { PhraseId = "esc_1" } } };

        var phrase = GraphEditing.CreateNode(NodeTypes.Phrase, 1, "esc", existing);
        Assert.Equal(NodeTypes.Phrase, phrase.Type);
        Assert.Equal(144d, phrase.X);
        Assert.Equal(136d, phrase.Y);
        Assert.Equal("esc_2", phrase.Data!.PhraseId);

        var end = GraphEditing.CreateNode(NodeTypes.End, 0, "esc", existing);
        Assert.Equal(NodeTypes.End, end.Type);
        Assert.Equal("", end.Data!.PhraseId);
        Assert.StartsWith("end_", end.Id);

        var fallback = GraphEditing.CreateNode(null, 0, "esc", existing);
        Assert.Equal(NodeTypes.Phrase, fallback.Type);
    }

    [Fact]
    public void Duplicate_offsets_copy_and_clears_phrase_id()
    {
        var source = new DialogueNode
        {
            Id = "npc_hi",
            Type = NodeTypes.Phrase,
            X = 340,
            Y = 150,
            Data = new NodeData
            {
                PhraseId = "trader_demo_1",
                Text = "Ну что, сталкер?",
                Metadata = new() { ["has_info"] = new() { "+quest_done" } }
            }
        };

        var copy = GraphEditing.Duplicate(source);
        Assert.Matches("^node_[a-z0-9]{6}$", copy.Id);
        Assert.Equal(380d, copy.X);
        Assert.Equal(190d, copy.Y);
        Assert.Equal("", copy.Data!.PhraseId);
        Assert.Equal("Ну что, сталкер?", copy.Data.Text);

        // Копия независима: правка метаданных не задевает исходный узел.
        copy.Data.Metadata["has_info"].Add("+other");
        Assert.Single(source.Data!.Metadata["has_info"]);
    }

    [Fact]
    public void WithoutNode_drops_node_and_its_edges()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "start" }, new() { Id = "mid" }, new() { Id = "end" }
        };
        var edges = new List<DialogueEdge>
        {
            new() { Id = "e1", Source = "start", Target = "mid" },
            new() { Id = "e2", Source = "mid", Target = "end" }
        };

        var (keptNodes, keptEdges) = GraphEditing.WithoutNode(nodes, edges, "mid");
        Assert.Equal(new[] { "start", "end" }, keptNodes.Select(node => node.Id));
        Assert.Empty(keptEdges);

        var (sameNodes, sameEdges) = GraphEditing.WithoutNode(null, null, "mid");
        Assert.Empty(sameNodes);
        Assert.Empty(sameEdges);
    }

    [Fact]
    public void WithoutEdge_removes_only_matching_edge()
    {
        var edges = new List<DialogueEdge>
        {
            new() { Id = "e1", Source = "a", Target = "b" },
            new() { Id = "e2", Source = "b", Target = "c" }
        };

        var kept = GraphEditing.WithoutEdge(edges, "e1");
        Assert.Single(kept);
        Assert.Equal("e2", kept[0].Id);
        Assert.Empty(GraphEditing.WithoutEdge(null, "e1"));
    }

    [Fact]
    public void Connect_rejects_duplicates_and_self_loops()
    {
        var edges = new List<DialogueEdge> { new() { Id = "e_x", Source = "a", Target = "b" } };

        Assert.Null(GraphEditing.Connect(edges, "a", "b"));
        Assert.Null(GraphEditing.Connect(edges, "a", "a"));
        Assert.Null(GraphEditing.Connect(edges, null, "b"));
        Assert.Null(GraphEditing.Connect(edges, "a", ""));

        var added = GraphEditing.Connect(edges, "b", "c");
        Assert.NotNull(added);
        Assert.Equal("b", added!.Source);
        Assert.Equal("c", added.Target);
        Assert.StartsWith("e_", added.Id);

        // Обратная связь допустима, id не совпадает с уже существующими.
        var back = GraphEditing.Connect(new[] { edges[0], added! }, "c", "b");
        Assert.NotNull(back);
        Assert.NotEqual(added.Id, back!.Id);
    }

    [Fact]
    public void MarqueeIds_selects_nodes_intersecting_rect()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "inside", X = 100, Y = 100 },
            new() { Id = "touching", X = 300, Y = 190 },
            new() { Id = "outside", X = 700, Y = 700 }
        };

        Assert.Equal(new[] { "inside" }, GraphEditing.MarqueeIds(nodes, 90, 90, 200, 200));
        Assert.Equal(new[] { "inside", "touching" }, GraphEditing.MarqueeIds(nodes, 90, 90, 320, 300));
        // Координаты рамки могут быть заданы в обратном порядке (drag влево-вверх).
        Assert.Equal(new[] { "inside" }, GraphEditing.MarqueeIds(nodes, 200, 200, 90, 90));
        Assert.Empty(GraphEditing.MarqueeIds(null, 0, 0, 10, 10));
    }

    [Fact]
    public void MarqueeRect_normalizes_corners_and_keeps_click_width()
    {
        Assert.Equal((10d, 20d, 90d, 80d), GraphEditing.MarqueeRect(10, 20, 100, 100));
        // Рамка может тянуться влево-вверх: углы нормализуются.
        Assert.Equal((10d, 20d, 90d, 80d), GraphEditing.MarqueeRect(100, 100, 10, 20));
        // Клик без движения: ширина не меньше 1px (`Math.max(1, x2 − x1)` в JS), высота — 0.
        Assert.Equal((10d, 20d, 1d, 0d), GraphEditing.MarqueeRect(10, 20, 10, 20));
    }

    [Fact]
    public void DragDelta_divides_screen_shift_by_zoom()
    {
        Assert.Equal((30d, -10d), GraphEditing.DragDelta(100, 100, 130, 90, 1));
        // При зуме 200% курсор проходит вдвое меньше координат графа (шаг 6.3).
        Assert.Equal((15d, -5d), GraphEditing.DragDelta(100, 100, 130, 90, 2));
        // Некорректный зум считается единицей: деления на ноль нет.
        Assert.Equal((30d, -10d), GraphEditing.DragDelta(100, 100, 130, 90, 0));
    }

    [Fact]
    public void PanShift_adds_screen_delta_to_origin_pan()
    {
        Assert.Equal((-20d, 40d), GraphEditing.PanShift(0, 0, 300, 200, 280, 240));
        // Сдвиг холста не зависит от зума: панорама идёт 1:1 за курсором.
        Assert.Equal((1.5d, 17.5d), GraphEditing.PanShift(10, 10, 0, 0, -8.5, 7.5));
    }

    [Fact]
    public void ResolveDragMode_matches_mouse_down_branches_of_the_editor()
    {
        // Shift+клик по карточке — только переключение группового выделения, переноса нет.
        Assert.Equal(GraphDragMode.None, GraphEditing.ResolveDragMode(true, true, false, 0));
        Assert.Equal(GraphDragMode.None, GraphEditing.ResolveDragMode(true, true, true, 3));
        // Узел группы (больше одного) — перенос всей группы.
        Assert.Equal(GraphDragMode.Group, GraphEditing.ResolveDragMode(true, false, true, 3));
        // Группа из одного узла переносится как один узел (`multiSelect.length > 1` в JS).
        Assert.Equal(GraphDragMode.Node, GraphEditing.ResolveDragMode(true, false, true, 1));
        Assert.Equal(GraphDragMode.Node, GraphEditing.ResolveDragMode(true, false, false, 0));
        // Пустой холст: Shift — рамка выделения, без Shift — панорама.
        Assert.Equal(GraphDragMode.Marquee, GraphEditing.ResolveDragMode(false, true, false, 0));
        Assert.Equal(GraphDragMode.Pan, GraphEditing.ResolveDragMode(false, false, false, 0));
    }

    [Fact]
    public void Matches_searches_label_and_id_case_insensitively()
    {
        Assert.True(GraphEditing.Matches("Ну что, сталкер", "npc_hi", "СТАЛКЕР"));
        Assert.True(GraphEditing.Matches("Ну что", "npc_hi", "npc_"));
        Assert.True(GraphEditing.Matches("Ну что", "npc_hi", ""));
        Assert.True(GraphEditing.Matches("Ну что", "npc_hi", null));
        Assert.False(GraphEditing.Matches("Ну что", "npc_hi", "артефакт"));
    }

    [Fact]
    public void ClampZoom_and_ZoomBy_keep_zoom_in_range()
    {
        Assert.Equal(0.5, GraphEditing.ClampZoom(0.1));
        Assert.Equal(2.0, GraphEditing.ClampZoom(9));
        Assert.Equal(1.0, GraphEditing.ClampZoom(1));
        Assert.Equal(0.7, GraphEditing.ZoomBy(0.6, GraphEditing.ZoomStep));
        Assert.Equal(0.5, GraphEditing.ZoomBy(0.5, -GraphEditing.ZoomStep));
        Assert.Equal(2.0, GraphEditing.ZoomBy(2, GraphEditing.ZoomStep));
    }

    [Fact]
    public void CenterPan_centers_content_and_defaults_for_empty_graph()
    {
        Assert.Equal((20d, 20d), GraphEditing.CenterPan(Array.Empty<DialogueNode>(), 800, 600));
        Assert.Equal((20d, 20d), GraphEditing.CenterPan(null, 800, 600));

        var nodes = new List<DialogueNode> { new() { Id = "start", X = 60, Y = 180 } };
        var (x, y) = GraphEditing.CenterPan(nodes, 800, 600);
        // Центр содержимого: (60 + (60 + 210)) / 2 = 165 по X и (180 + (180 + 100)) / 2 = 230 по Y.
        Assert.Equal(235d, x);
        Assert.Equal(70d, y);
    }

    [Fact]
    public void Capture_makes_independent_snapshot()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "start", Type = NodeTypes.Start, X = 60, Data = new NodeData { Text = "hi" } }
        };
        var edges = new List<DialogueEdge> { new() { Id = "e1", Source = "start", Target = "start" } };

        var snapshot = GraphEditing.Capture(nodes, edges);
        nodes[0].X = 999;
        nodes[0].Data!.Text = "changed";
        nodes.Add(new DialogueNode { Id = "extra" });
        edges.Clear();

        Assert.Single(snapshot.Nodes);
        Assert.Equal(60d, snapshot.Nodes[0].X);
        Assert.Equal("hi", snapshot.Nodes[0].Data!.Text);
        Assert.Single(snapshot.Edges);
        Assert.Equal("e1", snapshot.Edges[0].Id);
    }

    [Fact]
    public void ResolvePortClick_arms_first_port_and_connects_second()
    {
        // Первый клик по порту взводит его (`connectFrom = { id, kind }`).
        var armed = GraphEditing.ResolvePortClick(null, "start", GraphPortKind.Out);
        Assert.False(armed.CreatesEdge);
        Assert.Null(armed.Source);
        Assert.Null(armed.Target);
        Assert.Equal(new GraphPortRef("start", GraphPortKind.Out), armed.Pending);

        // Второй порт другого вида замыкает пару: выход источника → вход цели.
        var pair = GraphEditing.ResolvePortClick(armed.Pending, "phrase_1", GraphPortKind.In);
        Assert.True(pair.CreatesEdge);
        Assert.Equal("start", pair.Source);
        Assert.Equal("phrase_1", pair.Target);
        Assert.Null(pair.Pending);

        // Взведённый вход разворачивает пару: источник — кликнутый выход.
        var reversed = GraphEditing.ResolvePortClick(
            new GraphPortRef("phrase_1", GraphPortKind.In), "start", GraphPortKind.Out);
        Assert.Equal("start", reversed.Source);
        Assert.Equal("phrase_1", reversed.Target);
    }

    [Fact]
    public void ResolvePortClick_cancels_on_same_port_or_same_kind()
    {
        var armed = new GraphPortRef("start", GraphPortKind.Out);

        // Повторный клик по тому же порту снимает взвод.
        var same = GraphEditing.ResolvePortClick(armed, "start", GraphPortKind.Out);
        Assert.Null(same.Pending);
        Assert.False(same.CreatesEdge);

        // Порт того же вида на другой карточке — тоже отмена (`connectFrom.kind === kind`).
        var sameKind = GraphEditing.ResolvePortClick(armed, "phrase_1", GraphPortKind.Out);
        Assert.Null(sameKind.Pending);
        Assert.False(sameKind.CreatesEdge);

        // Клик без id (порта нет) ничего не взводит и связи не создаёт.
        Assert.Null(GraphEditing.ResolvePortClick(null, null, GraphPortKind.Out).Pending);
        Assert.Null(GraphEditing.ResolvePortClick(null, "", GraphPortKind.In).Pending);
    }

    [Fact]
    public void Connect_after_port_click_rejects_existing_edge()
    {
        var edges = new List<DialogueEdge> { new() { Id = "e_1", Source = "start", Target = "phrase_1" } };

        var pair = GraphEditing.ResolvePortClick(
            new GraphPortRef("start", GraphPortKind.Out), "phrase_1", GraphPortKind.In);

        // Связь уже есть: `portClick` в JS молча снимает взвод, дубликата не появляется.
        Assert.Null(GraphEditing.Connect(edges, pair.Source, pair.Target));
        Assert.Single(edges);
    }

    [Fact]
    public void EdgePath_connects_right_port_of_source_to_left_port_of_target()
    {
        var source = new DialogueNode { Id = "start", X = 60, Y = 180 };
        var target = new DialogueNode { Id = "phrase_1", X = 300, Y = 180 };

        // x1 = 60 + 210 = 270, y1 = 180 + 44 = 224; dx = max(80, |300 − 270| / 2 = 15) = 80.
        Assert.Equal("M 270 224 C 350 224, 220 224, 300 224", GraphEditing.EdgePath(source, target));
    }

    [Fact]
    public void EdgePath_uses_minimum_curve_for_backward_edges()
    {
        var source = new DialogueNode { Id = "a", X = 500, Y = 0 };
        var target = new DialogueNode { Id = "b", X = 100, Y = 40 };

        // dx = max(80, |100 − 710| / 2 = 305) = 305 — связь «назад» выгибается петлёй.
        Assert.Equal("M 710 44 C 1015 44, -205 84, 100 84", GraphEditing.EdgePath(source, target));
    }

    [Fact]
    public void EdgePath_is_empty_without_both_ends()
    {
        var node = new DialogueNode { Id = "a", X = 10, Y = 10 };

        Assert.Equal(string.Empty, GraphEditing.EdgePath(null, node));
        Assert.Equal(string.Empty, GraphEditing.EdgePath(node, null));
        Assert.Equal(string.Empty, GraphEditing.EdgePath(null, null));
    }

}
