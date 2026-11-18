using XRayForge.Core.Scripts;

namespace XRayForge.Tests;

/// <summary>
/// Ссылки на файлы в тексте скрипта (`ScriptLinks`): разбор директив, поиск токенов
/// с расширением и попадание курсора в ссылку для Ctrl+ЛКМ.
/// </summary>
public class ScriptLinksTests
{
    private const string Source =
        "#include \"scripts/helper.lua\"\n" +
        "dofile('marsh/task.lua')\n" +
        "require \"config\"\n" +
        "-- см. zaton.script\n";

    [Fact]
    public void Scan_finds_include_dofile_and_require()
    {
        var links = ScriptLinks.Scan(Source);

        Assert.Equal(
            new[] { "scripts/helper.lua", "marsh/task.lua", "config", "zaton.script" },
            links.Select(link => link.Target));
        Assert.Equal(new[] { 0, 1, 2, 3 }, links.Select(link => link.Line));

        // Три директивы + токен из комментария.
        Assert.Equal(new[] { true, true, true, false }, links.Select(link => link.IsDirective));
        Assert.All(links, link => Assert.Equal(link.Start + link.Length, link.End));
    }

    /// <summary>Токен файла внутри `#include`/`dofile` не дублируется отдельной ссылкой.</summary>
    [Fact]
    public void Scan_does_not_duplicate_tokens_inside_directives()
    {
        var text = "-- zaton.script и ui_main.xml\n#include \"zaton.script\"\n";
        var links = ScriptLinks.Scan(text);

        Assert.Equal(3, links.Count);
        Assert.Equal(
            new[] { "zaton.script", "ui_main.xml", "zaton.script" },
            links.Select(link => link.Target));
        Assert.True(links[2].IsDirective);
    }

    [Fact]
    public void At_returns_link_under_offset_only()
    {
        var offset = Source.IndexOf("marsh", StringComparison.Ordinal);
        var link = ScriptLinks.At(Source, offset);

        Assert.NotNull(link);
        Assert.Equal("marsh/task.lua", link!.Target);
        Assert.True(link.IsDirective);
        // Курсор в конце ссылки (сразу за кавычкой) ещё считается попаданием.
        Assert.Equal(link.Target, ScriptLinks.At(Source, link.End)!.Target);

        // Первая директива тоже попадает под курсор (`#include` начинается с 0).
        Assert.Equal("scripts/helper.lua", ScriptLinks.At(Source, 2)!.Target);
        // Между ссылками — пусто (`см.` в комментарии ссылкой не считается).
        Assert.Null(ScriptLinks.At(Source, Source.IndexOf("см.", StringComparison.Ordinal)));
        Assert.Null(ScriptLinks.At("local x = 1", 4));
        Assert.Null(ScriptLinks.At("", 0));
        Assert.Null(ScriptLinks.At(null, 0));
    }

    /// <summary>`require "tasks/bar"` — путь без расширения: тип файла определит вызывающий код.</summary>
    [Fact]
    public void Scan_keeps_extensionless_require_targets()
    {
        var link = Assert.Single(ScriptLinks.Scan("require \"tasks/bar\""));

        Assert.Equal("tasks/bar", link.Target);
        Assert.Equal("", Path.GetExtension(link.Target));
    }

    /// <summary>`level.actor()` — ссылка на функцию `actor` в скрипте `level`.</summary>
    [Fact]
    public void Scan_finds_script_function_calls()
    {
        var links = ScriptLinks.Scan("level.actor()\nxr_effects.set_sound_volume(obj, 0.5)");

        Assert.Equal(new[] { "level.actor", "xr_effects.set_sound_volume" }, links.Select(link => link.Target));
        Assert.All(links, link => Assert.True(link.IsCall));
        Assert.Equal("level", links[0].Script);
        Assert.Equal("actor", links[0].Member);
        Assert.Equal(0, links[0].Start);
        Assert.Equal(1, links[1].Line);
        Assert.False(links[0].IsDirective);
    }

    /// <summary>Стандартные библиотеки, `self`/`this` и локальные объекты — не ссылки на скрипты.</summary>
    [Fact]
    public void Scan_skips_library_and_local_receivers()
    {
        var text =
            "math.floor(1.5)\n" +
            "self.on_update()\n" +
            "obj:give_item(\"wpn_ak74\")\n" +
            "db.actor:has_info(\"quest\")\n";
        Assert.Empty(ScriptLinks.Scan(text));
    }

    /// <summary>Клик по `level.actor()` попадает в ссылку; скобка уже не ссылка.</summary>
    [Fact]
    public void At_returns_call_link_under_offset()
    {
        const string text = "local a = level.actor()";
        var link = ScriptLinks.At(text, text.IndexOf("actor", StringComparison.Ordinal));

        Assert.NotNull(link);
        Assert.True(link!.IsCall);
        Assert.Equal("level.actor", link.Target);
        Assert.Null(ScriptLinks.At(text, text.IndexOf('(') + 1));
    }

    [Fact]
    public void Scan_handles_empty_text()
    {
        Assert.Empty(ScriptLinks.Scan(""));
        Assert.Empty(ScriptLinks.Scan(null));
    }
}
