namespace XRayForge.Core.Models;

public sealed class NodeData
{
    public string PhraseId { get; set; } = "";
    public string Role { get; set; } = "unknown";
    public string RoleSource { get; set; } = "unknown";
    public string TextId { get; set; } = "";
    public bool UseId { get; set; }
    public string Text { get; set; } = "";
    public string VoiceId { get; set; } = "";
    public string Expression { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string Params { get; set; } = "";
    public Dictionary<string, List<string>> Metadata { get; set; } = new();
}

public sealed class DialogueNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = NodeTypes.Phrase;
    public double X { get; set; }
    public double Y { get; set; }
    public NodeData? Data { get; set; }
}

public sealed class DialogueEdge
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public bool Broken { get; set; }
    public string? OriginalNext { get; set; }
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
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<DialogueNode> Nodes { get; set; } = new();
    public List<DialogueEdge> Edges { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool Imported { get; set; }
}
