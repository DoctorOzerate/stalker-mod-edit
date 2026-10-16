using System.Text.RegularExpressions;

namespace XRayForge.Core.Lua;

/// <summary>
/// Анализ Lua-скриптов: автодополнение, граф вызовов, неиспользуемые функции,
/// баланс блоков, глобальные присваивания, проверка script_binding (порт lua.js).
/// </summary>
public static class LuaAnalyzer
{
    private static Regex Js(string pattern) => new(pattern, RegexOptions.ECMAScript | RegexOptions.Compiled);

    private static readonly Regex FunctionName = Js(@"function\s+([A-Za-z_][\w.:]*)");
    private static readonly Regex FunctionDeclCount = Js(@"function\s+[A-Za-z_][\w.:]*\s*\(");
    private static readonly Regex CommentTail = Js(@"--.*$");
    private static readonly Regex LocalFunctionDecl = Js(@"\blocal\s+function\s+([A-Za-z_]\w*)");
    private static readonly Regex LocalDecl = Js(@"\blocal\s+([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)");
    private static readonly Regex SimpleName = Js(@"^[A-Za-z_]\w*$");
    private static readonly Regex AssignStart = Js(@"^([A-Za-z_]\w*)\s*(?:,\s*[A-Za-z_]\w*)*\s*=");
    private static readonly Regex ScriptSuffix = Js(@"\.script$");
    private static readonly Regex ScriptBinding = Js(@"script_binding\s*=\s*([A-Za-z_][\w.]*)");
    private static readonly Regex RegexSpecials = Js(@"[.*+?^${}()|[\]\\]");

    private static readonly HashSet<string> BlockOpeners = new() { "function", "if", "for", "while", "repeat" };

    private static string Escape(string x) => RegexSpecials.Replace(x, "\\$&");

    /// <summary>Автодополнение по префиксу (пустой префикс → весь список API).</summary>
    public static List<string> Completions(string? prefix)
    {
        var p = (prefix ?? string.Empty).ToLowerInvariant();
        if (p.Length == 0) return LuaCatalog.Api.ToList();
        return LuaCatalog.Api.Where(f => f.ToLowerInvariant().Contains(p, StringComparison.Ordinal)).ToList();
    }

    /// <summary>Граф вызовов между объявленными функциями.</summary>
    public static LuaCallGraph BuildCallGraph(string? code)
    {
        var lines = (code ?? string.Empty).Split('\n');
        var blocks = LuaFormatter.FunctionBlocks(lines);
        var defs = new Dictionary<string, string>();
        var names = new List<string>();

        foreach (var b in blocks)
        {
            var m = FunctionName.Match(lines[b.Start]);
            if (!m.Success) continue;
            var name = m.Groups[1].Value;
            names.Add(name);
            defs[name] = string.Join('\n', lines.Skip(b.Start).Take(b.End - b.Start + 1));
        }

        var edges = new List<LuaCallEdge>();
        foreach (var name in names)
        {
            foreach (var other in names)
            {
                if (other == name) continue;
                if (Regex.IsMatch(defs[name], @"\b" + Escape(other) + @"\s*\(", RegexOptions.ECMAScript))
                    edges.Add(new LuaCallEdge { From = name, To = other });
            }
        }

        return new LuaCallGraph { Functions = names, Edges = edges };
    }

    /// <summary>Функции, встречающиеся в тексте один раз (то есть нигде не вызванные).</summary>
    public static List<string> Unused(string? code)
    {
        var s = code ?? string.Empty;
        var unused = new List<string>();
        foreach (Match m in FunctionName.Matches(s))
        {
            var name = m.Groups[1].Value;
            var count = Regex.Matches(s, @"\b" + Escape(name) + @"\b", RegexOptions.ECMAScript).Count;
            if (count <= 1) unused.Add(name);
        }
        return unused;
    }

    /// <summary>Проверка баланса блоков (function/if/for/while/repeat vs end/until) (#31).</summary>
    public static List<LuaIssue> Check(string? code)
    {
        var issues = new List<LuaIssue>();
        var depth = 0;

        foreach (var tk in LuaTokenizer.Tokenize(code))
        {
            if (tk.Cls != LuaTokenClasses.Keyword) continue;
            if (BlockOpeners.Contains(tk.Text)) depth++;
            else if (tk.Text is "end" or "until") depth--;

            if (depth < 0)
            {
                issues.Add(new LuaIssue { Severity = "error", Code = "lua_extra_end", Message = "Лишний end/until" });
                depth = 0;
            }
        }

        if (depth > 0)
            issues.Add(new LuaIssue { Severity = "error", Code = "lua_unclosed", Message = "Незакрытые блоки: " + depth });

        return issues;
    }

    /// <summary>Эвристика: глобальные присваивания без local (#29).</summary>
    public static List<LuaIssue> UndeclaredGlobals(string? code)
    {
        var lines = (code ?? string.Empty).Split('\n');
        var issues = new List<LuaIssue>();
        var locals = new HashSet<string>();
        var allow = new HashSet<string>(LuaCatalog.EngineGlobals);

        foreach (var raw in lines)
        {
            var t = CommentTail.Replace(raw, "");
            var m = LocalFunctionDecl.Match(t);
            if (m.Success)
            {
                locals.Add(m.Groups[1].Value);
                continue;
            }

            m = LocalDecl.Match(t);
            if (!m.Success) continue;
            foreach (var part in m.Groups[1].Value.Split(','))
            {
                var name = part.Trim();
                if (SimpleName.IsMatch(name)) locals.Add(name);
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var t = CommentTail.Replace(lines[i], "").Trim();
            var m = AssignStart.Match(t);
            if (!m.Success) continue;

            var name = m.Groups[1].Value;
            if (locals.Contains(name) || allow.Contains(name) || name == "end") continue;

            issues.Add(new LuaIssue
            {
                Severity = "warning",
                Code = "lua_global",
                Line = i + 1,
                Message = "Возможное глобальное присваивание (нет local): " + name
            });
        }

        return issues;
    }

    /// <summary>Проверка привязок script_binding (#56).</summary>
    public static List<LuaIssue> ValidateScriptBindings(string? ltxText, IEnumerable<LuaScriptFile>? scripts)
    {
        var issues = new List<LuaIssue>();
        var scriptMap = new Dictionary<string, string>();

        foreach (var s in scripts ?? Enumerable.Empty<LuaScriptFile>())
            scriptMap[ScriptSuffix.Replace((s.Name ?? string.Empty).ToLowerInvariant(), "")] = s.Text ?? string.Empty;

        foreach (Match m in ScriptBinding.Matches(ltxText ?? string.Empty))
        {
            var binding = m.Groups[1].Value;
            var dot = binding.IndexOf('.');
            var file = (dot >= 0 ? binding.Substring(0, dot) : binding).ToLowerInvariant();
            var fn = dot >= 0 ? binding.Substring(dot + 1) : "";

            if (!scriptMap.TryGetValue(file, out var body))
                issues.Add(new LuaIssue
                {
                    Code = "binding_no_file",
                    Binding = binding,
                    Message = "script_binding: файл не найден — " + file + ".script (" + binding + ")"
                });
            else if (fn.Length > 0 && !Regex.IsMatch(body, @"function\s+" + Escape(fn) + @"\b", RegexOptions.ECMAScript))
                issues.Add(new LuaIssue
                {
                    Code = "binding_no_fn",
                    Binding = binding,
                    Message = "script_binding: функция не найдена — " + binding
                });
        }

        return issues;
    }

    /// <summary>Счётчик объявлений функций.</summary>
    public static int CountFunctions(string? code) => FunctionDeclCount.Matches(code ?? string.Empty).Count;

    /// <summary>Дубликаты имён функций (в порядке первого появления).</summary>
    public static List<LuaDuplicateFunction> DuplicateFunctions(string? code)
    {
        var s = code ?? string.Empty;
        var counts = new Dictionary<string, int>();
        var order = new List<string>();

        foreach (Match m in FunctionName.Matches(s))
        {
            var name = m.Groups[1].Value;
            if (counts.TryGetValue(name, out var c)) counts[name] = c + 1;
            else
            {
                counts[name] = 1;
                order.Add(name);
            }
        }

        var dups = new List<LuaDuplicateFunction>();
        foreach (var name in order)
            if (counts[name] > 1) dups.Add(new LuaDuplicateFunction { Name = name, Count = counts[name] });

        return dups;
    }
}