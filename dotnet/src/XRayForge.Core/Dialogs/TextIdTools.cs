using System.Text.RegularExpressions;
using XRayForge.Core.Models;

namespace XRayForge.Core.Dialogs;

public sealed class TextIdIssue
{
    public string? Dialogue { get; set; }
    public string? Locale { get; set; }
    public string TextId { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class TextIdResolution
{
    public string Locale { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class TextIdCoverage
{
    public int Total { get; set; }
    public int Translated { get; set; }
    public int Pct { get; set; }
}

/// <summary>Отчёты по ссылкам text_id и переводам (#5, #77).</summary>
public static class TextIdTools
{
    /// <summary>Ссылки text_id без перевода (#5).</summary>
    public static List<TextIdIssue> ValidateTextIds(IEnumerable<DialogueGraph?>? dialogues, IReadOnlyList<Language>? languages)
    {
        var issues = new List<TextIdIssue>();

        var langIds = new HashSet<string>();
        foreach (var l in languages ?? Array.Empty<Language>())
            foreach (var e in l.Entries)
                if (e.Id.Length > 0) langIds.Add(e.Id);

        foreach (var d in dialogues ?? Enumerable.Empty<DialogueGraph?>())
        {
            if (d == null) continue;
            var name = !string.IsNullOrEmpty(d.Id) ? d.Id : d.Name;
            foreach (var n in d.Nodes)
            {
                if (n.Type != NodeTypes.Phrase) continue;
                var t = n.Data?.TextId;
                if (!string.IsNullOrEmpty(t) && !langIds.Contains(t))
                    issues.Add(new TextIdIssue { Dialogue = name, TextId = t, Message = name + ": нет перевода для " + t });
            }
        }

        return issues;
    }

    /// <summary>Резолв text_id → перевод по всем локалям (#5).</summary>
    public static TextIdResolution? ResolveTextId(IReadOnlyList<Language>? languages, string? textId)
    {
        foreach (var lang in languages ?? Array.Empty<Language>())
        {
            var e = lang.Entries.FirstOrDefault(x => x.Id == textId && x.Text.Trim().Length > 0);
            if (e != null) return new TextIdResolution { Locale = lang.Locale, Text = e.Text };
        }
        return null;
    }

    /// <summary>Проверка text_id: дубликаты, пустые переводы, отсутствие перевода (#77).</summary>
    public static List<TextIdIssue> FindTextIdIssues(IReadOnlyList<Language>? languages, IEnumerable<DialogueGraph?>? dialogues)
    {
        var issues = new List<TextIdIssue>();

        var used = new HashSet<string>();
        foreach (var d in dialogues ?? Enumerable.Empty<DialogueGraph?>())
        {
            if (d == null) continue;
            foreach (var n in d.Nodes)
                if (n.Type == NodeTypes.Phrase && !string.IsNullOrEmpty(n.Data?.TextId)) used.Add(n.Data!.TextId);
        }

        foreach (var lang in languages ?? Array.Empty<Language>())
        {
            var seen = new HashSet<string>();
            foreach (var e in lang.Entries)
            {
                if (e.Id.Length == 0) continue;
                if (!seen.Add(e.Id))
                    issues.Add(new TextIdIssue { Locale = lang.Locale, TextId = e.Id, Message = "Дубликат id: " + e.Id });
                if (e.Text.Trim().Length == 0)
                    issues.Add(new TextIdIssue { Locale = lang.Locale, TextId = e.Id, Message = "Пустой перевод: " + e.Id });
            }
        }

        foreach (var id in used)
        {
            var has = (languages ?? Array.Empty<Language>())
                .Any(lang => lang.Entries.Any(e => e.Id == id && e.Text.Trim().Length > 0));
            if (!has)
                issues.Add(new TextIdIssue { Locale = "", TextId = id, Message = "Нет перевода ни в одной локали: " + id });
        }

        return issues;
    }

    /// <summary>Покрытие text_id переводами (в %).</summary>
    public static TextIdCoverage Coverage(IEnumerable<DialogueGraph?>? dialogues, IReadOnlyList<Language>? languages)
    {
        var ids = new HashSet<string>();
        foreach (var d in dialogues ?? Enumerable.Empty<DialogueGraph?>())
        {
            if (d == null) continue;
            foreach (var n in d.Nodes)
                if (n.Type == NodeTypes.Phrase && !string.IsNullOrEmpty(n.Data?.TextId)) ids.Add(n.Data!.TextId);
        }

        if (ids.Count == 0) return new TextIdCoverage { Total = 0, Translated = 0, Pct = 100 };

        var translated = ids.Count(id => (languages ?? Array.Empty<Language>())
            .Any(l => l.Entries.Any(e => e.Id == id && e.Text.Trim().Length > 0)));

        return new TextIdCoverage
        {
            Total = ids.Count,
            Translated = translated,
            Pct = (int)Math.Round((double)translated / ids.Count * 100, MidpointRounding.AwayFromZero)
        };
    }

    // --- правки таблиц переводов из панели свойств реплики (шаг 6.5) ---

    /// <summary>Локаль таблицы переводов по умолчанию (`bindId` в `DialoguesTab.js`).</summary>
    public const string DefaultLocale = "rus";

    /// <summary>Имя таблицы переводов по умолчанию.</summary>
    public const string DefaultLocaleName = "Русский";

    /// <summary>`'st_' + selectedNode.id.replace(/[^a-z0-9_]/gi, '_')` — text_id из id узла.</summary>
    public static string GeneratedTextId(string? nodeId) =>
        "st_" + Regex.Replace(nodeId ?? string.Empty, "[^a-zA-Z0-9_]", "_");

    /// <summary>
    /// `langs.find(l => l.locale === activePhraseLocale) || langs[0]` — активная таблица переводов
    /// (если локаль не выбрана или не найдена, берётся первая).
    /// </summary>
    public static Language? ActiveLanguage(IReadOnlyList<Language>? languages, string? locale)
    {
        var list = languages ?? Array.Empty<Language>();
        if (list.Count == 0) return null;
        return list.FirstOrDefault(l => l.Locale == locale) ?? list[0];
    }

    /// <summary>`entry ? entry.text : ''` — перевод text_id в указанной локали.</summary>
    public static string TranslationOf(IReadOnlyList<Language>? languages, string? locale, string? textId)
    {
        var language = ActiveLanguage(languages, locale);
        if (language == null || string.IsNullOrEmpty(textId)) return "";
        return language.Entries.FirstOrDefault(e => e.Id == textId)?.Text ?? "";
    }

    /// <summary>
    /// `bindId`: привязать text_id к таблицам переводов. Без таблиц создаётся одна (`rus`/«Русский»)
    /// с fallback-текстом реплики; если такого id нет ни в одной локали, запись (`text`) добавляется
    /// во все локали (текст попадает только в `rus`), иначе список возвращается без изменений.
    /// </summary>
    public static List<Language> BindTextId(IReadOnlyList<Language>? languages, string? textId, string? fallbackText)
    {
        var list = languages ?? Array.Empty<Language>();
        if (string.IsNullOrEmpty(textId)) return Copy(list);

        if (list.Count == 0)
            return new List<Language>
            {
                new()
                {
                    Locale = DefaultLocale,
                    Name = DefaultLocaleName,
                    Entries = new List<LanguageEntry> { new() { Id = textId!, Text = fallbackText ?? "" } }
                }
            };

        if (list.Any(l => l.Entries.Any(e => e.Id == textId))) return Copy(list);

        var result = Copy(list);
        foreach (var language in result)
            language.Entries.Add(new LanguageEntry
            {
                Id = textId!,
                Text = language.Locale == DefaultLocale ? fallbackText ?? "" : ""
            });

        return result;
    }

    /// <summary>
    /// `setTranslation`: правка перевода активной локали — существующая запись перезаписывается,
    /// отсутствующая добавляется (в локаль, где запись есть, изменения не уходят).
    /// </summary>
    public static List<Language> SetTranslation(
        IReadOnlyList<Language>? languages, string? locale, string? textId, string? text)
    {
        var result = Copy(languages ?? Array.Empty<Language>());
        var active = ActiveLanguage(languages, locale);
        if (active == null || string.IsNullOrEmpty(textId)) return result;

        var target = result.First(l => l.Locale == active.Locale);
        var entry = target.Entries.FirstOrDefault(e => e.Id == textId);
        if (entry != null) entry.Text = text ?? "";
        else target.Entries.Add(new LanguageEntry { Id = textId!, Text = text ?? "" });

        return result;
    }

    /// <summary>Копия списка таблиц переводов со всеми записями (правки не меняют проект на месте).</summary>
    private static List<Language> Copy(IReadOnlyList<Language> languages)
    {
        var result = new List<Language>(languages.Count);
        foreach (var language in languages)
            result.Add(new Language
            {
                Locale = language.Locale,
                Name = language.Name,
                Path = language.Path,
                Entries = language.Entries
                    .Select(e => new LanguageEntry { Id = e.Id, Text = e.Text })
                    .ToList(),
                Extra = language.Extra
            });

        return result;
    }
}