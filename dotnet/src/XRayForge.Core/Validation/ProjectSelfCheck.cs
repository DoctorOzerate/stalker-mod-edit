using XRayForge.Core.Dialogs;
using XRayForge.Core.Export;
using XRayForge.Core.Graph;
using XRayForge.Core.Ltx;
using XRayForge.Core.Lua;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Quests;
using XRayForge.Core.Text;

namespace XRayForge.Core.Validation;

public sealed class SelfCheckItem
{
    public string Name { get; set; } = "";
    public bool Ok { get; set; }
    public string Details { get; set; } = "";
    public string Status => Ok ? "✓" : "✗";
}

public sealed class SelfCheckReport
{
    public List<SelfCheckItem> Items { get; set; } = new();
    public int FailedCount => Items.Count(item => !item.Ok);
    public bool Passed => FailedCount == 0;
    public string Summary => (Passed ? "OK" : "FAIL") + " · " +
        (Items.Count - FailedCount) + "/" + Items.Count;
}

/// <summary>
/// Встроенная самопроверка проекта (этап 8): целостность ссылок, граф диалогов,
/// LTX/инклюды, Lua-скрипты, квесты, локализации, кодировка и сборка игрового ZIP.
/// Результат — список проверок для окна «Самопроверка»; xUnit-набор остаётся
/// независимым и покрывает Core-модули.
/// </summary>
public static class ProjectSelfCheck
{
    public static SelfCheckReport Run(Project? project)
    {
        var report = new SelfCheckReport();

        Check(report, "Проект", project != null, project == null ? "Проект не открыт" : "OK");
        if (project == null) return report;

        var integrity = ProjectIntegrity.Validate(project);
        Check(report, "Ссылки между вкладками", integrity.Count == 0,
            integrity.Count == 0 ? "OK" : integrity.Count + " проблем: " +
                string.Join("; ", integrity.Take(3).Select(i => i.Message)));

        var graphIssues = new List<GraphIssue>();
        if (project.Nodes.Count > 0 || project.Edges.Count > 0)
            graphIssues.AddRange(GraphValidator.Validate(project.Nodes, project.Edges));
        foreach (var dialogue in project.Dialogues)
            if (dialogue.Nodes.Count > 0 || dialogue.Edges.Count > 0)
                graphIssues.AddRange(GraphValidator.Validate(dialogue.Nodes, dialogue.Edges));
        var graphErrors = graphIssues.Count(issue => issue.Severity == "error");
        Check(report, "Граф диалогов", graphErrors == 0,
            graphErrors == 0 ? "OK" : graphErrors + " ошибок графа");

        var sections = SectionMap(project);
        var duplicateSections = LtxInheritance.FindDuplicateSections(sections);
        var cycles = LtxInheritance.FindInheritanceCycles(project.SystemLtx + "\n" + project.ItemsLtx);
        var refs = LtxInheritance.ValidateRefs(project.SystemLtx + "\n" + project.ItemsLtx, sections);
        var brokenIncludes = LtxParser.Parse(project.SystemLtx).Includes
            .Count(include => !LtxIncludes.IsFileLoaded(include.Path, project.LtxFiles));
        var ltxOk = duplicateSections.Count == 0 && cycles.Count == 0 && refs.Count == 0 && brokenIncludes == 0;
        Check(report, "LTX и инклюды", ltxOk, ltxOk
            ? "OK"
            : "Дубликаты: " + duplicateSections.Count + ", циклы: " + cycles.Count +
              ", ссылки: " + refs.Count + ", инклюды: " + brokenIncludes);

        var luaErrors = 0;
        var luaDetails = new List<string>();
        foreach (var script in project.Scripts ?? new List<ScriptFile>())
        {
            var errors = LuaAnalyzer.Check(script.Text).Count(issue => issue.Severity == "error");
            var brackets = LuaTokenizer.CountBrackets(script.Text);
            if (brackets.Open != brackets.Close)
            {
                luaDetails.Add(script.Name + ": скобки " + brackets.Open + "/" + brackets.Close);
                errors++;
            }
            luaErrors += errors;
        }
        Check(report, "Lua-скрипты", luaErrors == 0,
            luaErrors == 0 ? "OK" : string.Join("; ", luaDetails.Take(4)));

        var questIssues = QuestAnalysis.Validate(project.Quests, project.Languages);
        var questErrors = questIssues.Count(issue => issue.Severity == "error");
        Check(report, "Квесты", questErrors == 0,
            questErrors == 0 ? "OK" : questErrors + " ошибок: " +
                string.Join("; ", questIssues.Where(i => i.Severity == "error").Take(3).Select(i => i.Message)));

        var languageIssues = TextIdTools.FindTextIdIssues(project.Languages, project.Dialogues);
        var duplicateIds = languageIssues.Count(issue => issue.Message.StartsWith("Дубликат", StringComparison.Ordinal));
        var emptyTranslations = languageIssues.Count(issue => issue.Message.StartsWith("Пустой", StringComparison.Ordinal));
        Check(report, "Локализации", duplicateIds == 0,
            duplicateIds == 0 ? "Пустых переводов: " + emptyTranslations : "Дубликатов id: " + duplicateIds);

        const string probe = "Привет, Сталкер";
        var encoded = GameEncoding.EncodeCp1251(probe);
        var decoded = GameEncoding.DecodeCp1251(encoded, 0, encoded.Length);
        Check(report, "Windows-1251", decoded == probe, decoded == probe ? "OK" : "Ошибка round-trip");

        var package = GamePackage.TextFiles(project);
        var hasContent = project.Nodes.Count > 0 || project.Dialogues.Count > 0 ||
                         project.Quests.Count > 0 || project.SystemLtx.Length > 0 ||
                         (project.Scripts?.Count ?? 0) > 0;
        Check(report, "Игровой пакет", !hasContent || package.Count > 0, package.Count + " файлов");
        return report;
    }

    private static void Check(SelfCheckReport report, string name, bool ok, string details) =>
        report.Items.Add(new SelfCheckItem { Name = name, Ok = ok, Details = details });

    private static Dictionary<string, List<string>> SectionMap(Project project)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string? text, string file)
        {
            foreach (var section in LtxParser.Parse(text).Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Name)) continue;
                if (!map.TryGetValue(section.Name, out var files))
                    map[section.Name] = files = new List<string>();
                if (!files.Contains(file)) files.Add(file);
            }
        }

        Add(project.SystemLtx, "system.ltx");
        Add(project.ItemsLtx, "items.ltx");
        foreach (var pair in project.LtxFiles ?? new Dictionary<string, string>()) Add(pair.Value, pair.Key);
        return map;
    }
}
