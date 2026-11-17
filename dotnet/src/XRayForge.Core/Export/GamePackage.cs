using System.IO.Compression;
using XRayForge.Core.Characters;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Ltx;
using XRayForge.Core.Projects;
using XRayForge.Core.Quests;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Export;

/// <summary>
/// Игровой пакет проекта: диалоги, персонажи, item-файлы, string_table, квесты, system.ltx
/// и скрипты собираются в карту «путь в gamedata → текст» и упаковываются в ZIP без внешних
/// зависимостей (аналог `exportGameZip` в JS-версии). Ключи карты всегда относительны
/// `gamedata/`: абсолютные пути и `..` отбрасываются (см. <see cref="Put"/>).
/// </summary>
public static class GamePackage
{
    /// <summary>Все текстовые файлы экспорта (пути всегда начинаются с `gamedata/`).</summary>
    public static Dictionary<string, string> TextFiles(Project? project)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (project == null) return files;

        foreach (var pair in DialogueGameExport.GameFiles(project))
            Put(files, pair.Key, pair.Value);

        foreach (var pair in CharacterGameExport.GameFiles(project))
            Put(files, pair.Key, pair.Value);

        foreach (var pair in ItemGameExport.GameFiles(project))
            Put(files, pair.Key, pair.Value);

        var config = GameSeriesCatalog.ConfigDirectory(project.Series);
        var encoding = project.Settings.ExportEncoding ?? "windows-1251";

        if (project.Quests.Count > 0)
        {
            if (project.Quests.Any(q => q.Format == "task_manager"))
            {
                Put(files, "gamedata/" + config + "/gameplay/task_manager.ltx",
                    QuestExport.TaskManagerLtx(project.Quests));
                Put(files, "gamedata/" + config + "/gameplay/task_manager.xml",
                    QuestExport.QuestsToXml(project.Quests, encoding));
            }
            else
            {
                Put(files, "gamedata/" + config + "/gameplay/game_tasks.xml",
                    QuestExport.QuestsToXml(project.Quests, encoding));
            }
        }

        if (!string.IsNullOrWhiteSpace(project.SystemLtx))
            Put(files, "gamedata/" + config + "/system.ltx", project.SystemLtx);

        foreach (var script in project.Scripts ?? new List<ScriptFile>())
        {
            if (string.IsNullOrWhiteSpace(script.Name) || string.IsNullOrEmpty(script.Text)) continue;

            var path = NormalizePath(script.Path);
            if (!path.StartsWith("gamedata/", StringComparison.OrdinalIgnoreCase))
            {
                var name = NormalizePath(script.Name);
                if (name.Length == 0) continue;
                path = name.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)
                    ? "gamedata/" + name
                    : "gamedata/scripts/" + name;
            }

            Put(files, path, script.Text);
        }

        return files;
    }

    /// <summary>ZIP с игровыми файлами в кодировке экспорта проекта.</summary>
    public static byte[] Zip(Project? project) => Zip(TextFiles(project), project?.Settings.ExportEncoding);

    /// <summary>ZIP для готовой карты файлов (кодировка по умолчанию — windows-1251).</summary>
    public static byte[] Zip(IReadOnlyDictionary<string, string> files, string? encoding)
    {
        var name = encoding ?? "windows-1251";
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var pair in files)
            {
                var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                var bytes = GameEncoding.Encode(pair.Value, name);
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
        File.WriteAllBytes(path, Zip(files, project?.Settings.ExportEncoding));
        return files.Count;
    }

    /// <summary>
    /// Добавление файла в пакет: пишутся только пути внутри `gamedata/`. Абсолютные пути,
    /// диски и выход за корень (`..`) после <see cref="Paths.Normalize"/> дают пустую строку,
    /// поэтому записать что-либо вне `gamedata` пакет не может.
    /// </summary>
    private static void Put(Dictionary<string, string> files, string? key, string text)
    {
        var path = NormalizePath(key);
        if (!path.StartsWith("gamedata/", StringComparison.OrdinalIgnoreCase)) return;
        files[path] = text;
    }

    private static string NormalizePath(string? path) => Paths.Normalize(path);
}
