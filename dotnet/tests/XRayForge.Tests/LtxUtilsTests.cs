using XRayForge.Core.Ltx;

namespace XRayForge.Tests;

/// <summary>Порт функций `src/lib/ltx.js`, не покрытых `test/ltx.test.mjs`.</summary>
public class LtxUtilsTests
{
    [Fact]
    public void SplitLines_removes_cr()
    {
        Assert.Equal(new[] { "a", "b" }, LtxTextTools.SplitLines("a\r\nb"));
    }

    [Fact]
    public void UpdateLine_replaces_line_and_ignores_out_of_range()
    {
        Assert.Equal("a\nc", LtxTextTools.UpdateLine("a\nb", 2, "c"));
        Assert.Equal("a\nb", LtxTextTools.UpdateLine("a\nb", 5, "c"));
    }

    [Fact]
    public void RemoveSection_drops_section_and_leading_blank_lines()
    {
        const string text = "[wpn_base]\ncost = 100\n\n[wpn_ak74]:wpn_base\ncost = 200";
        Assert.Equal("[wpn_base]\ncost = 100", LtxTextTools.RemoveSection(text, "wpn_ak74"));

        const string two = "[a]\nx = 1\n[b]\ny = 2\n";
        Assert.Equal("[b]\ny = 2\n", LtxTextTools.RemoveSection(two, "a"));
        Assert.Equal(two, LtxTextTools.RemoveSection(two, "missing"));
    }

    [Fact]
    public void AddSection_appends_block_trimming_trailing_whitespace()
    {
        Assert.Equal("[a]\nx = 1\n\n[b]\ninv_name = b\n",
            LtxTextTools.AddSection("[a]\nx = 1\n\n", "b"));
        Assert.Equal("[b]\ninv_name = b\n", LtxTextTools.AddSection("", "b"));
    }

    [Fact]
    public void AddFieldToSection_inserts_after_header()
    {
        Assert.Equal("[a]\ncost = 100\nx = 1\n",
            LtxTextTools.AddFieldToSection("[a]\nx = 1\n", "a", "cost", "100"));
        Assert.Equal("[a]\nx = 1\n", LtxTextTools.AddFieldToSection("[a]\nx = 1\n", "b", "cost", "100"));
    }

    [Fact]
    public void RemoveFieldByLine_removes_and_ignores_out_of_range()
    {
        Assert.Equal("[a]\ny = 2", LtxTextTools.RemoveFieldByLine("[a]\nx = 1\ny = 2", 2));
        Assert.Equal("[a]\nx = 1\ny = 2", LtxTextTools.RemoveFieldByLine("[a]\nx = 1\ny = 2", 9));
    }

    [Fact]
    public void Grid_validators_accept_only_numbers()
    {
        Assert.True(IconGrid.IsValidGridValue("12"));
        Assert.False(IconGrid.IsValidGridValue("1 2"));
        Assert.False(IconGrid.IsValidGridValue("-1"));
        Assert.True(IconGrid.IsValidGridWH("3"));
        Assert.False(IconGrid.IsValidGridWH("0"));
    }

    [Fact]
    public void IconCoords_multiplies_by_cell_size()
    {
        var (x, y) = IconGrid.IconCoords("3", "2", 50);
        Assert.Equal(150, x);
        Assert.Equal(100, y);
        Assert.True(double.IsNaN(IconGrid.IconCoords("abc", "0", 50).X));
    }

    [Fact]
    public void AtlasPresets_contains_four_variants()
    {
        Assert.Equal(4, IconGrid.AtlasPresets.Count);
        Assert.Equal(5, IconGrid.AtlasPresets[0].Columns);
        Assert.Equal(64, IconGrid.AtlasPresets[^1].CellW);
    }

    [Fact]
    public void IsFileLoaded_matches_by_exact_path_and_by_base_name()
    {
        var files = new Dictionary<string, string>
        {
            ["system.ltx"] = "",
            ["config/weapons.ltx"] = ""
        };

        Assert.True(LtxIncludes.IsFileLoaded("config\\weapons.ltx", files));
        Assert.True(LtxIncludes.IsFileLoaded("weapons.ltx", files));
        Assert.False(LtxIncludes.IsFileLoaded("missing.ltx", files));
        Assert.False(LtxIncludes.IsFileLoaded("", files));
    }

    [Fact]
    public void FindIncludeSuggestion_returns_last_match()
    {
        var files = new Dictionary<string, string>
        {
            ["a/weapons.ltx"] = "",
            ["b/weapons.ltx"] = ""
        };

        Assert.Equal("b/weapons.ltx", LtxIncludes.FindIncludeSuggestion("weapons.ltx", files));
        Assert.Null(LtxIncludes.FindIncludeSuggestion("missing.ltx", files));
    }

    [Fact]
    public void ResolveIncludes_reports_found_missing_and_cycles()
    {
        var files = new Dictionary<string, string>
        {
            ["a.ltx"] = "#include \"b.ltx\"\n",
            ["b.ltx"] = "#include \"a.ltx\"\n#include \"ghost.ltx\"\n"
        };

        var rows = LtxIncludes.ResolveIncludes("#include \"a.ltx\"\n", files);

        Assert.Equal(new[] { "a.ltx", "b.ltx", "a.ltx", "ghost.ltx" }, rows.Select(r => r.Path));
        Assert.Equal(new[] { false, false, true, false }, rows.Select(r => r.Cyclic));
        Assert.Equal(new[] { true, true, true, false }, rows.Select(r => r.Found));
    }

    [Fact]
    public void IncludeTreeRows_and_graph_svg()
    {
        var files = new Dictionary<string, string> { ["a.ltx"] = "#include \"nested/b.ltx\"\n" };
        var rows = new List<IncludeTreeRow>();
        LtxIncludes.IncludeTreeRows("#include \"a.ltx\"\n", files, 0, new HashSet<string>(), rows);

        Assert.Equal(2, rows.Count);
        Assert.Equal(0, rows[0].Depth);
        Assert.Equal(1, rows[1].Depth);
        Assert.Equal("nested/b.ltx", rows[1].Path);
        Assert.False(rows[1].Found);

        var svg = LtxIncludes.IncludeGraphSvg("#include \"a.ltx\"\n", files);
        Assert.Contains("system.ltx", svg);
        Assert.Contains("width=\"570\"", svg);
        Assert.Equal("", LtxIncludes.IncludeGraphSvg("[a]\nx = 1\n", files));
    }

    [Fact]
    public void MergedEntries_merges_parent_fields_and_overrides_values()
    {
        const string text = "[wpn_base]\ncost = 100\nhit_power = 30\n\n[wpn_ak74]:wpn_base\ncost = 200\n";
        var parsed = LtxParser.Parse(text);
        var map = parsed.Sections.ToDictionary(s => s.Name.ToLowerInvariant(), s => s);
        var sec = parsed.Sections.First(s => s.Name == "wpn_ak74");

        var merged = LtxInheritance.MergedEntries(sec, map);

        Assert.Equal(new[] { "cost", "hit_power" }, merged.Select(m => m.Key));
        Assert.Equal("200", merged[0].Value);
        Assert.False(merged[0].Inherited);
        Assert.Equal("30", merged[1].Value);
        Assert.True(merged[1].Inherited);
        Assert.Equal("wpn_base", merged[1].From);
    }

    [Fact]
    public void ItemFileCategory_and_IsItemFile_classify_file_names()
    {
        Assert.Equal("Оружие", ItemCatalog.ItemFileCategory("wpn_ak74.ltx"));
        Assert.Equal("Юзабельные", ItemCatalog.ItemFileCategory("items/medkit.ltx"));
        Assert.Null(ItemCatalog.ItemFileCategory("system.ltx"));
        Assert.True(ItemCatalog.IsItemFile("items/ammo_9x18.ltx"));
        Assert.False(ItemCatalog.IsItemFile("system.ltx"));
    }

    [Fact]
    public void Catalog_tables_keep_original_sizes()
    {
        Assert.Equal(13, ItemCatalog.Categories.Count);
        Assert.Equal(15, ItemCatalog.CommonFields.Count);
        Assert.Equal(36, ItemCatalog.CommunityIcons.Count);
        Assert.Equal(9, ItemCatalog.FieldProfiles.Count);
        Assert.Equal(83, ItemCatalog.FieldDescriptions.Count);
    }

    [Fact]
    public void Highlight_splits_keys_values_and_comments()
    {
        var kv = LtxHighlight.HighlightLine("cost = 2000 ; price");
        Assert.Equal(new[] { "plain", "key", "plain", "value", "comment" }, kv.Select(t => t.Cls));
        Assert.Equal("cost", kv[1].Text);
        Assert.Equal(" = ", kv[2].Text);
        Assert.Equal("2000 ", kv[3].Text);
        Assert.Equal("; price", kv[4].Text);

        var sec = LtxHighlight.HighlightLine("[a]:base ; note");
        Assert.Equal(new[] { "plain", "section", "comment" }, sec.Select(t => t.Cls));
        Assert.Equal(":base ; note", sec[2].Text);

        var inc = LtxHighlight.HighlightLine("#include \"x.ltx\"");
        Assert.Equal(new[] { "plain", "include" }, inc.Select(t => t.Cls));

        var comment = LtxHighlight.HighlightLine("; whole line");
        Assert.Single(comment);
        Assert.Equal("comment", comment[0].Cls);
    }

    [Fact]
    public void Highlight_html_matches_original_colors()
    {
        var html = LtxHighlight.ToHtml("[wpn_ak74]:base\n; comment\ncost = 2000 ; price\n");

        Assert.Contains("#a78bfa", html);
        Assert.Contains("#fbbf24", html);
        Assert.Contains("#6b7280", html);
        Assert.Contains("<span style=\"color:#a78bfa;font-weight:bold\">[wpn_ak74]</span>", html);
    }

    [Fact]
    public void CommunityIcons_keep_expected_code_points()
    {
        Assert.Equal("1f3ad", CodePoints("actor"));
        Assert.Equal("1f6e1+fe0f", CodePoints("dolg"));
        Assert.Equal("2622+fe0f", CodePoints("monolith"));
        Assert.Equal("1f981", CodePoints("chimera"));
        Assert.Equal("1f47e", CodePoints("arena_monstr"));
        Assert.Equal("1fa78", CodePoints("bloodsucker"));
    }

    /// <summary>Кодовые точки значения COMMUNITY_ICONS через «+» (эталон из src/lib/ltx.js).</summary>
    private static string CodePoints(string key)
    {
        var s = ItemCatalog.CommunityIcons[key];
        var parts = new List<string>();
        for (var i = 0; i < s.Length; i++)
        {
            parts.Add(char.ConvertToUtf32(s, i).ToString("x"));
            if (char.IsHighSurrogate(s[i])) i++;
        }
        return string.Join('+', parts);
    }
}