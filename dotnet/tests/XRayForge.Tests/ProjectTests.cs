using System.Text.Json;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Tests;

/// <summary>Модель проекта, миграция версий, целостность и совместимость JSON с JS-версией.</summary>
public class ProjectTests
{
    private static Project Migrate(string json) => ProjectSerializer.Deserialize(json);

    private static DialogueNode Phrase(string id, string textId = "") => GraphData.SafeNode(
        new DialogueNode
        {
            Id = id,
            Type = NodeTypes.Phrase,
            Data = new NodeData { TextId = textId, PhraseId = id, Metadata = GraphData.SafeMetadata(null) }
        },
        0);

    [Fact]
    public void Empty_matches_empty_project_defaults()
    {
        var p = ProjectMigration.Empty();

        Assert.Equal(3, p.Version);
        Assert.Equal("new_dialogue", p.ProjectName);
        Assert.Equal("new_dialogue", p.DialogueId);
        Assert.Equal("soc", p.Series);
        Assert.Equal("dark", p.Theme);
        Assert.Empty(p.Nodes);
        Assert.Empty(p.Edges);
        Assert.Empty(p.Quests);
        Assert.Empty(p.GeneralCommunities);
        Assert.Equal("", p.ItemsLtx);
        Assert.Equal("", p.SystemLtx);
        Assert.Equal(5, p.Atlas.Columns);
        Assert.Equal(50, p.Atlas.CellSize);
        Assert.Equal(50, p.Atlas.CellHeight);
        Assert.Equal("auto", p.Settings.ImportEncoding);
        Assert.Equal("windows-1251", p.Settings.ExportEncoding);
        Assert.True(p.Settings.ShowNodeText);
        Assert.Equal(60d, p.Settings.AutosaveInterval!.Value);
        Assert.Empty(p.PinnedIcons);
        Assert.True(p.PinCollections.ContainsKey("default"));
        Assert.Empty(p.PinCollections["default"]);
        Assert.NotNull(p.Scripts);
        Assert.NotNull(p.LtxFiles);
        Assert.NotNull(p.Textures);
        Assert.NotNull(p.ItemFiles);
        Assert.NotNull(p.ItemOrigins);
        Assert.Null(p.GameRelations);
        Assert.Null(p.Trade);
    }

    [Fact]
    public void Serialize_uses_js_key_names()
    {
        var project = ProjectMigration.Empty();
        project.Nodes.Add(Phrase("n1", "st_1"));

        var json = ProjectSerializer.Serialize(project);

        Assert.Contains("\"text_id\": \"st_1\"", json);
        Assert.Contains("\"use_id\": false", json);
        Assert.Contains("\"phraseId\": \"n1\"", json);
        Assert.DoesNotContain("\"textId\"", json);
        Assert.Contains("\"importEncoding\": \"auto\"", json);
        Assert.Contains("\"dialogueId\": \"new_dialogue\"", json);
        Assert.StartsWith("{", json);
    }

    [Fact]
    public void Serialize_keeps_dictionary_keys_verbatim_and_cyrillic_text()
    {
        var project = ProjectMigration.Empty();
        project.LtxFiles!["config/Misc/Items.ltx"] = "; Привет";
        project.ItemOrigins!["wpn_ak74"] = "Оружие";
        project.PinCollections["default"].Add(new PinnedIcon { X = 1, Y = 2, W = 3, H = 4, Name = "icon_1_2" });

        var json = ProjectSerializer.Serialize(project);

        Assert.Contains("\"config/Misc/Items.ltx\"", json);
        Assert.Contains("Привет", json);
        Assert.Contains("\"wpn_ak74\"", json);
        Assert.Contains("\"dataURL\"", ProjectSerializer.Serialize(new Project
        {
            Textures = new List<ProjectTexture> { new() { Name = "t", DataUrl = "data:image/png;base64,AA" } }
        }));

        var back = ProjectSerializer.Deserialize(json);
        Assert.Equal("; Привет", back.LtxFiles!["config/Misc/Items.ltx"]);
        Assert.Equal("icon_1_2", back.PinCollections["default"][0].Name);
        Assert.Equal(4, back.PinCollections["default"][0].H);
    }

    [Fact]
    public void ToPersistable_strips_gamedata_fields_without_touching_source()
    {
        var project = ProjectMigration.Empty();
        project.LtxFiles!["a.ltx"] = "x";
        project.ItemFiles!["a.ltx"] = "x";
        project.ItemOrigins!["s"] = "Оружие";
        project.Scripts!.Add(new ScriptFile { Name = "a.script", Text = "t" });
        project.Textures!.Add(new ProjectTexture { Name = "t.dds", Width = 4, Height = 4 });
        project.Nodes.Add(Phrase("n1"));

        var json = ProjectSerializer.Serialize(ProjectSerializer.ToPersistable(project));

        Assert.DoesNotContain("ltxFiles", json);
        Assert.DoesNotContain("itemFiles", json);
        Assert.DoesNotContain("itemOrigins", json);
        Assert.DoesNotContain("\"scripts\"", json);
        Assert.DoesNotContain("\"textures\"", json);
        Assert.Contains("\"nodes\"", json);

        Assert.Single(project.Scripts!);
        Assert.Single(project.Textures!);
        Assert.Single(project.LtxFiles!);
    }

    [Fact]
    public void Migrate_maps_legacy_node_types_and_keeps_unknown_fields()
    {
        var p = Migrate(
            "{\"nodes\":[{\"id\":\"x\",\"type\":\"NPC_REPLY\",\"x\":0,\"y\":0,\"custom\":5," +
            "\"data\":{\"speaker\":\"foo\",\"role\":\"npc\"}}],\"unknownTop\":{\"a\":1},\"playthrough\":null}");

        Assert.Equal(NodeTypes.Phrase, p.Nodes[0].Type);
        Assert.Equal("npc", p.Nodes[0].Data!.Role);
        Assert.Equal("migration", p.Nodes[0].Data!.RoleSource);
        Assert.NotNull(p.Nodes[0].Extra);
        Assert.True(p.Nodes[0].Extra!.ContainsKey("custom"));
        Assert.True(p.Nodes[0].Data!.Extra!.ContainsKey("speaker")); // в JS `safeNode` его тоже сохраняет
        Assert.NotNull(p.Extra);
        Assert.True(p.Extra!.ContainsKey("unknownTop"));
        Assert.True(p.Extra.ContainsKey("playthrough"));
    }

    [Fact]
    public void Migrate_guards_arrays_ids_and_templates()
    {
        var p = Migrate(
            "{\"nodes\":\"oops\",\"edges\":[null,{\"source\":\"a\",\"target\":\"b\"}]," +
            "\"languages\":[null,{\"code\":\"RUS\",\"entries\":[{\"id\":1},null]}]," +
            "\"scripts\":[{\"text\":\"t\"}],\"quests\":[{\"title\":\"x\"},{\"id\":\"q1\",\"title\":\"t\",\"requires\":[\"a\",1]}]}");

        Assert.Empty(p.Nodes);
        Assert.Single(p.Edges);
        Assert.Equal("edge_0", p.Edges[0].Id);
        Assert.Equal("a", p.Edges[0].Source);
        Assert.Single(p.Languages);
        Assert.Equal("rus", p.Languages[0].Locale);
        Assert.Equal("Language 1", p.Languages[0].Name);
        Assert.Equal("1", p.Languages[0].Entries[0].Id);
        Assert.Equal("script_0.script", p.Scripts![0].Name);
        Assert.Equal("script_0.script", p.Scripts[0].Path);
        Assert.Single(p.Quests);
        Assert.Equal("q1", p.Quests[0].Id);
        Assert.Equal(new[] { "a", "1" }, p.Quests[0].Requires);
        Assert.Equal("game_tasks", p.Quests[0].Format);
        Assert.Equal("", p.Quests[0].SourcePath);
        Assert.Null(p.Quests[0].Original);
    }

    [Fact]
    public void Migrate_clamps_atlas_and_validates_settings()
    {
        var p = Migrate(
            "{\"atlas\":{\"columns\":0,\"cellSize\":\"abc\",\"cellHeight\":12.5}," +
            "\"settings\":{\"importEncoding\":\"cp866\",\"exportEncoding\":\"latin1\"," +
            "\"autosaveInterval\":null,\"showNodeText\":false,\"custom\":true}}");

        Assert.Equal(5, p.Atlas.Columns);
        Assert.Equal(50, p.Atlas.CellSize);
        Assert.Equal(12.5, p.Atlas.CellHeight);
        Assert.Equal("auto", p.Settings.ImportEncoding);
        Assert.Equal("windows-1251", p.Settings.ExportEncoding);
        Assert.Equal(0, p.Settings.AutosaveInterval!.Value);
        Assert.False(p.Settings.ShowNodeText);
        Assert.True(p.Settings.Extra!.ContainsKey("custom"));
    }

    [Fact]
    public void Migrate_tolerates_invalid_json_types_per_element()
    {
        var p = Migrate("{\"characters\":[{\"id\":5,\"name\":\"N\"},{\"id\":\"ok\"}]}");

        Assert.Equal(2, p.Characters.Count);
        Assert.Equal("5", p.Characters[0].Id);
        Assert.Equal("N", p.Characters[0].Name);
        Assert.Equal("ok", p.Characters[1].Id);
    }

    [Fact]
    public void Deserialize_throws_on_broken_json_and_try_returns_null()
    {
        Assert.ThrowsAny<JsonException>(() => ProjectSerializer.Deserialize("{oops"));
        Assert.Null(ProjectSerializer.TryDeserialize("{oops"));
        Assert.Null(ProjectSerializer.Load(Path.Combine(Path.GetTempPath(), "нет-такого-файла.project.json")));
    }

    [Fact]
    public void MigrateLegacy_matches_core_js_migrateProject()
    {
        Assert.Throws<ArgumentException>(() => ProjectMigration.MigrateLegacy(JsonDocument.Parse("null").RootElement));
        Assert.Throws<ArgumentException>(() => ProjectMigration.MigrateLegacy(JsonDocument.Parse("[]").RootElement));

        using var document = JsonDocument.Parse(
            "{\"nodes\":[{\"id\":\"x\",\"type\":\"PLAYER_REPLY\",\"data\":{\"speaker\":\"foo\"}}]}");
        var p = ProjectMigration.MigrateLegacy(document.RootElement);

        Assert.Equal(4, p.Version);
        Assert.Equal("new_dialogue", p.ProjectName);
        Assert.Equal("soc", p.Series);
        Assert.Equal(NodeTypes.Phrase, p.Nodes[0].Type);
        Assert.Equal("player", p.Nodes[0].Data!.Role);
        Assert.Equal("migration", p.Nodes[0].Data!.RoleSource);
        Assert.Equal("x", p.Nodes[0].Data!.PhraseId);
        var data = p.Nodes[0].Data!;
        Assert.False(data.Extra != null && data.Extra.ContainsKey("speaker"));
        Assert.Equal("auto", p.Settings.ImportEncoding);
        Assert.Equal("windows-1251", p.Settings.ExportEncoding);
    }

    [Fact]
    public void Project_integrity_reports_broken_links()
    {
        var project = ProjectMigration.Empty();
        project.Languages.Add(new Language { Locale = "rus", Entries = { new LanguageEntry { Id = "st_ok" } } });
        project.Dialogues.Add(new DialogueGraph { Id = "d1", Name = "d1" });
        project.Nodes.Add(Phrase("n1", "st_missing"));
        project.Characters.Add(new Character
        {
            Id = "c1",
            StartDialog = { "d1", "d_missing" },
            ActorDialog = { "d2" }
        });
        project.Quests.Add(new Quest { Id = "q1", Title = "st_ok", Text = "st_missing" });
        project.SystemLtx = "#include \"config/misc/items.ltx\"\n";

        var issues = ProjectIntegrity.Validate(project);

        Assert.Contains(issues, i => i.Message == "text_id без перевода: st_missing");
        Assert.Contains(issues, i => i.Message == "c1: start_dialog не найден — d_missing");
        Assert.Contains(issues, i => i.Message == "c1: actor_dialog не найден — d2");
        Assert.Contains(issues, i => i.Where == "system.ltx" && i.Message == "битый include: config/misc/items.ltx");
        Assert.DoesNotContain(issues, i => i.Message.Contains("d1", StringComparison.Ordinal));
        Assert.Equal(2, issues.Count(i => i.Where is "Диалоги" or "Квесты"));

        project.LtxFiles!["config/misc/items.ltx"] = "";
        Assert.DoesNotContain(ProjectIntegrity.Validate(project), i => i.Where == "system.ltx");
    }

    [Fact]
    public void Real_js_project_file_round_trips_without_field_loss()
    {
        var path = FindRepositoryFile(Path.Combine("saves", "new_dialogue.project.json"));
        if (path == null) return; // тесты вне репозитория

        var project = ProjectSerializer.Deserialize(File.ReadAllText(path));
        Assert.Equal(3, project.Version);
        Assert.NotEmpty(project.Nodes);
        Assert.NotEmpty(project.Languages);
        Assert.NotEmpty(project.ItemsLtx);
        Assert.Empty(project.LtxFiles!); // gamedata-поля в `.project.json` не пишутся (toPersistable)
        Assert.Equal("soc", project.Series);
        Assert.All(project.Nodes, n => Assert.False(string.IsNullOrEmpty(n.Id)));

        var second = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project));
        Assert.Equal(project.Nodes.Count, second.Nodes.Count);
        Assert.Equal(project.Edges.Count, second.Edges.Count);
        Assert.Equal(project.Characters.Count, second.Characters.Count);
        Assert.Equal(project.Dialogues.Count, second.Dialogues.Count);
        Assert.Equal(project.Quests.Count, second.Quests.Count);
        Assert.Equal(project.Languages.Count, second.Languages.Count);
        Assert.Equal(project.GeneralCommunities.Count, second.GeneralCommunities.Count);
        Assert.Equal(project.Nodes[1].Data!.TextId, second.Nodes[1].Data!.TextId);
        Assert.Equal(project.ItemsLtx.Length, second.ItemsLtx.Length);
    }

    private static string? FindRepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}