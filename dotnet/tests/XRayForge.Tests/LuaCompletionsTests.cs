using XRayForge.Core.Lua;
using XRayForge.Core.Suggestions;

namespace XRayForge.Tests;

/// <summary>
/// Автодополнение Lua-редактора (`LuaCompletions`): определение вводимого токена, отсев
/// комментариев и подбор подсказок по части имени (`give_info`, `level.actor`, info-порции).
/// </summary>
public class LuaCompletionsTests
{
    private static readonly SuggestionEntry[] Source =
    {
        new("db.actor:give_info_portion", SuggestionKind.LuaApi, "Lua API"),
        new("db.actor:give_item", SuggestionKind.LuaApi, "Lua API"),
        new("level.actor", SuggestionKind.LuaApi, "Lua API"),
        new("has_info", SuggestionKind.ScriptFunction, "bind_stalker.script"),
        new("quest_done", SuggestionKind.InfoPortion, "quest")
    };

    [Fact]
    public void PrefixAt_takes_api_token_before_caret()
    {
        const string text = "db.actor:give_info_portion(\"x\")";
        var caret = "db.actor:give_info_portion".Length;
        var (start, prefix) = LuaCompletions.PrefixAt(text, caret);
        Assert.Equal(0, start);
        Assert.Equal("db.actor:give_info_portion", prefix);

        var inner = LuaCompletions.PrefixAt(text, "db.actor:give_info".Length);
        Assert.Equal(0, inner.Start);
        Assert.Equal("db.actor:give_info", inner.Prefix);

        // Каретка за скобкой — токена перед ней нет.
        Assert.Equal("", LuaCompletions.PrefixAt(text, text.Length).Prefix);

        Assert.Equal(0, LuaCompletions.PrefixAt("", 0).Start);
        Assert.Equal("", LuaCompletions.PrefixAt("", 0).Prefix);
        Assert.Equal("", LuaCompletions.PrefixAt(null, 0).Prefix);

        // Знак info-порции (`+`/`-`) в токен не входит — подсказка встанет сразу после него.
        Assert.Equal("quest_done", LuaCompletions.PrefixAt("+quest_done", 11).Prefix);
        Assert.Equal("quest_done", LuaCompletions.PrefixAt("-quest_done", 11).Prefix);
    }

    [Fact]
    public void TokenEnd_covers_rest_of_token()
    {
        const string text = "db.actor:give_item";
        Assert.Equal(text.Length, LuaCompletions.TokenEnd(text, "db.actor".Length));
        Assert.Equal(0, LuaCompletions.TokenEnd("", 0));
        Assert.Equal(text.Length, LuaCompletions.TokenEnd(text, text.Length));
    }

    [Fact]
    public void IsComment_detects_line_comments_only()
    {
        Assert.True(LuaCompletions.IsComment("local x = 1 -- note", 19));
        Assert.False(LuaCompletions.IsComment("local x = 1", 11));
        Assert.False(LuaCompletions.IsComment("x = \"--info\"", 12));
        Assert.False(LuaCompletions.IsComment("", 0));
    }

    [Fact]
    public void Suggest_matches_api_by_partial_name()
    {
        const string text = "db.actor:give_info";
        var matches = LuaCompletions.Suggest(text, text.Length, Source);

        Assert.NotNull(matches);
        Assert.Contains(matches!, entry => entry.Value == "db.actor:give_info_portion");
        Assert.DoesNotContain(matches!, entry => entry.Value == "db.actor:give_item");
    }

    [Fact]
    public void Suggest_matches_script_binding_and_info_portions()
    {
        Assert.Contains(LuaCompletions.Suggest("level.a", "level.a".Length, Source)!,
            entry => entry.Value == "level.actor");

        // `+quest_done` — знак перед токеном не мешает подсказке info-порции.
        Assert.Contains(LuaCompletions.Suggest("+quest_d", 9, Source)!,
            entry => entry.Value == "quest_done");
    }

    [Fact]
    public void Suggest_ranks_tail_match_first()
    {
        var source = new[]
        {
            new SuggestionEntry("db.actor:alive", SuggestionKind.LuaApi),
            new SuggestionEntry("db.actor:give_info_portion", SuggestionKind.LuaApi),
            new SuggestionEntry("db.actor:give_item", SuggestionKind.LuaApi)
        };

        // Ввод хвоста `give_info` поднимает нужный метод выше остальных методов `db.actor`.
        var matches = LuaCompletions.Suggest("db.actor:give_info", "db.actor:give_info".Length, source);
        Assert.NotNull(matches);
        Assert.Equal("db.actor:give_info_portion", matches![0].Value);
    }

    [Fact]
    public void Suggest_returns_null_for_short_prefix_comments_and_no_matches()
    {
        Assert.Null(LuaCompletions.Suggest("a", 1, Source));
        Assert.Null(LuaCompletions.Suggest(null, 0, Source));
        Assert.Null(LuaCompletions.Suggest("-- give_info", "-- give_info".Length, Source));
        Assert.Null(LuaCompletions.Suggest("zzzzzz", 6, Source));
        Assert.Null(LuaCompletions.Suggest("give_info", "give_info".Length, null));
    }
}
