using System.Text.Json;
using System.Text.Json.Serialization;
using XRayForge.Core.Graph;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Quests;

namespace XRayForge.Core.Projects;

/// <summary>
/// Миграция проектов: порт двух JS-функций — `migrate` из App.js (боевая, схема 3)
/// и `migrateProject` из core.js (legacy, схема 4, покрыта `test/core.test.mjs`).
/// </summary>
public static class ProjectMigration
{
    /// <summary>`emptyProject()` из App.js (полный набор полей схемы 3).</summary>
    public static Project Empty() => new();

    /// <summary>
    /// Порт `migrate(p)`: значения по умолчанию + нормализация полей.
    /// Не-объект (или массив) трактуется как `{}`, как и в JS.
    /// </summary>
    public static Project Migrate(JsonElement raw)
    {
        var r = new ProjectReader(raw);
        var p = Empty();

        p.Version = ProjectSchema.Version;
        var rawProjectName = r.Str("projectName");
        p.ProjectName = rawProjectName ?? ProjectSchema.DefaultProjectName;
        p.DialogueId = r.Truthy("dialogueId") ?? Truthy(rawProjectName) ?? ProjectSchema.DefaultProjectName;
        p.Series = r.Str("series") ?? p.Series;
        p.Theme = r.Str("theme") ?? p.Theme;
        p.GamedataPath = r.Str("gamedataPath") ?? p.GamedataPath;
        p.ResourcesPath = r.Str("resourcesPath") ?? p.ResourcesPath;
        p.ItemsLtx = r.Str("itemsLtx") ?? p.ItemsLtx;
        p.SystemLtx = r.Str("systemLtx") ?? p.SystemLtx;

        p.Nodes = r.Nodes();
        p.Edges = r.Edges();
        p.Characters = r.Characters();
        p.Dialogues = r.Dialogues();
        p.Quests = r.Quests();
        p.Languages = r.Languages();
        p.Scripts = r.Scripts();
        p.Textures = r.Objects<ProjectTexture>("textures");
        p.Atlas = r.Atlas();
        p.Settings = r.Settings();
        p.GeneralCommunities = r.StringList("generalCommunities");
        p.LtxFiles = r.StringMap("ltxFiles") ?? new(StringComparer.Ordinal);
        p.ItemCategories = r.StringMap("itemCategories") ?? p.ItemCategories;
        p.ItemFiles = r.StringMap("itemFiles") ?? new(StringComparer.Ordinal);
        p.ItemOrigins = r.StringMap("itemOrigins") ?? new(StringComparer.Ordinal);
        p.EquipSlots = r.StringMap("equipSlots") ?? p.EquipSlots;
        p.EquipSlotLayout = r.Objects<EquipSlotLayout>("equipSlotLayout");
        p.PinnedIcons = r.Objects<PinnedIcon>("pinnedIcons") ?? new();
        p.PinCollections = r.PinCollections(p.PinnedIcons);

        // Поля, которые наполняет сканер gamedata (в JS остаются в объекте проекта).
        p.GameRelations = r.Typed<GameRelationsInfo>("gameRelations");
        p.Squads = r.TypedList<SquadDesc>("squads");
        p.Secrets = r.Typed<SecretsInfo>("secrets");
        p.SpawnSections = r.TypedList<SpawnSectionInfo>("spawnSections");
        p.Actor = r.Typed<ActorInfo>("actor");
        p.Monsters = r.TypedList<MonsterInfo>("monsters");
        p.NpcProfiles = r.TypedList<NpcProfileCharacter>("npcProfiles");
        p.TextureMap = r.TypedMap<TextureMapEntry>("textureMap");
        p.InfoPortions = r.TypedList<InfoPortion>("infoPortions");
        p.Encyclopedia = r.TypedList<EncyclopediaArticle>("encyclopedia");
        p.Trade = r.Typed<TradeInfo>("trade");
        p.SurgeManager = r.Typed<SurgeManagerInfo>("surgeManager");
        p.GeneralDesc = r.Element("generalDesc");

        p.Extra = r.Remaining();
        return p;
    }

    internal static bool IsTruthy(string? value) => !string.IsNullOrEmpty(value);

    internal static string? Truthy(string? value) => IsTruthy(value) ? value : null;

    internal static T? TryDeserialize<T>(JsonElement element) where T : class
    {
        try { return element.Deserialize<T>(ProjectSerializer.Options); }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }

    /// <summary>Скаляр JSON → строка (числа и булевы — как их текст), иначе null.</summary>
    internal static string? Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    internal static double? Number(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var d)) return d;
        if (element.ValueKind == JsonValueKind.String
            && double.TryParse(element.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    internal static string? ScalarField(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) ? Scalar(v) : null;

    internal static double? NumberField(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) ? Number(v) : null;

    internal static bool BoolField(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.TryGetDouble(out var d) && d != 0,
            _ => false
        };
    }

    /// <summary>
    /// Порт `migrateProject(p)` из core.js: не-объект → исключение, версия схемы 4,
    /// нормализация узлов без `safeNode`. Используется как отдельная (legacy) ветка
    /// миграции — покрыта тестами `test/core.test.mjs`.
    /// </summary>
    public static Project MigrateLegacy(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Проект должен быть объектом", nameof(raw));

        var r = new ProjectReader(raw);
        var project = new Project
        {
            Version = ProjectSchema.LegacyVersion,
            ProjectName = r.Str("projectName") ?? ProjectSchema.DefaultProjectName,
            Series = r.Str("series") ?? "soc",
            Nodes = r.LegacyNodes(),
            Edges = r.Objects<DialogueEdge>("edges") ?? new(),
            Characters = r.Objects<Character>("characters") ?? new(),
            Languages = r.LegacyLanguages(),
            Scripts = r.Objects<ScriptFile>("scripts") ?? new(),
            Dialogues = r.Objects<DialogueGraph>("dialogues") ?? new(),
            LtxFiles = r.StringMap("ltxFiles") ?? new(StringComparer.Ordinal),
            Textures = r.Objects<ProjectTexture>("textures") ?? new(),
            ItemCategories = r.StringMap("itemCategories") ?? new(StringComparer.Ordinal),
            Settings = r.LegacySettings()
        };

        project.Extra = r.Remaining();
        return project;
    }
}