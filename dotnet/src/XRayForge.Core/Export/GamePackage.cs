using System.IO.Compression;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Projects;
using XRayForge.Core.Quests;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Export;

/// <summary>
/// Игровой пакет проекта: диалоги, string_table, квесты, system.ltx и скрипты
/// собираются в карту «путь в gamedata → текст» и упаковываются в ZIP без внешних
/// зависимостей (аналог `exportGameZip` в JS-версии).
/// </summary>
public static class GamePackage
{
    /// <summary>Все текстовые файлы экспорта (пути всегда начинаются с `gamedata/`).</summary>
    public static Dictionary<string, string> TextFiles(Project? project)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (project == null) return files;

        foreach (var pair in DialogueGameExport.GameFiles(project))
            files[NormalizePath(pair.Key)] = pair.Value;

        var config = GameSeriesCatalog.ConfigDirectory(project.Series);
        var encoding = project.Settings.ExportEncoding ?? "windows-1251";

        if (project.Quests.Count > 0)
        {
            if (project.Quests.Any(q => q.Format == "task_manager"))
            {
                files["gamedata/" + config + "/gameplay/task_manager.ltx"] =
                    QuestExport.TaskManagerLtx(project.Quests);
                files["gamedata/" + config + "/gameplay/task_manager.xml"] =
                    QuestExport.QuestsToXml(project.Quests, encoding);
            }
            else
            {
                files["gamedata/" + config + "/gameplay/game_tasks.xml"] =
                    QuestExport.QuestsToXml(project.Quests, encoding);
            }
        }

        if (!string.IsNullOrWhiteSpace(project.SystemLtx))
            files["gamedata/" + config + "/system.ltx"] = project.SystemLtx;

        foreach (var script in project.Scripts ?? new List<ScriptFile>())
        {
            if (string.IsNullOrWhiteSpace(script.Name) || string.IsNullOrEmpty(script.Text)) continue;
            var path = NormalizePath(script.Path);
            if (path.Length == 0 || !path.StartsWith("gamedata/", StringComparison.OrdinalIgnoreCase))
                path = "gamedata/scripts/" + NormalizePath(script.Name);
            files[path] = script.Text;
        }

        return files;
    }

    /// <summary>ZIP с игровыми файлами в кодировке экспорта проекта.</summary>
    public static byte[] Zip(Project? project)
    {
        var encoding = project?.Settings.ExportEncoding ?? "windows-1251";
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var pair in TextFiles(project))
            {
                var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                var bytes = GameEncoding.Encode(pair.Value, encoding);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Записать ZIP в файл; возвращает число файлов внутри архива.</summary>
    public static int WriteZip(Project? project, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Не указан путь ZIP", nameof(path));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var files = TextFiles(project);
        File.WriteAllBytes(path, Zip(project));
        return files.Count;
    }

    private static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
}
