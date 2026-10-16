using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Core.Xml;

/// <summary>Импорт диалогов из XML с нормализацией и раскладкой (порт xml.js parseDialogueXml).</summary>
public static class DialogueImporter
{
    public static List<DialogueGraph> Import(string? text)
    {
        var result = new List<DialogueGraph>();
        foreach (var dialog in GraphData.FlattenDialogues(XmlParsers.ParseDialogueXml(text)))
        {
            for (var i = 0; i < dialog.Nodes.Count; i++)
                dialog.Nodes[i] = GraphData.SafeNode(dialog.Nodes[i], i);

            foreach (var n in dialog.Nodes)
            {
                if (n.Type != NodeTypes.Phrase || n.Data == null) continue;
                if (n.Data.RoleSource.Length == 0)
                {
                    n.Data.Role = "unknown";
                    n.Data.RoleSource = "unknown";
                }
            }

            GraphLayout.LayoutDialogue(dialog.Nodes, dialog.Edges.Where(e => !e.Broken).ToList());
            result.Add(dialog);
        }

        return result;
    }
}