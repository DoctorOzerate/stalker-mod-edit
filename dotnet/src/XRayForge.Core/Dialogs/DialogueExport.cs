using System.Text;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.Core.Dialogs;

/// <summary>Экспорт диалогов в игровые XML: &lt;dialog&gt;, dialogs.ltx (#1, #4).</summary>
public static class DialogueExport
{
    /// <summary>Экранирование XML-сущностей (аналог esc из dialogs.js).</summary>
    public static string Esc(string? v) =>
        (v ?? string.Empty)
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&apos;");

    /// <summary>
    /// Один диалог → строка &lt;dialog&gt;...&lt;/dialog&gt; с полным round-trip:
    /// text, sound, 6 тегов метаданных, next (#4). Бросает исключение при ошибках графа.
    /// </summary>
    public static string DialogueToXml(DialogueGraph? dialog)
    {
        var name = !string.IsNullOrEmpty(dialog?.Id) ? dialog!.Id
            : !string.IsNullOrEmpty(dialog?.Name) ? dialog!.Name
            : "dialogue";

        var nodes = dialog?.Nodes ?? new List<DialogueNode>();
        var edges = dialog?.Edges ?? new List<DialogueEdge>();

        var errors = GraphValidator.Validate(nodes, edges).Count(i => i.Severity == "error");
        if (errors > 0)
            throw new InvalidOperationException($"Диалог {name}: исправьте ошибки графа ({errors})");

        var real = nodes.Where(n => n.Type == NodeTypes.Phrase).ToList();

        var idOf = new Dictionary<string, string>();
        var used = new HashSet<string>();
        for (var i = 0; i < real.Count; i++)
        {
            var node = real[i];
            var id = !string.IsNullOrEmpty(node.Data?.PhraseId) ? node.Data!.PhraseId : i.ToString();
            var baseId = id;
            var k = 1;
            while (used.Contains(id)) id = baseId + "_" + k++;
            used.Add(id);
            idOf[node.Id] = id;
        }

        var outMap = new Dictionary<string, List<string>>();
        foreach (var e in edges)
        {
            if (e.Broken || !idOf.ContainsKey(e.Source) || !idOf.ContainsKey(e.Target)) continue;
            if (!outMap.TryGetValue(e.Source, out var list)) outMap[e.Source] = list = new List<string>();
            list.Add(idOf[e.Target]);
        }

        var phrases = new StringBuilder();
        foreach (var node in real)
        {
            var d = node.Data ?? new NodeData();
            phrases.Append("<phrase id=\"").Append(Esc(idOf[node.Id])).Append("\"><text>")
                .Append(Esc(d.TextId)).Append("</text>");
            if (d.VoiceId.Length > 0)
                phrases.Append("<sound>").Append(Esc(d.VoiceId)).Append("</sound>");
            phrases.Append(Tags(d));
            if (outMap.TryGetValue(node.Id, out var targets))
                foreach (var t in targets)
                    phrases.Append("<next>").Append(Esc(t)).Append("</next>");
            phrases.Append("</phrase>");
        }

        return "<dialog id=\"" + Esc(name) + "\"><phrase_list>" + phrases + "</phrase_list></dialog>";
    }

    /// <summary>Теги метаданных реплики в фиксированном порядке has_info…action.</summary>
    private static string Tags(NodeData d)
    {
        var sb = new StringBuilder();
        foreach (var key in NodeTypes.MetaKeys)
        {
            if (!d.Metadata.TryGetValue(key, out var values) || values == null) continue;
            foreach (var v in values)
                sb.Append('<').Append(key).Append('>').Append(Esc(v)).Append("</").Append(key).Append('>');
        }
        return sb.ToString();
    }

    /// <summary>`dialogs.ltx` со списком всех диалогов (#1).</summary>
    public static string DialogsLtx(IEnumerable<string>? ids) =>
        "[dialogs]\ndialogs = " + string.Join(", ", ids ?? Enumerable.Empty<string>()) + "\n";
}