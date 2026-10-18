using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Dialogs;

/// <summary>
/// Сборка игровых файлов диалогов из проекта (#1, #2, #4): активный граф
/// (<c>project.nodes/edges</c>) и загруженные диалоги (<c>project.dialogues</c>) раскладываются
/// по <c>dialogs_*.xml</c>, затем дополняются <c>dialogs.ltx</c> и таблицами переводов.
/// В отличие от JS-версии имена файлов берутся из <c>sourceFile</c> импортированных диалогов,
/// поэтому повторный экспорт не разбивает один исходный файл на несколько.
/// </summary>
public static class DialogueGameExport
{
    /// <summary>
    /// Карта «путь в gamedata → текст» для всех диалогов проекта. Кодировка и каталог
    /// (<c>config</c>/<c>configs</c>) определяются настройками и серией проекта.
    /// </summary>
    /// <param name="project">Проект редактора.</param>
    /// <param name="fileNames">Необязательные переопределения имён файлов по id диалога.</param>
    public static Dictionary<string, string> GameFiles(
        Project? project, IReadOnlyDictionary<string, string>? fileNames = null)
    {
        var groups = Groups(project, fileNames);
        if (groups.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);

        return DialogueFiles.GameFilesFromDialogues(
            groups,
            GameSeriesCatalog.ConfigDirectory(project?.Series),
            project?.Settings.ExportEncoding,
            project?.Languages,
            project?.ProjectName);
    }

    /// <summary>
    /// Диалоги проекта, сгруппированные по целевому <c>dialogs_*.xml</c>. Активный граф идёт первым
    /// (его правки важнее копии в списке), диалоги с уже встреченным id пропускаются, а
    /// <c>sourceFile</c> импортированного диалога сохраняет исходную группировку.
    /// </summary>
    public static List<DialogueFileGroup> Groups(
        Project? project, IReadOnlyDictionary<string, string>? fileNames = null)
    {
        var groups = new List<DialogueFileGroup>();
        if (project == null) return groups;

        var byFile = new Dictionary<string, DialogueFileGroup>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var dialogues = new List<DialogueGraph>();
        if (project.Nodes.Count > 0 || project.Edges.Count > 0)
        {
            var active = new DialogueGraph
            {
                Id = project.DialogueId,
                Name = project.ProjectName,
                Nodes = project.Nodes,
                Edges = project.Edges
            };

            // «Загруженный диалог» хранится в списке вместе с исходным файлом. Активный граф
            // содержит те же id, поэтому переносим sourceFile, чтобы правки вернулись в свой XML.
            var source = project.Dialogues.FirstOrDefault(d => IdOf(d) == IdOf(active));
            if (source != null)
            {
                active.SourceFile = source.SourceFile;
                active.SourcePath = source.SourcePath;
            }

            dialogues.Add(active);
        }

        dialogues.AddRange(project.Dialogues.Where(d => d != null));

        foreach (var dialogue in dialogues)
        {
            var id = IdOf(dialogue);
            if (id.Length > 0 && !seen.Add(id)) continue;

            var fileName = fileNames != null && id.Length > 0 &&
                           fileNames.TryGetValue(id, out var custom) &&
                           !string.IsNullOrWhiteSpace(custom)
                ? custom
                : SourceFileName(dialogue);
            fileName = DialogueFiles.SafeName(fileName);

            if (!byFile.TryGetValue(fileName, out var group))
            {
                group = new DialogueFileGroup { FileName = fileName };
                byFile[fileName] = group;
                groups.Add(group);
            }

            group.Dialogues.Add(dialogue);
        }

        return groups;
    }

    /// <summary>Абсолютные пути, которые будут записаны в выбранный каталог.</summary>
    public static List<string> TargetPaths(IReadOnlyDictionary<string, string>? files, string root)
    {
        var paths = new List<string>();
        if (files == null) return paths;

        foreach (var path in files.Keys) paths.Add(TargetPath(root, path));
        return paths;
    }

    /// <summary>
    /// Путь назначения для ключа карты (<c>gamedata/config/...</c>). Если пользователь выбрал сам
    /// каталог <c>gamedata</c>, префикс не дублируется — файл кладётся сразу в <c>config/...</c>.
    /// </summary>
    public static string TargetPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Не указан каталог экспорта", nameof(root));

        var normalizedRoot = Path.GetFullPath(root);
        var trimmedRoot = normalizedRoot
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = (relativePath ?? string.Empty)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var prefix = "gamedata" + Path.DirectorySeparatorChar;
        var rootName = trimmedRoot.Length > 0 ? Path.GetFileName(trimmedRoot) : "";
        if (rootName.Equals("gamedata", StringComparison.OrdinalIgnoreCase) &&
            relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring(prefix.Length);
        }

        // Combine с исходным (возможно, с завершающим разделителем) корнем: для `C:\`
        // обрезанный `C:` дал бы путь относительно текущего каталога диска.
        return Path.GetFullPath(Path.Combine(normalizedRoot, relative));
    }

    /// <summary>
    /// Записать карту файлов в каталог в кодировке проекта. Возвращает список созданных файлов;
    /// существующие файлы перезаписываются (подтверждение — на стороне UI).
    /// </summary>
    public static List<string> WriteFiles(
        IReadOnlyDictionary<string, string>? files, string root, string? encoding = "windows-1251")
    {
        var written = new List<string>();
        if (files == null) return written;

        foreach (var pair in files)
        {
            var path = TargetPath(root, pair.Key);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllBytes(path, GameEncoding.Encode(pair.Value, encoding));
            written.Add(path);
        }

        return written;
    }

    private static string IdOf(DialogueGraph? dialogue) =>
        dialogue == null ? string.Empty :
        !string.IsNullOrEmpty(dialogue.Id) ? dialogue.Id : dialogue.Name;

    /// <summary>Имя без `dialogs_` и расширения: `dialogs_bar.xml` → `bar`.</summary>
    private static string SourceFileName(DialogueGraph dialogue)
    {
        var source = dialogue.SourceFile;
        if (!string.IsNullOrWhiteSpace(source))
        {
            var name = Path.GetFileNameWithoutExtension(source);
            if (name.StartsWith("dialogs_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("dialogs_".Length);
            if (name.Length > 0) return name;
        }

        return IdOf(dialogue);
    }
}
