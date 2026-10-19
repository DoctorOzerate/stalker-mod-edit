using System.Text.Json;
using System.Text.RegularExpressions;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Dialogs;

/// <summary>
/// Использование одного <c>text_id</c>: число реплик и наличие ссылки в файлах проекта.
/// </summary>
/// <param name="Phrases">Сколько раз id встречается в PHRASE-узлах (с учётом активного графа).</param>
/// <param name="InProjectFiles">Есть ли точное совпадение в LTX, скриптах, квестах, описаниях и т. п.</param>
public sealed record TextIdUsage(int Phrases, bool InProjectFiles)
{
    /// <summary>Id используется хотя бы одним способом.</summary>
    public bool IsUsed => Phrases > 0 || InProjectFiles;
}

/// <summary>
/// Собирает ссылки на <c>text_id</c> по проекту. В отличие от старого счётчика
/// (только PHRASE-узлы) учитывает также значения LTX, строковые литералы Lua,
/// описания персонажей, квесты, энциклопедию и поля из <c>Extra</c>.
/// </summary>
public static class TextIdUsageCatalog
{
    private static readonly Regex ScriptStringPattern = new(
        "\"((?:\\\\.|[^\"\\\\])*)\"|'((?:\\\\.|[^'\\\\])*)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Один проход по проекту: возвращает карту всех найденных id. Ключи сравниваются
    /// с учётом регистра — так же, как в игре и в legacy-версии редактора.
    /// </summary>
    /// <param name="project">Проект для анализа; <c>null</c> даёт пустую карту.</param>
    /// <param name="knownIds">
    /// Необязательный набор id таблиц перевода. Если задан, ссылки из файлов собираются
    /// только для него — так вкладка «Языки» не держит в памяти все строки LTX и Lua.
    /// </param>
    public static Dictionary<string, TextIdUsage> Analyze(
        Project? project,
        IEnumerable<string>? knownIds = null)
    {
        if (project == null) return new Dictionary<string, TextIdUsage>(StringComparer.Ordinal);

        var filter = knownIds == null
            ? null
            : new HashSet<string>(knownIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);

        var phrases = new Dictionary<string, int>(StringComparer.Ordinal);
        CountPhrases(project, phrases);

        var files = new HashSet<string>(StringComparer.Ordinal);
        CollectFileReferences(project, files, filter);

        var result = new Dictionary<string, TextIdUsage>(StringComparer.Ordinal);
        foreach (var pair in phrases)
            result[pair.Key] = new TextIdUsage(pair.Value, files.Contains(pair.Key));
        foreach (var id in files)
        {
            if (!result.ContainsKey(id)) result[id] = new TextIdUsage(0, true);
        }

        return result;
    }

    /// <summary>
    /// PHRASE-узлы активного графа и импортированных диалогов. Активный граф часто
    /// продублирован в <c>dialogues[]</c>, поэтому его id пропускается, если узлы
    /// уже посчитаны из <see cref="Project.Nodes"/>.
    /// </summary>
    private static void CountPhrases(Project project, IDictionary<string, int> counts)
    {
        var seenGraphs = new HashSet<string>(StringComparer.Ordinal);
        if (project.Nodes is { Count: > 0 } && !string.IsNullOrEmpty(project.DialogueId))
            seenGraphs.Add(project.DialogueId);

        CountNodes(project.Nodes, counts);

        foreach (var dialogue in project.Dialogues ?? Enumerable.Empty<DialogueGraph>())
        {
            if (dialogue == null) continue;
            if (!string.IsNullOrEmpty(dialogue.Id) && !seenGraphs.Add(dialogue.Id)) continue;
            CountNodes(dialogue.Nodes, counts);
        }
    }


    /// <summary>Значения из файлов проекта (не сами ключи LTX и не служебные поля).</summary>
    private static void CollectFileReferences(
        Project project,
        ISet<string> references,
        ISet<string>? filter)
    {
        AddLtx(project.ItemsLtx, references, filter);
        AddLtx(project.SystemLtx, references, filter);
        AddLtxMap(project.LtxFiles?.Values, references, filter);
        AddLtxMap(project.ItemFiles?.Values, references, filter);

        foreach (var script in project.Scripts ?? Enumerable.Empty<ScriptFile>())
        {
            if (script == null) continue;
            AddScriptLiterals(script.Text, references, filter);
            AddScriptLiterals(script.Original, references, filter);
            AddJsonMap(script.Extra, references, filter);
        }

        foreach (var character in project.Characters ?? Enumerable.Empty<Character>())
        {
            if (character == null) continue;
            AddExact(character.Name, references, filter);
            AddExact(character.Bio, references, filter);
            AddJson(character.Original, references, filter);
            AddJsonMap(character.Extra, references, filter);
        }

        foreach (var quest in project.Quests ?? Enumerable.Empty<Quest>())
        {
            if (quest == null) continue;
            AddExact(quest.Title, references, filter);
            AddExact(quest.Text, references, filter);
            AddExact(quest.Article, references, filter);
            foreach (var objective in quest.Objectives ?? Enumerable.Empty<QuestObjective>())
            {
                if (objective == null) continue;
                AddExact(objective.Text, references, filter);
                AddExact(objective.Article, references, filter);
            }
            AddJson(quest.Original, references, filter);
            AddJsonMap(quest.Extra, references, filter);
        }

        foreach (var article in project.Encyclopedia ?? Enumerable.Empty<EncyclopediaArticle>())
        {
            if (article == null) continue;
            AddExact(article.Name, references, filter);
            AddExact(article.Ltx, references, filter);
            AddExact(article.Text, references, filter);
        }

        AddGraphExtras(project.Nodes, references, filter);
        AddGraphExtras(project.Edges, references, filter);
        foreach (var dialogue in project.Dialogues ?? Enumerable.Empty<DialogueGraph>())
        {
            if (dialogue == null) continue;
            AddJsonMap(dialogue.Extra, references, filter);
            AddGraphExtras(dialogue.Nodes, references, filter);
            AddGraphExtras(dialogue.Edges, references, filter);
        }

        AddJson(project.GeneralDesc, references, filter);
        AddJsonMap(project.Extra, references, filter);
    }

    /// <summary>Незнакомые поля узлов/связей: в них тоже могут лежать ссылки на перевод.</summary>
    private static void AddGraphExtras(
        IEnumerable<DialogueNode>? nodes,
        ISet<string> references,
        ISet<string>? filter)
    {
        if (nodes == null) return;
        foreach (var node in nodes)
        {
            if (node == null) continue;
            AddJsonMap(node.Extra, references, filter);
            AddJsonMap(node.Data?.Extra, references, filter);
        }
    }

    private static void AddGraphExtras(
        IEnumerable<DialogueEdge>? edges,
        ISet<string> references,
        ISet<string>? filter)
    {
        if (edges == null) return;
        foreach (var edge in edges)
        {
            if (edge == null) continue;
            AddJsonMap(edge.Extra, references, filter);
        }
    }

    private static void AddLtxMap(
        IEnumerable<string>? texts,
        ISet<string> references,
        ISet<string>? filter)
    {
        if (texts == null) return;
        foreach (var text in texts) AddLtx(text, references, filter);
    }

    /// <summary>
    /// Значение LTX после знака «=». Комментарий (после «;» вне кавычек) и кавычки
    /// отбрасываются: в игре <c>inv_name = st_item_name</c> — ссылка на запись таблицы.
    /// </summary>
    private static void AddLtx(string? text, ISet<string> references, ISet<string>? filter)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var value = LtxValue(line);
            AddReference(value, references, filter);
        }
    }

    private static string LtxValue(string line)
    {
        var equals = -1;
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }
            if (c == ';') break;
            if (c == '=')
            {
                equals = i;
                break;
            }
        }

        if (equals < 0 || equals + 1 >= line.Length) return "";
        return CleanValue(StripLtxComment(line[(equals + 1)..]));
    }

    private static string StripLtxComment(string value)
    {
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '"' or '\'') quote = c;
            else if (c == ';') return value[..i];
        }
        return value;
    }

    private static void AddScriptLiterals(
        string? text,
        ISet<string> references,
        ISet<string>? filter)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (Match match in ScriptStringPattern.Matches(text))
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            AddReference(value, references, filter);
        }
    }

    private static void AddJson(
        JsonElement? element,
        ISet<string> references,
        ISet<string>? filter)
    {
        if (element is not { } value) return;
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AddExact(value.GetString(), references, filter);
                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                    AddJson(property.Value, references, filter);
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                    AddJson(item, references, filter);
                break;
        }
    }

    private static void AddJsonMap(
        Dictionary<string, JsonElement>? map,
        ISet<string> references,
        ISet<string>? filter)
    {
        if (map == null) return;
        foreach (var value in map.Values) AddJson(value, references, filter);
    }

    private static void AddExact(string? value, ISet<string> references, ISet<string>? filter)
    {
        AddReference(CleanValue(value), references, filter);
    }

    private static void AddReference(string? value, ISet<string> references, ISet<string>? filter)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (filter != null && !filter.Contains(value)) return;
        references.Add(value);
    }

    private static string CleanValue(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length < 2) return text;

        var first = text[0];
        var last = text[^1];
        if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            return text[1..^1].Trim();
        return text;
    }

    private static void CountNodes(IEnumerable<DialogueNode>? nodes, IDictionary<string, int> counts)
    {
        if (nodes == null) return;
        foreach (var node in nodes)
        {
            if (node == null || node.Type != NodeTypes.Phrase) continue;
            var id = (node.Data?.TextId ?? "").Trim();
            if (id.Length == 0) continue;
            counts.TryGetValue(id, out var count);
            counts[id] = count + 1;
        }
    }
}

