namespace XRayForge.Core.Ltx;

/// <summary>Поле секции с учётом унаследованных значений (mergedItemEntries).</summary>
public sealed class MergedEntry
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public bool Inherited { get; set; }
    public string? From { get; set; }
    public int Line { get; set; }
}

/// <summary>Диагностика LTX (validateLtxRefs / findInheritanceCycles).</summary>
public sealed class LtxIssue
{
    public string Severity { get; set; } = "";
    public string Code { get; set; } = "";
    public string Section { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>Секция, найденная в нескольких файлах (findDuplicateSections).</summary>
public sealed class DuplicateSection
{
    public string Name { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

/// <summary>Дерево наследования секций: корни и карта «родитель → дети» (#3).</summary>
public sealed class LtxInheritanceTree
{
    public List<string> Roots { get; set; } = new();
    public Dictionary<string, List<string>> Children { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Наследование и проверки LTX — порт `mergedItemEntries`, `ltxInheritanceTree`,
/// `validateLtxRefs`, `findDuplicateSections`, `findInheritanceCycles`.
/// </summary>
public static class LtxInheritance
{
    /// <summary>
    /// mergedItemEntries: поля секции вместе с унаследованными (глубина — в ширину родителей).
    /// Ключи в <paramref name="sectionsMap"/> — в нижнем регистре; поздние значения
    /// (свои поля) перекрывают унаследованные, порядок первого появления сохраняется.
    /// </summary>
    public static List<MergedEntry> MergedEntries(LtxSection? sec, IDictionary<string, LtxSection>? sectionsMap)
    {
        var order = new List<string>();
        var map = new Dictionary<string, MergedEntry>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var sections = sectionsMap ?? new Dictionary<string, LtxSection>();

        void Walk(string? name, bool inherited, string? source)
        {
            if (string.IsNullOrEmpty(name)) return;
            var key = name.ToLowerInvariant();
            if (visited.Contains(key)) return;
            if (!sections.TryGetValue(key, out var s)) return;
            visited.Add(key);

            foreach (var p in s.Parents) Walk(p, true, p);
            foreach (var e in s.Entries)
            {
                var entry = new MergedEntry
                {
                    Key = e.Key,
                    Value = e.Value,
                    Inherited = inherited,
                    From = inherited ? source : null,
                    Line = e.Line
                };
                if (!map.ContainsKey(e.Key)) order.Add(e.Key);
                map[e.Key] = entry;
            }
        }

        Walk(sec?.Name, false, null);
        return order.Select(k => map[k]).ToList();
    }

    /// <summary>ltxInheritanceTree: корни (без родителей) и карта «родитель → дети».</summary>
    public static LtxInheritanceTree InheritanceTree(string? text)
    {
        var sections = LtxParser.Parse(text).Sections;
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var roots = new List<string>();

        foreach (var s in sections)
        {
            if (s.Name.Length == 0) continue;
            if (s.Parents.Count == 0)
            {
                if (!roots.Contains(s.Name)) roots.Add(s.Name);
                continue;
            }

            foreach (var p in s.Parents)
            {
                if (!children.TryGetValue(p, out var list))
                {
                    list = new List<string>();
                    children[p] = list;
                }
                if (!list.Contains(s.Name)) list.Add(s.Name);
            }
        }

        return new LtxInheritanceTree { Roots = roots, Children = children };
    }

    /// <summary>
    /// validateLtxRefs: ссылки на несуществующие секции в родителях и в полях
    /// ammo_class / grenade_class / ammo_box_class / scope_name (#19, #34).
    /// </summary>
    public static List<LtxIssue> ValidateRefs(string? text, IEnumerable<string>? knownSections)
    {
        var known = new HashSet<string>(
            (knownSections ?? Enumerable.Empty<string>()).Select(s => (s ?? "").ToLowerInvariant()),
            StringComparer.Ordinal);

        var issues = new List<LtxIssue>();
        foreach (var sec in LtxParser.Parse(text).Sections)
        {
            foreach (var p in sec.Parents)
            {
                if (p.Length > 0 && !known.Contains(p.ToLowerInvariant()))
                {
                    issues.Add(new LtxIssue
                    {
                        Severity = "warning",
                        Code = "ltx_ref_parent",
                        Section = sec.Name,
                        Message = "[" + sec.Name + "]: родитель не найден — " + p
                    });
                }
            }

            foreach (var en in sec.Entries)
            {
                if (en.Key != "ammo_class" && en.Key != "grenade_class" &&
                    en.Key != "ammo_box_class" && en.Key != "scope_name") continue;

                foreach (var v in en.Value.Split(','))
                {
                    var vv = v.Trim();
                    if (vv.Length == 0 || known.Contains(vv.ToLowerInvariant())) continue;
                    issues.Add(new LtxIssue
                    {
                        Severity = "warning",
                        Code = "ltx_ref_value",
                        Section = sec.Name,
                        Message = "[" + sec.Name + "]: " + en.Key + " не найден — " + vv
                    });
                }
            }
        }

        return issues;
    }

    /// <summary>validateLtxRefs по карте секций {имя: [файлы]} — проверяются её ключи.</summary>
    public static List<LtxIssue> ValidateRefs(string? text, IDictionary<string, List<string>>? sectionMap) =>
        ValidateRefs(text, (sectionMap ?? new Dictionary<string, List<string>>()).Keys);

    /// <summary>findDuplicateSections: сводка дубликатов секций по карте {имя: [файлы]}.</summary>
    public static List<DuplicateSection> FindDuplicateSections(IDictionary<string, List<string>>? sectionMap)
    {
        var outList = new List<DuplicateSection>();
        foreach (var kv in sectionMap ?? new Dictionary<string, List<string>>())
        {
            var files = kv.Value ?? new List<string>();
            if (files.Count > 1)
                outList.Add(new DuplicateSection { Name = kv.Key, Files = new List<string>(files) });
        }

        outList.Sort((a, b) =>
        {
            var byCount = b.Files.Count.CompareTo(a.Files.Count);
            return byCount != 0 ? byCount : string.CompareOrdinal(a.Name, b.Name);
        });
        return outList;
    }

    /// <summary>findInheritanceCycles: циклы наследования по первому родителю (#33).</summary>
    public static List<LtxIssue> FindInheritanceCycles(string? text)
    {
        var sections = LtxParser.Parse(text).Sections;
        var parents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var s in sections)
            parents[s.Name.ToLowerInvariant()] = s.Parents.Select(p => p.ToLowerInvariant()).ToList();

        var issues = new List<LtxIssue>();
        foreach (var s in sections)
        {
            var key = s.Name.ToLowerInvariant();
            var chain = new List<string> { key };
            var seen = new HashSet<string>(StringComparer.Ordinal) { key };
            var cur = key;
            var guard = 0;

            while (parents.TryGetValue(cur, out var parentsOf) && parentsOf.Count > 0 && guard++ < 200)
            {
                var p = parentsOf[0];
                if (seen.Contains(p))
                {
                    issues.Add(new LtxIssue
                    {
                        Severity = "error",
                        Code = "ltx_inherit_cycle",
                        Section = s.Name,
                        Message = "Цикл наследования: " + string.Join(" → ", chain.Concat(new[] { p }))
                    });
                    break;
                }

                seen.Add(p);
                chain.Add(p);
                cur = p;
            }
        }

        return issues;
    }
}