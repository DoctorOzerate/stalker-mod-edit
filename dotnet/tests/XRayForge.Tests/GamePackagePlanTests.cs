using System.IO.Compression;
using System.Text;
using System.Text.Json;
using XRayForge.Core.Characters;
using XRayForge.Core.Export;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;

namespace XRayForge.Tests;

/// <summary>
/// План игрового пакета (остаток P0 §3.13): выбор имён `dialogs_*.xml`, набор локалей,
/// предпросмотр файлов с размерами и манифест `xrayforge-package.json`.
/// </summary>
public class GamePackagePlanTests
{
    /// <summary>Проект с одним диалогом и двумя локалями — минимальный набор для пакета.</summary>
    private static Project ProjectWithDialogue()
    {
        var project = ProjectMigration.Empty();
        project.ProjectName = "mod";
        project.DialogueId = "bar_a";
        project.Series = "soc";
        project.Nodes = new List<DialogueNode>
        {
            new() { Id = "start", Type = NodeTypes.Start },
            new()
            {
                Id = "p1", Type = NodeTypes.Phrase,
                Data = new NodeData { PhraseId = "0", TextId = "st_bar_a", Text = "Привет", UseId = true }
            },
            new() { Id = "end", Type = NodeTypes.End }
        };
        project.Edges = new List<DialogueEdge>
        {
            new() { Id = "e1", Source = "start", Target = "p1" },
            new() { Id = "e2", Source = "p1", Target = "end" }
        };
        project.Languages = new List<Language>
        {
            new()
            {
                Locale = "rus", Name = "Русский",
                Entries = new List<LanguageEntry> { new() { Id = "st_bar_a", Text = "Привет" } }
            },
            new()
            {
                Locale = "eng", Name = "English",
                Entries = new List<LanguageEntry> { new() { Id = "st_bar_a", Text = "Hello" } }
            }
        };
        project.Settings.ExportEncoding = "windows-1251";
        return project;
    }

    [Fact]
    public void Plan_limits_string_table_to_selected_locales()
    {
        var project = ProjectWithDialogue();

        var all = GamePackage.Plan(project);
        var onlyEnglish = GamePackage.Plan(project, new GamePackageOptions { Locales = new[] { "eng" } });
        var none = GamePackage.Plan(project, new GamePackageOptions { Locales = Array.Empty<string>() });

        Assert.Contains("gamedata/config/text/rus/st_mod.xml", all.Files.Keys);
        Assert.Contains("gamedata/config/text/eng/st_mod.xml", all.Files.Keys);

        Assert.DoesNotContain(onlyEnglish.Files.Keys, key => key.Contains("/text/rus/", StringComparison.Ordinal));
        Assert.Contains("gamedata/config/text/eng/st_mod.xml", onlyEnglish.Files.Keys);

        // Пустой набор означает «все локали»: так же ведёт себя Core без фильтра.
        Assert.Equal(all.Files.Count, none.Files.Count);
    }

    [Fact]
    public void Plan_does_not_drop_dialogues_when_locales_are_filtered()
    {
        var project = ProjectWithDialogue();
        project.Languages.Clear(); // локалей нет — Core подставляет rus

        var plan = GamePackage.Plan(project, new GamePackageOptions { Locales = new[] { "eng" } });

        Assert.Contains("gamedata/config/gameplay/dialogs_bar_a.xml", plan.Files.Keys);
        Assert.Contains("gamedata/config/gameplay/dialogs.ltx", plan.Files.Keys);
        Assert.DoesNotContain(plan.Files.Keys, key => key.Contains("/text/", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_applies_dialogue_file_name_overrides()
    {
        var project = ProjectWithDialogue();

        var plan = GamePackage.Plan(project, new GamePackageOptions
        {
            DialogueFileNames = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bar_a"] = "quest bar!"
            }
        });

        Assert.Contains("gamedata/config/gameplay/dialogs_quest_bar_.xml", plan.Files.Keys);
        Assert.DoesNotContain("gamedata/config/gameplay/dialogs_bar_a.xml", plan.Files.Keys);
    }

    [Fact]
    public void Plan_classifies_every_kind_of_package_file()
    {
        var project = ProjectWithDialogue();
        project.Characters = new List<Character> { new() { Id = "npc_a", Name = "st_npc_a_name" } };
        project.ItemsLtx = "; === items.ltx === \n[wpn_a]\ncost = 1\n";
        project.Quests = new List<Quest> { new() { Id = "q1", Title = "st_q1_title", Text = "st_q1_text" } };
        project.SystemLtx = "[test]\nvalue = 1\n";
        project.Scripts = new List<ScriptFile> { new() { Name = "mod_main.lua", Text = "function f() end\n" } };

        var plan = GamePackage.Plan(project);
        var kinds = plan.Entries.ToDictionary(entry => entry.Path, entry => entry.Kind, StringComparer.Ordinal);

        Assert.Equal(GamePackageKind.Dialogues, kinds["gamedata/config/gameplay/dialogs_bar_a.xml"]);
        Assert.Equal(GamePackageKind.Dialogues, kinds["gamedata/config/gameplay/dialogs.ltx"]);
        Assert.Equal(GamePackageKind.Localization, kinds["gamedata/config/text/rus/st_mod.xml"]);
        Assert.Equal(GamePackageKind.Characters, kinds["gamedata/config/gameplay/character_desc_bar_a.xml"]);
        Assert.Equal(GamePackageKind.Items, kinds["gamedata/config/misc/items.ltx"]);
        Assert.Equal(GamePackageKind.Quests, kinds["gamedata/config/gameplay/game_tasks.xml"]);
        Assert.Equal(GamePackageKind.SystemLtx, kinds["gamedata/config/system.ltx"]);
        Assert.Equal(GamePackageKind.Scripts, kinds["gamedata/scripts/mod_main.lua"]);
    }

    [Fact]
    public void Plan_entries_are_sorted_and_measured_in_export_encoding()
    {
        var project = ProjectWithDialogue();

        var plan = GamePackage.Plan(project);
        var paths = plan.Entries.Select(entry => entry.Path).ToList();

        Assert.Equal(paths.OrderBy(path => path, StringComparer.Ordinal), paths);
        Assert.Equal(plan.Files.Count, plan.Entries.Count);
        Assert.Equal(plan.Entries.Sum(entry => entry.Bytes), plan.TotalBytes);

        foreach (var entry in plan.Entries)
        {
            Assert.Equal(
                GameEncoding.Encode(plan.Files[entry.Path], plan.Encoding).Length,
                entry.Bytes);
        }

        // Кириллица в windows-1251 — один байт на символ, а не два (UTF-8).
        var russian = plan.Entries.Single(entry =>
            entry.Path.EndsWith("text/rus/st_mod.xml", StringComparison.Ordinal));
        Assert.Contains("Привет", GameEncoding.DecodeCp1251(
            GameEncoding.Encode(plan.Files[russian.Path], "windows-1251")));
    }

    [Fact]
    public void Plan_manifest_lists_files_sizes_and_encoding()
    {
        var project = ProjectWithDialogue();

        var plan = GamePackage.Plan(project, new GamePackageOptions { ManifestVersion = "2.1" });

        Assert.Equal("xrayforge-package.json", plan.ManifestName);
        Assert.NotNull(plan.Manifest);

        using var document = JsonDocument.Parse(plan.Manifest!);
        var root = document.RootElement;

        Assert.Equal("xrayforge-game-package", root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("schema").GetInt32());
        Assert.Equal("2.1", root.GetProperty("version").GetString());
        Assert.Equal("mod", root.GetProperty("project").GetString());
        Assert.Equal("bar_a", root.GetProperty("dialogueId").GetString());
        Assert.Equal("soc", root.GetProperty("series").GetString());
        Assert.Equal("windows-1251", root.GetProperty("encoding").GetString());

        var files = root.GetProperty("files").EnumerateArray().ToList();
        Assert.Equal(plan.Entries.Count, files.Count);

        var dialog = files.Single(file =>
            file.GetProperty("kind").GetString() == "dialogues" &&
            file.GetProperty("path").GetString()!.EndsWith("dialogs_bar_a.xml", StringComparison.Ordinal));
        Assert.True(dialog.GetProperty("bytes").GetInt32() > 0);
    }

    [Fact]
    public void Plan_manifest_version_defaults_to_one()
    {
        var project = ProjectWithDialogue();

        var plan = GamePackage.Plan(project, new GamePackageOptions { ManifestVersion = "  " });

        using var document = JsonDocument.Parse(plan.Manifest!);
        Assert.Equal("1", document.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void Plan_without_manifest_has_no_manifest_entry()
    {
        var plan = GamePackage.Plan(ProjectWithDialogue(), GamePackageOptions.Plain);

        Assert.Null(plan.Manifest);
        Assert.Null(plan.ManifestName);
    }

    [Fact]
    public void Zip_writes_manifest_only_when_it_is_enabled()
    {
        var project = ProjectWithDialogue();

        var legacy = ReadEntryNames(GamePackage.Zip(project));
        var full = ReadEntryNames(GamePackage.Zip(GamePackage.Plan(project)));

        Assert.DoesNotContain("xrayforge-package.json", legacy);
        Assert.Contains("xrayforge-package.json", full);
        Assert.Equal(legacy.Count + 1, full.Count);
        Assert.Single(legacy, name => name.Contains("dialogs_bar_a", StringComparison.Ordinal));
    }

    [Fact]
    public void Zip_manifest_is_utf8_json_even_for_windows_encoding()
    {
        var project = ProjectWithDialogue();
        project.ProjectName = "мод";

        using var archive = new ZipArchive(new MemoryStream(GamePackage.Zip(GamePackage.Plan(project))));

        var entry = archive.GetEntry("xrayforge-package.json");
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        var json = reader.ReadToEnd();

        Assert.Contains("\"project\": \"мод\"", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("мод", document.RootElement.GetProperty("project").GetString());
    }

    [Fact]
    public void WriteZip_reports_game_file_count_without_manifest()
    {
        var project = ProjectWithDialogue();
        var directory = Path.Combine(Path.GetTempPath(), "xrayforge-plan-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "package.zip");
        try
        {
            var plan = GamePackage.Plan(project);
            var count = GamePackage.WriteZip(plan, path);

            using var archive = ZipFile.OpenRead(path);
            Assert.Equal(plan.Entries.Count, count);
            Assert.Equal(count + 1, archive.Entries.Count);
            Assert.NotNull(archive.GetEntry("xrayforge-package.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static List<string> ReadEntryNames(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        return archive.Entries.Select(entry => entry.FullName).ToList();
    }
}
