using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// <see cref="Plan"/> добавляет предпросмотр (размеры, категории) и манифест
/// `xrayforge-package.json` — по нему проверяют установку пакета.
/// </summary>
public static class GamePackage
{
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Все текстовые файлы экспорта (пути всегда начинаются с `gamedata/`).</summary>
    public static Dictionary<string, string> TextFiles(Project? project) => TextFiles(project, null);

    /// <summary>
    /// Все текстовые файлы экспорта с учётом параметров: переопределения имён
    /// `dialogs_*.xml` и набор локалей `string_table`.
    /// </summary>
    public static Dictionary<string, string> TextFiles(Project? project, GamePackageOptions? options)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (project == null) return files;

        foreach (var pair in DialogueGameExport.GameFiles(
                     project, options?.DialogueFileNames, options?.Locales))
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

    /// <summary>
    /// План пакета: текстовые файлы, записи предпросмотра и манифест. Параметры по умолчанию —
    /// <see cref="GamePackageOptions.Default"/> (манифест включён).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Диалог проекта содержит ошибки графа — как в legacy `buildGameFiles`.
    /// </exception>
    public static GamePackagePlan Plan(Project? project, GamePackageOptions? options = null)
    {
        var opts = options ?? GamePackageOptions.Default;
        var encoding = project?.Settings.ExportEncoding ?? "windows-1251";
        var files = TextFiles(project, opts);

        // Манифест строится по уже готовым записям, поэтому файлы кодируются один раз.
        var plan = new GamePackagePlan(files, encoding);
        if (!opts.IncludeManifest) return plan;
        return plan.WithManifest(ManifestText(project, plan, opts), opts.ManifestName);
    }

    /// <summary>ZIP с игровыми файлами без манифеста (паритет с legacy `gamePackage`).</summary>
    public static byte[] Zip(Project? project) => Zip(Plan(project, GamePackageOptions.Plain));

    /// <summary>
    /// ZIP по плану: сначала игровые файлы в кодировке экспорта (порядок — <see
    /// cref="GamePackagePlan.Entries"/>), затем манифест в UTF-8, если он включён.
    /// </summary>
    public static byte[] Zip(GamePackagePlan plan)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in plan.Entries)
            {
                if (!plan.Files.TryGetValue(entry.Path, out var text)) continue;
                Write(archive, entry.Path, GameEncoding.Encode(text, plan.Encoding));
            }

            // Манифест пишется в UTF-8 независимо от кодировки игры: JSON-парсеры
            // и человек ожидают юникод, а кодировка игры к этому файлу не относится.
            if (plan.Manifest != null && plan.ManifestName != null)
                Write(archive, plan.ManifestName, Encoding.UTF8.GetBytes(plan.Manifest));
        }

        return stream.ToArray();
    }

    /// <summary>ZIP для готовой карты файлов (кодировка по умолчанию — windows-1251).</summary>
    public static byte[] Zip(IReadOnlyDictionary<string, string> files, string? encoding) =>
        Zip(new GamePackagePlan(files, encoding ?? "windows-1251"));

    /// <summary>
    /// Записать ZIP в файл по параметрам по умолчанию (с манифестом); возвращает число
    /// игровых файлов внутри архива без манифеста.
    /// </summary>
    public static int WriteZip(Project? project, string path) => WriteZip(Plan(project), path);

    /// <summary>Записать готовый план в файл; возвращает число игровых файлов.</summary>
    public static int WriteZip(GamePackagePlan plan, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Не указан путь ZIP", nameof(path));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllBytes(path, Zip(plan));
        return plan.Entries.Count;
    }

    /// <summary>
    /// Манифест пакета (`xrayforge-package.json`): формат, версия, проект, серия, кодировка
    /// и список файлов с размерами. Дата не пишется — манифест детерминирован, его можно
    /// сравнивать в тестах и diff-проверке установки.
    /// </summary>
    private static string ManifestText(Project? project, GamePackagePlan plan, GamePackageOptions options)
    {
        var version = string.IsNullOrWhiteSpace(options.ManifestVersion)
            ? "1"
            : options.ManifestVersion.Trim();

        var document = new ManifestDocument(
            "xrayforge-game-package",
            1,
            version,
            project?.ProjectName ?? string.Empty,
            project?.DialogueId ?? string.Empty,
            project?.Series ?? string.Empty,
            plan.Encoding,
            plan.Entries
                .Select(entry => new ManifestFile(
                    entry.Path, GamePackagePlan.KindName(entry.Kind), entry.Bytes))
                .ToList());

        return JsonSerializer.Serialize(document, ManifestSerializerOptions) + "\n";
    }

    private static void Write(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Запись файла в манифесте (`path`/`kind`/`bytes`).</summary>
    private sealed record ManifestFile(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("bytes")] int Bytes);

    /// <summary>Корневой документ манифеста пакета.</summary>
    private sealed record ManifestDocument(
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("schema")] int Schema,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("project")] string Project,
        [property: JsonPropertyName("dialogueId")] string DialogueId,
        [property: JsonPropertyName("series")] string Series,
        [property: JsonPropertyName("encoding")] string Encoding,
        [property: JsonPropertyName("files")] List<ManifestFile> Files);

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
