using System.Text.RegularExpressions;
using XRayForge.Core.Text;

namespace XRayForge.Core.Ltx;

/// <summary>Классы токенов подсветки LTX.</summary>
public static class LtxTokenClasses
{
    public const string Plain = "plain";
    public const string Comment = "comment";
    public const string Section = "section";
    public const string Include = "include";
    public const string Key = "key";
    public const string Value = "value";
}

/// <summary>Токен строки LTX: текст и класс подсветки.</summary>
public sealed class LtxToken
{
    public string Text { get; set; } = "";
    public string Cls { get; set; } = LtxTokenClasses.Plain;
}

/// <summary>
/// Подсветка синтаксиса LTX (#2) — порт `highlightLtx`. Вместо HTML-строки ядро отдаёт
/// модель токенов (комментарий/секция/include/ключ/значение), а <see cref="ToHtml"/>
/// сохраняет исходное HTML-представление для проверки соответствия и экспорта.
/// </summary>
public static class LtxHighlight
{
    private static readonly Regex ReCommentLine = new(@"^\s*;", RegexOptions.Compiled);
    private static readonly Regex ReSection = new(@"^(\s*)(\[[^\]]*\])(\s*)(.*)$", RegexOptions.Compiled);
    private static readonly Regex ReInclude = new("^(\\s*)(#include\\s+\"[^\"]*\")(\\s*)(.*)$", RegexOptions.Compiled);
    private static readonly Regex ReKeyValue = new(@"^(\s*)([^=;]+?)(\s*=\s*)([^;]*)(;.*)?$", RegexOptions.Compiled);

    /// <summary>highlightLtx: токены по строкам (строка заканчивается на '\n').</summary>
    public static List<List<LtxToken>> Highlight(string? text) =>
        (text ?? "").Split('\n').Select(HighlightLine).ToList();

    /// <summary>Токены одной строки LTX.</summary>
    public static List<LtxToken> HighlightLine(string line)
    {
        if (ReCommentLine.IsMatch(line))
            return new List<LtxToken> { new() { Text = line, Cls = LtxTokenClasses.Comment } };

        var sec = ReSection.Match(line);
        if (sec.Success)
        {
            var tokens = new List<LtxToken>
            {
                new() { Text = sec.Groups[1].Value, Cls = LtxTokenClasses.Plain },
                new() { Text = sec.Groups[2].Value, Cls = LtxTokenClasses.Section }
            };
            if (sec.Groups[4].Value.Length > 0)
                tokens.Add(new LtxToken { Text = sec.Groups[3].Value + sec.Groups[4].Value, Cls = LtxTokenClasses.Comment });
            return tokens;
        }

        var inc = ReInclude.Match(line);
        if (inc.Success)
        {
            var tokens = new List<LtxToken>
            {
                new() { Text = inc.Groups[1].Value, Cls = LtxTokenClasses.Plain },
                new() { Text = inc.Groups[2].Value, Cls = LtxTokenClasses.Include }
            };
            if (inc.Groups[4].Value.Length > 0)
                tokens.Add(new LtxToken { Text = inc.Groups[3].Value + inc.Groups[4].Value, Cls = LtxTokenClasses.Comment });
            return tokens;
        }

        var kv = ReKeyValue.Match(line);
        if (kv.Success)
        {
            var tokens = new List<LtxToken>
            {
                new() { Text = kv.Groups[1].Value, Cls = LtxTokenClasses.Plain },
                new() { Text = kv.Groups[2].Value, Cls = LtxTokenClasses.Key },
                new() { Text = kv.Groups[3].Value, Cls = LtxTokenClasses.Plain },
                new() { Text = kv.Groups[4].Value, Cls = LtxTokenClasses.Value }
            };
            if (kv.Groups[5].Success && kv.Groups[5].Value.Length > 0)
                tokens.Add(new LtxToken { Text = kv.Groups[5].Value, Cls = LtxTokenClasses.Comment });
            return tokens;
        }

        return new List<LtxToken> { new() { Text = line, Cls = LtxTokenClasses.Plain } };
    }

    /// <summary>HTML-рендер токенов (как в JS-версии highlightLtx, для проверки паритета).</summary>
    public static string TokensToHtml(IEnumerable<LtxToken> tokens) =>
        string.Concat(tokens.Select(t => t.Cls switch
        {
            LtxTokenClasses.Comment => Span("#6b7280", XmlEscaping.EscapeText(t.Text)),
            LtxTokenClasses.Section => Span("#a78bfa;font-weight:bold", XmlEscaping.EscapeText(t.Text)),
            LtxTokenClasses.Include => Span("#2dd4bf", XmlEscaping.EscapeText(t.Text)),
            LtxTokenClasses.Key => Span("#fbbf24", XmlEscaping.EscapeText(t.Text)),
            LtxTokenClasses.Value => Span("#d1d5db", XmlEscaping.EscapeText(t.Text)),
            _ => XmlEscaping.EscapeText(t.Text)
        }));

    /// <summary>highlightLtx: полный HTML-текст LTX (строки через '\n').</summary>
    public static string ToHtml(string? text) =>
        string.Join("\n", Highlight(text).Select(TokensToHtml));

    private static string Span(string color, string text) =>
        "<span style=\"color:" + color + "\">" + text + "</span>";
}