using XRayForge.Core.Gamedata;
using XRayForge.Core.Text;

namespace XRayForge.Core.Storage;

/// <summary>Результат чтения каталога gamedata с диска.</summary>
public sealed class DirectoryReadResult
{
    /// <summary>Имя корневой папки (в JS — `handle.name`).</summary>
    public string TopName { get; set; } = "";

    public List<GamedataFile> Files { get; } = new();
    public List<SkippedFile> Skipped { get; } = new();
}

/// <summary>
/// Чтение gamedata с диска: порт `readDirectoryFiles` из App.js плюс файловые операции
/// Electron (`fs`). Нечитаемые файлы попадают в `Skipped`, а не роняют скан.
/// </summary>
public static class FileSystemService
{
    /// <summary>Рекурсивно прочитать каталог: пути нормализуются в стиль `/`, как `normalizePath`.</summary>
    public static DirectoryReadResult ReadDirectory(string? root)
    {
        var result = new DirectoryReadResult { TopName = DirectoryName(root) };
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Paths.Normalize(Path.GetRelativePath(root, file));
            if (relative.Length == 0) continue;
            try
            {
                result.Files.Add(new GamedataFile(Path.GetFileName(file), relative, File.ReadAllBytes(file)));
            }
            catch (IOException e) { result.Skipped.Add(new SkippedFile(relative, e.Message)); }
            catch (UnauthorizedAccessException e) { result.Skipped.Add(new SkippedFile(relative, e.Message)); }
        }

        return result;
    }

    /// <summary>Имя корневой папки (в JS — `handle.name`, идёт в `gamedataPath`).</summary>
    public static string DirectoryName(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return "";
        var trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\');
        if (trimmed.Length == 0) return "";
        return Path.GetFileName(trimmed);
    }

    /// <summary>Прочитать игровой текст (`file.arrayBuffer()` + `readGameText`).</summary>
    public static string ReadGameText(string path, string encoding = "auto") =>
        GameEncoding.Decode(File.ReadAllBytes(path), encoding);
}