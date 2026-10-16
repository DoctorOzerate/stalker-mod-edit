using XRayForge.Core.Dialogs;
using XRayForge.Core.Models;

namespace XRayForge.Tests;

public class DialogsTests
{
    private static DialogueGraph MakeDialog(string id, NodeData? data = null) => new()
    {
        Id = id,
        Nodes = new List<DialogueNode>
        {
            new() { Id = "start", Type = NodeTypes.Start },
            new() { Id = "p1", Type = NodeTypes.Phrase, Data = data ?? new NodeData { PhraseId = "0", TextId = "st_1", UseId = true } },
            new() { Id = "end", Type = NodeTypes.End }
        },
        Edges = new List<DialogueEdge>
        {
            new() { Id = "e1", Source = "start", Target = "p1" },
            new() { Id = "e2", Source = "p1", Target = "end" }
        }
    };

    [Fact]
    public void DialogueToXml_round_trips_sound_and_metadata()
    {
        var dialog = MakeDialog("bar", new NodeData
        {
            PhraseId = "0",
            TextId = "st_1",
            UseId = true,
            VoiceId = "chars\\bar\\bar.ogg",
            Metadata = new Dictionary<string, List<string>>
            {
                ["has_info"] = new() { "a" },
                ["give_info"] = new() { "b" }
            }
        });

        var xml = DialogueExport.DialogueToXml(dialog);
        Assert.Contains("<dialog id=\"bar\">", xml);
        Assert.Contains("<sound>chars\\bar\\bar.ogg</sound>", xml);
        Assert.Contains("<has_info>a</has_info>", xml);
        Assert.Contains("<give_info>b</give_info>", xml);
    }

    [Fact]
    public void DialogueToXml_emits_next_only_between_phrases()
    {
        var dialog = MakeDialog("two");
        dialog.Nodes.Insert(2, new DialogueNode
        {
            Id = "p2",
            Type = NodeTypes.Phrase,
            Data = new NodeData { PhraseId = "1", TextId = "st_2", UseId = true }
        });
        dialog.Edges.Add(new DialogueEdge { Id = "e3", Source = "p1", Target = "p2" });

        var xml = DialogueExport.DialogueToXml(dialog);
        Assert.Contains("<phrase id=\"0\">", xml);
        Assert.Contains("<next>1</next>", xml);
    }

    [Fact]
    public void DialogueToXml_throws_on_graph_errors()
    {
        var broken = MakeDialog("bad");
        broken.Nodes.RemoveAt(1); // без PHRASE связи ведут в никуда

        Assert.Throws<InvalidOperationException>(() => DialogueExport.DialogueToXml(broken));
    }

    [Fact]
    public void DialogsLtx_lists_ids()
    {
        Assert.Equal("[dialogs]\ndialogs = a, b\n", DialogueExport.DialogsLtx(new[] { "a", "b" }));
    }

    [Fact]
    public void ValidateTextIds_reports_missing_translations()
    {
        var ok = TextIdTools.ValidateTextIds(
            new[] { MakeDialog("d") },
            new List<Language>
            {
                new() { Locale = "rus", Entries = new() { new LanguageEntry { Id = "st_1", Text = "x" } } }
            });
        Assert.Empty(ok);

        var bad = TextIdTools.ValidateTextIds(
            new[]
            {
                new DialogueGraph
                {
                    Id = "d",
                    Nodes = new() { new DialogueNode { Type = NodeTypes.Phrase, Data = new NodeData { TextId = "missing" } } }
                }
            },
            new List<Language>());

        Assert.Single(bad);
        Assert.Equal("missing", bad[0].TextId);
    }

    [Fact]
    public void GameFilesFromDialogues_builds_multi_dialog_xml_and_ltx()
    {
        var files = DialogueFiles.GameFilesFromDialogues(
            new[]
            {
                new DialogueFileGroup { FileName = "bar", Dialogues = new() { MakeDialog("bar"), MakeDialog("baz") } }
            },
            "config", "windows-1251",
            new List<Language>
            {
                new() { Locale = "rus", Name = "Русский", Entries = new() { new LanguageEntry { Id = "st_1", Text = "Привет" } } }
            },
            "bar_mod");

        Assert.Contains("gamedata/config/gameplay/dialogs_bar.xml", files.Keys);
        Assert.Contains("bar", files["gamedata/config/gameplay/dialogs_bar.xml"]);
        Assert.Contains("bar", files["gamedata/config/gameplay/dialogs.ltx"]);
        Assert.Contains("baz", files["gamedata/config/gameplay/dialogs.ltx"]);
        Assert.Contains("gamedata/config/text/rus/st_bar_mod.xml", files.Keys);
    }

    [Fact]
    public void GameFilesFromDialogues_uses_configs_dir_and_utf8_declaration()
    {
        var files = DialogueFiles.GameFilesFromDialogues(
            new[] { new DialogueFileGroup { FileName = "a b c", Dialogues = new() { MakeDialog("d") } } },
            "configs", "utf-8", null, null);

        Assert.Contains("gamedata/configs/gameplay/dialogs_a_b_c.xml", files.Keys);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", files["gamedata/configs/gameplay/dialogs_a_b_c.xml"]);
        Assert.Contains("gamedata/configs/text/rus/st_dialogue.xml", files.Keys);
    }

    [Fact]
    public void FindTextIdIssues_reports_duplicates_empty_missing()
    {
        var languages = new List<Language>
        {
            new()
            {
                Locale = "rus",
                Entries = new()
                {
                    new LanguageEntry { Id = "a", Text = "x" },
                    new LanguageEntry { Id = "a", Text = "y" },
                    new LanguageEntry { Id = "b", Text = "" }
                }
            }
        };
        var dialogues = new[]
        {
            new DialogueGraph
            {
                Nodes = new()
                {
                    new DialogueNode { Type = NodeTypes.Phrase, Data = new NodeData { TextId = "a" } },
                    new DialogueNode { Type = NodeTypes.Phrase, Data = new NodeData { TextId = "missing" } }
                }
            }
        };

        var issues = TextIdTools.FindTextIdIssues(languages, dialogues);
        Assert.Contains(issues, i => i.Message.Contains("Дубликат"));
        Assert.Contains(issues, i => i.Message.Contains("Пустой"));
        Assert.Contains(issues, i => i.Message.Contains("missing"));
    }

    [Fact]
    public void ResolveTextId_finds_translation_across_locales()
    {
        var languages = new List<Language>
        {
            new() { Locale = "eng", Entries = new() { new LanguageEntry { Id = "a", Text = "" } } },
            new() { Locale = "rus", Entries = new() { new LanguageEntry { Id = "a", Text = "Привет" } } }
        };

        var resolved = TextIdTools.ResolveTextId(languages, "a");
        Assert.NotNull(resolved);
        Assert.Equal("Привет", resolved!.Text);
        Assert.Equal("rus", resolved.Locale);
        Assert.Null(TextIdTools.ResolveTextId(languages, "nope"));
    }

    [Fact]
    public void TextIdCoverage_computes_translation_coverage()
    {
        var dialogues = new[]
        {
            new DialogueGraph
            {
                Nodes = new()
                {
                    new DialogueNode { Type = NodeTypes.Phrase, Data = new NodeData { TextId = "a" } },
                    new DialogueNode { Type = NodeTypes.Phrase, Data = new NodeData { TextId = "b" } },
                    new DialogueNode { Type = NodeTypes.Phrase, Data = new NodeData { TextId = "c" } }
                }
            }
        };
        var languages = new List<Language>
        {
            new()
            {
                Locale = "rus",
                Entries = new() { new LanguageEntry { Id = "a", Text = "x" }, new LanguageEntry { Id = "b", Text = "y" } }
            }
        };

        var cov = TextIdTools.Coverage(dialogues, languages);
        Assert.Equal(3, cov.Total);
        Assert.Equal(2, cov.Translated);
        Assert.Equal(67, cov.Pct);
        Assert.Equal(100, TextIdTools.Coverage(Array.Empty<DialogueGraph>(), languages).Pct);
    }
}