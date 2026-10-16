using System.Text;
using System.Text.Json;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Storage;

/// <summary>
/// Профиль приложения в `%APPDATA%\XRayForge` — замена `localStorage` из браузерной
/// версии и файла `saves/*.project.json` из Electron:
/// `settings.json`, `current.project.json`, `projects.json`, `backups.json`, `gamedata.json`.
/// </summary>
public sealed class AppDataService
{
    public const string DirectoryName = "XRayForge";
    public const string SettingsFileName = "settings.json";
    public const string CurrentProjectFileName = "current.project.json";
    public const string ProjectsFileName = "projects.json";
    public const string BackupsFileName = "backups.json";
    public const string GamedataFileName = "gamedata.json";
    public const string SavesDirectoryName = "saves";

    private static readonly JsonSerializerOptions Json = Projects.ProjectSerializer.Options;

    public AppDataService(string? root = null)
    {
        Root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DirectoryName)
            : Path.GetFullPath(root);
    }

    /// <summary>Каталог профиля (по умолчанию `%APPDATA%\XRayForge`).</summary>
    public string Root { get; }

    /// <summary>Каталог `saves` — аналог папки рядом с приложением в Electron.</summary>
    public string SavesDirectory => Path.Combine(Root, SavesDirectoryName);

    /// <summary>Время в миллисекундах, как `Date.now()`.</summary>
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public string PathFor(string fileName) => Path.Combine(Root, fileName);

    // --- настройки программы ---

    public ProgramSettings LoadProgramSettings() => ProgramSettingsCatalog.FromJson(ReadFile(SettingsFileName));

    public void SaveProgramSettings(ProgramSettings? settings) =>
        WriteFile(SettingsFileName, JsonSerializer.Serialize(settings ?? new ProgramSettings(), Json));

    // --- текущий проект ---

    /// <summary>Как `loadProject`: null, если файла нет или он битый.</summary>
    public Project? LoadCurrentProject()
    {
        var json = ReadFile(CurrentProjectFileName);
        return json == null ? null : Projects.ProjectSerializer.TryDeserialize(json);
    }

    /// <summary>Как `localStorage.setItem(STORAGE_KEY, …)` — только «персистируемая» часть.</summary>
    public void SaveCurrentProject(Project? project) =>
        WriteFile(CurrentProjectFileName, Projects.ProjectSerializer.Serialize(Projects.ProjectSerializer.ToPersistable(project)));

    /// <summary>`saveProject()`/автобэкап: проект встаёт в начало списка с новым `updatedAt`.</summary>
    public void SaveProject(Project? project)
    {
        if (project == null) return;
        var list = ListProjects().Where(p => p.Name != project.ProjectName).ToList();
        list.Insert(0, new StoredProject
        {
            Name = project.ProjectName,
            UpdatedAt = Now(),
            Data = Projects.ProjectSerializer.ToPersistable(project)
        });
        SaveProjects(list);
    }

    /// <summary>Удаление проекта из списка (`removeProject`).</summary>
    public void RemoveProject(string? name)
    {
        var list = ListProjects().Where(p => p.Name != name).ToList();
        SaveProjects(list);
    }

    /// <summary>Переименование проекта в списке (`renameProject`).</summary>
    public void RenameProject(string? oldName, string? newName)
    {
        var list = ListProjects();
        foreach (var item in list)
            if (item.Name == oldName) item.Name = newName ?? "";
        SaveProjects(list);
    }

    public void SaveProjects(List<StoredProject>? list) =>
        WriteFile(ProjectsFileName, JsonSerializer.Serialize(list ?? new List<StoredProject>(), Json));

    // --- список проектов ---

    /// <summary>`loadProjects()` — список с нормализованными name/updatedAt/data.</summary>
    public List<StoredProject> ListProjects()
    {
        var list = new List<StoredProject>();
        var json = ReadFile(ProjectsFileName);
        if (json == null) return list;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return list;

            var index = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var name = item.TryGetProperty("name", out var rawName) ? Projects.ProjectMigration.Scalar(rawName) : null;
                list.Add(new StoredProject
                {
                    Name = string.IsNullOrEmpty(name) ? "project_" + index : name,
                    UpdatedAt = ReadNumber(item, "updatedAt"),
                    Data = item.TryGetProperty("data", out var rawData) ? ReadProject(rawData) : new Project()
                });
                index++;
            }
        }
        catch (JsonException) { return new List<StoredProject>(); }

        return list;
    }

    // --- резервные копии ---

    /// <summary>`loadBackups()`.</summary>
    public List<StoredBackup> LoadBackups()
    {
        var list = new List<StoredBackup>();
        var json = ReadFile(BackupsFileName);
        if (json == null) return list;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return list;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                list.Add(new StoredBackup
                {
                    Timestamp = ReadNumber(item, "t"),
                    Data = item.TryGetProperty("data", out var rawData) ? ReadProject(rawData) : new Project()
                });
            }
        }
        catch (JsonException) { return new List<StoredBackup>(); }
        return list;
    }

    /// <summary>`saveBackups`: новая копия в начале, хранится не больше `ProjectSchema.BackupsLimit`.</summary>
    public void AddBackup(Project? project, long? timestamp = null)
    {
        if (project == null) return;
        var list = LoadBackups();
        list.Insert(0, new StoredBackup
        {
            Timestamp = timestamp ?? Now(),
            Data = Projects.ProjectSerializer.ToPersistable(project)
        });
        if (list.Count > ProjectSchema.BackupsLimit) list = list.Take(ProjectSchema.BackupsLimit).ToList();
        WriteFile(BackupsFileName, JsonSerializer.Serialize(list, Json));
    }

    // --- снапшоты gamedata ---

    /// <summary>`loadGamedataSnapshot(name)`.</summary>
    public GamedataSnapshot? LoadGamedataSnapshot(string? projectName)
    {
        if (string.IsNullOrEmpty(projectName)) return null;
        var all = ReadGamedataSnapshots();
        return all.TryGetValue(projectName, out var snapshot) ? snapshot : null;
    }

    /// <summary>`saveGamedataSnapshot(name, data)`.</summary>
    public void SaveGamedataSnapshot(string? projectName, GamedataSnapshot? snapshot)
    {
        if (string.IsNullOrEmpty(projectName)) return;
        var all = ReadGamedataSnapshots();
        all[projectName] = snapshot ?? new GamedataSnapshot();
        WriteFile(GamedataFileName, JsonSerializer.Serialize(all, Json));
    }

    /// <summary>`removeGamedataSnapshot(name)`.</summary>
    public void RemoveGamedataSnapshot(string? projectName)
    {
        if (string.IsNullOrEmpty(projectName)) return;
        var all = ReadGamedataSnapshots();
        if (!all.Remove(projectName)) return;
        WriteFile(GamedataFileName, JsonSerializer.Serialize(all, Json));
    }

    /// <summary>Снапшот gamedata из полей проекта (`gamedataOf`).</summary>
    public static GamedataSnapshot ToSnapshot(Project? project) => new()
    {
        LtxFiles = project?.LtxFiles,
        Textures = project?.Textures,
        Scripts = project?.Scripts,
        ItemFiles = project?.ItemFiles,
        ItemOrigins = project?.ItemOrigins
    };

    /// <summary>`resetStorage`: удалить все данные редактора.</summary>
    public void Reset()
    {
        foreach (var fileName in new[] { SettingsFileName, CurrentProjectFileName, ProjectsFileName, BackupsFileName, GamedataFileName })
        {
            var path = PathFor(fileName);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { /* файл занят — в JS ошибка глотается так же */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private Dictionary<string, GamedataSnapshot> ReadGamedataSnapshots()
    {
        var map = new Dictionary<string, GamedataSnapshot>(StringComparer.Ordinal);
        var json = ReadFile(GamedataFileName);
        if (json == null) return map;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return map;
            foreach (var prop in document.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                var snapshot = TryDeserialize<GamedataSnapshot>(prop.Value);
                if (snapshot != null) map[prop.Name] = snapshot;
            }
        }
        catch (JsonException) { }
        return map;
    }

    private static long ReadNumber(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var raw)) return 0;
        var value = Projects.ProjectMigration.Number(raw);
        return value.HasValue && double.IsFinite(value.Value) ? (long)value.Value : 0;
    }

    private static Project ReadProject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return new Project();
        return TryDeserialize<Project>(element) ?? new Project();
    }

    private static T? TryDeserialize<T>(JsonElement element) where T : class
    {
        try { return element.Deserialize<T>(Json); }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }

    private string? ReadFile(string fileName)
    {
        var path = PathFor(fileName);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void WriteFile(string fileName, string json)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(PathFor(fileName), json, new UTF8Encoding(false));
    }
}