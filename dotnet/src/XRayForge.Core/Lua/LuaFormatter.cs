using System.Text.RegularExpressions;

namespace XRayForge.Core.Lua;

/// <summary>Форматирование и свёртка Lua-скриптов (порт formatLua/luaFunctionBlocks).</summary>
public static class LuaFormatter
{
    private const char Nl = '\n';

    private static readonly Regex Closers = new(@"^(end|else|elseif|until)\b", RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static readonly Regex Openers = new(
        @"(^function\b|\bthen\s*$|\bdo\s*$|^repeat\b$|^else\b$|^elseif\b)",
        RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static readonly Regex FunctionStart = new(@"^function\b", RegexOptions.ECMAScript | RegexOptions.Compiled);
    private static readonly Regex LocalFunctionStart = new(@"^local\s+function\b", RegexOptions.ECMAScript | RegexOptions.Compiled);
    private static readonly Regex EndStart = new(@"^end\b", RegexOptions.ECMAScript | RegexOptions.Compiled);

    /// <summary>Простое форматирование: отступы 2 пробела по блокам function/if/for/while/repeat/do.</summary>
    public static string Format(string? code)
    {
        var lines = (code ?? string.Empty).Split(Nl);
        var indent = 0;
        var result = new List<string>(lines.Length);

        foreach (var raw in lines)
        {
            var t = raw.Trim();
            if (t.Length == 0)
            {
                result.Add("");
                continue;
            }

            if (Closers.IsMatch(t)) indent = Math.Max(0, indent - 1);
            result.Add(string.Concat(Enumerable.Repeat("  ", indent)) + t);
            if (Openers.IsMatch(t)) indent++;
        }

        return string.Join(Nl, result);
    }

    /// <summary>Диапазоны строк тел функций (для свёртки).</summary>
    public static List<LuaBlock> FunctionBlocks(IReadOnlyList<string>? lines)
    {
        var blocks = new List<LuaBlock>();
        if (lines == null) return blocks;

        var depth = 0;
        var start = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var t = (lines[i] ?? string.Empty).Trim();
            if (FunctionStart.IsMatch(t) || LocalFunctionStart.IsMatch(t))
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (EndStart.IsMatch(t))
            {
                depth = Math.Max(0, depth - 1);
                if (depth == 0 && start != -1)
                {
                    blocks.Add(new LuaBlock { Start = start, End = i });
                    start = -1;
                }
            }
        }

        return blocks;
    }
}