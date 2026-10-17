using System.Globalization;
using System.Text;

namespace XRayForge.Core.Ltx;

/// <summary>Группа левой панели вкладки «Предметы»: категория и её секции.</summary>
public sealed class ItemCategoryGroup
{
    public string Category { get; set; } = "";
    public List<LtxSection> Items { get; set; } = new();
}

/// <summary>
/// Чистая логика вкладки «Предметы» (`ItemsTab.js` и обработчики `App.js`): группировка
/// секций по категориям, поиск, сортировка, цепочка наследования, ссылки, быстрые поля,
/// правка строки поля с сохранением комментария и CSV-экспорт. UI-независимо, поэтому
/// проверяется тестами; `ItemsViewModel` в WPF-проекте держит состояние вкладки.
/// </summary>
public static class ItemEditor
{
    /// <summary>Пункт фильтра категорий «Все» в JS (`['Все'].concat(ITEM_CATEGORIES)`).</summary>
    public const string AllCategories = "Все";

    /// <summary>Поля, по которым секция считается ссылающейся на другую (`ItemsTab.js`).</summary>
    public static readonly IReadOnlyList<string> ReferenceKeys = new[]
    {
        "immunities_sect", "bones_koeff_protection", "hit_fraction"
    };

    /// <summary>
    /// `ITEM_CATEGORIES.map(...)`: секции фильтруются по имени/полям, сортируются (`name` или
    /// `count`) и раскладываются по категориям в порядке <see cref="ItemCatalog.Categories"/>.
    /// </summary>
    public static List<ItemCategoryGroup> Group(
        IEnumerable<LtxSection>? sections, string? query, string? category, string? sortMode)
    {
        var needle = (query ?? "").ToLowerInvariant();
        var list = (sections ?? Enumerable.Empty<LtxSection>())
            .Where(section => section != null && !string.IsNullOrEmpty(section.Name))
            .Where(section => needle.Length == 0 || SearchText(section).Contains(needle, StringComparison.Ordinal))
            .ToList();

        IOrderedEnumerable<LtxSection> ordered = string.Equals(sortMode, "count", StringComparison.Ordinal)
            ? list.OrderByDescending(section => section.Entries.Count)
            : list.OrderBy(section => section.Name, StringComparer.CurrentCulture);

        var groups = new List<ItemCategoryGroup>();
        foreach (var name in ItemCatalog.Categories)
        {
            if (!string.IsNullOrEmpty(category) && category != AllCategories &&
                !string.Equals(category, name, StringComparison.Ordinal)) continue;

            var items = ordered
                .Where(section => string.Equals(ItemCatalog.ClassifyItem(section), name, StringComparison.Ordinal))
                .ToList();
            if (items.Count == 0) continue;
            groups.Add(new ItemCategoryGroup { Category = name, Items = items });
        }

        return groups;
    }

    /// <summary>`(s.name + ' ' + entries.map(key=value))`: строка поиска секции в нижнем регистре.</summary>
    public static string SearchText(LtxSection? section) => section == null
        ? ""
        : (section.Name + " " + string.Join(" ", section.Entries.Select(entry => entry.Key + "=" + entry.Value)))
            .ToLowerInvariant();

    /// <summary>Карта секций по имени в нижнем регистре (`itemsByName` в JS).</summary>
    public static Dictionary<string, LtxSection> MapByName(IEnumerable<LtxSection>? sections)
    {
        var map = new Dictionary<string, LtxSection>(StringComparer.Ordinal);
        foreach (var section in sections ?? Enumerable.Empty<LtxSection>())
        {
            if (section == null || section.Name.Length == 0) continue;
            map[section.Name.ToLowerInvariant()] = section;
        }
        return map;
    }

    /// <summary>Секция без родителей: в JS такая секция заблокирована до «Разблокировать».</summary>
    public static bool IsRoot(LtxSection? section) => section == null || section.Parents.Count == 0;

    /// <summary>`mergedItemEntries(selSection, itemsByName)` — порт уже есть в <see cref="LtxInheritance"/>.</summary>
    public static List<MergedEntry> MergedEntries(LtxSection? section, IDictionary<string, LtxSection>? map) =>
        LtxInheritance.MergedEntries(section, map);

    /// <summary>`selSection.parents.map(...)` с сохранением порядка и отметкой существования.</summary>
    public static List<(string Name, bool Exists)> Parents(
        LtxSection? section, IDictionary<string, LtxSection>? map)
    {
        var result = new List<(string Name, bool Exists)>();
        foreach (var parent in section?.Parents ?? new List<string>())
        {
            var exists = map != null && map.ContainsKey((parent ?? "").ToLowerInvariant());
            result.Add((parent ?? "", exists));
        }
        return result;
    }

    /// <summary>`brokenParents`: родители, которых нет среди секций файла.</summary>
    public static List<string> BrokenParents(LtxSection? section, IDictionary<string, LtxSection>? map) =>
        Parents(section, map).Where(parent => !parent.Exists).Select(parent => parent.Name).ToList();

    /// <summary>
    /// `overridden`: собственное поле, значение которого отличается от такого же поля
    /// у любого прямого родителя (в JS подпись «✏️» и янтарная рамка).
    /// </summary>
    public static bool IsOverridden(
        LtxSection? section, MergedEntry? entry, IDictionary<string, LtxSection>? map)
    {
        if (section == null || entry == null || entry.Inherited || map == null) return false;
        foreach (var parentName in section.Parents)
        {
            if (!map.TryGetValue((parentName ?? "").ToLowerInvariant(), out var parent)) continue;
            if (parent.Entries.Any(item =>
                    string.Equals(item.Key, entry.Key, StringComparison.Ordinal) &&
                    !string.Equals(item.Value, entry.Value, StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// `ltxInheritanceTree` + цикл в JS: цепочка от корня до выбранной секции по первому
    /// родителю (не более 50 шагов); отсутствующий родитель цепочку обрывает.
    /// </summary>
    public static List<string> InheritanceChain(LtxSection? section, IDictionary<string, LtxSection>? map)
    {
        var chain = new List<string>();
        if (section == null || map == null) return chain;

        var current = section;
        var guard = 0;
        while (current != null && guard++ < 50)
        {
            chain.Insert(0, current.Name);
            var parentName = current.Parents.FirstOrDefault();
            if (string.IsNullOrEmpty(parentName) ||
                !map.TryGetValue(parentName.ToLowerInvariant(), out var parent)) break;
            current = parent;
        }
        return chain;
    }

    /// <summary>Дети секции из `ltxInheritanceTree` без самоссылки.</summary>
    public static List<string> Children(string name, LtxInheritanceTree? tree)
    {
        if (tree == null || !tree.Children.TryGetValue(name, out var children)) return new List<string>();
        return children
            .Where(child => !string.Equals(child, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>`sections.filter(...)`: секции, ссылающиеся на выбранную в полях <see cref="ReferenceKeys"/>.</summary>
    public static List<LtxSection> ReferencingSections(IEnumerable<LtxSection>? sections, string? target)
    {
        var result = new List<LtxSection>();
        if (string.IsNullOrEmpty(target)) return result;
        var wanted = target.ToLowerInvariant();

        foreach (var section in sections ?? Enumerable.Empty<LtxSection>())
        {
            if (section == null ||
                string.Equals(section.Name, target, StringComparison.OrdinalIgnoreCase)) continue;
            if (section.Entries.Any(entry => ReferenceKeys.Contains(entry.Key, StringComparer.Ordinal) &&
                                             string.Equals((entry.Value ?? "").Trim().ToLowerInvariant(),
                                                 wanted, StringComparison.Ordinal)))
                result.Add(section);
        }
        return result;
    }

    /// <summary>
    /// `setItemField`: значение строки заменяется, комментарий после первого `;` сохраняется
    /// (`key = value` + ` ' ' + comment`).
    /// </summary>
    public static string SetField(string text, int lineNo, string key, string? value)
    {
        var lines = LtxTextTools.SplitLines(text);
        var original = lineNo - 1 >= 0 && lineNo - 1 < lines.Count ? lines[lineNo - 1] : "";
        var commentIndex = original.IndexOf(';');
        var comment = commentIndex >= 0 ? " " + original[commentIndex..] : "";
        return LtxTextTools.UpdateLine(text, lineNo, key + " = " + (value ?? "") + comment);
    }

    /// <summary>`datalist`: до 30 уникальных непустых значений поля из всех секций.</summary>
    public static List<string> ValueSuggestions(IEnumerable<LtxSection>? sections, string key)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var values = new List<string>();
        foreach (var section in sections ?? Enumerable.Empty<LtxSection>())
        {
            foreach (var entry in section.Entries)
            {
                if (!string.Equals(entry.Key, key, StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(entry.Value) || !seen.Add(entry.Value)) continue;
                values.Add(entry.Value);
                if (values.Count >= 30) return values;
            }
        }
        return values;
    }

    /// <summary>`new_item_` + 4 base36-символа (как `Math.random().toString(36).slice(2, 6)`), без коллизий.</summary>
    public static string NewSectionName(string? text)
    {
        var existing = new HashSet<string>(
            LtxParser.Parse(text).Sections.Select(section => section.Name.ToLowerInvariant()),
            StringComparer.Ordinal);

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var value = Random.Shared.Next(36 * 36 * 36 * 36);
            var suffix = ToBase36(value).PadLeft(4, '0');
            var name = "new_item_" + suffix;
            if (!existing.Contains(name)) return name;
        }

        return "new_item_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// `exportItemsCsv`: строки `section;key;value`; ячейка цитируется, если содержит `"`,
    /// `\`, перевод строки, `,` или `;` (как `csvCell` в App.js).
    /// </summary>
    public static string BuildCsv(IEnumerable<LtxSection>? sections)
    {
        var rows = new List<string> { CsvCell("section") + ";" + CsvCell("key") + ";" + CsvCell("value") };
        foreach (var section in sections ?? Enumerable.Empty<LtxSection>())
        {
            foreach (var entry in section.Entries)
                rows.Add(CsvCell(section.Name) + ";" + CsvCell(entry.Key) + ";" + CsvCell(entry.Value));
        }
        return string.Join("\n", rows);
    }

    /// <summary>Одна ячейка CSV: кавычки и обратный слэш экранируются удвоением.</summary>
    public static string CsvCell(string? value)
    {
        var text = value ?? "";
        var needsQuotes = text.IndexOfAny(new[] { '"', '\\', '\n', '\r', ',', ';' }) >= 0;
        return needsQuotes ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    /// <summary>`value.toString(36)` для неотрицательного числа.</summary>
    private static string ToBase36(int value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value <= 0) return "0";
        var builder = new StringBuilder();
        while (value > 0)
        {
            builder.Insert(0, digits[value % 36]);
            value /= 36;
        }
        return builder.ToString();
    }
}
