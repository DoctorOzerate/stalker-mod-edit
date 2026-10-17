using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using XRayForge.Core.Models;
using XRayForge.Core.Text;

namespace XRayForge.Core.Xml;

/// <summary>Мелкие XML-утилиты и работа с локалями (порт xmlAttr/xmlTag/detectLocale/mergeLanguages).</summary>
public static class XmlHelpers
{
    private const char Q = '"';

    private static readonly Regex Comments = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Убрать XML-комментарии. Реальные файлы X-Ray часто содержат `&lt;!--- ... --&gt;`
    /// (двойной дефис внутри комментария), что строгий .NET-парсер считает ошибкой,
    /// а `@xmldom/xmldom` в JS пропускает. Комментарии для разбора не нужны.
    /// </summary>
    internal static string CleanXml(string? xml) => Comments.Replace(xml ?? string.Empty, "");

    /// <summary>Достать значение атрибута из сырой строки атрибутов (в двойных кавычках).</summary>
    public static string XmlAttr(string? attrs, string name)
    {
        var a = attrs ?? string.Empty;
        var key = name + "=";
        var i = a.IndexOf(key, StringComparison.Ordinal);
        if (i == -1) return "";

        var q1 = a.IndexOf(Q, i + key.Length);
        if (q1 == -1) return "";
        var q2 = a.IndexOf(Q, q1 + 1);
        if (q2 == -1) return "";
        return a.Substring(q1 + 1, q2 - q1 - 1);
    }

    /// <summary>Текст между &lt;tag&gt; и &lt;/tag&gt; в сыром теле тега.</summary>
    public static string XmlTag(string? body, string tag)
    {
        var b = body ?? string.Empty;
        var open = b.IndexOf("<" + tag, StringComparison.Ordinal);
        if (open == -1) return "";

        var gt = b.IndexOf('>', open);
        if (gt == -1) return "";

        var close = b.IndexOf("</" + tag + ">", gt, StringComparison.Ordinal);
        if (close == -1) return "";
        return b.Substring(gt + 1, close - gt - 1).Trim();
    }

    public static readonly IReadOnlyDictionary<string, string> LocaleNames = new Dictionary<string, string>
    {
        ["rus"] = "Русский",
        ["eng"] = "English",
        ["ukr"] = "Українська",
        ["ger"] = "Deutsch",
        ["deu"] = "Deutsch",
        ["fra"] = "Français",
        ["fre"] = "Français",
        ["ita"] = "Italiano",
        ["spa"] = "Español",
        ["esp"] = "Español",
        ["pol"] = "Polski",
        ["cz"] = "Čeština",
        ["czc"] = "Čeština",
        ["tur"] = "Türkçe",
        ["jap"] = "日本語"
    };

    /// <summary>Отображаемое имя локали или её код в верхнем регистре.</summary>
    public static string LocaleName(string locale) =>
        LocaleNames.TryGetValue(locale, out var n) ? n : locale.ToUpperInvariant();

    /// <summary>Определить локаль по пути: сегмент после «text/» либо суффикс имени файла.</summary>
    public static string DetectLocale(string? path)
    {
        var parts = Paths.Normalize(path).Split('/').ToList();

        var textIndex = parts.FindIndex(x => x.ToLowerInvariant() == "text");
        if (textIndex >= 0 && textIndex + 1 < parts.Count && parts[textIndex + 1].Length > 0)
            return parts[textIndex + 1].ToLowerInvariant();

        var last = parts.Count > 0 ? parts[^1] : "";
        var dot = last.IndexOf('.');
        var baseName = (dot >= 0 ? last.Substring(0, dot) : last).ToLowerInvariant();
        var m = Regex.Match(baseName, @"(?:^|[_\-])(rus|eng|ukr|ger|deu|fra|fre|ita|spa|esp|pol|cz|czc|tur|jap)$");
        return m.Success ? m.Groups[1].Value : "unknown";
    }

    internal static XDocument? TryParse(string? xml)
    {
        try { return XDocument.Parse(CleanXml(xml)); }
        catch (XmlException) { return null; }
    }

    /// <summary>Текст первого дочернего элемента с указанным именем (с Trim) или пустая строка.</summary>
    internal static string ChildText(XElement el, string tag)
    {
        var c = el.Elements().FirstOrDefault(e => e.Name.LocalName == tag);
        return c != null ? c.Value.Trim() : "";
    }

    internal static IEnumerable<XElement> Children(XElement el, string tag) =>
        el.Elements().Where(e => e.Name.LocalName == tag);

    internal static List<string> KidTexts(XElement el, string tag) =>
        Children(el, tag).Select(x => x.Value.Trim()).ToList();

    internal static double Num(string? v) =>
        string.IsNullOrEmpty(v) ? 0
        : double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    /// <summary>Слияние списков локализаций: существующие сохраняются, из входящих доливаются новые записи.</summary>
    public static List<Language> MergeLanguages(IEnumerable<Language?>? existing, IEnumerable<Language?>? incoming)
    {
        var result = new List<Language>();
        var byLocale = new Dictionary<string, Language>();

        foreach (var l in existing ?? Enumerable.Empty<Language?>())
        {
            if (l == null) continue;
            var locale = (l.Locale ?? string.Empty).ToLowerInvariant();
            var lang = new Language
            {
                Locale = locale,
                Name = l.Name ?? string.Empty,
                Entries = l.Entries
                    .Where(e => e != null && e.Id.Length > 0)
                    .Select(e => new LanguageEntry { Id = e.Id, Text = e.Text ?? string.Empty })
                    .ToList()
            };

            result.Add(lang);
            if (locale.Length > 0) byLocale[locale] = lang;
        }

        foreach (var inc in incoming ?? Enumerable.Empty<Language?>())
        {
            if (inc == null) continue;
            var locale = (inc.Locale ?? string.Empty).ToLowerInvariant();
            if (locale.Length == 0) continue;

            if (!byLocale.TryGetValue(locale, out var lang))
            {
                lang = new Language
                {
                    Locale = locale,
                    Name = !string.IsNullOrEmpty(inc.Name) ? inc.Name : LocaleName(locale)
                };
                byLocale[locale] = lang;
                result.Add(lang);
            }
            else if (lang.Name.Length == 0 && !string.IsNullOrEmpty(inc.Name))
            {
                lang.Name = inc.Name;
            }

            var ids = new HashSet<string>(lang.Entries.Select(e => e.Id));
            foreach (var e in inc.Entries)
            {
                if (e == null || e.Id.Length == 0 || !ids.Add(e.Id)) continue;
                lang.Entries.Add(new LanguageEntry { Id = e.Id, Text = e.Text ?? string.Empty });
            }
        }

        return result;
    }

    /// <summary>
    /// Список id сообществ (community) и фракций (faction) из character_desc_general.xml:
    /// communities в порядке появления, затем factions.
    /// </summary>
    public static List<string> ParseGeneralCharactersXml(string? text)
    {
        var doc = TryParse(text);
        var root = doc?.Root;
        if (root == null || root.Name.LocalName != "character_desc_general") return new List<string>();

        var result = new List<string>();
        foreach (var name in new[] { "community", "faction" })
        {
            foreach (var el in root.Descendants().Where(e => e.Name.LocalName == name))
            {
                var id = (string?)el.Attribute("id") ?? "";
                if (id.Length > 0 && !result.Contains(id)) result.Add(id);
            }
        }

        return result;
    }
}