using System.Text.Json;
using System.Text.RegularExpressions;
using XRayForge.Core.Characters;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Validation;

namespace XRayForge.Tests;

/// <summary>
/// Шаг 7.1: чистая логика вкладки «Персонажи» (`CharacterCatalog`) — создание, поиск,
/// группировка по community, дубликаты id, связанные диалоги, diff с `original`, исходный XML,
/// автосвязь `start_dialog` и поле `npc_rank` вне модели.
/// </summary>
public class CharacterCatalogTests
{
    private static Character Character(string id, string name = "", string community = "") => new()
    {
        Id = id,
        Name = name,
        Community = community
    };

    private static DialogueGraph Dialogue(string id, string name = "") => new() { Id = id, Name = name };

    [Fact]
    public void Create_fills_editor_defaults_like_the_js_button()
    {
        var character = CharacterCatalog.Create("char_x", "Новый персонаж");

        Assert.Equal("char_x", character.Id);
        Assert.Equal("Новый персонаж", character.Name);
        Assert.Equal("stalker", character.Community);
        Assert.Equal("0", character.Rank);
        Assert.Equal("", character.Bio);
        Assert.Equal("", character.Icon);
        Assert.Equal("", character.Visual);
        Assert.Empty(character.StartDialog);
        Assert.Equal(CharacterCatalog.EditorSource, character.Source);
    }

    [Fact]
    public void NewId_uses_char_prefix_and_base36_timestamp()
    {
        var id = CharacterCatalog.NewId();

        Assert.True(Regex.IsMatch(id, "^char_[0-9a-z]+$"), id);
    }

    [Fact]
    public void Filter_searches_name_id_bio_visual_community_and_dialogs()
    {
        var characters = new List<Character>
        {
            new() { Id = "bar_nimble", Name = "Нимбль", Bio = "Торговец", Community = "stalker" },
            new()
            {
                Id = "bar_barman", Name = "Бармен", Visual = "actors\\barman", Community = "dolg",
                StartDialog = new List<string> { "bar_barman_start" }
            }
        };

        Assert.Equal(2, CharacterCatalog.Filter(characters, "").Count);
        Assert.Equal("bar_nimble", Assert.Single(CharacterCatalog.Filter(characters, "торговец")).Id);
        Assert.Equal("bar_barman", Assert.Single(CharacterCatalog.Filter(characters, "ACTORS\\BARMAN")).Id);
        Assert.Equal("bar_barman", Assert.Single(CharacterCatalog.Filter(characters, "dolg")).Id);
        Assert.Equal("bar_barman", Assert.Single(CharacterCatalog.Filter(characters, "bar_barman_start")).Id);
        Assert.Empty(CharacterCatalog.Filter(characters, "нет такого"));
    }

    [Fact]
    public void SearchText_lowercases_and_joins_dialog_lists_with_commas()
    {
        var character = new Character
        {
            Id = "Id",
            Name = "Имя",
            StartDialog = new List<string> { "a", "b" },
            ActorDialog = new List<string> { "c" }
        };

        Assert.Equal("имя id    a,b c", CharacterCatalog.SearchText(character));
    }

    [Fact]
    public void DisplayName_falls_back_to_id()
    {
        Assert.Equal("Имя", CharacterCatalog.DisplayName(Character("id", "Имя")));
        Assert.Equal("id", CharacterCatalog.DisplayName(Character("id")));
        Assert.Equal("", CharacterCatalog.DisplayName(null));
    }

    [Fact]
    public void CommunityIcon_uses_registry_and_glyph_fallback()
    {
        Assert.Equal(ItemCatalog.CommunityIcons["stalker"], CharacterCatalog.CommunityIcon("STALKER"));
        Assert.Equal(CharacterCatalog.DefaultIcon, CharacterCatalog.CommunityIcon("неизвестно"));
        Assert.Equal(CharacterCatalog.DefaultIcon, CharacterCatalog.CommunityIcon(null));
    }

    [Fact]
    public void DuplicateIds_flags_repeated_ids_only()
    {
        var duplicates = CharacterCatalog.DuplicateIds(new List<Character>
        {
            Character("a"), Character("b"), Character("a"), Character("c"), Character("b")
        });

        Assert.Equal(new[] { "a", "b" }, duplicates.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Empty(CharacterCatalog.DuplicateIds(null));
    }

    [Fact]
    public void Group_puts_empty_community_into_placeholder_and_sorts_groups()
    {
        var characters = new List<Character>
        {
            Character("c1", "Второй", "stalker"),
            Character("c2", "Без группы"),
            Character("c3", "Первый", "bandit")
        };

        var groups = CharacterCatalog.Group(characters, "", false);

        Assert.Equal(new[] { "bandit", "stalker", CharacterCatalog.NoCommunity },
            groups.Select(group => group.Community).ToArray());
        Assert.Equal(ItemCatalog.CommunityIcons["bandit"], groups[0].Icon);
        Assert.Equal(new[] { "c2" }, groups[2].Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void Group_sorts_items_by_name_when_asked_and_filters_by_query()
    {
        var characters = new List<Character>
        {
            Character("b", "Бета", "stalker"),
            Character("a", "Альфа", "stalker"),
            Character("z", "Гамма", "dolg")
        };

        var sorted = CharacterCatalog.Group(characters, "", true).Single(group => group.Community == "stalker");
        Assert.Equal(new[] { "a", "b" }, sorted.Items.Select(item => item.Id).ToArray());

        var filtered = CharacterCatalog.Group(characters, "гам", true);
        Assert.Equal("dolg", Assert.Single(filtered).Community);
    }

    [Fact]
    public void Related_matches_start_and_actor_dialogs_by_id_or_name()
    {
        var character = new Character
        {
            StartDialog = new List<string> { "d1", "Имя диалога" },
            ActorDialog = new List<string> { "d3" }
        };
        var dialogues = new List<DialogueGraph>
        {
            Dialogue("d1"), Dialogue("d2"), Dialogue("d4", "Имя диалога"), Dialogue("d3")
        };

        var related = CharacterCatalog.Related(dialogues, character);

        Assert.Equal(new[] { "d1", "d4", "d3" }, related.Select(dialogue => dialogue.Id).ToArray());
        Assert.Empty(CharacterCatalog.Related(dialogues, new Character()));
    }

    [Fact]
    public void Diff_lists_only_changed_fields_in_editor_order()
    {
        var character = new Character { Id = "npc", Name = "Нимбль", Bio = "Торговец", Rank = "2" };
        character.Original = JsonSerializer.SerializeToElement(character);

        character.Name = "Нимбль (бар)";
        character.Bio = "";

        var rows = CharacterCatalog.Diff(character);

        Assert.Equal(new[] { "charDiffName", "charDiffBio" }, rows.Select(row => row.LabelKey).ToArray());
        Assert.Equal("Нимбль", rows[0].Before);
        Assert.Equal("Нимбль (бар)", rows[0].After);
        Assert.Equal("Торговец", rows[1].Before);
        Assert.Equal("", rows[1].After);
    }

    [Fact]
    public void Diff_formats_dialog_arrays_with_commas_and_ignores_missing_original()
    {
        var character = new Character { Id = "npc", StartDialog = new List<string> { "a", "b" } };
        character.Original = JsonSerializer.SerializeToElement(character);
        character.StartDialog = new List<string> { "a" };

        var row = Assert.Single(CharacterCatalog.Diff(character));
        Assert.Equal("charDiffStartDialog", row.LabelKey);
        Assert.Equal("a,b", row.Before);
        Assert.Equal("a", row.After);

        Assert.Empty(CharacterCatalog.Diff(new Character()));
        Assert.Empty(CharacterCatalog.Diff(null));
    }

    [Fact]
    public void ExtractXml_returns_specific_character_block_or_null()
    {
        const string file = "text = a\n<specific_character id=\"npc_a\">\n  <bio>x</bio>\n</specific_character>\n";
        var files = new Dictionary<string, string> { ["characters.xml"] = file };

        Assert.Equal("<specific_character id=\"npc_a\">\n  <bio>x</bio>\n</specific_character>",
            CharacterCatalog.ExtractXml("npc_a", files));
        Assert.Null(CharacterCatalog.ExtractXml("npc_b", files));
        Assert.Null(CharacterCatalog.ExtractXml("npc_a", null));
    }

    [Fact]
    public void ExtractXml_returns_whole_file_without_block_bounds()
    {
        const string file = "id='npc_a' без разметки";
        var files = new Dictionary<string, string> { ["a.xml"] = file };

        Assert.Equal(file, CharacterCatalog.ExtractXml("npc_a", files));
    }

    [Fact]
    public void AutofillDialogs_fills_only_empty_start_dialogs()
    {
        var characters = new List<Character>
        {
            Character("bar_nimble"),
            new() { Id = "bar_barman", StartDialog = new List<string> { "keep" } }
        };
        var dialogues = new List<DialogueGraph>
        {
            Dialogue("bar_nimble_start"), Dialogue("bar_dolg_petrenko_start")
        };

        var changed = CharacterCatalog.AutofillDialogs(characters, dialogues);

        Assert.Equal("bar_nimble", Assert.Single(changed).Id);
        Assert.Equal(new[] { "bar_nimble_start" }, characters[0].StartDialog);
        Assert.Equal(new[] { "keep" }, characters[1].StartDialog);
    }

    [Fact]
    public void ExtraString_reads_and_writes_npc_rank_outside_the_model()
    {
        var character = new Character { Id = "npc" };
        Assert.Equal("", CharacterCatalog.ExtraString(character, "npc_rank"));

        CharacterCatalog.SetExtraString(character, "npc_rank", "");
        Assert.Null(character.Extra);

        CharacterCatalog.SetExtraString(character, "npc_rank", "3");
        Assert.Equal("3", CharacterCatalog.ExtraString(character, "npc_rank"));
        Assert.Equal(JsonValueKind.String, character.Extra!["npc_rank"].ValueKind);

        CharacterCatalog.SetExtraString(character, "npc_rank", "4");
        Assert.Equal("4", CharacterCatalog.ExtraString(character, "npc_rank"));
    }

    [Fact]
    public void Validator_issues_keep_codes_and_legacy_strings()
    {
        var character = new Character { Id = "npc", Community = "dolg" };
        IReadOnlyDictionary<string, object?> textures = new Dictionary<string, object?> { ["portrait"] = null };

        Assert.Equal(
            new[] { CharacterIssue.MissingVisual, CharacterIssue.MissingIcon },
            CharacterValidator.Issues(character, new[] { "dolg" }, textures).ToArray());
        Assert.Equal(
            new[] { "не указан visual (модель)", "не указана icon (портрет)" },
            CharacterValidator.Validate(character, new[] { "dolg" }, textures).ToArray());

        var broken = new Character { Id = "npc", Visual = "v", Icon = "missing", Community = "unknown" };
        IReadOnlyDictionary<string, object?> map = new Dictionary<string, object?> { ["portrait"] = null };

        Assert.Equal(
            new[] { CharacterIssue.UnknownCommunity, CharacterIssue.UnknownTexture },
            CharacterValidator.Issues(broken, new[] { "dolg" }, map).ToArray());
        Assert.Equal(
            new[] { "группировка не найдена: unknown", "портрет не найден в дескрипторах текстур: missing" },
            CharacterValidator.Validate(broken, new[] { "dolg" }, map).ToArray());
    }
}
