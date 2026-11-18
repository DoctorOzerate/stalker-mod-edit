using XRayForge.Core.Text;

namespace XRayForge.Core.Export;

/// <summary>
/// Категория файла пакета: используется в предпросмотре перед упаковкой, в манифесте
/// и в подписях UI (`gamePackageKind*`).
/// </summary>
public enum GamePackageKind
{
    /// <summary>`dialogs_*.xml` и `dialogs.ltx`.</summary>
    Dialogues,

    /// <summary>Таблицы переводов `text/&lt;locale&gt;/st_*.xml`.</summary>
    Localization,

    /// <summary>`character_desc_*.xml`.</summary>
    Characters,

    /// <summary>Item-файлы LTX (исходные пути `misc`, `weapons`, ...).</summary>
    Items,

    /// <summary>`game_tasks.xml`, `task_manager.ltx` и `task_manager.xml`.</summary>
    Quests,

    /// <summary>`system.ltx`.</summary>
    SystemLtx,

    /// <summary>Скрипты `scripts/*.lua` (и прочие файлы из `project.scripts`).</summary>
    Scripts,

    /// <summary>Всё остальное (инклюды, незнакомые LTX и т. п.).</summary>
    Other
}

/// <summary>
/// Параметры сборки игрового пакета: выбор имён `dialogs_*.xml`, набор локалей
/// (какие `string_table` попадут в архив) и манифест со списком файлов.
/// `null` в наборах означает «как в проекте»: все группы диалогов и все локали.
/// </summary>
public sealed class GamePackageOptions
{
    /// <summary>
    /// Переопределения имён файлов по id диалога (`dialogueId` → `dialogs_&lt;имя&gt;.xml`).
    /// Имя проходит через <c>DialogueFiles.SafeName</c>, как и в JS-версии.
    /// </summary>
    public IReadOnlyDictionary<string, string>? DialogueFileNames { get; init; }

    /// <summary>
    /// Локали `string_table`, которые нужно включить. `null` или пустой набор — все локали
    /// проекта (фолбэк на `rus`, если локалей нет).
    /// </summary>
    public IReadOnlyCollection<string>? Locales { get; init; }

    /// <summary>Добавить в архив манифест со списком файлов и кодировкой (по умолчанию — да).</summary>
    public bool IncludeManifest { get; init; } = true;

    /// <summary>Версия пакета для манифеста (`version`). Пустая строка → `1`.</summary>
    public string ManifestVersion { get; init; } = "1";

    /// <summary>Имя манифеста в корне архива (вне `gamedata/`).</summary>
    public string ManifestName { get; init; } = "xrayforge-package.json";

    /// <summary>Параметры по умолчанию: полный пакет с манифестом (сценарий UI).</summary>
    public static GamePackageOptions Default => new();

    /// <summary>
    /// Параметры без манифеста: паритет с legacy `gamePackage()` и с прежним поведением
    /// <see cref="GamePackage.Zip(XRayForge.Core.Projects.Project?)"/>.
    /// </summary>
    public static GamePackageOptions Plain => new() { IncludeManifest = false };
}

/// <summary>Запись плана пакета: путь в архиве, категория и размер в байтах кодировки экспорта.</summary>
/// <param name="Path">Путь в архиве (всегда `gamedata/...`).</param>
/// <param name="Kind">Категория файла.</param>
/// <param name="Bytes">Размер закодированного текста.</param>
public sealed record GamePackageEntry(string Path, GamePackageKind Kind, int Bytes);

/// <summary>
/// План пакета: текстовые файлы, их записи для предпросмотра и необязательный манифест.
/// План считается один раз; ZIP и запись на диск используют уже готовые данные
/// (размеры не пересчитываются повторно).
/// </summary>
public sealed class GamePackagePlan
{
    /// <summary>Создать план из готовой карты файлов (используется <see cref="GamePackage.Plan"/>).</summary>
    /// <param name="files">Карта «путь → текст», ключи уже проверены на принадлежность `gamedata/`.</param>
    /// <param name="encoding">Кодировка экспорта (влияет на размеры записей).</param>
    /// <param name="manifest">Текст манифеста или `null`.</param>
    /// <param name="manifestName">Имя манифеста в архиве или `null`.</param>
    public GamePackagePlan(
        IReadOnlyDictionary<string, string> files,
        string encoding,
        string? manifest = null,
        string? manifestName = null)
        : this(files, encoding, BuildEntries(files, encoding), manifest, manifestName)
    {
    }

    private GamePackagePlan(
        IReadOnlyDictionary<string, string> files,
        string encoding,
        IReadOnlyList<GamePackageEntry> entries,
        string? manifest,
        string? manifestName)
    {
        Files = files;
        Encoding = encoding;
        Manifest = manifest;
        ManifestName = manifest != null ? manifestName ?? "xrayforge-package.json" : null;
        Entries = entries;
        TotalBytes = Entries.Sum(entry => entry.Bytes);
    }

    /// <summary>
    /// Копия плана с манифестом: записи и размеры переиспользуются, поэтому манифест
    /// не требует повторного кодирования всех файлов.
    /// </summary>
    internal GamePackagePlan WithManifest(string? manifest, string? manifestName) =>
        new(Files, Encoding, Entries, manifest, manifestName);

    /// <summary>Текстовые файлы пакета (только `gamedata/...`).</summary>
    public IReadOnlyDictionary<string, string> Files { get; }

    /// <summary>Кодировка, в которой файлы попадут в архив.</summary>
    public string Encoding { get; }

    /// <summary>Записи предпросмотра, отсортированы по пути (стабильный порядок архива).</summary>
    public IReadOnlyList<GamePackageEntry> Entries { get; }

    /// <summary>Суммарный размер игровых файлов в байтах кодировки экспорта.</summary>
    public int TotalBytes { get; }

    /// <summary>Текст манифеста (JSON) или `null`, если манифест отключён.</summary>
    public string? Manifest { get; }

    /// <summary>Имя манифеста в корне архива или `null`.</summary>
    public string? ManifestName { get; }

    /// <summary>
    /// Записи предпросмотра по карте файлов: сортировка по пути даёт стабильный порядок
    /// архива и манифеста независимо от порядка добавления файлов.
    /// </summary>
    private static IReadOnlyList<GamePackageEntry> BuildEntries(
        IReadOnlyDictionary<string, string> files, string encoding) =>
        files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new GamePackageEntry(
                pair.Key, KindOf(pair.Key), GameEncoding.Encode(pair.Value, encoding).Length))
            .ToList()
            .AsReadOnly();

    /// <summary>Категория файла по его пути в пакете.</summary>
    public static GamePackageKind KindOf(string? path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        if (normalized.Length == 0) return GamePackageKind.Other;

        var fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);
        var lower = normalized.ToLowerInvariant();

        if (fileName.Equals("system.ltx", StringComparison.OrdinalIgnoreCase)) return GamePackageKind.SystemLtx;
        if (fileName.StartsWith("dialogs_", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("dialogs.ltx", StringComparison.OrdinalIgnoreCase))
            return GamePackageKind.Dialogues;
        if (lower.Contains("/text/", StringComparison.Ordinal)) return GamePackageKind.Localization;
        if (fileName.StartsWith("character_desc_", StringComparison.OrdinalIgnoreCase)) return GamePackageKind.Characters;
        if (fileName.Equals("game_tasks.xml", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("task_manager.", StringComparison.OrdinalIgnoreCase))
            return GamePackageKind.Quests;
        if (lower.Contains("/scripts/", StringComparison.Ordinal)) return GamePackageKind.Scripts;
        if (fileName.EndsWith(".ltx", StringComparison.OrdinalIgnoreCase)) return GamePackageKind.Items;

        return GamePackageKind.Other;
    }

    /// <summary>Имя категории для манифеста (стабильные латинские значения).</summary>
    public static string KindName(GamePackageKind kind) => kind switch
    {
        GamePackageKind.Dialogues => "dialogues",
        GamePackageKind.Localization => "localization",
        GamePackageKind.Characters => "characters",
        GamePackageKind.Items => "items",
        GamePackageKind.Quests => "quests",
        GamePackageKind.SystemLtx => "system_ltx",
        GamePackageKind.Scripts => "scripts",
        _ => "other"
    };
}
