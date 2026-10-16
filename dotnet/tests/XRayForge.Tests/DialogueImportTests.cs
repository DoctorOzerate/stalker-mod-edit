using XRayForge.Core.Models;
using XRayForge.Core.Xml;

namespace XRayForge.Tests;

/// <summary>
/// Шаг 6.8: импорт файла `game_dialogs*.xml` в проект (`DialogueImporter.ImportFile`) —
/// снятие `#include`, нормализация с раскладкой, метка источника и отсев уже загруженных id.
/// </summary>
public class DialogueImportTests
{
    private static string Dialog(string id, string phrase) =>
        "<dialog id=\"" + id + "\"><phrase_list><phrase id=\"0\"><text>" + phrase + "</text></phrase></phrase_list></dialog>";

    private static string Wrap(params string[] dialogs) =>
        "<game_dialogs>\n" + string.Join("\n", dialogs) + "\n</game_dialogs>";

    private static string Xml() => Wrap(
        "<dialog id=\"esc_d1\"><phrase_list>" +
        "<phrase id=\"0\"><text>st_esc_1</text><next>1</next></phrase>" +
        "<phrase id=\"1\"><text>st_esc_2</text></phrase>" +
        "</phrase_list></dialog>",
        Dialog("esc_d2", "st_esc_3"));

    /// <summary>`#include` перед корневым тегом: X-Ray так и делает, а строгий XML на этом падает.</summary>
    private static string WithInclude() => "#include \"dialogs.ltx\"\n" + Wrap(Dialog("esc_d1", "st_esc_1"));

    [Fact]
    public void ImportFile_normalizes_nodes_and_lays_them_out()
    {
        var result = DialogueImporter.ImportFile(Xml(), null, "gamedata/config/gameplay/dialogs_esc.xml", "dialogs_esc.xml");

        Assert.Equal(new[] { "esc_d1", "esc_d2" }, result.Imported.Select(d => d.Id));
        Assert.Empty(result.Skipped);
        Assert.Equal("gamedata/config/gameplay/dialogs_esc.xml", result.Imported[0].SourcePath);
        Assert.Equal("dialogs_esc.xml", result.Imported[0].SourceFile);
        Assert.True(result.Imported[0].Imported);

        var dialog = result.Imported[0];
        // `safeNode` + `safeData`: тип PHRASE, роль и её источник — «unknown» (в XML нет actor).
        var phrase = dialog.Nodes.Single(n => n.Type == NodeTypes.Phrase && n.Data!.PhraseId == "0");
        Assert.Equal("unknown", phrase.Data!.Role);
        Assert.Equal("unknown", phrase.Data.RoleSource);
        Assert.Equal("st_esc_1", phrase.Data.TextId);
        Assert.True(phrase.Data.UseId);
        // Раскладка (`layoutDialogue`): START встаёт на 60/80, уровни идут по 320/110.
        var start = dialog.Nodes.Single(n => n.Type == NodeTypes.Start);
        Assert.Equal(60, start.X);
        Assert.Equal(80, start.Y);
    }

    [Fact]
    public void ImportFile_strips_include_lines_before_parsing()
    {
        // `#include` — особенность X-Ray XML, обычный разбор на нём спотыкается.
        Assert.Throws<InvalidOperationException>(() => XmlParsers.ParseDialogueXml(WithInclude()));

        var result = DialogueImporter.ImportFile(WithInclude(), null, "dialogs_esc.xml", "dialogs_esc.xml");
        Assert.Single(result.Imported);
        Assert.Equal("esc_d1", result.Imported[0].Id);
    }

    [Fact]
    public void ImportFile_skips_dialogues_already_loaded_in_project()
    {
        var result = DialogueImporter.ImportFile(Xml(), new[] { "esc_d1" });

        Assert.Equal(new[] { "esc_d2" }, result.Imported.Select(d => d.Id));
        Assert.Equal(new[] { "esc_d1" }, result.Skipped);

        // Дубль того же id внутри одного файла тоже пропускается.
        var duplicated = DialogueImporter.ImportFile(Wrap(Dialog("esc_d1", "st_1"), Dialog("esc_d1", "st_2")), null);
        Assert.Equal(new[] { "esc_d1" }, duplicated.Imported.Select(d => d.Id));
        Assert.Equal(new[] { "esc_d1" }, duplicated.Skipped);
    }

    [Fact]
    public void ImportFile_reports_nothing_for_foreign_xml()
    {
        var result = DialogueImporter.ImportFile("<character_desc><specific_character id=\"a\"/></character_desc>", null);

        Assert.Empty(result.Imported);
        Assert.Empty(result.Skipped);
    }
}
