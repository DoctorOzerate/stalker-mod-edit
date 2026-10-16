using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRayForge.Core.Models;

/// <summary>
/// Данные реплики узла. Имена ключей в `.project.json` — snake_case (`text_id`, `use_id`,
/// `voice_id`, `action_type`), поэтому свойства размечены явно.
/// </summary>
public sealed class NodeData
{
    [JsonPropertyName("phraseId")] public string PhraseId { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "unknown";
    [JsonPropertyName("roleSource")] public string RoleSource { get; set; } = "unknown";
    [JsonPropertyName("text_id")] public string TextId { get; set; } = "";
    [JsonPropertyName("use_id")] public bool UseId { get; set; }
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("voice_id")] public string VoiceId { get; set; } = "";
    [JsonPropertyName("expression")] public string Expression { get; set; } = "";
    [JsonPropertyName("action_type")] public string ActionType { get; set; } = "";
    [JsonPropertyName("params")] public string Params { get; set; } = "";
    [JsonPropertyName("metadata")] public Dictionary<string, List<string>> Metadata { get; set; } = new();

    /// <summary>Поля вне модели (JS `Object.assign` их сохраняет — здесь идут в `JsonExtensionData`).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class DialogueNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = NodeTypes.Phrase;
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("data")] public NodeData? Data { get; set; }

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class DialogueEdge
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("broken")] public bool Broken { get; set; }
    [JsonPropertyName("originalNext")] public string? OriginalNext { get; set; }

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class GraphIssue
{
    public string Severity { get; set; } = ""; // "error" | "warning"
    public string Code { get; set; } = "";
    public string? Key { get; set; }
    public string? Message { get; set; }
}

public sealed class DialogueGraph
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("nodes")] public List<DialogueNode> Nodes { get; set; } = new();
    [JsonPropertyName("edges")] public List<DialogueEdge> Edges { get; set; } = new();
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = new();
    [JsonPropertyName("imported")] public bool Imported { get; set; }

    /// <summary>Путь файла-источника (ставится сканером gamedata, в JS — только у импортированных).</summary>
    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; set; }

    /// <summary>Имя файла-источника (ставится сканером gamedata).</summary>
    [JsonPropertyName("sourceFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFile { get; set; }

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
