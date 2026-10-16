using System.Text.RegularExpressions;
using XRayForge.Core.Text;

namespace XRayForge.Core.Ltx;

/// <summary>
/// Текстовые операции над LTX-файлом (правка строк, секций и полей без полного
/// пересбора) — порт `src/lib/ltx.js`: splitLines, updateLtxLine, removeSection,
/// addSection, addFieldToSection, removeFieldByLine, renameSection, replaceFieldValues.
/// </summary>
public static class LtxTextTools
{
    private const string Nl = "\n";

    /// <summary>splitLines: разбить текст по '\n' и убрать '\r' в строках.</summary>
    public static List<string> SplitLines(string? text) =>
        (text ?? string.Empty).Split('\n').Select(l => l.Replace("\r", "")).ToList();

    /// <summary>updateLtxLine: заменить строку по номеру (1-based); вне диапазона — без изменений.</summary>
    public static string UpdateLine(string text, int lineNo, string newLine)
    {
        var lines = SplitLines(text);
        if (lineNo >= 1 && lineNo <= lines.Count)
        {
            lines[lineNo - 1] = newLine;
            return string.Join(Nl, lines);
        }
        return text;
    }

    /// <summary>removeSection: удалить секцию вместе с её строками (имя без учёта регистра).</summary>
    public static string RemoveSection(string text, string? sectionName)
    {
        if (sectionName == null) return text;
        var lines = SplitLines(text);
        var parsed = LtxParser.Parse(text);
        var sec = parsed.Sections.FirstOrDefault(
            s => s.Name.ToLowerInvariant() == sectionName.ToLowerInvariant());
        if (sec == null) return text;

        var next = parsed.Sections.FirstOrDefault(s => s.StartLine > sec.StartLine);
        var endLine = next != null ? next.StartLine - 1 : lines.Count;
        var start = sec.StartLine;
        while (start > 1 && lines[start - 2].Trim().Length == 0) start--;
        lines.RemoveRange(start - 1, endLine - start + 1);
        return string.Join(Nl, lines);
    }

    /// <summary>addSection: добавить секцию с полем inv_name в конец файла.</summary>
    public static string AddSection(string? text, string name)
    {
        var block = "[" + name + "]" + Nl + "inv_name = " + name + Nl;
        var raw = text ?? string.Empty;
        return (raw.Trim().Length > 0 ? Regex.Replace(raw, @"\s+$", "") + Nl + Nl : "") + block;
    }

    /// <summary>addFieldToSection: вставить поле сразу после заголовка секции.</summary>
    public static string AddFieldToSection(string text, string? sectionName, string key, string value)
    {
        if (sectionName == null) return text;
        var lines = SplitLines(text);
        var parsed = LtxParser.Parse(text);
        var sec = parsed.Sections.FirstOrDefault(
            s => s.Name.ToLowerInvariant() == sectionName.ToLowerInvariant());
        if (sec == null) return text;

        lines.Insert(sec.StartLine, key + " = " + value);
        return string.Join(Nl, lines);
    }

    /// <summary>removeFieldByLine: удалить строку по номеру (1-based); вне диапазона — без изменений.</summary>
    public static string RemoveFieldByLine(string text, int lineNo)
    {
        var lines = SplitLines(text);
        if (lineNo >= 1 && lineNo <= lines.Count)
        {
            lines.RemoveAt(lineNo - 1);
            return string.Join(Nl, lines);
        }
        return text;
    }

    /// <summary>renameSection: переименовать секцию, сохранив список родителей (`[new: parent]`).</summary>
    public static string RenameSection(string text, string? oldName, string? newName)
    {
        if (oldName == null) return text;
        var oldLower = oldName.ToLowerInvariant();
        var lines = SplitLines(text).Select(line =>
        {
            var m = Regex.Match(line, @"^\[([^\]]+)\]");
            if (!m.Success) return line;

            var inner = m.Groups[1].Value;
            var ci = inner.IndexOf(':');
            var name = (ci >= 0 ? inner[..ci] : inner).Trim();
            if (name.ToLowerInvariant() != oldLower) return line;

            var rest = line[(m.Index + m.Length)..];
            return "[" + newName + (ci >= 0 ? inner[ci..] : "") + "]" + rest;
        });

        return string.Join(Nl, lines);
    }

    /// <summary>replaceFieldValues: заменить подстроку в значении ключа во всех секциях.</summary>
    public static string ReplaceFieldValues(string text, string key, string from, string to)
    {
        var lines = SplitLines(text).Select(line =>
        {
            var ci = line.IndexOf(';');
            var code = (ci != -1 ? line[..ci] : line).Trim();
            var eq = code.IndexOf('=');
            if (eq > 0 && code[..eq].Trim() == key)
            {
                var comment = ci != -1 ? " " + line[ci..] : "";
                return key + " = " + JsReplaceAll(code[(eq + 1)..].Trim(), from, to) + comment;
            }
            return line;
        });

        return string.Join(Nl, lines);
    }

    /// <summary>
    /// Аналог JS `s.split(from).join(to)`, в том числе для пустого `from`
    /// (вставка `to` между всеми символами и по краям), где `string.Replace` не применим.
    /// </summary>
    internal static string JsReplaceAll(string value, string from, string to)
    {
        if (from.Length == 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(to);
            foreach (var c in value) sb.Append(c).Append(to);
            return sb.ToString();
        }
        return value.Replace(from, to);
    }
}