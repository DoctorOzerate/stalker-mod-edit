using XRayForge.Core.Lua;

namespace XRayForge.Tests;

public class LuaTests
{
    [Fact]
    public void LuaCheck_detects_unclosed_block_and_extra_end()
    {
        Assert.Single(LuaAnalyzer.Check("function f()\n"));
        Assert.Single(LuaAnalyzer.Check("end"));
        Assert.Empty(LuaAnalyzer.Check("function f()\nend"));
    }

    [Fact]
    public void LuaUndeclaredGlobals_flags_global_assignment_but_not_local()
    {
        var issues = LuaAnalyzer.UndeclaredGlobals("local x = 1\nx = 2\ny = 3\n");
        Assert.Contains(issues, i => i.Message!.Contains('y'));
        Assert.DoesNotContain(issues, i => i.Message!.Contains('x'));
    }

    [Fact]
    public void Stalker_lua_snippets_and_api_present()
    {
        Assert.NotEmpty(LuaCatalog.Snippets);
        Assert.Contains("db.actor:give_item", LuaCatalog.Api);
    }

    [Fact]
    public void LuaCompletions_filters_by_prefix()
    {
        Assert.NotEmpty(LuaAnalyzer.Completions("db.actor"));
        Assert.True(LuaAnalyzer.Completions("").Count > 10);
        Assert.Empty(LuaAnalyzer.Completions("zzz_nothing"));
    }

    [Fact]
    public void LuaCallGraph_builds_call_edges()
    {
        var g = LuaAnalyzer.BuildCallGraph("function a()\n  b()\nend\nfunction b()\nend\n");
        Assert.Contains("a", g.Functions);
        Assert.Contains(g.Edges, e => e.From == "a" && e.To == "b");
    }

    [Fact]
    public void LuaUnused_finds_uncalled_functions()
    {
        Assert.Equal(new[] { "b" }, LuaAnalyzer.Unused("function a()\nend\nfunction b()\n  a()\nend\n"));
    }

    [Fact]
    public void ValidateScriptBindings_finds_missing_bindings()
    {
        const string ltx = "[actor]\nscript_binding = bind_stalker.actor_init\n[monster]\nscript_binding = bind_monster.bind\n";
        var scripts = new[] { new LuaScriptFile { Name = "bind_monster.script", Text = "function bind() end" } };

        var issues = LuaAnalyzer.ValidateScriptBindings(ltx, scripts);
        Assert.Contains(issues, i => i.Binding == "bind_stalker.actor_init");
        Assert.DoesNotContain(issues, i => i.Binding == "bind_monster.bind");
    }

    [Fact]
    public void CountLuaFunctions_and_luaDuplicateFunctions()
    {
        const string code = "function a() end\nfunction b() end\nfunction a() end\n";
        Assert.Equal(3, LuaAnalyzer.CountFunctions(code));

        var dups = LuaAnalyzer.DuplicateFunctions(code);
        Assert.Single(dups);
        Assert.Equal("a", dups[0].Name);
        Assert.Equal(2, dups[0].Count);
    }

    [Fact]
    public void Tokenize_marks_comments_strings_keywords_and_brackets()
    {
        var tokens = LuaTokenizer.Tokenize("-- c\nlocal s = \"a\\\"b\"\nfunction f() end\n");
        Assert.Contains(tokens, t => t.Cls == LuaTokenClasses.Comment && t.Text == "-- c");
        Assert.Contains(tokens, t => t.Cls == LuaTokenClasses.StringLiteral && t.Text == "\"a\\\"b\"");
        Assert.Contains(tokens, t => t.Cls == LuaTokenClasses.Keyword && t.Text == "function");
        Assert.Contains(tokens, t => t.Cls == LuaTokenClasses.Bracket && t.Text == "(");

        var count = LuaTokenizer.CountBrackets("f([{}])");
        Assert.Equal(3, count.Open);
        Assert.Equal(3, count.Close);
    }

    [Fact]
    public void TokenizeLines_splits_multiline_tokens()
    {
        var lines = LuaTokenizer.TokenizeLines("--[[a\nb]]\nlocal x = 1");
        Assert.Equal(3, lines.Count);
        Assert.Equal("--[[a", lines[0][0].Text);
        Assert.Equal("b]]", lines[1][0].Text);
        Assert.Equal(LuaTokenClasses.Comment, lines[1][0].Cls);
        Assert.Equal("local", lines[2][0].Text);
    }

    [Fact]
    public void LongBracketEnd_matches_nested_equal_signs()
    {
        const string src = "[==[body]==]";
        Assert.Equal(src.Length, LuaTokenizer.LongBracketEnd(src, 0));
        Assert.Equal(-1, LuaTokenizer.LongBracketEnd(src, 1));
    }

    [Fact]
    public void Format_indents_blocks()
    {
        Assert.Equal("function f()\n  x = 1\nend", LuaFormatter.Format("function f()\nx = 1\nend"));
    }

    [Fact]
    public void FunctionBlocks_returns_line_ranges()
    {
        var lines = new[] { "function a()", "  local t = 1", "end", "local function b()", "end" };
        var blocks = LuaFormatter.FunctionBlocks(lines);
        Assert.Equal(2, blocks.Count);
        Assert.Equal(0, blocks[0].Start);
        Assert.Equal(2, blocks[0].End);
        Assert.Equal(3, blocks[1].Start);
        Assert.Equal(4, blocks[1].End);
    }
}