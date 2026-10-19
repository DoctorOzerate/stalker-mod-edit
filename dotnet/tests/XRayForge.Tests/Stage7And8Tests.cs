using System.IO.Compression;
using System.Text;
using XRayForge.Core.Export;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;
using XRayForge.Core.Validation;

namespace XRayForge.Tests;

public class Stage7And8Tests
{
    [Fact]
    public void GamePackage_includes_dialogues_quests_system_and_scripts()
    {
        var project = ProjectMigration.Empty();
        project.ProjectName = "package";
        project.Nodes = new List<DialogueNode>
        {
            new() { Id = "start", Type = NodeTypes.Start },
            new()
            {
                Id = "n1", Type = NodeTypes.Phrase,
                Data = new NodeData { Text = "Hello", TextId = "st_hello" }
            },
            new() { Id = "end", Type = NodeTypes.End }
        };
        project.Edges = new List<DialogueEdge>
        {
            new() { Id = "e1", Source = "start", Target = "n1" },
            new() { Id = "e2", Source = "n1", Target = "end" }
        };
        project.Languages = new List<Language>
        {
            new()
            {
                Locale = "rus", Name = "Русский",
                Entries = new List<LanguageEntry> { new() { Id = "st_hello", Text = "Привет" } }
            }
        };
        project.Quests = new List<Quest>
        {
            new() { Id = "q1", Title = "t", Text = "x", Format = "game_tasks" }
        };
        project.SystemLtx = "[system]\nkey = value\n";
        project.Scripts = new List<ScriptFile>
        {
            new() { Name = "test.script", Path = "gamedata/scripts/test.script", Text = "function f() end\n" }
        };

        var files = GamePackage.TextFiles(project);

        Assert.Contains(files.Keys, path => path.StartsWith("gamedata/config/gameplay/dialogs_"));
        Assert.Contains("gamedata/config/gameplay/dialogs.ltx", files.Keys);
        Assert.Contains(files.Keys, path => path.StartsWith("gamedata/config/text/rus/"));
        Assert.Contains("gamedata/config/gameplay/game_tasks.xml", files.Keys);
        Assert.Contains("gamedata/config/system.ltx", files.Keys);
        Assert.Equal("function f() end\n", files["gamedata/scripts/test.script"]);
        Assert.Contains(files, pair => pair.Key.StartsWith("gamedata/config/text/rus/") &&
                                      pair.Value.Contains("st_hello"));
    }

    [Fact]
    public void GamePackage_zip_uses_export_encoding_and_contains_entries()
    {
        var project = ProjectMigration.Empty();
        project.ProjectName = "zip";
        project.SystemLtx = "[system]\nname = Привет\n";

        var bytes = GamePackage.Zip(project);
        Assert.NotEmpty(bytes);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("gamedata/config/system.ltx");
        Assert.NotNull(entry);
        using var reader = entry!.Open();
        using var copy = new MemoryStream();
        reader.CopyTo(copy);
        var text = GameEncoding.Decode(copy.ToArray(), "auto");
        Assert.Contains("Привет", text);
    }

    [Fact]
    public void SelfCheck_passes_empty_project_and_reports_failure_for_broken_include()
    {
        var clean = ProjectSelfCheck.Run(ProjectMigration.Empty());
        Assert.True(clean.Passed, string.Join(" | ", clean.Items.Select(i => i.Name + "=" + i.Ok + ":" + i.Details)));
        Assert.Equal(9, clean.Items.Count);
        Assert.Equal(clean.Items.Count, clean.Items.Count(item => item.Ok));

        var broken = ProjectMigration.Empty();
        broken.SystemLtx = "#include \"missing.ltx\"\n";
        var report = ProjectSelfCheck.Run(broken);
        Assert.False(report.Passed);
        var ltx = Assert.Single(report.Items, item => item.Name == "LTX и инклюды");
        Assert.False(ltx.Ok);
        Assert.Contains("инклюды", ltx.Details);
    }

    [Fact]
    public void SelfCheck_reports_lua_and_quest_errors()
    {
        var project = ProjectMigration.Empty();
        project.Scripts = new List<ScriptFile>
        {
            new() { Name = "bad.script", Text = "function f(\n" }
        };
        project.Quests = new List<Quest>
        {
            new() { Id = "q", Title = "q_t", Text = "q_x", Prior = "abc" },
            new() { Id = "q", Title = "q_t", Text = "q_x" }
        };

        var report = ProjectSelfCheck.Run(project);

        Assert.False(report.Passed);
        Assert.False(report.Items.Single(item => item.Name == "Lua-скрипты").Ok);
        Assert.False(report.Items.Single(item => item.Name == "Квесты").Ok);
    }
}
