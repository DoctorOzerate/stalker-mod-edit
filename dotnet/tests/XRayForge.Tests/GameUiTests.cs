using XRayForge.Core.GameUi;

namespace XRayForge.Tests;

/// <summary>Фундамент вкладки UI-окон: разбор игровых XML, адресация и каталог тегов.</summary>
public class GameUiTests
{
    private const string SocSample = """
        <?xml version="1.0" encoding="utf-8"?>
        <window>
            <background x="0" y="0" width="1024" height="768">
                <texture>ui_menu_backgraund</texture>
            </background>
            <menu_main btn_height="30" font="graffiti22">
                <btn name="btn_new_game" caption="ui_mm_newgame" />
                <btn name="btn_quit" caption="ui_mm_quit2windows" />
            </menu_main>
        </window>
        """;

    private const string CoPSample = """
        <w>
            <main x="0" y="0" width="1024" height="768"/>
            <quick_info x="1" y="670" width="1022" height="50" complex_mode="1" light_anim="ui_btn_hint">
                <text align="c" font="graffiti22" r="225" g="225" b="250" a="255" />
            </quick_info>
            <icons_scroll_view x="900" y="350" width="65" height="270" always_show_scroll="0" />
        </w>
        """;

    [Fact]
    public void Parses_soc_style_window_with_nested_textures_and_buttons()
    {
        var document = GameUiParser.Parse(SocSample, "config/ui/ui_mm_main.xml");

        var root = Assert.Single(document.Roots);
        Assert.Equal("window", root.Tag);
        Assert.Empty(document.Diagnostics);
        Assert.Equal(2, root.Children.Count);

        var background = root.Children[0];
        Assert.Equal("background", background.Tag);
        Assert.Equal(new GameUiRect(0, 0, 1024, 768), background.Rect);
        Assert.Equal("ui_menu_backgraund", background.Children[0].InnerTextTrimmed);

        var menu = root.Children[1];
        Assert.Equal("menu_main", menu.Tag);
        Assert.Equal("30", menu.Get("btn_height"));
        Assert.Equal("graffiti22", menu.Get("font"));
        Assert.Equal(2, menu.Children.Count);

        var button = menu.Children[0];
        Assert.Equal("btn", button.Tag);
        Assert.Equal("btn_new_game", button.Name);
        Assert.Equal("ui_mm_newgame", button.Caption);
        Assert.Equal(GameUiElementKind.Button, button.Kind);
        Assert.True(button.IsSelfClosing);
    }

    [Fact]
    public void Parses_cop_style_root_and_flags()
    {
        var document = GameUiParser.Parse(CoPSample, "configs/ui/maingame.xml");

        var root = Assert.Single(document.Roots);
        Assert.Equal("w", root.Tag);
        Assert.Equal(3, root.Children.Count);

        var scroll = root.Children[2];
        Assert.Equal(GameUiElementKind.ScrollView, scroll.Kind);
        Assert.False(scroll.Flag("always_show_scroll"));
        Assert.Equal(900, scroll.Int("x"));
        Assert.Equal(270, scroll.Int("height"));

        var text = root.Children[1].Children[0];
        Assert.Equal(GameUiElementKind.Text, text.Kind);
        Assert.Equal("c", text.Get("align"));
        Assert.Equal(255, text.Int("a"));
    }

    [Fact]
    public void Skips_comments_includes_and_processing_instructions()
    {
        const string sample = """
            #include "ui/color_defs.xml"
            <?xml version="1.0" encoding="windows-1251"?>
            <!--- старый комментарий с двойным дефисом
                 и переносом --->
            <window name="w">
                <static x="0" y="0" width="10" height="10" />
            </window>
            """;

        var document = GameUiParser.Parse(sample, "config/ui/test.xml");

        var root = Assert.Single(document.Roots);
        Assert.Equal("w", root.Name);
        Assert.Single(root.Children);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void Reports_unclosed_tags_but_keeps_the_tree()
    {
        const string sample = """
            <window name="w">
                <static name="orphan">
                <button name="b" />
            </window>
            """;

        var document = GameUiParser.Parse(sample, "config/ui/broken.xml");

        var root = Assert.Single(document.Roots);
        Assert.Equal("static", root.Children[0].Tag);
        Assert.Contains(document.Diagnostics, d => d.Code == GameUiParser.UnclosedTag);
    }

    [Fact]
    public void Reports_unclosed_comment_and_quote()
    {
        var commentDocument = GameUiParser.Parse("<window><!-- не закрыт\n</window>");
        Assert.Contains(commentDocument.Diagnostics, d => d.Code == GameUiParser.UnclosedComment);

        var quoteDocument = GameUiParser.Parse("<window name=\"w\"><static hint=\"ui_hint /></window>");
        Assert.Contains(quoteDocument.Diagnostics, d => d.Code == GameUiParser.UnclosedQuote);
    }

    [Fact]
    public void Addresses_elements_by_path_and_finds_by_name()
    {
        var document = GameUiParser.Parse(SocSample, "config/ui/ui_mm_main.xml");
        var root = document.Root!;

        Assert.Equal("0", root.Path);
        Assert.Equal("0.1.1", root.Children[1].Children[1].Path);

        var menu = document.Find("0.1");
        Assert.NotNull(menu);
        Assert.Equal("menu_main", menu!.Tag);

        Assert.Equal("btn_quit", document.Find("0.1.1")!.Name);
        Assert.Null(document.Find("0.9"));
        Assert.Null(document.Find("bad"));
        Assert.Single(document.FindAll("quit"));
        Assert.Equal("background", document.FindTag("background")!.Tag);
    }

    [Fact]
    public void Canvas_uses_root_size_or_children_bounds()
    {
        Assert.Equal((1024d, 768d), GameUiParser.Parse(SocSample).Canvas);

        const string noSize = "<window><static x=\"0\" y=\"0\" width=\"800\" height=\"600\" /></window>";
        Assert.Equal((800d, 600d), GameUiParser.Parse(noSize).Canvas);

        Assert.Equal((GameUiDocument.DefaultWidth, GameUiDocument.DefaultHeight), GameUiParser.Parse("").Canvas);
    }

    [Fact]
    public void Catalog_classifies_tags_and_attribute_kinds()
    {
        Assert.Equal(GameUiElementKind.Window, GameUiElementCatalog.Classify("window"));
        Assert.Equal(GameUiElementKind.Window, GameUiElementCatalog.Classify("shniaga_wnd"));
        Assert.Equal(GameUiElementKind.Static, GameUiElementCatalog.Classify("indicator_health"));
        Assert.Equal(GameUiElementKind.Button, GameUiElementCatalog.Classify("btn"));
        Assert.Equal(GameUiElementKind.Container, GameUiElementCatalog.Classify("menu_main"));
        Assert.Equal(GameUiElementKind.ScrollView, GameUiElementCatalog.Classify("icons_scroll_view"));
        Assert.Equal(GameUiElementKind.Unknown, GameUiElementCatalog.Classify("custom_mod_thing"));

        Assert.Equal(GameUiValueKind.Number, GameUiElementCatalog.KindOf("width"));
        Assert.Equal(GameUiValueKind.Flag, GameUiElementCatalog.KindOf("stretch"));
        Assert.Equal(GameUiValueKind.Alignment, GameUiElementCatalog.KindOf("vert_alignment"));
        Assert.Equal(GameUiValueKind.Text, GameUiElementCatalog.KindOf("unknown_attr"));
        Assert.True(GameUiElementCatalog.IsGeometry("height"));
        Assert.False(GameUiElementCatalog.IsGeometry("font"));
        Assert.Equal("c", GameUiElementCatalog.NormalizeAlignment("center"));
        Assert.Equal("r", GameUiElementCatalog.NormalizeAlignment("RIGHT"));
        Assert.Equal("", GameUiElementCatalog.NormalizeAlignment("middle"));
    }

    [Fact]
    public void Computes_absolute_rects_through_nesting()
    {
        const string sample = """
            <window name="w" x="10" y="20" width="1000" height="700">
                <menu name="m" x="5" y="5" width="100" height="100">
                    <static name="s" x="1" y="2" width="10" height="20" />
                </menu>
            </window>
            """;

        var document = GameUiParser.Parse(sample);
        var placements = GameUiLayout.Flatten(document);

        Assert.Equal(3, placements.Count);
        var inner = GameUiLayout.Find(placements, "0.0.0")!;
        Assert.Equal(new GameUiRect(1, 2, 10, 20), inner.Local);
        Assert.Equal(new GameUiRect(16, 27, 10, 20), inner.Absolute);
        Assert.Equal(2, inner.Depth);

        var hit = GameUiLayout.HitTest(placements, 18, 30);
        Assert.Equal("s", hit!.DisplayName);

        Assert.Equal(new[] { "s" }, GameUiLayout.ChildrenOf(placements, GameUiLayout.Find(placements, "0.0")!)
            .Select(p => p.DisplayName));
    }

    [Fact]
    public void Clamp_fits_rect_into_canvas()
    {
        Assert.Equal(
            new GameUiRect(0, 568, 200, 200),
            GameUiLayout.Clamp(new GameUiRect(-10, 700, 200, 200), 1024, 768));

        Assert.Equal(
            new GameUiRect(0, 0, 1024, 768),
            GameUiLayout.Clamp(new GameUiRect(5, 5, 5000, 5000), 1024, 768));
    }

    [Fact]
    public void Parses_attribute_values_with_entity_codes()
    {
        var document = GameUiParser.Parse(
            "<window name=\"w\"><static hint=\"ui_a&amp;b &quot;x&quot;\" /></window>");

        Assert.Equal("ui_a&b \"x\"", document.Find("0.0")!.Get("hint"));
    }

    [Fact]
    public void Set_attribute_replaces_only_the_value()
    {
        var document = GameUiParser.Parse(
            "<window name=\"w\">\n\t<static name=\"s\" x=\"10\" y=\"20\" width=\"30\" height=\"40\" />\n</window>");
        var original = document.Text;
        var element = document.Find("0.0")!;

        Assert.True(GameUiEditor.SetAttribute(document, element, "x", "15"));
        Assert.Equal(original.Replace("x=\"10\"", "x=\"15\""), document.Text);
        Assert.Equal(15, document.Find("0.0")!.Int("x"));

        Assert.False(GameUiEditor.SetAttribute(document, document.Find("0.0")!, "x", "15"));
    }

    [Fact]
    public void Set_attribute_adds_missing_attribute_before_tag_end()
    {
        var document = GameUiParser.Parse("<window name=\"w\">\n\t<static name=\"s\" />\n</window>");
        var element = document.Find("0.0")!;

        Assert.True(GameUiEditor.SetAttribute(document, element, "hint", "ui_hint"));
        Assert.Equal("ui_hint", document.Find("0.0")!.Get("hint"));
        Assert.Contains("<static name=\"s\" hint=\"ui_hint\" />", document.Text);
    }

    [Fact]
    public void Remove_attribute_removes_it_cleanly()
    {
        var document = GameUiParser.Parse("<static name=\"s\" x=\"10\" y=\"20\" hint=\"h\" />");
        var element = document.Root!;

        Assert.True(GameUiEditor.RemoveAttribute(document, element, "y"));
        Assert.Equal("<static name=\"s\" x=\"10\" hint=\"h\" />", document.Text);
        Assert.Null(document.Find("0")!.Attribute("y"));
    }

    [Fact]
    public void Set_inner_text_escapes_markup()
    {
        var document = GameUiParser.Parse("<window><texture>ui_old</texture></window>");
        Assert.True(GameUiEditor.SetInnerText(document, document.Find("0.0")!, "ui_new"));
        Assert.Equal("ui_new", document.Find("0.0")!.InnerTextTrimmed);

        var textDocument = GameUiParser.Parse("<window><text>x</text></window>");
        Assert.True(GameUiEditor.SetInnerText(textDocument, textDocument.Find("0.0")!, "a & b <c>"));
        Assert.Contains("a &amp; b &lt;c&gt;", textDocument.Text);
        Assert.Equal("a & b <c>", textDocument.Find("0.0")!.InnerTextTrimmed);
    }

    [Fact]
    public void Rename_tag_updates_open_and_close_tags()
    {
        var document = GameUiParser.Parse(
            "<window>\n\t<static name=\"s\" />\n\t<menu name=\"m\">\n\t</menu>\n</window>");

        Assert.True(GameUiEditor.RenameTag(document, document.Find("0.1")!, "menu_main"));
        Assert.Equal("menu_main", document.Find("0.1")!.Tag);
        Assert.Contains("</menu_main>", document.Text);
    }

    [Fact]
    public void Add_child_inserts_with_file_indent_and_returns_element()
    {
        var document = GameUiParser.Parse("<window name=\"w\">\n\t<static name=\"s\" />\n</window>");
        var added = GameUiEditor.AddChild(document, document.Root!, "<button name=\"b\" />");

        Assert.NotNull(added);
        Assert.Equal("0.1", added!.Path);
        Assert.Equal("b", added.Name);
        Assert.Equal(2, document.Root!.Children.Count);
        Assert.Contains("\t<button name=\"b\" />\n", document.Text);
    }

    [Fact]
    public void Add_child_rejects_leaf_elements()
    {
        var document = GameUiParser.Parse("<window>\n\t<static name=\"s\" />\n</window>");
        Assert.Null(GameUiEditor.AddChild(document, document.Find("0.0")!, "<text>id</text>"));
    }

    [Fact]
    public void Remove_element_drops_the_whole_line()
    {
        var document = GameUiParser.Parse(
            "<window name=\"w\">\n\t<static name=\"a\" />\n\t<button name=\"b\" />\n</window>");

        Assert.True(GameUiEditor.RemoveElement(document, document.Find("0.0")!));
        Assert.DoesNotContain("static", document.Text);
        Assert.Single(document.Root!.Children);
        Assert.Equal("button", document.Root!.Children[0].Tag);
    }

    [Fact]
    public void Move_element_between_containers()
    {
        const string sample = """
            <window>
                <menu name="m1">
                    <button name="b" />
                </menu>
                <menu name="m2">
                    <static name="s" />
                </menu>
            </window>
            """;

        var document = GameUiParser.Parse(sample);
        Assert.True(GameUiEditor.MoveElement(document, document.Find("0.0.0")!, document.Find("0.1")!));

        Assert.Null(document.Find("0.0.0"));
        Assert.Empty(document.Find("0.0")!.Children);
        Assert.Equal("b", document.Find("0.1.1")!.Name);
    }

    [Fact]
    public void Move_and_resize_by_update_geometry()
    {
        var document = GameUiParser.Parse(
            "<window><static name=\"s\" x=\"10\" y=\"20\" width=\"30\" height=\"40\" /></window>");

        Assert.True(GameUiEditor.MoveBy(document, document.Find("0.0")!, 5, -5));
        Assert.Equal(15, document.Find("0.0")!.Int("x"));
        Assert.Equal(15, document.Find("0.0")!.Int("y"));

        Assert.True(GameUiEditor.ResizeBy(document, document.Find("0.0")!, -100, 10));
        Assert.Equal(0, document.Find("0.0")!.Int("width"));
        Assert.Equal(50, document.Find("0.0")!.Int("height"));
    }

    [Fact]
    public void Serialize_round_trips_structure()
    {
        var document = GameUiParser.Parse(SocSample);
        var reparsed = GameUiParser.Parse(GameUiEditor.Serialize(document));

        Assert.Equal(document.Roots.Count, reparsed.Roots.Count);
        Assert.Equal(document.Root!.Children.Count, reparsed.Root!.Children.Count);
        Assert.Equal("ui_menu_backgraund", reparsed.Find("0.0.0")!.InnerTextTrimmed);
        Assert.Equal(2, reparsed.Find("0.1")!.Children.Count);
        Assert.Empty(reparsed.Diagnostics);
    }

    [Fact]
    public void Templates_create_parseable_window_and_elements()
    {
        var document = GameUiTemplates.CreateDocument("config/ui/new.xml", "my_window");

        Assert.Equal("window", document.Root!.Tag);
        Assert.Equal("my_window", document.Root!.Name);
        Assert.Equal((1024d, 768d), document.Canvas);
        Assert.Empty(document.Diagnostics);

        var button = GameUiParser.Parse(GameUiTemplates.Element("button", "ok", 10, 20, 100, 30)).Root!;
        Assert.Equal("button", button.Tag);
        Assert.Equal("ok", button.Name);
        Assert.Equal(new GameUiRect(10, 20, 100, 30), button.Rect);

        var text = GameUiParser.Parse(GameUiTemplates.Element("text", "t", 0, 0, 50, 20)).Root!;
        Assert.Equal("letterica16", text.Get("font"));
        Assert.Equal("l", text.Get("alignment"));
    }

    [Fact]
    public void Palette_entries_and_next_name_stay_unique()
    {
        var document = GameUiTemplates.CreateDocument();
        Assert.Equal("new_static", GameUiTemplates.NextName(document, "static"));
        Assert.Contains(GameUiTemplates.Palette, template => template.Id == "button");

        var added = GameUiEditor.AddChild(document, document.Root!, GameUiTemplates.FromPalette("static"));
        Assert.NotNull(added);
        Assert.Equal("new_static", added!.Name);
        Assert.Equal("new_static_2", GameUiTemplates.NextName(document, "static"));
    }


}
