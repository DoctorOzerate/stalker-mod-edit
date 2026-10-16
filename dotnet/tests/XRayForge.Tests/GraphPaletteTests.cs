using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Tests;

/// <summary>Этап 6, шаг 6.5: палитра узлов и метаданные реплики (`GraphPalette`).</summary>
public class GraphPaletteTests
{
    [Fact]
    public void Items_keep_palette_order_and_hide_service_types()
    {
        var items = GraphPalette.Items("");
        Assert.Equal(new[] { "START", "PHRASE", "RANDOM", "END" }, items.Select(i => i.Type));
        Assert.Equal("Реплика", items[1].Info.Label);
        Assert.Equal("#2563eb", items[1].Info.Color);
        Assert.DoesNotContain(items, i => NodeTypes.PaletteHidden.Contains(i.Type));
    }

    [Fact]
    public void Items_filter_by_type_label_and_description()
    {
        Assert.Equal(new[] { "START" }, GraphPalette.Items("старт").Select(i => i.Type));
        Assert.Equal(new[] { "PHRASE" }, GraphPalette.Items("РЕПЛИКА").Select(i => i.Type));
        Assert.Equal(new[] { "RANDOM" }, GraphPalette.Items("random").Select(i => i.Type));
        // Совпадение по описанию типа («Случайная ветка»).
        Assert.Equal(new[] { "RANDOM" }, GraphPalette.Items("случайная").Select(i => i.Type));
        // Служебный ACTION скрыт даже когда фильтр совпадает с его подписью.
        Assert.Empty(GraphPalette.Items("Действие"));
        Assert.DoesNotContain(GraphPalette.Items(null), i => i.Type == "ACTION");
    }

    [Fact]
    public void Snippets_match_source_labels_and_values()
    {
        Assert.Equal(6, GraphPalette.Snippets.Count);
        Assert.Equal(
            new[] { "has_info", "dont_has_info", "give_info", "disable_info", "precondition", "action" },
            GraphPalette.Snippets.Select(s => s.Key));
        Assert.Equal(
            new[] { "+info", "-info", "выдать info", "снять info", "has_info()", "give_item" },
            GraphPalette.Snippets.Select(s => s.Label));
        Assert.Equal(
            new[] { "%+quest_done%", "%+quest_done%", "quest_given", "quest_given", "has_info(\"%+quest_done%\")", "give_item:medkit" },
            GraphPalette.Snippets.Select(s => s.Value));
    }

    [Fact]
    public void WithSnippet_appends_value_and_keeps_other_keys()
    {
        var metadata = new Dictionary<string, List<string>>
        {
            ["has_info"] = new() { "%a%" },
            ["custom"] = new() { "x" }
        };

        var result = GraphPalette.WithSnippet(metadata, GraphPalette.Snippets[0]);
        Assert.Equal(new[] { "%a%", "%+quest_done%" }, result["has_info"]);
        // Нестандартные ключи сохраняются (`Object.assign` в JS).
        Assert.Equal(new[] { "x" }, result["custom"]);
        // Исходные метаданные не меняются на месте.
        Assert.Equal(new[] { "%a%" }, metadata["has_info"]);

        var added = GraphPalette.WithSnippet(null, GraphPalette.Snippets[5]);
        Assert.Equal(new[] { "give_item:medkit" }, added["action"]);
        Assert.Empty(GraphPalette.WithSnippet(null, null));
    }

    [Fact]
    public void TextAndLines_round_trip_metadata_values()
    {
        var metadata = new Dictionary<string, List<string>>
        {
            ["precondition"] = new() { "has_info(\"%a%\")", "has_info(\"%b%\")" }
        };

        Assert.Equal("has_info(\"%a%\")\nhas_info(\"%b%\")", GraphPalette.Text(metadata, "precondition"));
        Assert.Equal("", GraphPalette.Text(metadata, "absent"));

        // `split(/\r?\n/)`: CRLF и LF разделяют, пустая строка в конце сохраняется.
        var updated = GraphPalette.WithText(metadata, "precondition", "a\r\nb\n");
        Assert.Equal(new[] { "a", "b", "" }, updated["precondition"]);
        Assert.Equal("a\nb\n", GraphPalette.Text(updated, "precondition"));

        Assert.Equal(new[] { "" }, GraphPalette.Lines(""));
        Assert.Equal(new[] { "" }, GraphPalette.WithText(null, "action", "")["action"]);
        Assert.Equal(new[] { "only" }, GraphPalette.WithText(null, "action", "only")["action"]);
    }
}
