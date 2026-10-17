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
    /// <summary>
    /// Рекурсивно перечислить каталог: пути нормализуются в стиль `/`, как `normalizePath`.
    /// Содержимое файлов не читается — <see cref="GamedataFile"/> делает это лениво при
    /// разборе. Иначе выбор реальной gamedata (6+ ГБ ассетов) забивал память ещё до скана.
    /// </summary>
    public static DirectoryReadResult ReadDirectory(string? root)
    {
        var result = new DirectoryReadResult { TopName = DirectoryName(root) };
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return result;

        Walk(root, root, result);
        return result;
    }

    /// <summary>Обход каталога с пропуском недоступных папок и символических ссылок (без циклов).</summary>
    private static void Walk(string root, string directory, DirectoryReadResult result)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directory);
        }
        catch (Exception e) when (IsDirectoryError(e))
        {
            result.Skipped.Add(new SkippedFile(RelativePath(root, directory), e.Message));
            return;
        }

        foreach (var file in files)
        {
            var relative = RelativePath(root, file);
            if (relative.Length == 0) continue;
            result.Files.Add(new GamedataFile(Path.GetFileName(file), relative, file));
        }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(directory);
        }
        catch (Exception e) when (IsDirectoryError(e))
        {
            result.Skipped.Add(new SkippedFile(RelativePath(root, directory), e.Message));
            return;
        }

        foreach (var child in directories)
        {
            try
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
            }
            catch (Exception e) when (IsDirectoryError(e))
            {
                result.Skipped.Add(new SkippedFile(RelativePath(root, child), e.Message));
                continue;
            }

            Walk(root, child, result);
        }
    }

    private static bool IsDirectoryError(Exception error) =>
        error is IOException or UnauthorizedAccessException;

    private static string RelativePath(string root, string path)
    {
        var relative = Paths.Normalize(Path.GetRelativePath(root, path));
        return relative == "." ? "" : relative;
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