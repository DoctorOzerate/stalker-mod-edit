using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

public sealed class DialogueStats
{
    public int Nodes { get; set; }
    public int Edges { get; set; }
    public int Phrases { get; set; }
    public int Branches { get; set; }
    public int MaxDepth { get; set; }
    public int AvgText { get; set; }
}

/// <summary>Раскладка и представление графа диалога (порт layoutDialogue/nodeLabel/roleColor/dialogueStats).</summary>
public static class GraphLayout
{
    /// <summary>Шаг уровней по горизонтали (`xs`): карточка 210 px плюс просвет между колонками.</summary>
    public const double LevelStep = 320;

    /// <summary>Минимальный шаг узлов одного уровня по вертикали (`ys`).</summary>
    public const double RowStep = 110;

    /// <summary>Просвет между карточками одного уровня (`110 − 90` исходной сетки).</summary>
    public const double RowGap = 20;

    /// <summary>Ширина карточки (`w-[210px]`), повторяет `GraphEditing.NodeWidth`.</summary>
    private const double CardWidth = 210;

    /// <summary>Высота карточки с одной строкой текста и без бейджей (минимальная).</summary>
    private const double CardBaseHeight = 90;

    /// <summary>Шапка карточки (цветная полоса с иконкой типа).</summary>
    private const double CardHeaderHeight = 26;

    /// <summary>Вертикальные отступы контента карточки (10 сверху + 12 снизу).</summary>
    private const double CardPadding = 22;

    /// <summary>Область текста карточки: `210 − отступы 12+12`.</summary>
    private const double TextWidth = CardWidth - 24;

    /// <summary>Средняя ширина символа при `DensitySmallFontSize` = 11 px (оценка переноса).</summary>
    private const double CharWidth = 6.2;

    /// <summary>Высота одной строки текста реплики.</summary>
    private const double TextLineHeight = 15;

    /// <summary>Высота строки бейджа (озвучка/условия/действия: шрифт 10 px + отступ 4).</summary>
    private const double BadgeLineHeight = 19;

    /// <summary>
    /// Разложить узлы по уровням слева-направо от START (мутирует X/Y узлов). Ряд одного уровня
    /// раздвигается по фактической высоте карточек: длинная реплика выше базовых 90 px и иначе
    /// накрыла бы соседа снизу. Горизонтальный шаг — <see cref="LevelStep"/>, минимальный
    /// вертикальный — <see cref="RowStep"/> (совпадает с прежней сеткой 320×110).
    /// </summary>
    public static void LayoutDialogue(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var list = nodes ?? Array.Empty<DialogueNode>();
        var adj = BuildAdjacency(edges);

        var byId = new Dictionary<string, DialogueNode>();
        foreach (var node in list) byId.TryAdd(node.Id, node);

        var level = new Dictionary<string, int>();
        var start = list.FirstOrDefault(n => n.Type == NodeTypes.Start);
        var queue = new List<string>();
        if (start != null)
        {
            queue.Add(start.Id);
            level[start.Id] = 0;
        }

        var visited = new HashSet<string>();
        var qIndex = 0;
        while (qIndex < queue.Count)
        {
            var cur = queue[qIndex++];
            if (!visited.Add(cur)) continue;
            if (!adj.TryGetValue(cur, out var targets)) continue;
            foreach (var t in targets)
            {
                if (!level.ContainsKey(t)) level[t] = (level.TryGetValue(cur, out var curLevel) ? curLevel : 0) + 1;
                queue.Add(t);
            }
        }

        foreach (var n in list)
            if (!level.ContainsKey(n.Id)) level[n.Id] = 0;

        var groups = new Dictionary<int, List<string>>();
        foreach (var n in list)
        {
            var l = level[n.Id];
            if (!groups.TryGetValue(l, out var ids)) groups[l] = ids = new List<string>();
            ids.Add(n.Id);
        }

        const double lx = 60, ly = 80;
        foreach (var l in groups.Keys.OrderBy(k => k))
        {
            var ids = groups[l];
            var y = ly;
            foreach (var id in ids)
            {
                if (!byId.TryGetValue(id, out var n)) continue;
                n.X = lx + l * LevelStep;
                n.Y = y;
                // Ряд раздвигается по высоте карточки: длинная реплика (или бейджи) выше базовых
                // 90 px и иначе накрыла бы узлы ниже; для типовых карточек шаг остаётся 110 px.
                y += Math.Max(RowStep, NodeHeight(n) + RowGap);
            }
        }
    }

    /// <summary>
    /// Оценка высоты карточки узла на холсте: WPF растягивает карточку по числу строк
    /// переносящегося текста и бейджей (`MinHeight` 64, но связи и хит-тесты считают от 90).
    /// Раскладке нужна та же оценка, иначе высокие карточки накладываются друг на друга.
    /// </summary>
    public static double NodeHeight(DialogueNode? node)
    {
        var data = node?.Data;
        if (data == null) return CardBaseHeight;

        var text = RawText(data);
        var charsPerLine = Math.Max(1, (int)Math.Floor(TextWidth / CharWidth));
        var lines = Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerLine));
        var badges = BadgeCount(data);

        var height = CardHeaderHeight + CardPadding + lines * TextLineHeight + badges * BadgeLineHeight;
        return Math.Max(CardBaseHeight, Math.Round(height, MidpointRounding.AwayFromZero));
    }

    /// <summary>Текст карточки без перевода (`text`, `expression`, `actionType` или `#text_id`).</summary>
    private static string RawText(NodeData data)
    {
        if (data.UseId && data.TextId.Length > 0) return "#" + data.TextId;
        if (data.Text.Length > 0) return data.Text;
        if (data.Expression.Length > 0) return data.Expression;
        return data.ActionType;
    }

    /// <summary>Число строк-бейджей карточки: озвучка, условия и действия (как `RefreshBadges`).</summary>
    private static int BadgeCount(NodeData data)
    {
        var count = 0;
        if (!string.IsNullOrEmpty(data.VoiceId)) count++;
        if (HasValues(data.Metadata, "has_info", "dont_has_info", "precondition")) count++;
        if (HasValues(data.Metadata, "action", "give_info")) count++;
        return count;
    }

    /// <summary>Есть ли у ключей метаданных хотя бы одно непустое значение (бейдж не пустой).</summary>
    private static bool HasValues(IReadOnlyDictionary<string, List<string>>? metadata, params string[] keys)
    {
        if (metadata == null) return false;
        foreach (var key in keys)
            if (metadata.TryGetValue(key, out var values) && values != null && values.Exists(v => !string.IsNullOrEmpty(v)))
                return true;
        return false;
    }

    /// <summary>Текст для узла: перевод text_id или собственный текст реплики.</summary>
    public static string NodeLabel(DialogueNode? n, IReadOnlyList<Language>? languages, bool showText = true)
    {
        var d = n?.Data ?? new NodeData();
        if (d.UseId && d.TextId.Length > 0)
        {
            if (!showText) return "#" + d.TextId;
            foreach (var lang in languages ?? Array.Empty<Language>())
            {
                var e = lang.Entries.FirstOrDefault(x => x.Id == d.TextId);
                if (e != null && e.Text.Length > 0) return e.Text;
            }
            return "#" + d.TextId;
        }

        var text = d.Text.Length > 0 ? d.Text : d.Expression.Length > 0 ? d.Expression : d.ActionType;
        return text.Length > 0 ? text : "Нет содержимого";
    }

    /// <summary>Цвет узла реплики по роли (npc/player/не определено); для остальных типов — null.</summary>
    public static string? RoleColor(DialogueNode? n)
    {
        if (n == null || n.Type != NodeTypes.Phrase) return null;
        var role = n.Data?.Role;
        return role == "npc" ? "#2563eb" : role == "player" ? "#16a34a" : "#64748b";
    }

    /// <summary>Сводная статистика диалога.</summary>
    public static DialogueStats Stats(IReadOnlyList<DialogueNode>? nodes, IReadOnlyList<DialogueEdge>? edges)
    {
        var list = nodes ?? Array.Empty<DialogueNode>();
        var edgeList = edges ?? Array.Empty<DialogueEdge>();
        var phrases = list.Where(n => n.Type == NodeTypes.Phrase).ToList();
        var adj = BuildAdjacency(edgeList);

        var depth = new Dictionary<string, int>();
        var queue = new List<string>();
        foreach (var n in list.Where(n => n.Type == NodeTypes.Start))
        {
            depth[n.Id] = 0;
            queue.Add(n.Id);
        }

        var qi = 0;
        while (qi < queue.Count)
        {
            var id = queue[qi++];
            if (!adj.TryGetValue(id, out var targets)) continue;
            foreach (var t in targets)
            {
                if (!depth.ContainsKey(t))
                {
                    depth[t] = depth[id] + 1;
                    queue.Add(t);
                }
            }
        }

        var maxDepth = 0;
        foreach (var v in depth.Values) maxDepth = Math.Max(maxDepth, v);

        var totalText = phrases.Sum(n => (n.Data?.Text ?? string.Empty).Length);

        return new DialogueStats
        {
            Nodes = list.Count,
            Edges = edgeList.Count,
            Phrases = phrases.Count,
            Branches = phrases.Count(n => adj.TryGetValue(n.Id, out var t) && t.Count > 1),
            MaxDepth = maxDepth,
            AvgText = phrases.Count > 0 ? (int)Math.Round((double)totalText / phrases.Count, MidpointRounding.AwayFromZero) : 0
        };
    }

    internal static Dictionary<string, List<string>> BuildAdjacency(IReadOnlyList<DialogueEdge>? edges)
    {
        var adj = new Dictionary<string, List<string>>();
        foreach (var e in edges ?? Array.Empty<DialogueEdge>())
        {
            if (!adj.TryGetValue(e.Source, out var targets)) adj[e.Source] = targets = new List<string>();
            targets.Add(e.Target);
        }
        return adj;
    }
}