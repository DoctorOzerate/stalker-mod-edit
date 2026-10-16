using XRayForge.Core.Models;
using XRayForge.Core.Xml;

namespace XRayForge.Tests;

public class XmlTests
{
    [Fact]
    public void ParseCharacterDescGeneral_reads_communities_and_relations()
    {
        const string xml = "<character_desc_general><communities><community id=\"stalker\"><name>st_stalker</name><icon>ui_inGame2_Stalker</icon><relation community=\"bandit\">-100</relation></community><community id=\"bandit\"/></communities></character_desc_general>";

        var r = GameXmlParsers.ParseCharacterDescGeneral(xml);
        Assert.NotNull(r);
        Assert.Equal(2, r!.Communities.Count);
        Assert.Equal("st_stalker", r.Communities[0].Name);
        Assert.Equal("ui_inGame2_Stalker", r.Communities[0].Icon);
        Assert.Equal("bandit", r.Communities[0].Relations[0].Community);
        Assert.Equal("-100", r.Communities[0].Relations[0].Value);
        Assert.Empty(r.Communities[1].Relations);
    }

    [Fact]
    public void CharacterDescGeneralToXml_round_trips()
    {
        var data = new CharacterDescGeneral
        {
            Communities = new()
            {
                new Community
                {
                    Id = "a",
                    Name = "na",
                    Icon = "ic",
                    Relations = new() { new CommunityRelation { Community = "b", Value = "1" } }
                }
            }
        };

        var xml = GameXmlParsers.CharacterDescGeneralToXml(data);
        Assert.Contains("<community id=\"a\">", xml);
        Assert.Contains("<relation community=\"b\">1</relation>", xml);

        var r = GameXmlParsers.ParseCharacterDescGeneral(xml);
        Assert.NotNull(r);
        Assert.Equal("1", r!.Communities[0].Relations[0].Value);
        Assert.Equal("na", r.Communities[0].Name);
    }

    [Fact]
    public void ParseStringTableXml_handles_fragment_without_root()
    {
        var lang = StringTableParser.Parse(
            "<string id=\"a\"><text>Привет</text></string><string id=\"b\"><text>Пока</text></string>",
            "gamedata/config/text/rus/stable.xml");

        Assert.NotNull(lang);
        Assert.Equal("rus", lang!.Locale);
        Assert.Equal("Русский", lang.Name);
        Assert.Equal("gamedata/config/text/rus/stable.xml", lang.Path);
        Assert.Equal(2, lang.Entries.Count);
        Assert.Equal("Привет", lang.Entries[0].Text);
    }

    [Fact]
    public void ParseStringTableXml_returns_null_for_non_string_table()
    {
        Assert.Null(StringTableParser.Parse(
            "<xml><specific_character id=\"x\"><name>n</name></specific_character></xml>",
            "gamedata/config/gameplay/character_desc.xml"));
    }

    [Fact]
    public void ParseInfoPortions_reads_info_portions()
    {
        const string xml = "<game_information_portions><info_portion id=\"p1\"><task>t1</task><dialog>d1</dialog><disable>p2</disable></info_portion></game_information_portions>";

        var r = GameXmlParsers.ParseInfoPortions(xml);
        Assert.Single(r);
        Assert.Equal("p1", r[0].Id);
        Assert.Equal("t1", r[0].Tasks[0]);
        Assert.Equal("d1", r[0].Dialogs[0]);
        Assert.Equal("p2", r[0].Disables[0]);
    }

    [Fact]
    public void ParseEncyclopedia_reads_articles()
    {
        const string xml = "<encyclopedia><article id=\"a1\" name=\"Bread\" group=\"Equipment/Food\"><ltx>bread</ltx><text>enc_equipment_food_bread1</text></article></encyclopedia>";

        var r = GameXmlParsers.ParseEncyclopedia(xml);
        Assert.Single(r);
        Assert.Equal("Bread", r[0].Name);
        Assert.Equal("Equipment/Food", r[0].Group);
        Assert.Equal("bread", r[0].Ltx);
        Assert.Equal("enc_equipment_food_bread1", r[0].Text);
    }

    [Fact]
    public void ParseNpcProfile_reads_characters()
    {
        const string xml = "<xml><character id=\"escape_trader\"><class>Trader</class><specific_character>escape_trader</specific_character></character><character id=\"default\"><class>Stalker_general</class></character></xml>";

        var r = GameXmlParsers.ParseNpcProfile(xml);
        Assert.Equal(2, r.Count);
        Assert.Equal("Trader", r[0].Class);
        Assert.Equal("escape_trader", r[0].SpecificCharacter);
        Assert.Equal("", r[1].SpecificCharacter);
    }

    [Fact]
    public void ParseTextureDescriptor_reads_file_and_texture_coords()
    {
        const string xml = "<w><file name=\"ui\\ui_npc_unique\"><texture id=\"ui_npc_u_actor\" x=\"0\" y=\"0\" width=\"165\" height=\"108\"/><texture id=\"ui_npc_u_bandit_1\" x=\"165\" y=\"108\" width=\"165\" height=\"108\"/></file></w>";

        var r = GameXmlParsers.ParseTextureDescriptor(xml);
        Assert.NotNull(r);
        Assert.Equal("ui\\ui_npc_unique", r!.File);
        Assert.Equal(2, r.Textures.Count);
        Assert.Equal("ui_npc_u_actor", r.Textures[0].Id);
        Assert.Equal(165.0, r.Textures[0].Width);
        Assert.Equal(108.0, r.Textures[0].Height);
        Assert.Equal(165.0, r.Textures[1].X);
    }

    [Fact]
    public void XmlAttr_and_XmlTag_extract_raw_values()
    {
        Assert.Equal("ui", XmlHelpers.XmlAttr(" id=\"a\" name=\"ui\"", "name"));
        Assert.Equal("", XmlHelpers.XmlAttr("id=a", "id"));
        Assert.Equal("hi", XmlHelpers.XmlTag("<bio attr=\"1\">hi</bio>", "bio"));
        Assert.Equal("", XmlHelpers.XmlTag("<bio>x</bio>", "name"));
    }

    [Fact]
    public void DetectLocale_prefers_text_segment_then_filename_suffix()
    {
        Assert.Equal("rus", XmlHelpers.DetectLocale("gamedata/config/text/rus/stable.xml"));
        Assert.Equal("eng", XmlHelpers.DetectLocale("gamedata/configs/text/eng/st_foo.xml"));
        Assert.Equal("eng", XmlHelpers.DetectLocale("abc_eng.xml"));
        Assert.Equal("unknown", XmlHelpers.DetectLocale("foo.xml"));
        Assert.Equal("unknown", XmlHelpers.DetectLocale(""));
    }

    [Fact]
    public void MergeLanguages_keeps_existing_and_appends_new_entries()
    {
        var existing = new List<Language?>
        {
            new Language { Locale = "RUS", Name = "Русский", Entries = new() { new LanguageEntry { Id = "a", Text = "x" } } }
        };
        var incoming = new List<Language?>
        {
            new Language
            {
                Locale = "rus",
                Entries = new() { new LanguageEntry { Id = "a", Text = "y" }, new LanguageEntry { Id = "b", Text = "z" } }
            },
            new Language { Locale = "eng", Entries = new() { new LanguageEntry { Id = "c", Text = "w" } } }
        };

        var merged = XmlHelpers.MergeLanguages(existing, incoming);
        Assert.Equal(2, merged.Count);
        Assert.Equal("rus", merged[0].Locale);
        Assert.Equal("Русский", merged[0].Name);
        Assert.Equal(2, merged[0].Entries.Count);
        Assert.Equal("x", merged[0].Entries[0].Text);
        Assert.Equal("z", merged[0].Entries[1].Text);
        Assert.Equal("eng", merged[1].Locale);
        Assert.Equal("English", merged[1].Name);
    }

    [Fact]
    public void ParseGeneralCharactersXml_collects_communities_then_factions()
    {
        var ids = XmlHelpers.ParseGeneralCharactersXml(
            "<character_desc_general><community id=\"stalker\"/><community id=\"bandit\"/><faction id=\"killer\"/><community id=\"stalker\"/></character_desc_general>");

        Assert.Equal(new[] { "stalker", "bandit", "killer" }, ids);
        Assert.Empty(XmlHelpers.ParseGeneralCharactersXml("<xml/>"));
    }

    [Fact]
    public void DialogueImporter_lays_out_and_normalizes_imported_dialogue()
    {
        const string xml = "<game_dialogs><dialog id=\"d\"><phrase_list><phrase id=\"0\"><text>st_0</text><next>2</next></phrase><phrase id=\"2\"><text>st_2</text></phrase></phrase_list></dialog></game_dialogs>";

        var d = DialogueImporter.Import(xml)[0];
        Assert.Equal("d", d.Id);
        Assert.Equal("d", d.Name);

        var phrase0 = d.Nodes.First(n => n.Data!.PhraseId == "0");
        var phrase2 = d.Nodes.First(n => n.Data!.PhraseId == "2");
        Assert.True(phrase0.X < phrase2.X);
        Assert.All(
            d.Nodes.Where(n => n.Type == NodeTypes.Phrase),
            n => Assert.Equal("unknown", n.Data!.RoleSource));
        Assert.All(
            d.Nodes.Where(n => n.Type == NodeTypes.Phrase),
            n => Assert.Equal("unknown", n.Data!.Role));
    }

    [Fact]
    public void StringTableParser_missing_file_returns_null_for_declaration_only()
    {
        Assert.Null(StringTableParser.Parse("<?xml version=\"1.0\" encoding=\"windows-1251\"?>", "x.xml"));
    }
}