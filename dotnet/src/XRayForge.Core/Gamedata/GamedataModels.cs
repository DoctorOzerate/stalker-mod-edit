using System.Text.Json;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Gamedata;

/// <summary>Файл gamedata: имя, нормализованный путь и содержимое (аналог `File` из браузера).</summary>
public sealed class GamedataFile
{
    private readonly Func<byte[]>? _loader;
    private byte[]? _content;

    public GamedataFile(string name, string path, byte[] content)
    {
        Name = name ?? "";
        Path = path ?? "";
        _content = content ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Файл на диске: содержимое читается при первом обращении, как `file.arrayBuffer()`
    /// в JS. Так сканирование не загружает в память гигабайты звуков/моделей/текстур,
    /// которые всё равно не разбираются.
    /// </summary>
    public GamedataFile(string name, string path, string fullPath)
    {
        Name = name ?? "";
        Path = path ?? "";
        _loader = () => File.ReadAllBytes(fullPath);
    }

    public string Name { get; } = "";
    public string Path { get; } = "";
    public byte[] Content => _content ??= _loader?.Invoke() ?? Array.Empty<byte>();
}

/// <summary>Файл, который не удалось разобрать (`skipped` в App.js).</summary>
public sealed class SkippedFile
{
    public SkippedFile(string path, string error)
    {
        Path = path ?? "";
        Error = error ?? "";
    }

    public string Path { get; }
    public string Error { get; }
}

/// <summary>Персонажи из одного XML (`foundCharacters`): файл, путь и результат разбора.</summary>
public sealed class ScannedCharacterFile
{
    public string File { get; set; } = "";
    public string Path { get; set; } = "";
    public List<Character> Characters { get; set; } = new();
}

/// <summary>Диалоги из одного XML (`foundDialogues`).</summary>
public sealed class ScannedDialogueFile
{
    public string File { get; set; } = "";
    public string Path { get; set; } = "";
    public List<DialogueGraph> Dialogues { get; set; } = new();
}

/// <summary>Настройки сканирования gamedata.</summary>
public sealed class GamedataScanOptions
{
    /// <summary>`project.settings.importEncoding`; null — взять из проекта (как в JS).</summary>
    public string? ImportEncoding { get; init; }

    /// <summary>RGBA → data URL (в Core нет canvas; ставится WPF-слоем, этапы 5–7).</summary>
    public Func<byte[], int, int, string>? PngEncoder { get; init; }

    /// <summary>Прогресс: `(done, total)` — как `setLoadProgress` (каждые `ProgressStep` файлов).</summary>
    public Action<int, int>? Progress { get; init; }

    /// <summary>Частота обновления прогресса (в JS — каждый 5-й файл и последний).</summary>
    public int ProgressStep { get; init; } = 5;
}

/// <summary>Счётчики сканирования — как объект, который возвращает `processGamedataMap`.</summary>
public sealed class GamedataScanStats
{
    public int Files { get; set; }
    public int Scripts { get; set; }
    public int Textures { get; set; }
    public int Characters { get; set; }
    public int Dialogues { get; set; }
    public int Languages { get; set; }
    public int Quests { get; set; }
    public int Items { get; set; }
    public int Skipped { get; set; }
}

/// <summary>
/// Набор полей для `patch()` — то, что сканер добавляет в проект (`next` в App.js).
/// Поля, которые сканер не нашёл, остаются null и проект не трогают.
/// </summary>
public sealed class GamedataPatch
{
    public string? GamedataPath { get; set; }
    public Dictionary<string, string>? LtxFiles { get; set; }
    public List<Character>? Characters { get; set; }
    public List<Language>? Languages { get; set; }
    public List<Quest>? Quests { get; set; }
    public Dictionary<string, string>? ItemFiles { get; set; }
    public Dictionary<string, string>? ItemOrigins { get; set; }
    public string? SystemLtx { get; set; }
    public string? ItemsLtx { get; set; }
    public List<ScriptFile>? Scripts { get; set; }
    public List<ProjectTexture>? Textures { get; set; }
    public List<DialogueGraph>? Dialogues { get; set; }
    public List<string>? GeneralCommunities { get; set; }
    public JsonElement? GeneralDesc { get; set; }
    public GameRelationsInfo? GameRelations { get; set; }
    public List<SquadDesc>? Squads { get; set; }
    public SecretsInfo? Secrets { get; set; }
    public List<SpawnSectionInfo>? SpawnSections { get; set; }
    public ActorInfo? Actor { get; set; }
    public List<MonsterInfo>? Monsters { get; set; }
    public List<NpcProfileCharacter>? NpcProfiles { get; set; }
    public Dictionary<string, TextureMapEntry>? TextureMap { get; set; }
    public List<InfoPortion>? InfoPortions { get; set; }
    public List<EncyclopediaArticle>? Encyclopedia { get; set; }
    public TradeInfo? Trade { get; set; }
    public SurgeManagerInfo? SurgeManager { get; set; }

    /// <summary>`patch(next)` — перенести найденное в проект.</summary>
    public void Apply(Project project)
    {
        if (project == null) return;
        if (GamedataPath != null) project.GamedataPath = GamedataPath;
        if (LtxFiles != null) project.LtxFiles = LtxFiles;
        if (Characters != null) project.Characters = Characters;
        if (Languages != null) project.Languages = Languages;
        if (Quests != null) project.Quests = Quests;
        if (ItemFiles != null) project.ItemFiles = ItemFiles;
        if (ItemOrigins != null) project.ItemOrigins = ItemOrigins;
        if (SystemLtx != null) project.SystemLtx = SystemLtx;
        if (ItemsLtx != null) project.ItemsLtx = ItemsLtx;
        if (Scripts != null) project.Scripts = Scripts;
        if (Textures != null) project.Textures = Textures;
        if (Dialogues != null) project.Dialogues = Dialogues;
        if (GeneralCommunities != null) project.GeneralCommunities = GeneralCommunities;
        if (GeneralDesc != null) project.GeneralDesc = GeneralDesc;
        if (GameRelations != null) project.GameRelations = GameRelations;
        if (Squads != null) project.Squads = Squads;
        if (Secrets != null) project.Secrets = Secrets;
        if (SpawnSections != null) project.SpawnSections = SpawnSections;
        if (Actor != null) project.Actor = Actor;
        if (Monsters != null) project.Monsters = Monsters;
        if (NpcProfiles != null) project.NpcProfiles = NpcProfiles;
        if (TextureMap != null) project.TextureMap = TextureMap;
        if (InfoPortions != null) project.InfoPortions = InfoPortions;
        if (Encyclopedia != null) project.Encyclopedia = Encyclopedia;
        if (Trade != null) project.Trade = Trade;
        if (SurgeManager != null) project.SurgeManager = SurgeManager;
    }
}

/// <summary>Результат сканирования: набор полей для `patch()` + статистика и пропуски.</summary>
public sealed class GamedataScanResult
{
    /// <summary>Поля, которые сканер добавляет в проект (`next` в App.js).</summary>
    public GamedataPatch Patch { get; init; } = new();

    public GamedataScanStats Stats { get; init; } = new();
    public List<SkippedFile> Skipped { get; init; } = new();
}

/// <summary>`generalDesc` сканера: исходный текст `character_desc_general.xml` и его сообщества.</summary>
public sealed class GamedataGeneralDesc
{
    [System.Text.Json.Serialization.JsonPropertyName("text")] public string Text { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("communities")] public List<Community> Communities { get; set; } = new();
}