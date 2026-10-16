using XRayForge.Core.Models;

namespace XRayForge.Core.Quests;

/// <summary>Валидация квестов и построение цепочек по инфопорциям (#37, #64).</summary>
public static class QuestAnalysis
{
    public static List<QuestIssue> Validate(IEnumerable<Quest?>? quests, IEnumerable<Language?>? languages)
    {
        var issues = new List<QuestIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var langs = languages?.Where(l => l != null).Select(l => l!).ToList() ?? new List<Language>();

        bool HasEntry(string id)
        {
            if (id.Length == 0) return false;
            foreach (var l in langs)
                if (l.Entries.Any(e => e.Id == id && e.Text.Length > 0)) return true;
            return false;
        }

        void Add(string quest, string severity, string msg) =>
            issues.Add(new QuestIssue { Quest = quest, Severity = severity, Message = msg });

        foreach (var q in quests ?? Enumerable.Empty<Quest?>())
        {
            if (q == null) continue;
            if (!seen.Add(q.Id)) Add(q.Id, "error", "Дубликат id: " + q.Id);

            if (q.Title.Trim().Length == 0) Add(q.Id, "warning", "Пустой заголовок (title)");
            if (q.Text.Trim().Length == 0) Add(q.Id, "warning", "Пустое описание (text)");
            if (q.Objectives.Count == 0) Add(q.Id, "warning", "Нет целей");
            if (q.Prior.Length > 0 && !IsDigits(q.Prior.Trim()))
                Add(q.Id, "error", "Приоритет не число: " + q.Prior);
            if (q.Title.Length > 0 && !HasEntry(q.Title))
                Add(q.Id, "warning", "Нет перевода title: " + q.Title);
            if (q.Text.Length > 0 && !HasEntry(q.Text))
                Add(q.Id, "warning", "Нет перевода text: " + q.Text);

            foreach (var o in q.Objectives)
            {
                var t = o.Text ?? "";
                if (t.Length > 0 && !HasEntry(t))
                    Add(q.Id, "warning", "Нет перевода цели: " + t);
            }
        }

        return issues;
    }

    private static bool IsDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s)
            if (c < '0' || c > '9') return false;
        return true;
    }

    /// <summary>
    /// Цепочки квестов: связь через requires → infoportion_complete предыдущего.
    /// Возвращает список путей (последовательности id) от корней до листьев.
    /// </summary>
    public static List<List<string>> ComputeChains(IEnumerable<Quest?>? quests)
    {
        var list = quests?.Where(q => q != null).Select(q => q!).ToList() ?? new List<Quest>();

        var completeTo = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var q in list)
            if (q.InfoportionComplete.Length > 0) completeTo[q.InfoportionComplete] = q.Id;

        var parentOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var q in list)
        {
            foreach (var r in q.Requires)
            {
                if (completeTo.TryGetValue(r, out var p) && p != q.Id) parentOf[q.Id] = p;
            }
        }

        var roots = new List<string>();
        foreach (var q in list)
            if (!parentOf.ContainsKey(q.Id)) roots.Add(q.Id);

        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var kv in parentOf)
        {
            if (!children.TryGetValue(kv.Value, out var kids))
                children[kv.Value] = kids = new List<string>();
            kids.Add(kv.Key);
        }

        var chains = new List<List<string>>();

        void Walk(string id, List<string> acc)
        {
            var path = new List<string>(acc) { id };
            if (!children.TryGetValue(id, out var kids) || kids.Count == 0)
            {
                chains.Add(path);
                return;
            }

            var before = chains.Count;
            foreach (var k in kids)
            {
                // В JS цикл приводит к переполнению стека; здесь просто пропускаем.
                if (path.Contains(k)) continue;
                Walk(k, path);
            }
            if (chains.Count == before) chains.Add(path);
        }

        foreach (var r in roots) Walk(r, new List<string>());
        return chains;
    }
}