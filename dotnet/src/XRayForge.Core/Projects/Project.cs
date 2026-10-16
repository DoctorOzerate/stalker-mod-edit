using System.Text.Json;
using System.Text.Json.Serialization;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;

namespace XRayForge.Core.Projects;

/// <summary>Версии схемы и лимиты персистенции проекта.</summary>
public static class ProjectSchema
{
    /// <summary>Текущая версия проекта (`SCHEMA_VERSION` в App.js).</summary>
    public const int Version = 3;

    /// <summary>Версия, которую пишет legacy-миграция `core.js:migrateProject`.</summary>
    public const int LegacyVersion = 4;

    /// <summary>Сколько резервных копий хранит `saveBackups` (App.js).</summary>
    public const int BackupsLimit = 10;

    /// <summary>Имя проекта по умолчанию (`emptyProject().projectName`).</summary>
    public const string DefaultProjectName = "new_dialogue";
}

/// <summary>Настройки проекта (`settings` в `.project.json`).</summary>
public sealed class ProjectSettings
{
    [JsonPropertyName("importEncoding")] public string ImportEncoding { get; set; } = "auto";
    [JsonPropertyName("exportEncoding")] public string ExportEncoding { get; set; } = "windows-1251";
    [JsonPropertyName("showNodeText")] public bool ShowNodeText { get; set; } = true;
    /// <summary>Интервал автосохранения в секундах (`null`/`NaN` в JS — автосейв выключен).</summary>
    [JsonPropertyName("autosaveInterval")] public double? AutosaveInterval { get; set; } = 60;

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Сетка атласа иконок (`atlas`): число колонок и размер ячейки.</summary>
public sealed class ProjectAtlas
{
    [JsonPropertyName("columns")] public double Columns { get; set; } = 5;
    [JsonPropertyName("cellSize")] public double CellSize { get; set; } = 50;
    [JsonPropertyName("cellHeight")] public double CellHeight { get; set; } = 50;

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Скрипт проекта (`scripts[]`): текущий текст и исходник для diff.</summary>
public sealed class ScriptFile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("original")] public string Original { get; set; } = "";

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// Текстура проекта (`textures[]`). В JS элемент — либо `{name, dataURL, width, height}`,
/// либо `{name, error}`; поэтому отсутствующие поля не пишутся.
/// </summary>
public sealed class ProjectTexture
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>PNG data URL; в Core не строится (нужен canvas/WPF-энкодер).</summary>
    [JsonPropertyName("dataURL")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataUrl { get; set; }

    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Закреплённая иконка атласа (`pinnedIcons[]`, значения `pinCollections{}`).</summary>
public sealed class PinnedIcon
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("w")] public double W { get; set; }
    [JsonPropertyName("h")] public double H { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Запись `textureMap`: файл атласа и прямоугольник иконки.</summary>
public sealed class TextureMapEntry
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

/// <summary>
/// Проект редактора (`.project.json`, схема 3). Порядок свойств повторяет
/// `emptyProject()` из App.js, чтобы JSON был устроен как в JS-версии.
/// Пять «тяжёлых» gamedata-полей пишутся только когда не null: `toPersistable`
/// вычищает их перед сохранением проекта (они живут в снапшоте gamedata).
/// </summary>
public sealed class Project
{
    [JsonPropertyName("version")] public int Version { get; set; } = ProjectSchema.Version;
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = ProjectSchema.DefaultProjectName;
    [JsonPropertyName("dialogueId")] public string DialogueId { get; set; } = ProjectSchema.DefaultProjectName;

    /// <summary>Серия игры: soc / cs / cop (влияет на путь экспорта `config`|`configs`).</summary>
    [JsonPropertyName("series")] public string Series { get; set; } = "soc";

    [JsonPropertyName("theme")] public string Theme { get; set; } = "dark";
    [JsonPropertyName("gamedataPath")] public string GamedataPath { get; set; } = "";
    [JsonPropertyName("resourcesPath")] public string ResourcesPath { get; set; } = "";
    [JsonPropertyName("nodes")] public List<DialogueNode> Nodes { get; set; } = new();
    [JsonPropertyName("edges")] public List<DialogueEdge> Edges { get; set; } = new();
    [JsonPropertyName("characters")] public List<Character> Characters { get; set; } = new();
    [JsonPropertyName("dialogues")] public List<DialogueGraph> Dialogues { get; set; } = new();
    [JsonPropertyName("quests")] public List<Quest> Quests { get; set; } = new();
    [JsonPropertyName("generalCommunities")] public List<string> GeneralCommunities { get; set; } = new();
    [JsonPropertyName("itemsLtx")] public string ItemsLtx { get; set; } = "";

    [JsonPropertyName("scripts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ScriptFile>? Scripts { get; set; } = new();

    [JsonPropertyName("systemLtx")] public string SystemLtx { get; set; } = "";
    [JsonPropertyName("languages")] public List<Language> Languages { get; set; } = new();
    [JsonPropertyName("atlas")] public ProjectAtlas Atlas { get; set; } = new();

    [JsonPropertyName("ltxFiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? LtxFiles { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("textures")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectTexture>? Textures { get; set; } = new();

    [JsonPropertyName("itemCategories")] public Dictionary<string, string> ItemCategories { get; set; } = new(StringComparer.Ordinal);
    [JsonPropertyName("settings")] public ProjectSettings Settings { get; set; } = new();

    [JsonPropertyName("itemFiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? ItemFiles { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("itemOrigins")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? ItemOrigins { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("equipSlots")] public Dictionary<string, string> EquipSlots { get; set; } = new(StringComparer.Ordinal);
    [JsonPropertyName("pinnedIcons")] public List<PinnedIcon> PinnedIcons { get; set; } = new();

    [JsonPropertyName("pinCollections")]
    public Dictionary<string, List<PinnedIcon>> PinCollections { get; set; } = new(StringComparer.Ordinal) { ["default"] = new() };

    // --- результаты сканирования gamedata: пишутся только если найдены (как в JS) ---

    [JsonPropertyName("gameRelations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameRelationsInfo? GameRelations { get; set; }

    [JsonPropertyName("squads")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SquadDesc>? Squads { get; set; }

    [JsonPropertyName("secrets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SecretsInfo? Secrets { get; set; }

    [JsonPropertyName("spawnSections")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SpawnSectionInfo>? SpawnSections { get; set; }

    [JsonPropertyName("actor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActorInfo? Actor { get; set; }

    [JsonPropertyName("monsters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MonsterInfo>? Monsters { get; set; }

    [JsonPropertyName("npcProfiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NpcProfileCharacter>? NpcProfiles { get; set; }

    [JsonPropertyName("textureMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TextureMapEntry>? TextureMap { get; set; }

    [JsonPropertyName("infoPortions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InfoPortion>? InfoPortions { get; set; }

    [JsonPropertyName("encyclopedia")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EncyclopediaArticle>? Encyclopedia { get; set; }

    [JsonPropertyName("trade")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TradeInfo? Trade { get; set; }

    /// <summary>surge_manager.ltx: настройки и список выбросов (#80).</summary>
    [JsonPropertyName("surgeManager")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SurgeManagerInfo? SurgeManager { get; set; }

    /// <summary>`generalDesc` сканера gamedata: текст файла и сообщества (в JS — как есть).</summary>
    [JsonPropertyName("generalDesc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? GeneralDesc { get; set; }

    /// <summary>Все прочие ключи файла — сохраняются без потерь (в JS живут в самом объекте).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}