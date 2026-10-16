using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRayForge.Core.Projects;

/// <summary>
/// Чтение/запись `.project.json`: `JSON.stringify(project, null, 2)` и `JSON.parse` + `migrate`.
/// Ключи словарей (пути LTX, имена секций) пишутся как есть — как в JS.
/// </summary>
public static class ProjectSerializer
{
    /// <summary>Имена ключей как в JS: snake_case задаётся атрибутами, остальное — camelCase.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = VerbatimNamingPolicy.Instance,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Сериализовать проект (`JSON.stringify(project, null, 2)`).</summary>
    public static string Serialize(Project? project) => JsonSerializer.Serialize(project ?? new Project(), Options);

    /// <summary>Разобрать и мигрировать проект (`JSON.parse` + `migrate`).</summary>
    public static Project Deserialize(string? json)
    {
        using var document = JsonDocument.Parse(json ?? "null");
        return ProjectMigration.Migrate(document.RootElement.Clone());
    }

    /// <summary>Как `loadProject`: при любой ошибке разбора — null.</summary>
    public static Project? TryDeserialize(string? json)
    {
        try { return Deserialize(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Копия проекта без «тяжёлых» gamedata-полей (`toPersistable`):
    /// ltxFiles, textures, scripts, itemFiles, itemOrigins.
    /// </summary>
    public static Project ToPersistable(Project? project)
    {
        var element = JsonSerializer.SerializeToElement(project ?? new Project(), Options);
        var copy = JsonSerializer.Deserialize<Project>(element, Options) ?? new Project();
        copy.LtxFiles = null;
        copy.Textures = null;
        copy.Scripts = null;
        copy.ItemFiles = null;
        copy.ItemOrigins = null;
        return copy;
    }

    /// <summary>Записать файл проекта (UTF-8 без BOM, каталог создаётся).</summary>
    public static string Save(string path, Project? project)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(full, Serialize(project), new UTF8Encoding(false));
        return full;
    }

    /// <summary>Прочитать файл проекта; null, если файла нет или он битый (`loadProject`).</summary>
    public static Project? Load(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try { return TryDeserialize(File.ReadAllText(path)); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Политика «имя ключа как есть» — пути и имена секций не должны преобразовываться.</summary>
    private sealed class VerbatimNamingPolicy : JsonNamingPolicy
    {
        public static readonly VerbatimNamingPolicy Instance = new();

        public override string ConvertName(string name) => name;
    }
}