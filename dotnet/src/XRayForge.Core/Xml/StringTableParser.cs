using System.Text.RegularExpressions;
using XRayForge.Core.Models;
using XRayForge.Core.Text;

namespace XRayForge.Core.Xml;

/// <summary>Разбор string_table (порт parseStringTableXml из xml.js).</summary>
public static class StringTableParser
{
    /// <summary>
    /// Разобрать string_table. Поддерживает фрагмент без корневого тега.
    /// Возвращает null, если это не string_table (или нет ни одного &lt;string&gt;).
    /// </summary>
    public static Language? Parse(string? text, string? path)
    {
        var src = text ?? string.Empty;
        var doc = XmlHelpers.TryParse(src);
        var root = doc?.Root;

        if (root == null || root.Name.LocalName != "string_table")
        {
            // Фрагмент без <string_table> — оборачиваем, только если реально есть <string>
            if (!Regex.IsMatch(src, @"<string[\s>]")) return null;
            doc = XmlHelpers.TryParse("<string_table>" + src + "</string_table>");
            if (doc == null) return null;
            root = doc.Root;
        }

        if (root == null || root.Name.LocalName != "string_table") return null;

        var entries = new List<LanguageEntry>();
        foreach (var s in root.Descendants().Where(e => e.Name.LocalName == "string"))
        {
            var id = (string?)s.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            var textEl = s.Elements().FirstOrDefault(e => e.Name.LocalName == "text");
            entries.Add(new LanguageEntry { Id = id, Text = textEl != null ? textEl.Value : "" });
        }

        var locale = XmlHelpers.DetectLocale(path);
        return new Language
        {
            Locale = locale,
            Name = XmlHelpers.LocaleName(locale),
            Path = Paths.Normalize(path),
            Entries = entries
        };
    }
}