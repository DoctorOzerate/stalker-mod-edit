using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Storage;

/// <summary>Проект в списке профиля (в JS — элементы `PROJECTS_KEY`).</summary>
public sealed class StoredProject
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; set; }
    [JsonPropertyName("data")] public Project Data { get; set; } = new();
}

/// <summary>Резервная копия проекта (в JS — элементы `BACKUPS_KEY`).</summary>
public sealed class StoredBackup
{
    [JsonPropertyName("t")] public long Timestamp { get; set; }
    [JsonPropertyName("data")] public Project Data { get; set; } = new();
}

/// <summary>Снапшот gamedata проекта (`gamedataOf`): то, что не пишется в `.project.json`.</summary>
public sealed class GamedataSnapshot
{
    [JsonPropertyName("ltxFiles")] public Dictionary<string, string>? LtxFiles { get; set; }
    [JsonPropertyName("textures")] public List<ProjectTexture>? Textures { get; set; }
    [JsonPropertyName("scripts")] public List<ScriptFile>? Scripts { get; set; }
    [JsonPropertyName("itemFiles")] public Dictionary<string, string>? ItemFiles { get; set; }
    [JsonPropertyName("itemOrigins")] public Dictionary<string, string>? ItemOrigins { get; set; }
}