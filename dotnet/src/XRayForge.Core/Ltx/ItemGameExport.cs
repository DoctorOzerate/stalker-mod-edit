using System.Text.RegularExpressions;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Ltx;

/// <summary>
/// Экспорт item-файлов по исходным путям (остаток P0). Сканер склеивает файлы предметов в
/// <c>project.itemsLtx</c> с заголовками <c>; === items.ltx ===</c>, а исходные пути остаются в
/// <c>project.ltxFiles</c>. Здесь склейка разбирается обратно, и каждый файл получает свой
/// путь (`configs/misc/items.ltx`, `configs/weapons/weapons.ltx`, …); если исходник не найден,
/// файл кладётся в <c>&lt;config&gt;/misc/</c>.
/// </summary>
public static class ItemGameExport
{
    /// <summary>Каталог для файлов, чей исходный путь неизвестен.</summary>
    public const string DefaultDirectory = "misc";

    /// <summary>Заголовок, который ставит <c>GamedataScanner</c>: `; === имя === `.</summary>
    private static readonly Regex HeaderLine = new(@"^\s*;\s*===\s*(?<name>.+?)\s*===\s*$");

    /// <summary>Карта «путь в gamedata → текст» для всех item-файлов проекта.</summary>
    public static Dictionary<string, string> GameFiles(Project? project)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (project == null) return files;

        var config = GameSeriesCatalog.ConfigDirectory(project.Series);
        foreach (var pair in SplitMerged(project.ItemsLtx))
        {
            var path = TargetPath(project, pair.Key, config);
            if (path.Length == 0) continue;
            files[path] = pair.Value;
        }

        return files;
    }

    /// <summary>
    /// Разбор склейки `itemsLtx` на «имя файла → текст» в порядке файлов. Строки заголовков
    /// выбрасываются, пустые края текста обрезаются, в конце — один перевод строки.
    /// </summary>
    public static List<KeyValuePair<string, string>> SplitMerged(string? merged)
    {
        var files = new List<KeyValuePair<string, string>>();
        if (string.IsNullOrEmpty(merged)) return files;

        var name = string.Empty;
        var buffer = new List<string>();

        void Flush()
        {
            if (name.Length == 0) return;
            var text = TrimBlankEdges(buffer);
            if (text.Length > 0) files.Add(new KeyValuePair<string, string>(name, text + "\n"));
            buffer.Clear();
        }

        foreach (var line in merged.Replace("\r\n", "\n").Split('\n'))
        {
            var header = HeaderLine.Match(line);
            if (header.Success)
            {
                Flush();
                name = header.Groups["name"].Value.Trim();
                continue;
            }

            buffer.Add(line);
        }

        Flush();
        return files;
    }

    /// <summary>
    /// Путь файла в gamedata: сначала исходник из `ltxFiles` (совпадение по имени файла),
    /// иначе каталог `misc` серии проекта. Пустой результат — имя не пригодно для пути.
    /// </summary>
    public static string TargetPath(Project? project, string? fileName, string? config = null)
    {
        var name = Paths.Normalize(fileName);
        if (name.Length == 0 || name.Contains('/')) return string.Empty;

        var origin = FindOrigin(project?.LtxFiles, name);
        var relative = origin.Length > 0
            ? origin
            : (config ?? GameSeriesCatalog.ConfigDirectory(project?.Series)) + "/" + DefaultDirectory + "/" + name;

        return GamePath(relative);
    }

    /// <summary>Ключ `ltxFiles`, чьё имя файла совпадает (без учёта регистра), иначе пустая строка.</summary>
    private static string FindOrigin(IReadOnlyDictionary<string, string>? ltxFiles, string fileName)
    {
        if (ltxFiles == null) return string.Empty;

        foreach (var pair in ltxFiles)
        {
            if (!pair.Key.EndsWith(".ltx", StringComparison.OrdinalIgnoreCase)) continue;
            var candidate = Paths.Normalize(pair.Key);
            var slash = candidate.LastIndexOf('/');
            var baseName = slash >= 0 ? candidate.Substring(slash + 1) : candidate;
            if (baseName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) return candidate;
        }

        return string.Empty;
    }

    /// <summary>Путь с префиксом `gamedata/` (ключи `ltxFiles` относительны выбранному каталогу).</summary>
    private static string GamePath(string relative)
    {
        var path = Paths.Normalize(relative);
        if (path.Length == 0) return string.Empty;
        return path.StartsWith("gamedata/", StringComparison.OrdinalIgnoreCase) ? path : "gamedata/" + path;
    }

    /// <summary>Убрать пустые строки по краям блока (внутри текста они сохраняются).</summary>
    private static string TrimBlankEdges(List<string> lines)
    {
        var start = 0;
        var end = lines.Count - 1;
        while (start <= end && lines[start].Trim().Length == 0) start++;
        while (end >= start && lines[end].Trim().Length == 0) end--;
        if (end < start) return string.Empty;

        return string.Join('\n', lines.Skip(start).Take(end - start + 1));
    }
}
