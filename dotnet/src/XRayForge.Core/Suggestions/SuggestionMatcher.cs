namespace XRayForge.Core.Suggestions;

/// <summary>
/// Фильтрация и ранжирование подсказок. Сначала идут значения, начинающиеся с введённого
/// текста, затем совпадения с начала сегмента (`bind_`/`ammo.`/`wpn/`) и только потом
/// вхождение в середину значения.
/// </summary>
public static class SuggestionMatcher
{
    /// <summary>
    /// 0 — значение начинается с префикса; 1 — префикс начинается с сегмента значения;
    /// 2 — вхождение в середину; <see cref="int.MaxValue"/> — не подходит.
    /// </summary>
    public static int Score(string? value, string? prefix)
    {
        var text = value ?? "";
        var query = (prefix ?? "").Trim();
        if (query.Length == 0) return 0;

        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return int.MaxValue;
        if (index == 0) return 0;

        var before = text[index - 1];
        return before is '_' or '.' or '-' or '/' or '\\' or ':' or ' ' ? 1 : 2;
    }

    /// <summary>Убрать пустые и повторяющиеся значения (регистр не важен, побеждает первое).</summary>
    public static List<SuggestionEntry> Distinct(IEnumerable<SuggestionEntry>? source)
    {
        var result = new List<SuggestionEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source ?? Enumerable.Empty<SuggestionEntry>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value)) continue;
            if (!seen.Add(entry.Value)) continue;
            result.Add(entry);
        }
        return result;
    }

    /// <summary>Отфильтровать и отсортировать подсказки для введённого префикса.</summary>
    public static List<SuggestionEntry> Filter(
        IEnumerable<SuggestionEntry>? source, string? prefix, int limit = 20)
    {
        var query = (prefix ?? "").Trim();
        var max = Math.Clamp(limit, 1, 500);
        var entries = Distinct(source);

        if (query.Length == 0)
            return entries
                .OrderBy(entry => entry.Kind)
                .ThenBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();

        return entries
            .Select(entry => (Entry: entry, Score: Score(entry.Value, query)))
            .Where(match => match.Score != int.MaxValue)
            .OrderBy(match => match.Score)
            .ThenBy(match => match.Entry.Value.Length)
            .ThenBy(match => match.Entry.Kind)
            .ThenBy(match => match.Entry.Value, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(match => match.Entry)
            .ToList();
    }
}
