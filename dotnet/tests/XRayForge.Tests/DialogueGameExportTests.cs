using XRayForge.Core.Dialogs;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;

namespace XRayForge.Tests;

/// <summary>Шаг 6.9: игровой экспорт диалогов в <c>gamedata</c>.</summary>
public class DialogueGameExportTests
{
    private static DialogueGraph MakeDialog(string id) => new()
    {
        Id = id,
        Nodes = new List<DialogueNode>
        {
            new() { Id = "start", Type = NodeTypes.Start },
            new()
            {
                Id = "p1", Type = NodeTypes.Phrase,
                Data = new NodeData { PhraseId = "0", TextId = "st_" + id, Text = "Привет", UseId = true }
            },
            new() { Id = "end", Type = NodeTypes.End }
        },
        Edges = new List<DialogueEdge>
        {
            new() { Id = "e1", Source = "start", Target = "p1" },
            new() { Id = "e2", Source = "p1", Target = "end" }
        }
    };

    [Fact]
    public void Groups_merge_dialogues_from_same_source_file()
    {
        var project = new Project();
        var first = MakeDialog("bar_a");
        first.SourceFile = "dialogs_bar.xml";
        var second = MakeDialog("bar_b");
        second.SourceFile = "dialogs_bar.xml";
        project.Dialogues.AddRange(new[] { first, second, MakeDialog("esc_c") });

        var groups = DialogueGameExport.Groups(project);

        Assert.Equal(2, groups.Count);
        Assert.Equal("bar", groups[0].FileName);
        Assert.Equal(new[] { "bar_a", "bar_b" }, groups[0].Dialogues.Select(d => d.Id));
        Assert.Equal("esc_c", groups[1].FileName);
    }

    [Fact]
    public void Groups_put_active_graph_first_and_skip_duplicate_id()
    {
        var active = MakeDialog("bar_a");
        var imported = MakeDialog("bar_a");
        imported.SourceFile = "dialogs_bar.xml";

        var project = new Project
        {
            ProjectName = "mod",
            DialogueId = "bar_a",
            Nodes = active.Nodes,
            Edges = active.Edges
        };
        project.Dialogues.AddRange(new[] { imported, MakeDialog("esc_c") });

        var groups = DialogueGameExport.Groups(project);

        Assert.Equal(2, groups.Count);
        Assert.Equal("bar", groups[0].FileName);
        Assert.Single(groups[0].Dialogues);
        Assert.Equal("bar_a", groups[0].Dialogues[0].Id);
        Assert.Equal("esc_c", groups[1].FileName);
    }

    [Fact]
    public void GameFiles_adds_ltx_and_string_table_once_per_dialogue()
    {
        var active = MakeDialog("bar_a");
        var project = new Project
        {
            ProjectName = "my_mod",
            DialogueId = "bar_a",
            Nodes = active.Nodes,
            Edges = active.Edges
        };
        project.Dialogues.Add(MakeDialog("esc_c"));
        project.Languages.Add(new Language
        {
            Locale = "rus",
            Name = "Русский",
            Entries = new() { new LanguageEntry { Id = "st_bar_a", Text = "Привет" } }
        });

        var files = DialogueGameExport.GameFiles(project);

        Assert.Contains("gamedata/config/gameplay/dialogs_bar_a.xml", files.Keys);
        Assert.Contains("gamedata/config/gameplay/dialogs_esc_c.xml", files.Keys);
        Assert.Contains("gamedata/config/gameplay/dialogs.ltx", files.Keys);
        Assert.Contains("gamedata/config/text/rus/st_my_mod.xml", files.Keys);
        Assert.Equal("[dialogs]\ndialogs = bar_a, esc_c\n",
            files["gamedata/config/gameplay/dialogs.ltx"]);

        var table = files["gamedata/config/text/rus/st_my_mod.xml"];
        Assert.Contains("<string id=\"st_bar_a\"><text>Привет</text></string>", table);
        Assert.Contains("<string id=\"st_esc_c\"><text>Привет</text></string>", table);
    }

    [Fact]
    public void GameFiles_uses_series_directory_and_project_encoding()
    {
        var active = MakeDialog("bar_a");
        var project = new Project
        {
            ProjectName = "mod",
            DialogueId = "bar_a",
            Series = "cop",
            Settings = new ProjectSettings { ExportEncoding = "utf-8" },
            Nodes = active.Nodes,
            Edges = active.Edges
        };

        var files = DialogueGameExport.GameFiles(project);

        Assert.Contains("gamedata/configs/gameplay/dialogs_bar_a.xml", files.Keys);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            files["gamedata/configs/gameplay/dialogs_bar_a.xml"]);
        Assert.Contains("gamedata/configs/text/rus/st_mod.xml", files.Keys);
    }

    [Fact]
    public void Groups_accept_custom_file_names_and_empty_project()
    {
        var project = new Project();
        project.Dialogues.Add(MakeDialog("esc_c"));

        var groups = DialogueGameExport.Groups(project,
            new Dictionary<string, string> { ["esc_c"] = "custom name" });

        Assert.Single(groups);
        Assert.Equal("custom_name", groups[0].FileName);
        Assert.Empty(DialogueGameExport.GameFiles(new Project()));
        Assert.Empty(DialogueGameExport.Groups(null));
    }

    [Fact]
    public void WriteFiles_encodes_and_does_not_duplicate_gamedata_prefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "xrf-" + Guid.NewGuid().ToString("N"));
        var drive = Path.GetPathRoot(root)!;
        Assert.Equal(Path.Combine(drive, "gamedata", "config", "x"),
            DialogueGameExport.TargetPath(drive, "gamedata/config/x"));
        try
        {
            var files = new Dictionary<string, string>
            {
                ["gamedata/config/gameplay/dialogs_test.xml"] = "<dialog>Привет</dialog>"
            };

            var written = DialogueGameExport.WriteFiles(files, root, "windows-1251");

            var expected = Path.Combine(root, "gamedata", "config", "gameplay", "dialogs_test.xml");
            Assert.Equal(new[] { expected }, written);
            Assert.Equal("<dialog>Привет</dialog>",
                GameEncoding.DecodeCp1251(File.ReadAllBytes(expected)));

            var gamedata = Path.Combine(root, "gamedata");
            var nested = DialogueGameExport.WriteFiles(files, gamedata, "utf-8");

            Assert.Equal(Path.Combine(gamedata, "config", "gameplay", "dialogs_test.xml"), nested[0]);
            var bytes = File.ReadAllBytes(nested[0]);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal("<dialog>Привет</dialog>", GameEncoding.Decode(bytes, "utf-8"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
