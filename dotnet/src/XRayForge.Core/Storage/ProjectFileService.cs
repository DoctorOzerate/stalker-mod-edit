using System.Text.RegularExpressions;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Storage;

/// <summary>
/// Сохранение проекта файлом (`saves/&lt;имя&gt;.project.json`) — порт IPC-обработчика
/// `xray:save-project` из `electron/main.cjs`.
/// </summary>
public static class ProjectFileService
{
    private static readonly Regex UnsafeName = new(@"[^\w\-]+", RegexOptions.ECMAScript);

    /// <summary>`String(name || 'project').replace(/[^\w\-]+/g, '_') || 'project'`.</summary>
    public static string SanitizeName(string? name)
    {
        var raw = string.IsNullOrEmpty(name) ? "project" : name;
        var sanitized = UnsafeName.Replace(raw, "_");
        return sanitized.Length == 0 ? "project" : sanitized;
    }

    /// <summary>Путь файла проекта в каталоге сохранений.</summary>
    public static string FilePathFor(string baseDirectory, string? name) =>
        Path.Combine(baseDirectory, SanitizeName(name) + ".project.json");

    /// <summary>Записать проект так же, как `fs.writeFileSync(file, JSON.stringify(data, null, 2))`.</summary>
    public static string SaveProjectFile(string baseDirectory, string? name, Project? data)
    {
        var path = FilePathFor(baseDirectory, name);
        ProjectSerializer.Save(path, data);
        return path;
    }
}