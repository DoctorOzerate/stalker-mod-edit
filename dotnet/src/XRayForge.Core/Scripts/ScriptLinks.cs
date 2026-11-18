using System.Text.RegularExpressions;

namespace XRayForge.Core.Scripts;

/// <summary>
/// Ссылка на файл внутри текста скрипта (`#include "x.lua"`, `dofile("x.lua")`,
/// `require "x"`, просто `path/to/x.lua`). Нужна для перехода Ctrl+ЛКМ.
/// </summary>
public sealed class ScriptLink
{
    /// <summary>Путь или имя файла из ссылки (без кавычек и директивы).</summary>
    public string Target { get; set; } = "";

    /// <summary>Смещение начала ссылки в тексте (0-based).</summary>
    public int Start { get; set; }

    /// <summary>Длина ссылки в символах.</summary>
    public int Length { get; set; }

    /// <summary>Строка, в которой найдена ссылка (0-based).</summary>
    public int Line { get; set; }

    /// <summary>Ссылка взята из `#include`/`dofile`/`require` — тип файла известен заранее.</summary>
    public bool IsDirective { get; set; }

    /// <summary>
    /// Ссылка на функцию скрипта: `level.actor()` / `xr_effects.set_sound_volume(...)`.
    /// Первая часть — имя скрипта (файла), вторая — функция внутри него.
    /// </summary>
    public bool IsCall { get; set; }

    /// <summary>Имя скрипта из вызова (`level` в `level.actor()`); пусто для файловых ссылок.</summary>
    public string Script { get; set; } = "";

    /// <summary>Имя функции из вызова (`actor` в `level.actor()`); пусто для файловых ссылок.</summary>
    public string Member { get; set; } = "";

    /// <summary>Конец ссылки (не включается).</summary>
    public int End => Start + Length;
}

/// <summary>
/// Поиск ссылок на файлы в Lua/script-тексте. Сначала разбираются явные директивы
/// (`#include "x.lua"`, `dofile('x.lua')`, `require "x"`), затем — любые токены с расширением
/// `.lua`/`.script`/`.ltx`/`.xml`, чтобы работали ссылки в комментариях.
/// </summary>
public static class ScriptLinks
{
    /// <summary>Директива с файлом в кавычках; группы 1/2 — двойные/одинарные кавычки.</summary>
    private static readonly Regex Directive = new(
        @"(?:#include\s*|(?:\bdofile|\bloadfile|\brequire)\s*\(?\s*)(?:""([^""\r\n]+)""|'([^'\r\n]+)')",
        RegexOptions.Compiled);

    /// <summary>Токен с известным расширением — ссылка без директивы (часто в комментарии).</summary>
    private static readonly Regex FileToken = new(
        @"[A-Za-z0-9_\-./\\]+\.(?:lua|script|ltx|xml)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Вызов функции скрипта `скрипт.функция(` / `скрипт:функция(`. Скобка в совпадение
    /// не входит — клик по самой скобке остаётся обычным кликом.
    /// </summary>
    private static readonly Regex CallToken = new(
        @"\b([A-Za-z_][A-Za-z0-9_]*)\s*[.:]\s*([A-Za-z_][A-Za-z0-9_]*)\s*(?=\()",
        RegexOptions.Compiled);

    /// <summary>Расширения, которые не могут быть именем функции (`x.lua(` — это файл).</summary>
    private static readonly HashSet<string> ScriptExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "lua", "script", "ltx", "xml" };

    /// <summary>
    /// Получатели, которые не являются скриптами проекта: `this`/`self`, стандартные
    /// библиотеки Lua и локальные переменные-объекты (`actor:give_item(...)`).
    /// </summary>
    private static readonly HashSet<string> NonScriptReceivers = new(StringComparer.Ordinal)
    {
        "this", "self", "math", "string", "table", "os", "io", "coroutine", "debug",
        "package", "utf8", "bit",
        // Локальные переменные X-Ray-скриптов: ссылка ведёт в никуда, переходить некуда.
        "obj", "npc", "actor", "sObject", "who", "victim", "itm", "item"
    };

    /// <summary>Все ссылки текста в порядке появления.</summary>
    public static List<ScriptLink> Scan(string? text)
    {
        var result = new List<ScriptLink>();
        var source = text ?? "";
        var line = 0;
        var lineStart = 0;
        while (lineStart <= source.Length)
        {
            var lineEnd = source.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = source.Length;
            var textLine = source.Substring(lineStart, lineEnd - lineStart);
            ScanLine(textLine, lineStart, line, result);
            if (lineEnd >= source.Length) break;
            lineStart = lineEnd + 1;
            line++;
        }

        return result;
    }

    /// <summary>Ссылка под указанным смещением (для Ctrl+ЛКМ) или `null`.</summary>
    public static ScriptLink? At(string? text, int offset)
    {
        foreach (var link in Scan(text))
            if (offset >= link.Start && offset <= link.End) return link;
        return null;
    }

    private static void ScanLine(string line, int lineStart, int lineNumber, List<ScriptLink> result)
    {
        var found = new List<ScriptLink>();

        bool Overlaps(int start, int length) =>
            found.Any(link => start < link.End && start + length > link.Start);

        foreach (Match match in Directive.Matches(line))
        {
            var target = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
            if (target.Length == 0) continue;
            found.Add(new ScriptLink
            {
                Target = target,
                Start = lineStart + match.Index,
                Length = match.Length,
                Line = lineNumber,
                IsDirective = true
            });
        }

        foreach (Match match in FileToken.Matches(line))
        {
            // Токен внутри директивы уже учтён (иначе в списке будут дубли).
            var start = lineStart + match.Index;
            if (Overlaps(start, match.Length)) continue;
            found.Add(new ScriptLink
            {
                Target = match.Value.Trim(),
                Start = start,
                Length = match.Length,
                Line = lineNumber,
                IsDirective = false
            });
        }

        // Вызов `скрипт.функция(` — ссылка на функцию внутри скрипта (`level.actor()`).
        foreach (Match match in CallToken.Matches(line))
        {
            var script = match.Groups[1].Value;
            var member = match.Groups[2].Value;
            if (NonScriptReceivers.Contains(script)) continue;
            if (ScriptExtensions.Contains(member)) continue;

            var start = lineStart + match.Index;
            if (Overlaps(start, match.Length)) continue;

            found.Add(new ScriptLink
            {
                Target = script + "." + member,
                Start = start,
                Length = match.Length,
                Line = lineNumber,
                IsCall = true,
                Script = script,
                Member = member
            });
        }

        found.Sort((a, b) => a.Start.CompareTo(b.Start));
        result.AddRange(found);
    }
}
