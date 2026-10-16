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
}