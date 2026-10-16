using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRayForge.Core.Models;

public sealed class MapIcon
{
    [JsonPropertyName("x")] public string X { get; set; } = "";
    [JsonPropertyName("y")] public string Y { get; set; } = "";
}

/// <summary>
/// Персонаж (`character_desc_*.xml`). Часть ключей в `.project.json` — snake_case
/// (`terrain_sect`, `snd_config`, `map_icon`, `start_dialog`, `actor_dialog`).
/// </summary>
public sealed class Character
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("community")] public string Community { get; set; } = "";
    [JsonPropertyName("rank")] public string Rank { get; set; } = "";
    [JsonPropertyName("reputation")] public string Reputation { get; set; } = "";
    [JsonPropertyName("class")] public string Class { get; set; } = "";
    [JsonPropertyName("terrain_sect")] public string TerrainSect { get; set; } = "";
    [JsonPropertyName("snd_config")] public string SndConfig { get; set; } = "";
    [JsonPropertyName("map_icon")] public MapIcon? MapIcon { get; set; }
    [JsonPropertyName("bio")] public string Bio { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
    [JsonPropertyName("visual")] public string Visual { get; set; } = "";
    [JsonPropertyName("supplies")] public string Supplies { get; set; } = "";
    [JsonPropertyName("start_dialog")] public List<string> StartDialog { get; set; } = new();
    [JsonPropertyName("actor_dialog")] public List<string> ActorDialog { get; set; } = new();
    [JsonPropertyName("source")] public string Source { get; set; } = "gamedata";

    /// <summary>Снимок полей до правок (в JS — `original`, ставится при слиянии из gamedata).</summary>
    [JsonPropertyName("original")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Original { get; set; }

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
