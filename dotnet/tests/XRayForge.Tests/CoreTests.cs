using XRayForge.Core.Graph;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Text;
using XRayForge.Core.Validation;
using XRayForge.Core.Xml;

namespace XRayForge.Tests;

public class CoreTests
{
    [Fact]
    public void Ltx_inheritance()
    {
        var p = LtxParser.Parse("[child]:base1, base2\r\nx=1\r\n");
        Assert.Equal(new[] { "base1", "base2" }, p.Sections[0].Parents);
        Assert.Equal("1", p.Sections[0].Entries[0].Value);
    }

    [Fact]
    public void Ltx_include_parsing()
    {
        var p = LtxParser.Parse("#include \"a.ltx\"\n#include \"b.ltx\"\n");
        Assert.Equal(new[] { "a.ltx", "b.ltx" }, p.Includes.Select(x => x.Path));
    }

    [Fact]
    public void ParseCharactersXml_captures_supplies()
    {
        var xml = "<xml><specific_character id=\"aes_Monolit_rpg\"><name>N</name><community>monolith</community><supplies>[spawn] \nwpn_walther \nammo_9x19_fmj</supplies></specific_character></xml>";
        var chars = XmlParsers.ParseCharactersXml(xml);
        Assert.Single(chars);
        Assert.Contains("wpn_walther", chars[0].Supplies);
    }

    [Fact]
    public void Phrase_list_and_id_zero()
    {
        var x = "<game_dialogs><dialog id=\"d\"><phrase_list><phrase id=\"0\"><text>t</text><next>2</next></phrase><phrase id=\"2\"><text>u</text></phrase></phrase_list></dialog></game_dialogs>";
        var d = XmlParsers.ParseDialogueXml(x)[0];
        Assert.Equal("d__phrase_0__0", d.Nodes[1].Id);
        Assert.Contains(d.Edges, e => e.Target == "d__phrase_2__0");
    }

    [Fact]
    public void Dialogue_phrase_sound_imports_as_voice_id()
    {
        var x = "<game_dialogs><dialog id=\"d\"><phrase_list><phrase id=\"0\"><text>st_0</text><sound>characters\\\\wolf\\\\wolf_0.ogg</sound></phrase></phrase_list></dialog></game_dialogs>";
        var d = XmlParsers.ParseDialogueXml(x)[0];
        Assert.Equal("characters\\\\wolf\\\\wolf_0.ogg", d.Nodes[1].Data!.VoiceId);
    }

    [Fact]
    public void Xml_entities_and_characters_metadata()
    {
        Assert.Equal("&lt;&amp;&gt;", XmlEscaping.EscapeText("<&>"));
        Assert.Equal("&quot;&apos;", XmlEscaping.EscapeAttribute("\"'"));
        var c = XmlParsers.ParseCharactersXml(
            "<r><specific_character id=\"x\"><start_dialog>a</start_dialog><start_dialog>b</start_dialog></specific_character></r>")[0];
        Assert.Equal(new[] { "a", "b" }, c.StartDialog);
    }

    [Fact]
    public void Character_fields_parsed_from_child_elements()
    {
        var xml = "<r><specific_character id=\"esc_wolf\" team=\"stalker\"><name>Волк</name><community>stalker</community><rank>100</rank><reputation>50</reputation><bio><text>Опытный сталкер</text></bio><visual>actors\\\\stalker_2\\\\stalker_2</visual><icon>ui_npc_u_wolf</icon><start_dialog>esc_wolf_start</start_dialog></specific_character></r>";
        var c = XmlParsers.ParseCharactersXml(xml)[0];
        Assert.Equal("esc_wolf", c.Id);
        Assert.Equal("Волк", c.Name);
        Assert.Equal("stalker", c.Community);
        Assert.Equal("100", c.Rank);
        Assert.Equal("50", c.Reputation);
        Assert.Equal("Опытный сталкер", c.Bio);
        Assert.Equal("actors\\\\stalker_2\\\\stalker_2", c.Visual);
        Assert.Equal("ui_npc_u_wolf", c.Icon);
        Assert.Equal(new[] { "esc_wolf_start" }, c.StartDialog);
    }

    [Fact]
    public void Cp1251_declaration_compatible_bytes()
    {
        var bytes = GameEncoding.EncodeCp1251("Привет");
        Assert.Equal("Привет", GameEncoding.Decode(bytes, "windows-1251"));
    }

    [Fact]
    public void Large_cycle_validation_iterative()
    {
        var n = new List<DialogueNode> { new() { Id = "s", Type = NodeTypes.Start } };
        var e = new List<DialogueEdge>();
        for (var i = 0; i < 10000; i++)
        {
            n.Add(new DialogueNode { Id = i.ToString(), Type = "NPC_REPLY" });
            e.Add(new DialogueEdge { Source = i != 0 ? " " + (i - 1) : "s", Target = i.ToString() });
        }

        var issues = GraphValidator.Validate(n, e);
        Assert.NotNull(issues);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void ValidateCharacter_flags_missing_fields_and_unknown_community()
    {
        var issues = CharacterValidator.Validate(
            new Character { Visual = "", Icon = "ui_npc_x", Community = "nope" },
            new[] { "stalker", "bandit" },
            new Dictionary<string, object?> { { "ui_npc_known", 1 } });

        Assert.Contains(issues, i => i.Contains("visual"));
        Assert.Contains(issues, i => i.Contains("группировка"));
        Assert.Contains(issues, i => i.Contains("портрет"));

        var ok = CharacterValidator.Validate(
            new Character { Visual = "v", Icon = "i", Community = "stalker" },
            new[] { "stalker" },
            new Dictionary<string, object?>());
        Assert.Empty(ok);
    }
}
