using System.IO.Compression;
using System.Text.Json;
using XRayForge.Core.Characters;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Export;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Tests;

/// <summary>
/// Полный игровой пакет (остаток P0): персонажи, item-файлы по исходным путям и защита от
/// записи за пределы каталога экспорта.
/// </summary>
public class GamePackageExportTests
{
    private static Character SampleCharacter() => new()
    {
        Id = "npc_a",
        Name = "st_npc_a_name",
        Icon = "ui_npc_a",
        MapIcon = new MapIcon { X = "1", Y = "2" },
        Bio = "Опытный <сталкер>",
        Class = "npc_a",
        Community = "stalker",
        TerrainSect = "stalker_terrain",
        SndConfig = @"characters_voice\human_03\stalker\",
        Rank = "100",
        Reputation = "0",
        Visual = @"actors\stalker_neutral\stalker_neutral_1",
        Supplies = "[spawn]\ndevice_torch\n#include \"gameplay\\character_food.xml\"",
        StartDialog = new List<string> { "dialogs_a" },
        ActorDialog = new List<string> { "dialogs_b" },
        Extra = new Dictionary<string, JsonElement>
        {
            ["npc_rank"] = JsonDocument.Parse("2").RootElement.Clone(),
            ["object_extra"] = JsonDocument.Parse("{\"a\":1}").RootElement.Clone()
        }
    };

    [Fact]
    public void CharactersToXml_writes_game_root_and_specific_character_fields()
    {
        var xml = CharacterGameExport.CharactersToXml(new[] { SampleCharacter() });

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1251\"?>\n<xml>\n", xml);
        Assert.EndsWith("</xml>\n", xml);
        Assert.Contains("\t<specific_character id=\"npc_a\">", xml);
        Assert.Contains("\t\t<name>st_npc_a_name</name>", xml);
        Assert.Contains("\t\t<map_icon x=\"1\" y=\"2\"></map_icon>", xml);
        Assert.Contains("<bio>Опытный &lt;сталкер&gt;</bio>", xml);
        Assert.Contains("\t\t<terrain_sect>stalker_terrain</terrain_sect>", xml);
        Assert.Contains("\t\t<start_dialog>dialogs_a</start_dialog>", xml);
        Assert.Contains("\t\t<actor_dialog>dialogs_b</actor_dialog>", xml);
        Assert.Contains("\t\t<npc_rank>2</npc_rank>", xml);
        Assert.DoesNotContain("object_extra", xml);
    }

    [Fact]
    public void CharactersToXml_keeps_supplies_raw_for_include_directives()
    {
        var xml = CharacterGameExport.CharactersToXml(new[] { SampleCharacter() });

        // Блок как есть (с отступом), без экранирования: `#include` и `[spawn]` читает
        // препроцессор игры до разбора XML.
        Assert.Contains(
            "\t\t<supplies>\n\t\t\t[spawn]\n\t\t\tdevice_torch\n\t\t\t#include \"gameplay\\character_food.xml\"\n\t\t</supplies>",
            xml);
        Assert.DoesNotContain("&quot;", xml);
        Assert.DoesNotContain("&lt;", xml.Replace("<bio>Опытный &lt;сталкер&gt;</bio>", string.Empty));
    }

    [Fact]
    public void CharactersToXml_writes_duplicate_ids_once_and_skips_blank_ids()
    {
        var xml = CharacterGameExport.CharactersToXml(new[]
        {
            SampleCharacter(),
            SampleCharacter(),
            new Character { Id = "  ", Name = "st_blank" }
        });

        var blocks = xml.Split("<specific_character").Length - 1;
        Assert.Equal(1, blocks);
        Assert.DoesNotContain("st_blank", xml);
    }

    [Theory]
    [InlineData("bar_quest", "character_desc_bar_quest.xml")]
    [InlineData("Bar Quest!", "character_desc_bar_quest.xml")]
    [InlineData("character_desc_dup", "character_desc_dup.xml")]
    [InlineData("вано", "character_desc_mod.xml")]
    [InlineData("", "character_desc_mod.xml")]
    public void FileName_uses_slug_of_dialogue_id(string id, string expected) =>
        Assert.Equal(expected, CharacterGameExport.FileName(id));

    [Fact]
    public void GameFiles_for_characters_uses_series_config_directory()
    {
        var project = ProjectMigration.Empty();
        project.Series = "cop";
        project.DialogueId = "jup_quest";
        project.Characters = new List<Character> { SampleCharacter() };

        var files = CharacterGameExport.GameFiles(project);

        var path = Assert.Single(files.Keys);
        Assert.Equal("gamedata/configs/gameplay/character_desc_jup_quest.xml", path);
    }

    [Fact]
    public void GameFiles_for_characters_is_empty_without_characters()
    {
        var project = ProjectMigration.Empty();

        Assert.Empty(CharacterGameExport.GameFiles(project));
        Assert.Empty(CharacterGameExport.GameFiles(null));
    }

    [Fact]
    public void SplitMerged_restores_item_files_and_drops_headers()
    {
        var merged = "; === items.ltx === \n[wpn_a]\ncost = 1\n\n; === weapons.ltx === \n[wpn_b]\ncost = 2\n\n";

        var files = ItemGameExport.SplitMerged(merged);

        Assert.Equal(2, files.Count);
        Assert.Equal("items.ltx", files[0].Key);
        Assert.Equal("[wpn_a]\ncost = 1\n", files[0].Value);
        Assert.Equal("weapons.ltx", files[1].Key);
        Assert.Equal("[wpn_b]\ncost = 2\n", files[1].Value);
        Assert.Empty(ItemGameExport.SplitMerged(""));
        Assert.Empty(ItemGameExport.SplitMerged("[wpn_a]\ncost = 1\n"));
    }

    [Fact]
    public void GameFiles_for_items_uses_original_ltx_paths()
    {
        var project = ProjectMigration.Empty();
        project.Series = "cop";
        project.LtxFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["configs/misc/items.ltx"] = "",
            ["configs/weapons/weapons.ltx"] = ""
        };
        project.ItemsLtx = "; === items.ltx === \n[wpn_a]\ncost = 1\n\n; === weapons.ltx === \n[wpn_b]\ncost = 2\n";

        var files = ItemGameExport.GameFiles(project);

        Assert.Equal(2, files.Count);
        Assert.Equal("[wpn_a]\ncost = 1\n", files["gamedata/configs/misc/items.ltx"]);
        Assert.Equal("[wpn_b]\ncost = 2\n", files["gamedata/configs/weapons/weapons.ltx"]);
    }

    [Theory]
    [InlineData("soc", "gamedata/config/misc/items.ltx")]
    [InlineData("cop", "gamedata/configs/misc/items.ltx")]
    public void TargetPath_for_items_falls_back_to_misc_directory(string series, string expected)
    {
        var project = ProjectMigration.Empty();
        project.Series = series;

        Assert.Equal(expected, ItemGameExport.TargetPath(project, "items.ltx"));
    }

    [Fact]
    public void TargetPath_for_items_rejects_names_without_file_part()
    {
        Assert.Equal("", ItemGameExport.TargetPath(ProjectMigration.Empty(), "../.."));
        Assert.Equal("", ItemGameExport.TargetPath(ProjectMigration.Empty(), "configs/misc/items.ltx"));
    }

    [Fact]
    public void GamePackage_includes_characters_and_item_files()
    {
        var project = ProjectMigration.Empty();
        project.Characters = new List<Character> { SampleCharacter() };
        project.ItemsLtx = "; === items.ltx === \n[wpn_a]\ncost = 1\n";

        var files = GamePackage.TextFiles(project);

        Assert.Contains("gamedata/config/gameplay/character_desc_new_dialogue.xml", files.Keys);
        Assert.Contains("gamedata/config/misc/items.ltx", files.Keys);
        Assert.Equal("[wpn_a]\ncost = 1\n", files["gamedata/config/misc/items.ltx"]);
        Assert.Contains("specific_character", files["gamedata/config/gameplay/character_desc_new_dialogue.xml"]);
    }

    [Fact]
    public void GamePackage_keeps_all_paths_inside_gamedata()
    {
        var project = ProjectMigration.Empty();
        project.Scripts = new List<ScriptFile>
        {
            new() { Name = "../../evil.script", Path = "C:\\evil.script", Text = "x" },
            new() { Name = "ok.script", Path = "../../../evil.script", Text = "y" }
        };

        var files = GamePackage.TextFiles(project);

        Assert.All(files.Keys, key => Assert.StartsWith("gamedata/", key));
        Assert.DoesNotContain(files.Keys, key => key.Contains(".."));

        // Ведущие `..` после нормализации дают пустой путь — такой скрипт отбрасывается,
        // а не пишется за пределами пакета.
        Assert.DoesNotContain(files.Keys, key => key.Contains("evil.script"));
        Assert.Contains("gamedata/scripts/ok.script", files.Keys);
    }

    [Fact]
    public void TargetPath_rejects_paths_outside_export_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "xrayforge-target-guard");

        Assert.Throws<ArgumentException>(() => DialogueGameExport.TargetPath(root, "..\\..\\evil.txt"));
        Assert.Throws<ArgumentException>(() => DialogueGameExport.TargetPath(root, "C:\\evil.txt"));
        Assert.EndsWith(Path.Combine("gamedata", "config", "system.ltx"),
            DialogueGameExport.TargetPath(root, "gamedata/config/system.ltx"));
    }

    [Fact]
    public void WriteZip_packs_characters_items_and_counts_entries()
    {
        var project = ProjectMigration.Empty();
        project.Characters = new List<Character> { SampleCharacter() };
        project.ItemsLtx = "; === items.ltx === \n[wpn_a]\ncost = 1\n";
        project.Settings.ExportEncoding = "windows-1251";

        var directory = Path.Combine(Path.GetTempPath(), "xrayforge-zip-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "package.zip");
        try
        {
            var count = GamePackage.WriteZip(project, path);

            using var archive = ZipFile.OpenRead(path);
            var names = archive.Entries.Select(entry => entry.FullName).ToList();
            Assert.Equal(count, names.Count);
            Assert.Contains("gamedata/config/gameplay/character_desc_new_dialogue.xml", names);
            Assert.Contains("gamedata/config/misc/items.ltx", names);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Real_project_builds_package_inside_gamedata()
    {
        var path = FindRepositoryFile(Path.Combine("saves", "new_dialogue.project.json"));
        if (path == null) return; // тесты вне репозитория

        var project = ProjectSerializer.Deserialize(File.ReadAllText(path));

        // Item-файлы собираются независимо от валидности графов.
        var items = ItemGameExport.GameFiles(project);
        Assert.All(items.Keys, key => Assert.StartsWith("gamedata/", key));
        Assert.DoesNotContain(items.Keys, key => key.Contains(".."));

        Dictionary<string, string> files;
        try
        {
            files = GamePackage.TextFiles(project);
        }
        catch (InvalidOperationException ex)
        {
            // Паритет с legacy (`dialogs.js`): диалог с ошибками графа пакет не собирает.
            Assert.Contains("ошибки графа", ex.Message);
            return;
        }

        Assert.NotEmpty(files);
        Assert.All(files.Keys, key => Assert.StartsWith("gamedata/", key));
        Assert.DoesNotContain(files.Keys, key => key.Contains(".."));
        Assert.NotEmpty(GamePackage.Zip(project));

        if (project.Characters.Count > 0)
        {
            var characters = files.Where(pair =>
                pair.Key.Contains("character_desc_", StringComparison.Ordinal)).ToList();
            Assert.Single(characters);
            Assert.Contains("<specific_character", characters[0].Value);
            Assert.DoesNotContain("&amp;lt;", characters[0].Value);
        }
    }

    /// <summary>Файл относительно корня репозитория (`null`, если тесты запущены вне него).</summary>
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
