using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Tests;

public class GraphTests
{
    [Fact]
    public void SuggestTextId_derives_from_phraseId_and_dialogueId()
    {
        var n = new DialogueNode { Id = "x__phrase_5__0", Data = new NodeData { PhraseId = "esc_dlg_5" } };
        Assert.Equal("esc_dlg_5", GraphData.SuggestTextId(n, "esc_dlg"));
        Assert.Equal("dialogue_abc", GraphData.SuggestTextId(
            new DialogueNode { Id = "abc", Data = new NodeData { PhraseId = "" } }, "dialogue"));
        Assert.Equal("dialogue_7", GraphData.SuggestTextId(
            new DialogueNode { Id = "a", Data = new NodeData { PhraseId = "dialogue_7" } }, "dialogue"));
    }

    [Fact]
    public void ValidateMetadataSyntax_flags_bad_info_and_unbalanced_precondition()
    {
        Assert.Empty(GraphData.ValidateMetadataSyntax(new Dictionary<string, List<string>>
        {
            ["has_info"] = new() { "+quest_done" },
            ["precondition"] = new() { "has_info(\"%x%\")" }
        }));

        var bad = GraphData.ValidateMetadataSyntax(new Dictionary<string, List<string>>
        {
            ["has_info"] = new() { "a b<c>" },
            ["precondition"] = new() { "has_info(\"x\"" }
        });
        Assert.Contains(bad, i => i.Code == "metadata_syntax");
        Assert.Contains(bad, i => i.Code == "metadata_balance");
    }

    [Fact]
    public void ValidateGraph_reports_dead_end_and_metadata_issues()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "s", Type = NodeTypes.Start },
            new()
            {
                Id = "p",
                Type = NodeTypes.Phrase,
                Data = new NodeData { Text = "hi", Metadata = new() { ["precondition"] = new() { "(" } } }
            }
        };
        var edges = new List<DialogueEdge> { new() { Id = "e", Source = "s", Target = "p" } };

        var issues = GraphValidator.Validate(nodes, edges);
        Assert.Contains(issues, i => i.Code == "dead_end");
        Assert.Contains(issues, i => i.Code == "metadata_balance");
    }

    [Fact]
    public void DialogueGroup_groups_by_id_prefix()
    {
        Assert.Equal("Кордон", DialogueGroups.DialogueGroup("esc_wolf_start"));
        Assert.Equal("Бар", DialogueGroups.DialogueGroup("bar_bar_visitors"));
        Assert.Equal("Торговля", DialogueGroups.DialogueGroup("tm_trader_dialog"));
        Assert.Equal("Служебные", DialogueGroups.DialogueGroup("dm_hello_dialog"));
        Assert.Equal("Осознание", DialogueGroups.DialogueGroup("oso_who_are_you"));
        Assert.Equal("Some", DialogueGroups.DialogueGroup("some_unknown_id"));
    }

    [Fact]
    public void Grouped_splits_loaded_dialogues_by_prefix_and_sorts_groups()
    {
        var dialogues = new List<DialogueGraph>
        {
            new() { Id = "tm_trader_1", Nodes = { new DialogueNode() }, Edges = { new DialogueEdge() } },
            new() { Id = "esc_wolf_1" },
            new() { Id = "bar_barman_1" },
            new() { Id = "tm_trader_2" },
            new() { Name = "" }
        };

        var buckets = DialogueGroups.Grouped(dialogues);
        // Группы отсортированы по коду символов, как `Object.keys(groups).sort()`.
        Assert.Equal(new[] { "Бар", "Кордон", "Прочее", "Торговля" }, buckets.Select(b => b.Group));
        Assert.Equal(new[] { 1, 1, 1, 2 }, buckets.Select(b => b.Dialogues.Count));
        // Диалоги внутри группы сохраняют порядок появления в проекте.
        Assert.Equal(new[] { "tm_trader_1", "tm_trader_2" }, buckets[3].Dialogues.Select(d => d.Id));
        Assert.Empty(DialogueGroups.Grouped(null));
    }

    [Fact]
    public void Name_falls_back_from_name_to_id()
    {
        Assert.Equal("Бармен", DialogueGroups.Name(new DialogueGraph { Id = "bar_barman", Name = "Бармен" }));
        Assert.Equal("bar_barman", DialogueGroups.Name(new DialogueGraph { Id = "bar_barman" }));
        Assert.Equal("", DialogueGroups.Name(new DialogueGraph { Id = "", Name = "" }));
        Assert.Equal("", DialogueGroups.Name(null));
    }

    [Fact]
    public void SafeNode_migrates_legacy_types_and_fills_defaults()
    {
        var migrated = GraphData.SafeNode(new DialogueNode { Id = "", Type = "NPC_REPLY" }, 3);
        Assert.Equal("node_3", migrated.Id);
        Assert.Equal(NodeTypes.Phrase, migrated.Type);
        Assert.Equal("npc", migrated.Data!.Role);
        Assert.Equal("migration", migrated.Data.RoleSource);

        var unknown = GraphData.SafeNode(new DialogueNode { Id = "x", Type = "" }, 0);
        Assert.Equal(NodeTypes.Unknown, unknown.Type);
    }

    [Fact]
    public void LayoutDialogue_places_start_first_and_followers_right()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "s", Type = NodeTypes.Start },
            new() { Id = "p", Type = NodeTypes.Phrase },
            new() { Id = "e", Type = NodeTypes.End }
        };
        var edges = new List<DialogueEdge>
        {
            new() { Source = "s", Target = "p" },
            new() { Source = "p", Target = "e" }
        };

        GraphLayout.LayoutDialogue(nodes, edges);
        Assert.Equal(60.0, nodes[0].X);
        Assert.Equal(380.0, nodes[1].X);
        Assert.Equal(700.0, nodes[2].X);
        Assert.Equal(80.0, nodes[0].Y);
    }

    [Fact]
    public void DialogueStats_counts_phrases_branches_and_depth()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "s", Type = NodeTypes.Start },
            new() { Id = "p1", Type = NodeTypes.Phrase, Data = new NodeData { Text = "1234" } },
            new() { Id = "p2", Type = NodeTypes.Phrase, Data = new NodeData { Text = "12" } },
            new() { Id = "e", Type = NodeTypes.End }
        };
        var edges = new List<DialogueEdge>
        {
            new() { Source = "s", Target = "p1" },
            new() { Source = "p1", Target = "p2" },
            new() { Source = "p1", Target = "e" },
            new() { Source = "p2", Target = "e" }
        };

        var stats = GraphLayout.Stats(nodes, edges);
        Assert.Equal(4, stats.Nodes);
        Assert.Equal(4, stats.Edges);
        Assert.Equal(2, stats.Phrases);
        Assert.Equal(1, stats.Branches);
        Assert.Equal(2, stats.MaxDepth);
        Assert.Equal(3, stats.AvgText);
    }

    [Fact]
    public void GraphToSvg_renders_nodes_and_edges()
    {
        var nodes = new List<DialogueNode>
        {
            new() { Id = "s", Type = NodeTypes.Start, X = 60, Y = 80 },
            new() { Id = "p", Type = NodeTypes.Phrase, X = 380, Y = 80, Data = new NodeData { Text = "hi <&>" } }
        };
        var edges = new List<DialogueEdge> { new() { Source = "s", Target = "p" } };

        var svg = GraphSvg.GraphToSvg(nodes, edges);
        Assert.Contains("<svg", svg);
        Assert.Contains("<path", svg);
        Assert.Contains("Реплика", svg);
        Assert.Contains("hi &lt;&amp;&gt;", svg);
        Assert.Equal("", GraphSvg.GraphToSvg(new List<DialogueNode>(), edges));
    }

    [Fact]
    public void NodeLabel_prefers_translation_and_falls_back_to_text_id()
    {
        var node = new DialogueNode
        {
            Id = "p",
            Type = NodeTypes.Phrase,
            Data = new NodeData { UseId = true, TextId = "st_hello", Text = "raw" }
        };
        var languages = new List<Language>
        {
            new() { Locale = "rus", Entries = new() { new LanguageEntry { Id = "st_hello", Text = "Привет" } } }
        };

        Assert.Equal("Привет", GraphLayout.NodeLabel(node, languages));
        Assert.Equal("#st_hello", GraphLayout.NodeLabel(node, languages, false));
        Assert.Equal("#st_hello", GraphLayout.NodeLabel(node, null));
        Assert.Equal("raw", GraphLayout.NodeLabel(
            new DialogueNode { Id = "p3", Data = new NodeData { Text = "raw" } }, languages));
        Assert.Equal("#st_missing", GraphLayout.NodeLabel(
            new DialogueNode { Id = "p2", Data = new NodeData { UseId = true, TextId = "st_missing" } }, null));
        Assert.Equal("Нет содержимого", GraphLayout.NodeLabel(new DialogueNode { Id = "p4" }, null));
    }
}