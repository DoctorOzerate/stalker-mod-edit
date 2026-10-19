using System.Text.Json;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Tests;

public class TextIdUsageCatalogTests
{
    private static DialogueGraph Graph(string id, params string[] textIds) => new()
    {
        Id = id,
        Nodes = textIds.Select((textId, index) => new DialogueNode
        {
            Id = "n" + index,
            Type = NodeTypes.Phrase,
            Data = new NodeData { TextId = textId, UseId = true }
        }).ToList()
    };

    [Fact]
    public void Analyze_counts_active_phrases_once_and_includes_imported_dialogues()
    {
        var project = new Project
        {
            DialogueId = "active",
            Nodes = Graph("active", "st_a", "st_a").Nodes,
            Dialogues = new()
            {
                Graph("active", "st_a", "st_a"),
                Graph("imported", "st_a", "st_b")
            }
        };

        var usage = TextIdUsageCatalog.Analyze(project);

        Assert.Equal(3, usage["st_a"].Phrases);
        Assert.Equal(1, usage["st_b"].Phrases);
        Assert.All(usage.Values, item => Assert.False(item.InProjectFiles));
    }

    [Fact]
    public void Analyze_reads_ltx_values_but_ignores_sections_and_comments()
    {
        var project = new Project
        {
            ItemsLtx = """
                [wpn_ak]
                inv_name = st_ak_name
                description = "st_ak_desc" ; комментарий
                ; inv_name = st_commented
                empty =
                [section_without_value]
                """,
            SystemLtx = "hint = st_hint",
            LtxFiles = new() { ["misc"] = "custom = st_custom" },
            ItemFiles = new() { ["weapons"] = "name = st_item" }
        };

        var usage = TextIdUsageCatalog.Analyze(project);

        AssertAllFilesUsage(usage, "st_ak_name", "st_ak_desc", "st_hint", "st_custom", "st_item");
        Assert.False(usage.ContainsKey("st_commented"));
        Assert.False(usage.ContainsKey("wpn_ak"));
        Assert.False(usage.ContainsKey("section_without_value"));
    }

    [Fact]
    public void Analyze_finds_ids_in_scripts_bio_quests_encyclopedia_and_extra()
    {
        var project = new Project
        {
            Scripts = new()
            {
                new ScriptFile
                {
                    Text = "local a = \"st_script\"\nlocal b = 'st_single'\nlocal c = st_identifier",
                    Original = "local old = \"st_script_original\""
                }
            },
            Dialogues = new()
            {
                new DialogueGraph
                {
                    Id = "extra_graph",
                    Extra = new()
                    {
                        ["tag"] = JsonSerializer.SerializeToElement("st_graph_extra")
                    },
                    Nodes = new()
                    {
                        new DialogueNode
                        {
                            Id = "extra_node",
                            Extra = new()
                            {
                                ["tag"] = JsonSerializer.SerializeToElement("st_node_extra")
                            },
                            Data = new NodeData
                            {
                                Extra = new()
                                {
                                    ["tag"] = JsonSerializer.SerializeToElement("st_data_extra")
                                }
                            }
                        }
                    },
                    Edges = new()
                    {
                        new DialogueEdge
                        {
                            Id = "extra_edge",
                            Extra = new()
                            {
                                ["tag"] = JsonSerializer.SerializeToElement("st_edge_extra")
                            }
                        }
                    }
                }
            },
            Edges = new()
            {
                new DialogueEdge
                {
                    Id = "active_edge",
                    Extra = new()
                    {
                        ["tag"] = JsonSerializer.SerializeToElement("st_active_edge_extra")
                    }
                }
            },
            Characters = new()
            {
                new Character { Name = "st_character", Bio = "st_bio" }
            },
            Quests = new()
            {
                new Quest
                {
                    Title = "st_quest",
                    Text = "st_quest_text",
                    Article = "st_quest_article",
                    Objectives = new()
                    {
                        new QuestObjective
                        {
                            Text = "st_objective",
                            Article = "st_objective_article"
                        }
                    }
                }
            },
            Encyclopedia = new()
            {
                new EncyclopediaArticle
                {
                    Name = "st_article",
                    Ltx = "st_article_ltx",
                    Text = "st_article_text"
                }
            },
            GeneralDesc = JsonSerializer.SerializeToElement(new { text = "st_general" }),
            Extra = new()
            {
                ["custom"] = JsonSerializer.SerializeToElement(new { nested = new[] { "st_extra" } })
            }
        };

        var usage = TextIdUsageCatalog.Analyze(project);

        AssertAllFilesUsage(usage,
            "st_script", "st_single", "st_script_original", "st_character", "st_bio", "st_quest",
            "st_quest_text", "st_quest_article", "st_objective", "st_objective_article",
            "st_article", "st_article_ltx", "st_article_text", "st_general", "st_extra",
            "st_graph_extra", "st_node_extra", "st_data_extra", "st_edge_extra", "st_active_edge_extra");
        Assert.False(usage.ContainsKey("st_identifier"));
    }

    [Fact]
    public void Analyze_with_known_ids_skips_other_file_values()
    {
        var project = new Project
        {
            ItemsLtx = "first = st_used\nsecond = st_other",
            SystemLtx = "third = st_unlisted"
        };

        var usage = TextIdUsageCatalog.Analyze(project, new[] { "st_used", "st_other" });

        Assert.True(usage["st_used"].InProjectFiles);
        Assert.True(usage["st_other"].InProjectFiles);
        Assert.False(usage.ContainsKey("st_unlisted"));
    }

    [Fact]
    public void Analyze_does_not_report_unknown_ids_and_handles_empty_project()
    {
        var usage = TextIdUsageCatalog.Analyze(new Project
        {
            DialogueId = "active",
            Nodes = Graph("active", "st_known").Nodes
        });

        Assert.True(usage["st_known"].IsUsed);
        Assert.False(usage["st_known"].InProjectFiles);
        Assert.False(usage.ContainsKey("st_missing"));
        Assert.Empty(TextIdUsageCatalog.Analyze(null));
        Assert.Empty(TextIdUsageCatalog.Analyze(ProjectMigration.Empty()));
    }

    private static void AssertAllFilesUsage(
        IReadOnlyDictionary<string, TextIdUsage> usage, params string[] ids)
    {
        foreach (var id in ids)
        {
            Assert.True(usage.TryGetValue(id, out var item), "Нет ссылки: " + id);
            Assert.Equal(0, item!.Phrases);
            Assert.True(item.InProjectFiles);
            Assert.True(item.IsUsed);
        }
    }
}
