using System.Text.RegularExpressions;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Core.Xml;

/// <summary>
/// Итог импорта файла `game_dialogs` (шаг 6.8): добавленные в проект диалоги, пропущенные
/// (их id уже есть в проекте) и файл-источник для `sourcePath`/`sourceFile`.
/// </summary>
public sealed class DialogueImportResult
{
    internal DialogueImportResult(string? sourcePath, string? sourceFile)
    {
        SourcePath = sourcePath;
        SourceFile = sourceFile;
    }

    /// <summary>Путь импортированного файла (в JS — `group.path` из сканирования gamedata).</summary>
    public string? SourcePath { get; }

    /// <summary>Имя импортированного файла.</summary>
    public string? SourceFile { get; }

    /// <summary>Диалоги, которые нужно добавить в `project.dialogues`.</summary>
    public List<DialogueGraph> Imported { get; } = new();

    /// <summary>Id диалогов, пропущенных как уже имеющиеся (свой id или дубль внутри файла).</summary>
    public List<string> Skipped { get; } = new();
}

/// <summary>Импорт диалогов из XML с нормализацией и раскладкой (порт xml.js parseDialogueXml).</summary>
public static class DialogueImporter
{
    /// <summary>Строка `#include` в X-Ray XML — разбору мешает (в JS снимается перед разбором).</summary>
    private static readonly Regex IncludeLine = new(@"^\s*#include\b[^\n]*$", RegexOptions.Multiline);

    /// <summary>
    /// Пустой диалог: ни одной реплики (`phrase`). В `dialogs.xml` такие `&lt;dialog&gt;` служебные
    /// (`dm_*` с `precondition`/`action` на самом диалоге), граф из них не строится, поэтому в проект
    /// они не загружаются.
    /// </summary>
    public static bool IsEmpty(DialogueGraph? dialog) =>
        dialog == null || !dialog.Nodes.Any(node => node != null && node.Type == NodeTypes.Phrase);

    public static List<DialogueGraph> Import(string? text)
    {
        var result = new List<DialogueGraph>();
        foreach (var dialog in GraphData.FlattenDialogues(XmlParsers.ParseDialogueXml(text)))
        {
            for (var i = 0; i < dialog.Nodes.Count; i++)
                dialog.Nodes[i] = GraphData.SafeNode(dialog.Nodes[i], i);

            foreach (var n in dialog.Nodes)
            {
                if (n.Type != NodeTypes.Phrase || n.Data == null) continue;
                if (n.Data.RoleSource.Length == 0)
                {
                    n.Data.Role = "unknown";
                    n.Data.RoleSource = "unknown";
                }
            }

            GraphLayout.LayoutDialogue(dialog.Nodes, dialog.Edges.Where(e => !e.Broken).ToList());
            result.Add(dialog);
        }

        return result;
    }
    /// <summary>
    /// Импорт одного файла `game_dialogs*.xml` в список диалогов проекта (шаг 6.8): строки
    /// `#include` снимаются как при сканировании gamedata, узлы нормализуются и раскладываются
    /// (<see cref="Import"/>), уже загруженные id пропускаются, а источник пишется в
    /// `sourcePath`/`sourceFile` (в JS эти поля ставит скан gamedata).
    /// </summary>
    /// <param name="text">Текст файла (декодированный в кодировке проекта).</param>
    /// <param name="existingIds">Id диалогов, уже загруженных в проект.</param>
    /// <param name="sourcePath">Путь файла-источника.</param>
    /// <param name="sourceFile">Имя файла-источника.</param>
    public static DialogueImportResult ImportFile(
        string? text, IEnumerable<string>? existingIds, string? sourcePath = null, string? sourceFile = null)
    {
        var result = new DialogueImportResult(sourcePath, sourceFile);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in existingIds ?? Enumerable.Empty<string>())
            if (!string.IsNullOrEmpty(id)) seen.Add(id);

        var cleaned = IncludeLine.Replace(text ?? string.Empty, string.Empty);
        foreach (var dialog in Import(cleaned))
        {
            // Служебные диалоги без реплик (`dm_*` в dialogs.xml) не подгружаем: в редакторе
            // нечего показывать, а список забивается пустыми графами.
            if (IsEmpty(dialog)) continue;

            var key = dialog.Id.Length > 0 ? dialog.Id : dialog.Name;
            if (key.Length > 0 && !seen.Add(key))
            {
                result.Skipped.Add(key);
                continue;
            }

            dialog.SourcePath = sourcePath;
            dialog.SourceFile = sourceFile;
            result.Imported.Add(dialog);
        }

        return result;
    }


}