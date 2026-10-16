using System.Text.RegularExpressions;
using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

public sealed class BracketBalance
{
    public bool Ok { get; set; }
    public int Depth { get; set; }
}

/// <summary>
/// Нормализация данных узлов диалога и проверки метаданных
/// (порт safeMetadata/safeData/safeNode/flattenDialogues/validateMetadataSyntax из graph.js).
/// </summary>
public static class GraphData
{
    /// <summary>Привести метаданные к каноническому набору из 6 ключей (значения — списки строк).</summary>
    public static Dictionary<string, List<string>> SafeMetadata(IDictionary<string, List<string>>? value)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var key in NodeTypes.MetaKeys)
        {
            var list = new List<string>();
            if (value != null && value.TryGetValue(key, out var v) && v != null)
                foreach (var x in v)
                    if (x != null) list.Add(x);
            result[key] = list;
        }
        return result;
    }

    /// <summary>Нормализовать данные реплики: допустимые роли, канонические метаданные.</summary>
    public static NodeData SafeData(NodeData? value)
    {
        var src = value ?? new NodeData();
        var role = src.Role is "unknown" or "npc" or "player" ? src.Role : "unknown";
        return new NodeData
        {
            PhraseId = src.PhraseId,
            Role = role,
            RoleSource = src.RoleSource,
            TextId = src.TextId,
            UseId = src.UseId,
            Text = src.Text,
            VoiceId = src.VoiceId,
            Expression = src.Expression,
            ActionType = src.ActionType,
            Params = src.Params,
            Metadata = SafeMetadata(src.Metadata),
            Extra = src.Extra
        };
    }

    /// <summary>Нормализовать узел: миграция NPC_REPLY/PLAYER_REPLY → PHRASE, дефолты id и координат.</summary>
    public static DialogueNode SafeNode(DialogueNode? node, int index)
    {
        var raw = node ?? new DialogueNode();
        var oldType = raw.Type;
        var type = oldType is "NPC_REPLY" or "PLAYER_REPLY"
            ? NodeTypes.Phrase
            : (string.IsNullOrEmpty(oldType) ? NodeTypes.Unknown : oldType);

        var data = SafeData(raw.Data);
        if (oldType == "NPC_REPLY") data.Role = "npc";
        if (oldType == "PLAYER_REPLY") data.Role = "player";
        if (oldType is "NPC_REPLY" or "PLAYER_REPLY") data.RoleSource = "migration";
        if (type == NodeTypes.Phrase && data.PhraseId.Length == 0) data.PhraseId = string.IsNullOrEmpty(raw.Id) ? index.ToString() : raw.Id;

        return new DialogueNode
        {
            Id = string.IsNullOrEmpty(raw.Id) ? "node_" + index : raw.Id,
            Type = type,
            X = raw.X,
            Y = raw.Y,
            Data = data,
            Extra = raw.Extra
        };
    }

    /// <summary>Развернуть список диалогов, нормализуя id/name/nodes/edges.</summary>
    public static List<DialogueGraph> FlattenDialogues(IEnumerable<DialogueGraph?>? value)
    {
        var source = new List<DialogueGraph>();
        foreach (var item in value ?? Enumerable.Empty<DialogueGraph?>())
            if (item != null) source.Add(item);

        var result = new List<DialogueGraph>();
        for (var i = 0; i < source.Count; i++)
        {
            var d = source[i];
            var id = string.IsNullOrEmpty(d.Id) ? $"dialogue_{i}" : d.Id;
            var nodes = new List<DialogueNode>();
            for (var n = 0; n < d.Nodes.Count; n++) nodes.Add(SafeNode(d.Nodes[n], n));

            result.Add(new DialogueGraph
            {
                Id = id,
                Name = string.IsNullOrEmpty(d.Name) ? id : d.Name,
                Nodes = nodes,
                Edges = d.Edges.Where(e => e != null).ToList(),
                Warnings = d.Warnings,
                Imported = d.Imported,
                SourcePath = d.SourcePath,
                SourceFile = d.SourceFile,
                Extra = d.Extra
            });
        }

        return result;
    }

    /// <summary>Подсказка text_id для реплики из phraseId/dialogueId (#3).</summary>
    public static string SuggestTextId(DialogueNode? node, string? dialogueId)
    {
        var d = SafeData(node?.Data);
        var baseName = Regex.Replace(string.IsNullOrEmpty(dialogueId) ? "dialogue" : dialogueId, "[^a-zA-Z0-9_]", "_");
        if (baseName.Length == 0) baseName = "dialogue";

        var rawPid = d.PhraseId.Length > 0 ? d.PhraseId : (node?.Id ?? string.Empty);
        var pid = Regex.Replace(rawPid, "[^a-zA-Z0-9_]", "_");
        if (pid.Length == 0) pid = "phrase";

        return pid.StartsWith(baseName + "_", StringComparison.Ordinal) ? pid : baseName + "_" + pid;
    }

    /// <summary>Проверка баланса скобок/кавычек в precondition/action (#4).</summary>
    public static BracketBalance BracketBalanceOf(string? s)
    {
        var str = s ?? string.Empty;
        var stack = new List<char>();
        var n = str.Length;
        var i = 0;

        while (i < n)
        {
            var c = str[i];

            if (c == '"' || c == '\'')
            {
                var q = c;
                i++;
                while (i < n)
                {
                    if (str[i] == '\\') i += 2;
                    else if (str[i] == q) { i++; break; }
                    else i++;
                }
                continue;
            }

            if (c == '-' && i + 1 < n && str[i + 1] == '-' && i + 2 < n && str[i + 2] == '[')
            {
                i += 3;
                while (i < n && !(str[i] == ']' && i + 1 < n && str[i + 1] == ']')) i++;
                i = Math.Min(i + 2, n);
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                stack.Add(c);
            }
            else if (c is ')' or ']' or '}')
            {
                var top = stack.Count > 0 ? stack[^1] : '\0';
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                var expected = c == ')' ? '(' : c == ']' ? '[' : '{';
                if (top != expected) return new BracketBalance { Ok = false, Depth = stack.Count };
            }

            i++;
        }

        return new BracketBalance { Ok = stack.Count == 0, Depth = stack.Count };
    }

    /// <summary>Синтаксическая проверка метаданных реплики (#4): info-порции и условия/действия.</summary>
    public static List<GraphIssue> ValidateMetadataSyntax(IDictionary<string, List<string>>? metadata)
    {
        var issues = new List<GraphIssue>();
        var m = SafeMetadata(metadata);

        foreach (var key in new[] { "has_info", "dont_has_info", "give_info", "disable_info" })
        {
            foreach (var raw in m[key])
            {
                var v = (raw ?? string.Empty).Trim();
                if (v.Length == 0) continue;
                if (Regex.IsMatch(v, @"[;{}\[\]<>]"))
                    issues.Add(new GraphIssue
                    {
                        Severity = "warning", Code = "metadata_syntax", Key = key,
                        Message = key + ": недопустимые символы в «" + v + "»"
                    });
                if (v.Contains('(') && !v.Contains(')'))
                    issues.Add(new GraphIssue
                    {
                        Severity = "warning", Code = "metadata_syntax", Key = key,
                        Message = key + ": незакрытая скобка в «" + v + "»"
                    });
            }
        }

        foreach (var key in new[] { "precondition", "action" })
        {
            foreach (var raw in m[key])
            {
                var v = raw ?? string.Empty;
                if (v.Trim().Length == 0) continue;
                if (!BracketBalanceOf(v).Ok)
                    issues.Add(new GraphIssue
                    {
                        Severity = "error", Code = "metadata_balance", Key = key,
                        Message = key + ": несбалансированные скобки/кавычки"
                    });
            }
        }

        return issues;
    }
}