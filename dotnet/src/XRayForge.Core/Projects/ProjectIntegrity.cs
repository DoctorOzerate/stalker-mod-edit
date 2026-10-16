using XRayForge.Core.Ltx;
using XRayForge.Core.Models;

namespace XRayForge.Core.Projects;

/// <summary>Одна проблема целостности проекта: где и что не сходится.</summary>
public sealed class ProjectIntegrityIssue
{
    public string Where { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>
/// Проверка целостности проекта (порт `validateProjectIntegrity` из App.js):
/// битые ссылки между вкладками — text_id, start_dialog, квесты, `#include` system.ltx.
/// </summary>
public static class ProjectIntegrity
{
    public static List<ProjectIntegrityIssue> Validate(Project? project)
    {
        var issues = new List<ProjectIntegrityIssue>();
        if (project == null) return issues;

        var languageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var language in project.Languages)
            foreach (var entry in language.Entries)
                if (!string.IsNullOrEmpty(entry.Id)) languageIds.Add(entry.Id);

        var dialogueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dialogue in project.Dialogues)
        {
            if (!string.IsNullOrEmpty(dialogue.Id)) dialogueIds.Add(dialogue.Id);
            if (!string.IsNullOrEmpty(dialogue.Name)) dialogueIds.Add(dialogue.Name);
        }

        foreach (var node in project.Nodes)
        {
            var textId = node.Data?.TextId;
            if (node.Type == NodeTypes.Phrase && !string.IsNullOrEmpty(textId) && !languageIds.Contains(textId))
                issues.Add(new ProjectIntegrityIssue { Where = "Диалоги", Message = "text_id без перевода: " + textId });
        }

        foreach (var character in project.Characters)
        {
            var who = string.IsNullOrEmpty(character.Id) ? "?" : character.Id;
            foreach (var start in character.StartDialog)
                if (!dialogueIds.Contains(start))
                    issues.Add(new ProjectIntegrityIssue { Where = "Персонажи", Message = who + ": start_dialog не найден — " + start });
            foreach (var actor in character.ActorDialog)
                if (!dialogueIds.Contains(actor))
                    issues.Add(new ProjectIntegrityIssue { Where = "Персонажи", Message = who + ": actor_dialog не найден — " + actor });
        }

        foreach (var quest in project.Quests)
        {
            var ids = new List<string> { quest.Title, quest.Text };
            ids.AddRange(quest.Objectives.Select(o => o.Text));
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id) && !languageIds.Contains(id))
                    issues.Add(new ProjectIntegrityIssue
                    {
                        Where = "Квесты",
                        Message = (string.IsNullOrEmpty(quest.Id) ? "?" : quest.Id) + ": text_id без перевода — " + id
                    });
        }

        foreach (var include in LtxParser.Parse(project.SystemLtx).Includes)
            if (!LtxIncludes.IsFileLoaded(include.Path, project.LtxFiles))
                issues.Add(new ProjectIntegrityIssue { Where = "system.ltx", Message = "битый include: " + include.Path });

        return issues;
    }
}