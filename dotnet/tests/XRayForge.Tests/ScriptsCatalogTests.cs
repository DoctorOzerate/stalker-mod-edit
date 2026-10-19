using XRayForge.Core.Projects;
using XRayForge.Core.Scripts;

namespace XRayForge.Tests;

/// <summary>Вкладка «Скрипты»: фильтр списка, правки, определения, вставка и предпросмотр.</summary>
public class ScriptsCatalogTests
{
    private static ScriptFile Script(string name, string text) =>
        new() { Name = name, Path = "gamedata/scripts/" + name, Text = text, Original = text };

    [Fact]
    public void Filter_matches_name_path_and_text_and_sorts_by_name()
    {
        var bind = Script("bind.script", "function bind_stalker()\nend");
        var task = Script("task_manager.lua", "function task_init()\nend");
        var hidden = Script("zzz.lua", "-- nothing");
        var list = new List<ScriptFile> { task, bind, hidden };

        Assert.Equal(new[] { task, bind, hidden }, ScriptsCatalog.Filter(list, ""));
        Assert.Equal(new[] { task }, ScriptsCatalog.Filter(list, "TASK_MAN"));
        Assert.Equal(new[] { bind }, ScriptsCatalog.Filter(list, "gamedata/scripts/bind"));
        Assert.Equal(new[] { task, bind }, ScriptsCatalog.Filter(list, "function"));
        Assert.Equal(
            new[] { "bind.script", "task_manager.lua", "zzz.lua" },
            ScriptsCatalog.Filter(list, "", sortByName: true).Select(ScriptsCatalog.Name));
        Assert.Empty(ScriptsCatalog.Filter(list, "missing"));
    }

    [Fact]
    public void Changes_and_replace_all_follow_legacy_replaceAll()
    {
        var one = Script("one.lua", "call()\ncall()");
        var two = Script("two.lua", "noop()");
        var scripts = new List<ScriptFile> { one, two };

        Assert.False(ScriptsCatalog.HasChanges(one));
        Assert.Equal(2, ScriptsCatalog.ReplaceAll(scripts, "call()", "run()"));
        Assert.Equal("run()\nrun()", one.Text);
        Assert.True(ScriptsCatalog.HasChanges(one));
        Assert.Equal("noop()", two.Text);

        Assert.Equal(0, ScriptsCatalog.ReplaceAll(scripts, "", "x"));
        Assert.Equal(0, ScriptsCatalog.ReplaceAll(scripts, "absent", "x"));
        Assert.Equal(2, ScriptsCatalog.CountOccurrences("aaaa", "aa"));
    }

    [Fact]
    public void Create_rename_remove_keep_file_names_valid()
    {
        var created = ScriptsCatalog.Create();
        Assert.Equal(ScriptsCatalog.DefaultName, created.Name);
        Assert.Equal(created.Text, created.Original);
        Assert.Equal("custom.lua", ScriptsCatalog.NormalizeFileName("custom"));
        Assert.Equal("custom.script", ScriptsCatalog.NormalizeFileName("custom.script"));
        Assert.Equal("new_script.lua", ScriptsCatalog.NormalizeFileName("  "));

        var scripts = new List<ScriptFile> { created };
        Assert.True(ScriptsCatalog.Rename(created, "renamed"));
        Assert.Equal("renamed.lua", created.Name);
        Assert.False(ScriptsCatalog.Rename(created, "renamed.lua"));
        Assert.True(ScriptsCatalog.Remove(scripts, created));
        Assert.Empty(scripts);
        Assert.False(ScriptsCatalog.Remove(scripts, null));
    }

    [Fact]
    public void Definition_search_prefers_current_file_then_project_order()
    {
        var a = Script("a.lua", "function caller()\n  helper()\nend");
        var b = Script("b.lua", "function helper()\nend");
        var c = Script("c.lua", "local function helper()\nend");
        var scripts = new List<ScriptFile> { a, b, c };

        var found = ScriptsCatalog.FindDefinition(scripts, "helper()", currentIndex: 0);
        Assert.Same(b, found!.File);
        Assert.Equal(0, found.Line);
        Assert.Equal(1, found.Index);

        Assert.Same(c, ScriptsCatalog.FindDefinition(scripts, "helper", currentIndex: 2)!.File);
        Assert.Null(ScriptsCatalog.FindDefinition(scripts, "unknown", currentIndex: 0));
        Assert.Null(ScriptsCatalog.FindDefinition(null, "helper"));
        Assert.Equal("helper", ScriptsCatalog.DeclaredFunctionName("  local function helper(x)"));
    }

    [Fact]
    public void Line_offsets_support_lf_and_crlf()
    {
        const string text = "one\r\ntwo\nthree";
        Assert.Equal(0, ScriptsCatalog.LineOffset(text, 0));
        Assert.Equal(5, ScriptsCatalog.LineOffset(text, 1));
        Assert.Equal(9, ScriptsCatalog.LineOffset(text, 2));
        Assert.Equal(2, ScriptsCatalog.LineAtOffset(text, 9));
        Assert.Equal(3, ScriptsCatalog.LineCount(text));
        Assert.Equal("1\n2\n3", ScriptsCatalog.LineNumbers(text));
        Assert.Equal("1", ScriptsCatalog.LineNumbers(""));
    }

    [Fact]
    public void Insert_at_cursor_replaces_selection_and_keeps_indent()
    {
        var result = ScriptsCatalog.InsertAtCursor("  local x\n", "if ok then\n  go()\nend", 10);
        Assert.Equal("  local x\nif ok then\n  go()\nend", result.Text);
        Assert.Equal("  local x\nif ok then\n  go()\nend".Length, result.Caret);

        var indented = ScriptsCatalog.InsertAtCursor("  ", "a\nb", 2);
        Assert.Equal("  a\n  b", indented.Text);

        var selected = ScriptsCatalog.InsertAtCursor("db.actor:give_item(\"medkit\")", "x", 0, 8);
        Assert.Equal("x:give_item(\"medkit\")", selected.Text);

        var clamped = ScriptsCatalog.InsertAtCursor("abc", "z", 99, 5);
        Assert.Equal("abcz", clamped.Text);
    }

    [Fact]
    public void Preview_collapses_function_blocks()
    {
        var text = "function a()\n  b()\nend\nc = 1\n";
        var full = ScriptsCatalog.BuildPreview(text);
        Assert.Equal(4, full.Count);
        Assert.True(full[0].IsBlockStart);
        Assert.Equal("▾", full[0].Marker);
        Assert.Equal("end", string.Concat(full[2].Tokens.Select(token => token.Text)));

        var collapsed = ScriptsCatalog.BuildPreview(text, new[] { 0 });
        Assert.Equal(3, collapsed.Count);
        Assert.True(collapsed[0].IsBlockStart);
        Assert.Equal("▸", collapsed[0].Marker);
        Assert.True(collapsed[1].IsSummary);
        Assert.Equal(2, collapsed[1].CollapsedCount);
        Assert.Equal("c = 1", string.Concat(collapsed[2].Tokens.Select(token => token.Text)));
    }

    [Fact]
    public void Format_check_graph_and_brackets_work_for_empty_script()
    {
        Assert.Equal("function f()\n  x = 1\nend", ScriptsCatalog.Format("function f()\nx = 1\nend"));
        Assert.Empty(ScriptsCatalog.Check("function f()\nend"));
        Assert.NotEmpty(ScriptsCatalog.Check("function f()"));
        Assert.Contains("a", ScriptsCatalog.CallGraph("function a()\n b()\nend\nfunction b()\nend").Functions);
        Assert.Contains("unused", ScriptsCatalog.Unused("function unused()\nend"));
        Assert.Equal(1, ScriptsCatalog.Brackets("f()").Open);
        Assert.Equal(1, ScriptsCatalog.Brackets("f()").Close);

        Assert.Equal("", ScriptsCatalog.Format(""));
        Assert.Empty(ScriptsCatalog.Check(""));
        Assert.Empty(ScriptsCatalog.BuildPreview(""));
        Assert.Null(ScriptsCatalog.FindDefinition(new List<ScriptFile>(), "x", 0));
    }
}
