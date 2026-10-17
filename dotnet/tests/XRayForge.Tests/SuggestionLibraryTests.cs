using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Suggestions;

namespace XRayForge.Tests;

/// <summary>Библиотека данных для автодополнения: индекс проекта и фильтрация по полям.</summary>
public class SuggestionLibraryTests
{
    private static Project ProjectWithData()
    {
        var project = new Project { DialogueId = "bar_dialog" };
        project.ItemsLtx =
            "[wpn_ak74]\nclass = WP_AK74\ninv_name = wpn_ak74_name\nammo_class = ammo_9x18\n\n" +
            "[ammo_9x18]\nclass = AMMO\n\n" +
            "[sect_outfit_immunities]\nburn_protection = 1\n\n" +
            "[upgr_ak74]\nclass = II_ATTCH\n";
        project.ItemOrigins = new Dictionary<string, string> { ["wpn_ak74"] = "Оружие" };
        project.SystemLtx = "[wpn_ak74]\nclass = WP_AK74\n";
        project.Monsters = new List<MonsterInfo>();
        project.SpawnSections = new List<SpawnSectionInfo>();

        project.Languages.Add(new Language
        {
            Locale = "rus",
            Entries =
            {
                new LanguageEntry { Id = "wpn_ak74_name", Text = "АК-74" },
                new LanguageEntry { Id = "bar_hello", Text = "Привет" }
            }
        });

        project.Scripts = new List<ScriptFile>
        {
            new()
            {
                Name = "bind_stalker.script",
                Text = "-- function ignored() end\n" +
                       "function actor_init() end\n" +
                       "function actor:update() end"
            }
        };

        project.Characters.Add(new Character
        {
            Id = "bar_barman",
            Name = "Бармен",
            Community = "dolg",
            Icon = "ui_npc_bar",
            Visual = "stalker_sv_bar"
        });

        project.Dialogues.Add(new DialogueGraph
        {
            Id = "bar_barman_dialog",
            Name = "Бармен",
            Nodes = { new DialogueNode { Data = new NodeData { TextId = "bar_hello" } } }
        });

        project.GameRelations = new GameRelationsInfo
        {
            Communities = { "dolg" },
            Scales = new RatingScales
            {
                Rating = { new ScaleMark { Name = "Новичок", Value = "0" } },
                Reputation = { new ScaleMark { Name = "Нейтрал", Value = "-1000" } }
            }
        };
        project.GeneralCommunities.Add("dolg");
        project.TextureMap = new Dictionary<string, TextureMapEntry>
        {
            ["ui_npc_bar"] = new() { File = "ui_npc.dds" }
        };
        project.Monsters.Add(new MonsterInfo
        {
            Id = "bloodsucker",
            Class = "SM_BLOODSUCKER",
            Visual = "monsters\\bloodsucker",
            ScriptBinding = "bind_monster.bind"
        });
        project.SpawnSections.Add(new SpawnSectionInfo
        {
            Id = "spawn_bar_barman",
            Base = "stalker",
            Community = "dolg",
            CharacterProfile = "bar_barman"
        });
        return project;
    }

    [Fact]
    public void Build_indexes_project_domains_without_duplicates()
    {
        var library = SuggestionLibrary.Build(ProjectWithData());

        Assert.Contains(library.OfKind(SuggestionKind.Item),
            entry => entry.Value == "wpn_ak74" && entry.Detail == "Оружие");
        Assert.Contains(library.OfKind(SuggestionKind.Class), entry => entry.Value == "WP_AK74");
        Assert.Contains(library.OfKind(SuggestionKind.ScriptBinding),
            entry => entry.Value == "bind_stalker.actor_init");
        Assert.Contains(library.OfKind(SuggestionKind.ScriptFunction),
            entry => entry.Value == "actor_init");
        Assert.Contains(library.OfKind(SuggestionKind.TextId),
            entry => entry.Value == "wpn_ak74_name" && entry.Detail == "АК-74");
        Assert.Contains(library.OfKind(SuggestionKind.TextId), entry => entry.Value == "bar_hello");
        Assert.Contains(library.OfKind(SuggestionKind.Dialogue), entry => entry.Value == "bar_dialog");
        Assert.Contains(library.OfKind(SuggestionKind.Dialogue), entry => entry.Value == "bar_barman_dialog");
        Assert.Contains(library.OfKind(SuggestionKind.Character), entry => entry.Value == "bar_barman");
        Assert.Contains(library.OfKind(SuggestionKind.Community), entry => entry.Value == "dolg");
        Assert.Contains(library.OfKind(SuggestionKind.Icon), entry => entry.Value == "ui_npc_bar");
        Assert.Contains(library.OfKind(SuggestionKind.Visual), entry => entry.Value == "stalker_sv_bar");
        Assert.Contains(library.OfKind(SuggestionKind.Monster), entry => entry.Value == "bloodsucker");
        Assert.Contains(library.OfKind(SuggestionKind.Rank), entry => entry.Value == "0");
        Assert.Contains(library.OfKind(SuggestionKind.LuaApi), entry => entry.Value == "db.actor");

        Assert.DoesNotContain(library.All, entry => entry.Value == "ignored");
        Assert.Single(library.OfKind(SuggestionKind.Item), entry => entry.Value == "wpn_ak74");
    }

    [Fact]
    public void ForItemField_uses_domain_sources_and_keeps_existing_values()
    {
        var library = SuggestionLibrary.Build(ProjectWithData());

        var text = library.ForItemField("inv_name", new[] { "custom_name" });
        Assert.Contains(text, entry => entry.Kind == SuggestionKind.TextId && entry.Value == "wpn_ak74_name");
        Assert.Contains(text, entry => entry.Kind == SuggestionKind.Value && entry.Value == "custom_name");
        Assert.DoesNotContain(text, entry => entry.Value == "wpn_ak74");

        Assert.Contains(library.ForItemField("ammo_class"), entry => entry.Value == "ammo_9x18");
        Assert.DoesNotContain(library.ForItemField("ammo_class"), entry => entry.Value == "wpn_ak74");
        Assert.Contains(library.ForItemField("script_binding"),
            entry => entry.Value == "bind_stalker.actor_init");
        Assert.Contains(library.ForItemField("$spawn"), entry => entry.Value == "spawn_bar_barman");
        Assert.Contains(library.ForItemField("immunities_sect"), entry => entry.Value == "sect_outfit_immunities");
    }

    [Fact]
    public void ForCharacterField_selects_rank_reputation_icon_visual_and_dialogues()
    {
        var library = SuggestionLibrary.Build(ProjectWithData());

        Assert.Contains(library.ForCharacterField("rank"), entry => entry.Value == "0");
        Assert.Contains(library.ForCharacterField("npc_rank"), entry => entry.Value == "0");
        Assert.Contains(library.ForCharacterField("reputation"), entry => entry.Value == "-1000");
        Assert.Contains(library.ForCharacterField("icon"), entry => entry.Value == "ui_npc_bar");
        Assert.Contains(library.ForCharacterField("visual"), entry => entry.Value == "stalker_sv_bar");
        Assert.Contains(library.ForCharacterField("start_dialog"), entry => entry.Value == "bar_barman_dialog");
        Assert.Empty(library.ForCharacterField("name"));
    }

    [Fact]
    public void ForMetadataField_uses_info_portions_for_conditions_and_scripts_for_actions()
    {
        var project = ProjectWithData();
        project.InfoPortions = new List<InfoPortion>
        {
            new() { Id = "quest_done", Tasks = { "task_1" } }
        };
        var library = SuggestionLibrary.Build(project);

        Assert.Contains(library.ForMetadataField("has_info"), entry => entry.Value == "quest_done");
        Assert.Contains(library.ForMetadataField("give_info"), entry => entry.Value == "quest_done");
        Assert.Contains(library.ForMetadataField("action"),
            entry => entry.Value == "bind_stalker.actor_init");
        Assert.Contains(library.ForMetadataField("precondition"), entry => entry.Value == "actor_init");
        Assert.Empty(library.ForMetadataField("unknown"));
    }

    [Fact]
    public void Suggest_ranks_prefix_then_segment_boundary_and_honours_kind_filter()
    {
        var source = new[]
        {
            new SuggestionEntry("ammo_9x18", SuggestionKind.Item),
            new SuggestionEntry("wpn_ak74", SuggestionKind.Item),
            new SuggestionEntry("ak74", SuggestionKind.Item)
        };

        var matches = SuggestionMatcher.Filter(source, "ak");
        Assert.Equal(new[] { "ak74", "wpn_ak74" }, matches.Select(entry => entry.Value));

        var library = SuggestionLibrary.Build(ProjectWithData());
        var communities = library.Suggest("do", new[] { SuggestionKind.Community });
        Assert.All(communities, entry => Assert.Equal(SuggestionKind.Community, entry.Kind));
        Assert.Contains(communities, entry => entry.Value == "dolg");
        Assert.DoesNotContain(communities, entry => entry.Value == "bar_dialog");
    }

    [Fact]
    public void Build_falls_back_to_item_files_and_returns_empty_for_null_project()
    {
        var project = new Project
        {
            ItemFiles = new Dictionary<string, string>
            {
                ["items.ltx"] = "[medkit]\nclass = II_MEDKI\n"
            }
        };

        var library = SuggestionLibrary.Build(project);
        Assert.Contains(library.OfKind(SuggestionKind.Item), entry => entry.Value == "medkit");
        Assert.Empty(SuggestionLibrary.Build(null).All);
    }
}
