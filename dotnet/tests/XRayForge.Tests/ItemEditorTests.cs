using XRayForge.Core.Ltx;

namespace XRayForge.Tests;

/// <summary>Логика вкладки «Предметы» (`ItemsTab.js`): группы, наследование, поля и CSV.</summary>
public class ItemEditorTests
{
    private static LtxSection Sec(string name, params (string Key, string Value)[] entries) => new()
    {
        Name = name,
        Parents = new List<string>(),
        Entries = entries.Select(entry => new LtxEntry { Key = entry.Key, Value = entry.Value }).ToList()
    };

    [Fact]
    public void Group_filters_by_search_and_category_and_sorts_by_field_count()
    {
        const string text = "[wpn_ak74]\nclass = WP_AK74\ncost = 1\n\n" +
                            "[wpn_abakan]\nclass = WP_AK74\n\n" +
                            "[ammo_9x18]\nclass = AMMO\n";
        var sections = LtxParser.Parse(text).Sections;

        var bySearch = ItemEditor.Group(sections, "cost=1", null, null);
        Assert.Single(bySearch);
        Assert.Equal("Оружие", bySearch[0].Category);
        Assert.Equal("wpn_ak74", bySearch[0].Items.Single().Name);

        var weapons = ItemEditor.Group(sections, null, "Оружие", "count");
        Assert.Equal(new[] { "wpn_ak74", "wpn_abakan" }, weapons.Single().Items.Select(section => section.Name));

        var all = ItemEditor.Group(sections, null, ItemEditor.AllCategories, "count");
        Assert.Equal(new[] { "Оружие", "Боеприпасы" }, all.Select(group => group.Category));
        Assert.Equal("ammo_9x18", all[1].Items.Single().Name);

        Assert.Empty(ItemEditor.Group(sections, "no-such-item", null, null));
        Assert.Contains("class=wp_ak74", ItemEditor.SearchText(sections[0]));
    }

    [Fact]
    public void Inheritance_helpers_mark_broken_parents_overrides_and_chain()
    {
        const string text = "[base]\ncost = 100\nhit = 30\n\n" +
                            "[child]:base,ghost\ncost = 200\nhit = 30\n\n" +
                            "[orphan]\ncost = 1\n";
        var sections = LtxParser.Parse(text).Sections;
        var map = ItemEditor.MapByName(sections);
        var child = map["child"];

        Assert.Equal(new[] { ("base", true), ("ghost", false) }, ItemEditor.Parents(child, map));
        Assert.Equal(new[] { "ghost" }, ItemEditor.BrokenParents(child, map));
        Assert.Equal(new[] { "base", "child" }, ItemEditor.InheritanceChain(child, map));
        Assert.True(ItemEditor.IsRoot(map["base"]));
        Assert.True(ItemEditor.IsRoot(map["orphan"]));
        Assert.False(ItemEditor.IsRoot(child));

        var merged = ItemEditor.MergedEntries(child, map);
        var cost = merged.Single(entry => entry.Key == "cost");
        var hit = merged.Single(entry => entry.Key == "hit");
        Assert.Equal("200", cost.Value);
        Assert.True(ItemEditor.IsOverridden(child, cost, map));
        Assert.False(ItemEditor.IsOverridden(child, hit, map));

        var tree = LtxInheritance.InheritanceTree(text);
        Assert.Equal(new[] { "child" }, ItemEditor.Children("base", tree));
        Assert.Empty(ItemEditor.Children("child", tree));
    }

    [Fact]
    public void IconPlacement_uses_inherited_grid_fields_and_requires_both_coordinates()
    {
        const string text = "[base]\ninv_grid_x = 4\ninv_grid_y = 5\n" +
                            "inv_grid_width = 2\ninv_grid_height = 3\n\n" +
                            "[child]:base\ninv_name = child\n\n" +
                            "[override]:base\ninv_grid_width = 1\ninv_grid_height = 2\n\n" +
                            "[bad]\ninv_grid_x = -1\ninv_grid_y = 7\ninv_grid_width = 0\n";
        var sections = LtxParser.Parse(text).Sections;
        var map = ItemEditor.MapByName(sections);

        var child = IconGrid.ResolvePlacement(map["child"], map);
        Assert.True(child.HasPosition);
        Assert.Equal(4, child.X);
        Assert.Equal(5, child.Y);
        Assert.Equal(2, child.Width);
        Assert.Equal(3, child.Height);

        var overridden = IconGrid.ResolvePlacement(map["override"], map);
        Assert.Equal(4, overridden.X);
        Assert.Equal(1, overridden.Width);
        Assert.Equal(2, overridden.Height);

        var bad = IconGrid.ResolvePlacement(map["bad"], map);
        Assert.False(bad.HasPosition);
        Assert.Equal(1, bad.Width);
        Assert.Equal(1, bad.Height);
    }

    [Fact]
    public void ReferencingSections_matches_only_reference_fields_and_skips_self()
    {
        const string text = "[target]\ncost = 1\n\n" +
                            "[sect_immunities]\nimmunities_sect = target\n\n" +
                            "[bones]\nbones_koeff_protection = TARGET\n\n" +
                            "[hit]\nhit_fraction = target \n\n" +
                            "[unrelated]\nfoo = target\n\n" +
                            "[target_self]\nimmunities_sect = target_self\n";
        var sections = LtxParser.Parse(text).Sections;

        Assert.Equal(new[] { "sect_immunities", "bones", "hit" },
            ItemEditor.ReferencingSections(sections, "target").Select(section => section.Name));
        Assert.Empty(ItemEditor.ReferencingSections(sections, ""));
        Assert.Equal(new[] { "sect_immunities", "bones", "hit" },
            ItemEditor.ReferencingSections(sections, "TARGET").Select(section => section.Name));
    }

    [Fact]
    public void SetField_replaces_value_and_keeps_comment()
    {
        const string text = "[wpn_ak74]\ncost = 100 ; base price\n\n[other]\n";
        var cost = LtxParser.Parse(text).Sections[0].Entries.Single(entry => entry.Key == "cost");

        Assert.Equal("[wpn_ak74]\ncost = 250 ; base price\n\n[other]\n",
            ItemEditor.SetField(text, cost.Line, "cost", "250"));
        Assert.Equal("[a]\ncost = 2\n", ItemEditor.SetField("[a]\ncost = 1\n", 2, "cost", "2"));
        Assert.Equal("[a]\ncost = \n", ItemEditor.SetField("[a]\ncost = 1\n", 2, "cost", null));
    }

    [Fact]
    public void ValueSuggestions_are_unique_non_empty_and_limited_to_thirty()
    {
        var sections = new[]
        {
            Sec("a", ("cost", "100"), ("other", "1")),
            Sec("b", ("cost", "100"), ("cost", "200"), ("cost", "")),
            Sec("c", ("cost", "300"))
        };

        Assert.Equal(new[] { "100", "200", "300" }, ItemEditor.ValueSuggestions(sections, "cost"));

        var many = Enumerable.Range(0, 35)
            .Select(index => Sec("s" + index, ("cost", index.ToString())))
            .ToList();
        Assert.Equal(30, ItemEditor.ValueSuggestions(many, "cost").Count);
    }

    [Fact]
    public void BuildCsv_writes_header_and_quotes_separators_and_quotes()
    {
        var sections = new[]
        {
            Sec("wpn_a", ("ammo_class", "ammo_9x18"), ("desc", "a;b"), ("quote", "say \"hi\"")),
            Sec("empty")
        };

        var rows = ItemEditor.BuildCsv(sections).Split('\n');
        Assert.Equal(4, rows.Length);
        Assert.Equal("section;key;value", rows[0]);
        Assert.Equal("wpn_a;ammo_class;ammo_9x18", rows[1]);
        Assert.Equal("wpn_a;desc;\"a;b\"", rows[2]);
        Assert.Equal("wpn_a;quote;\"say \"\"hi\"\"\"", rows[3]);
        Assert.Equal("\"a\\b\"", ItemEditor.CsvCell("a\\b"));
        Assert.Equal("", ItemEditor.CsvCell(null));
    }

    [Fact]
    public void NewSectionName_uses_js_prefix_and_avoids_existing_names()
    {
        var name = ItemEditor.NewSectionName("[new_item_0000]\n");

        Assert.Matches("^new_item_[0-9a-z]{4}$", name);
        Assert.NotEqual("new_item_0000", name);
    }
}
