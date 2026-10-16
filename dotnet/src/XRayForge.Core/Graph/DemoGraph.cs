using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>
/// Порт `makeNode`/`seedDialogue` из App.js (кнопка «Демо-граф» приветственного окна):
/// шаблон узла с дефолтными данными реплики и собранный демонстрационный диалог торговца
/// (`dialogueId = trader_demo`, 8 узлов / 8 связей).
/// </summary>
public static class DemoGraph
{
    /// <summary>Идентификатор диалога демо-графа (в JS — `dialogueId: 'trader_demo'`).</summary>
    public const string DialogueId = "trader_demo";

    private const string IdAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>Шаблон узла: `id = type.toLowerCase() + '_' + 6 случайных base36-символов`.</summary>
    public static DialogueNode MakeNode(string type, double x, double y)
    {
        var kind = string.IsNullOrEmpty(type) ? NodeTypes.Phrase : type;
        return new DialogueNode
        {
            Id = kind.ToLowerInvariant() + "_" + RandomSuffix(),
            Type = kind,
            X = x,
            Y = y,
            Data = new NodeData
            {
                PhraseId = "",
                Role = "unknown",
                RoleSource = "manual",
                Text = "",
                TextId = "",
                UseId = false,
                VoiceId = "",
                Expression = "",
                ActionType = "",
                Params = "",
                Metadata = new Dictionary<string, List<string>>(StringComparer.Ordinal)
                {
                    ["has_info"] = new List<string>(),
                    ["dont_has_info"] = new List<string>(),
                    ["precondition"] = new List<string>(),
                    ["give_info"] = new List<string>(),
                    ["disable_info"] = new List<string>(),
                    ["action"] = new List<string>()
                }
            }
        };
    }

    /// <summary>Демонстрационный граф (узлы прогнаны через `GroupData.SafeNode`, как в JS).</summary>
    public static (List<DialogueNode> Nodes, List<DialogueEdge> Edges) Seed()
    {
        var nodes = new List<DialogueNode>
        {
            WithId(MakeNode(NodeTypes.Start, 60, 180), "start"),
            WithId(WithText(MakeNode(NodeTypes.Phrase, 340, 150), "Ну что, сталкер, принёс артефакт?"), "npc_hi"),
            WithId(WithText(MakeNode(NodeTypes.Phrase, 650, 80), "Да, вот он."), "player_yes"),
            WithId(WithText(MakeNode(NodeTypes.Phrase, 650, 240), "Нет, я ещё вернусь."), "player_no"),
            WithId(WithExpression(MakeNode(NodeTypes.ItemCheck, 960, 80), "af_medusa"), "check_artifact"),
            WithId(WithAction(MakeNode(NodeTypes.Action, 1260, 80), "give_item", "medkit"), "reward"),
            WithId(WithText(MakeNode(NodeTypes.Phrase, 960, 250), "Возвращайся, когда будет что показать."), "bye"),
            WithId(MakeNode(NodeTypes.End, 1560, 150), "end")
        };

        for (var i = 0; i < nodes.Count; i++) nodes[i] = GraphData.SafeNode(nodes[i], i);

        var pairs = new (string Source, string Target)[]
        {
            ("start", "npc_hi"),
            ("npc_hi", "player_yes"),
            ("npc_hi", "player_no"),
            ("player_yes", "check_artifact"),
            ("check_artifact", "reward"),
            ("reward", "end"),
            ("player_no", "bye"),
            ("bye", "end")
        };

        var edges = new List<DialogueEdge>(pairs.Length);
        foreach (var pair in pairs)
        {
            edges.Add(new DialogueEdge
            {
                Id = "e_" + pair.Source + "_" + pair.Target,
                Source = pair.Source,
                Target = pair.Target
            });
        }

        return (nodes, edges);
    }

    private static string RandomSuffix()
    {
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++) chars[i] = IdAlphabet[Random.Shared.Next(IdAlphabet.Length)];
        return new string(chars);
    }

    private static DialogueNode WithId(DialogueNode node, string id)
    {
        node.Id = id;
        return node;
    }

    private static DialogueNode WithText(DialogueNode node, string text)
    {
        if (node.Data != null) node.Data.Text = text;
        return node;
    }

    private static DialogueNode WithExpression(DialogueNode node, string expression)
    {
        if (node.Data != null) node.Data.Expression = expression;
        return node;
    }

    private static DialogueNode WithAction(DialogueNode node, string actionType, string parameters)
    {
        if (node.Data != null)
        {
            node.Data.ActionType = actionType;
            node.Data.Params = parameters;
        }

        return node;
    }
}