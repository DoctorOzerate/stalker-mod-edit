using System.Text;
using XRayForge.Core.Gamedata;
using XRayForge.Core.Imaging;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Storage;
using XRayForge.Core.Text;

namespace XRayForge.Tests;

/// <summary>Сканирование `gamedata`: классификация файлов, кодировки, слияние и пропуски.</summary>
public class GamedataScanTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xrayforge-gamedata-" + Guid.NewGuid().ToString("N"));

    public GamedataScanTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch (IOException) { }
    }

    private void WriteText(string relative, string text) => WriteBytes(relative, GameEncoding.EncodeCp1251(text));

    private void WriteBytes(string relative, byte[] bytes)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private GamedataScanResult Scan(GamedataScanOptions? options = null, Project? project = null)
    {
        var read = FileSystemService.ReadDirectory(_root);
        return GamedataScanner.Scan(project, read.TopName, read.Files, options);
    }

    private static byte[] MakeDds(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 200;
            pixels[i + 1] = 100;
            pixels[i + 2] = 50;
            pixels[i + 3] = 255;
        }

        return DdsCodec.EncodeDds(pixels, width, height, DdsFormat.Dxt1, mipmaps: false);
    }

    private void WriteFullGamedata()
    {
        WriteText("config/system.ltx", "[system]\n");
        WriteText("config/misc/items.ltx", "[wpn_ak74]:identity_immunities\ncost = 100\n");
        WriteText("config/gameplay/game_relations.ltx",
            "[communities]\ncommunities = stalker, 0, bandit, 1\n[rating]\nnovice = 0\n");
        WriteText("scripts/bind_stalker.script", "function actor_init()\nend\n");
        WriteText("scripts/bind_monster.lua", "function bind()\nend\n");
        WriteText("config/text/rus/string_table_ui.xml",
            "<string_table><string id=\"st_ok\"><text>Привет</text></string></string_table>");
        WriteText("config/gameplay/character_desc_general.xml",
            "<character_desc_general><communities><community id=\"stalker\"/><community id=\"bandit\"/></communities></character_desc_general>");
        WriteText("config/gameplay/character_desc_stalkers.xml",
            "<specific_character id=\"esc_wolf\"><name>Волк</name><community>stalker</community><visual>actors\\stalker</visual><start_dialog>d1</start_dialog></specific_character>");
        WriteText("config/gameplay/dialogs_bar.xml",
            "<game_dialogs><dialog id=\"d1\"><phrase_list><phrase id=\"0\"><text>st_ok</text></phrase></phrase_list></dialog></game_dialogs>");
        WriteText("config/gameplay/game_tasks.xml",
            "<game_tasks><game_task id=\"q1\"><title>st_ok</title></game_task></game_tasks>");
        WriteBytes("textures/ui/ui_icon_equipment.dds", MakeDds(4, 4));
        WriteBytes("textures/ui/ui_npc_broken.dds", new byte[] { 1, 2, 3, 4 });
        WriteBytes("readme.txt", Encoding.UTF8.GetBytes("ignore me"));
    }

    [Fact]
    public void Scan_classifies_files_and_builds_patch()
    {
        WriteFullGamedata();
        var progress = new List<(int Done, int Total)>();

        var result = Scan(new GamedataScanOptions
        {
            ImportEncoding = "windows-1251",
            Progress = (done, total) => progress.Add((done, total))
        });

        var patch = result.Patch;
        Assert.Equal("[system]\n", patch.SystemLtx);
        Assert.True(patch.LtxFiles!.ContainsKey("config/system.ltx"));
        Assert.Equal("items.ltx", patch.ItemFiles!.Keys.Single());
        Assert.True(patch.ItemOrigins!.ContainsKey("wpn_ak74"));
        Assert.Equal(2, patch.Scripts!.Count);
        Assert.Contains(patch.Scripts, s => s.Name == "bind_stalker.script" && s.Path == "scripts/bind_stalker.script");
        Assert.Contains(patch.Scripts, s => s.Name == "bind_monster.lua" && s.Path == "scripts/bind_monster.lua");

        var languages = patch.Languages!;
        Assert.Single(languages);
        Assert.Equal("rus", languages[0].Locale);
        Assert.Equal("Привет", languages[0].Entries[0].Text);

        var characters = patch.Characters!;
        Assert.Single(characters);
        Assert.Equal("esc_wolf", characters[0].Id);
        Assert.Equal("Волк", characters[0].Name);
        Assert.NotNull(characters[0].Original);
        Assert.NotNull(patch.GeneralDesc);
        Assert.Contains("character_desc_general", patch.GeneralDesc!.Value.GetProperty("text").GetString());
        Assert.NotNull(patch.GameRelations);
        Assert.NotNull(patch.GameRelations!.Scales);
        Assert.Equal(new[] { "stalker", "bandit" }, patch.GeneralCommunities);

        var dialogues = patch.Dialogues!;
        Assert.Single(dialogues);
        Assert.Equal("config/gameplay/dialogs_bar.xml", dialogues[0].SourcePath);
        Assert.Equal("dialogs_bar.xml", dialogues[0].SourceFile);
        Assert.Equal("d1", dialogues[0].Id);

        var quests = patch.Quests!;
        Assert.Single(quests);
        Assert.Equal("q1", quests[0].Id);
        Assert.Equal("config/gameplay/game_tasks.xml", quests[0].SourcePath);
        Assert.NotNull(quests[0].Original);
        Assert.False(quests[0].Original!.Value.TryGetProperty("sourcePath", out _));

        Assert.NotNull(patch.Textures);
        Assert.Equal(2, patch.Textures!.Count);
        Assert.Equal("ui_icon_equipment.dds", patch.Textures[0].Name); // атлас иконок — первым
        Assert.Equal(4, patch.Textures[0].Width);
        Assert.Equal(4, patch.Textures[0].Height);
        Assert.Null(patch.Textures[0].DataUrl); // PNG-энкодер ставится WPF-слоем
        Assert.Equal("ui_npc_broken.dds", patch.Textures[1].Name);
        Assert.NotNull(patch.Textures[1].Error);
        Assert.Null(patch.Textures[1].Width);

        Assert.Equal(13, result.Stats.Files);
        Assert.Equal(2, result.Stats.Scripts);
        Assert.Equal(1, result.Stats.Characters);
        Assert.Equal(1, result.Stats.Dialogues);
        Assert.Equal(1, result.Stats.Languages);
        Assert.Equal(1, result.Stats.Quests);
        Assert.Equal(1, result.Stats.Items);
        Assert.Equal(0, result.Stats.Skipped);
        Assert.Empty(result.Skipped);

        Assert.Equal((0, 13), progress[0]);
        Assert.Equal((13, 13), progress[^1]);
        Assert.True(progress.Count >= 3);
    }

    [Fact]
    public void Scan_merges_item_files_into_single_ltx_sorted_by_name()
    {
        WriteText("config/misc/ammo.ltx", "[ammo_9x18_fmj]\ncost = 5\n");
        WriteText("config/misc/weapons.ltx", "[wpn_ak74]\ncost = 100\n");

        var result = Scan();
        var items = result.Patch.ItemsLtx!;

        Assert.Equal(2, result.Patch.ItemOrigins!.Count);
        Assert.Contains("; === ammo.ltx ===", items);
        Assert.Contains("; === weapons.ltx ===", items);
        Assert.True(items.IndexOf("ammo.ltx", StringComparison.Ordinal) < items.IndexOf("weapons.ltx", StringComparison.Ordinal));
        Assert.Equal(2, result.Stats.Items);
    }

    [Fact]
    public void Scan_reports_skipped_files_instead_of_failing()
    {
        WriteText("config/system.ltx", "[system]\n");

        var result = Scan(new GamedataScanOptions { ImportEncoding = "cp866" }); // кодировка не поддерживается

        Assert.Single(result.Skipped);
        Assert.Equal("config/system.ltx", result.Skipped[0].Path);
        Assert.NotEmpty(result.Skipped[0].Error);
        Assert.Empty(result.Patch.LtxFiles!);
        Assert.Equal(1, result.Stats.Skipped);
    }

    [Fact]
    public void Patch_applies_only_found_fields_and_keeps_project_state()
    {
        WriteText("scripts/bind.lua", "function bind()\nend\n");

        var project = ProjectMigration.Empty();
        project.ProjectName = "keep";
        project.SystemLtx = "[system]\noriginal = 1\n";
        project.Nodes.Add(new DialogueNode { Id = "keep_node", Type = NodeTypes.Start });

        var result = Scan(project: project);
        result.Patch.Apply(project);

        Assert.Equal(FileSystemService.DirectoryName(_root), project.GamedataPath);
        Assert.Equal("keep", project.ProjectName);
        Assert.Single(project.Nodes);
        Assert.Equal("[system]\noriginal = 1\n", project.SystemLtx); // system.ltx в скане не найден
        Assert.Single(project.Scripts!);
        Assert.Empty(project.Characters);
    }

    [Fact]
    public void Scan_ignores_dds_files_that_are_not_atlas_candidates()
    {
        WriteBytes("textures/act/act_stalker.dds", MakeDds(4, 4));

        var result = Scan();

        Assert.Null(result.Patch.Textures);
        Assert.Empty(result.Skipped);
        Assert.Equal(0, result.Stats.Textures);
    }

    [Fact]
    public void Scan_uses_png_encoder_for_textures()
    {
        WriteBytes("textures/ui/ui_npc_x.dds", MakeDds(4, 4));

        var result = Scan(new GamedataScanOptions
        {
            PngEncoder = (pixels, width, height) => $"data:image/png;base64,{pixels.Length}:{width}x{height}"
        });

        Assert.Equal("data:image/png;base64,64:4x4", result.Patch.Textures![0].DataUrl);
    }

    [Fact]
    public void Read_directory_normalizes_paths_and_keeps_top_name()
    {
        WriteText("config/misc/items.ltx", "x");
        WriteText("gamedata/config/system.ltx", "[system]\n");

        var read = FileSystemService.ReadDirectory(_root);

        Assert.Equal(FileSystemService.DirectoryName(_root), read.TopName);
        Assert.Contains("config/misc/items.ltx", read.Files.Select(f => f.Path));
        Assert.Contains("gamedata/config/system.ltx", read.Files.Select(f => f.Path));
        Assert.Equal("items.ltx", read.Files.Single(f => f.Path.EndsWith("items.ltx", StringComparison.Ordinal)).Name);
        Assert.Empty(read.Skipped);
        Assert.Empty(FileSystemService.ReadDirectory(Path.Combine(_root, "нет-такой-папки")).Files);
        Assert.Empty(FileSystemService.ReadDirectory(null).Files);
        Assert.Equal("", FileSystemService.DirectoryName(null));
    }
}