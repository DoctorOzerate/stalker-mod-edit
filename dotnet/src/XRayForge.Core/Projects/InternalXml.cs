using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XRayForge.Core.Dialogs;

namespace XRayForge.Core.Projects;

/// <summary>
/// Внутренний XML-экспорт редактора — порт `toXml` из App.js
/// (schema `x-ray-forge/internal-xml-v1`, payload — JSON узлов и связей).
/// </summary>
public static class InternalXml
{
    public const string Schema = "x-ray-forge/internal-xml-v1";
    public const string RootTag = "dialogue_editor";

    /// <summary>`toXml(project.dialogueId || project.projectName, …)`.</summary>
    public static string ToXml(Project? project)
    {
        var data = project ?? new Project();
        var name = string.IsNullOrEmpty(data.DialogueId) ? data.ProjectName : data.DialogueId;

        var payload = new JsonObject
        {
            ["schema"] = Schema,
            ["projectName"] = name,
            ["series"] = data.Series,
            ["nodes"] = JsonSerializer.SerializeToNode(data.Nodes ?? new List<Models.DialogueNode>(), ProjectSerializer.Options),
            ["edges"] = JsonSerializer.SerializeToNode(data.Edges ?? new List<Models.DialogueEdge>(), ProjectSerializer.Options)
        }.ToJsonString();

        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>").Append('\n');
        builder.Append("<dialogue_editor schema=\"internal-xml-v1\"><payload>")
            .Append(DialogueExport.Esc(payload))
            .Append("</payload><nodes count=\"")
            .Append((data.Nodes?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("\"/></dialogue_editor>").Append('\n');
        return builder.ToString();
    }

    /// <summary>Значение `schema` из payload — для самопроверки и тестов.</summary>
    public static string? PayloadSchema(string? xml)
    {
        var payload = Payload(xml);
        if (payload == null) return null;
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("schema", out var schema) ? schema.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Содержимое тега `payload` (без XML-экранирования) или `null`.</summary>
    public static string? Payload(string? xml)
    {
        var text = xml ?? "";
        var open = text.IndexOf("<payload>", StringComparison.Ordinal);
        if (open < 0) return null;
        var close = text.IndexOf("</payload>", open, StringComparison.Ordinal);
        if (close < 0) return null;
        var body = text[(open + "<payload>".Length)..close];
        return body
            .Replace("&quot;", "\"").Replace("&apos;", "'")
            .Replace("&lt;", "<").Replace("&gt;", ">")
            .Replace("&amp;", "&");
    }
}
