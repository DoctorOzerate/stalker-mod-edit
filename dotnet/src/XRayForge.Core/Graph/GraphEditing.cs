using System.Globalization;
using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>Снимок графа для истории отмены/повтора: глубокие копии узлов и связей.</summary>
public sealed class GraphSnapshot
{
    public GraphSnapshot(IEnumerable<DialogueNode>? nodes = null, IEnumerable<DialogueEdge>? edges = null)
    {
        Nodes = (nodes ?? Enumerable.Empty<DialogueNode>()).Select(GraphEditing.Clone).ToList();
        Edges = (edges ?? Enumerable.Empty<DialogueEdge>()).Select(GraphEditing.Clone).ToList();
    }

    /// <summary>Узлы снимка (независимые копии — правка графа снимок не меняет).</summary>
    public List<DialogueNode> Nodes { get; }

    /// <summary>Связи снимка.</summary>
    public List<DialogueEdge> Edges { get; }
}

/// <summary>Вид перетаскивания холста (`dragRef.current.kind` в App.js).</summary>
public enum GraphDragMode
{
    /// <summary>Ничего не тянется: Shift+клик только переключает групповое выделение.</summary>
    None,

    /// <summary>Перенос одного узла.</summary>
    Node,

    /// <summary>Перенос всей группы выделенных узлов (клик по узлу группы).</summary>
    Group,

    /// <summary>Панорамирование холста (нажатие по пустому месту).</summary>
    Pan,

    /// <summary>Рамка выделения (Shift+нажатие по пустому месту).</summary>
    Marquee
}

/// <summary>Порт карточки: вход (`in`, слева) или выход (`out`, справа) — кнопки в `DialoguesTab.js`.</summary>
public enum GraphPortKind
{
    /// <summary>Порт входа: есть у всех типов, кроме START (`n.type !== 'START'`).</summary>
    In,

    /// <summary>Порт выхода: есть у всех типов, кроме END (`n.type !== 'END'`).</summary>
    Out
}

/// <summary>Порт, ожидающий второй клик (`connectFrom = { id, kind }` в App.js).</summary>
public sealed record GraphPortRef(string Id, GraphPortKind Kind);

/// <summary>Итог `portClick`: оставшийся взведённым порт и пара для новой связи.</summary>
public sealed record GraphPortClick(GraphPortRef? Pending, string? Source, string? Target)
{
    /// <summary>Второй порт выбран: связь нужно добавить (если её ещё нет).</summary>
    public bool CreatesEdge => Source != null && Target != null;
}

/// <summary>
/// Чистые операции редактора графа — порт обработчиков App.js (`createDialogue`, `addNode`,
/// `nextPhraseId`, `duplicateNode`, `portClick`, `deleteNode`/`deleteEdge`, рамка выделения,
/// зум и «в центр»). Вынесены в Core, чтобы проверяться тестами без WPF: ViewModel вкладки
/// только вызывает их и обновляет свои коллекции.
/// </summary>
public static class GraphEditing
{
    /// <summary>Ширина карточки узла (CSS `w-[210px]` в DialoguesTab.js).</summary>
    public const double NodeWidth = 210;

    /// <summary>Высота карточки узла.</summary>
    public const double NodeHeight = 90;

    /// <summary>Высота для рамки выделения: в JS hit-тест берёт `n.y + 100`.</summary>
    public const double NodeHitHeight = 100;

    /// <summary>Смещение портов и центров связей по вертикали (`y + 44` в DialoguesTab.js).</summary>
    public const double PortOffsetY = 44;

    /// <summary>Шаг точечной сетки холста (CSS `backgroundSize: 20 * zoom`).</summary>
    public const double GridStep = 20;

    /// <summary>Минимальный масштаб (`setZoom(z => Math.max(0.5, …))`).</summary>
    public const double ZoomMin = 0.5;

    /// <summary>Максимальный масштаб (`Math.min(2, …)`).</summary>
    public const double ZoomMax = 2;

    /// <summary>Шаг масштаба кнопок «−»/«+» (`±0.1`).</summary>
    public const double ZoomStep = 0.1;

    /// <summary>Базовый отступ первого узла из палитры (`120 + n * 24`).</summary>
    public const double PaletteOffsetX = 120;

    /// <summary>Каскад новых узлов (`120 + n * 16`).</summary>
    public const double PaletteOffsetY = 120;

    /// <summary>Шаг каскада новых узлов по горизонтали.</summary>
    public const double PaletteStepX = 24;

    /// <summary>Шаг каскада новых узлов по вертикали.</summary>
    public const double PaletteStepY = 16;

    /// <summary>Смещение копии узла при дублировании (`x + 40`, `y + 40`).</summary>
    public const double CopyOffset = 40;

    /// <summary>Id диалога по умолчанию, если после очистки не осталось символов.</summary>
    public const string FallbackDialogueId = "dialogue";

    private const string SuffixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>`createDialogue`: `String(id).trim().replace(/[^a-zA-Z0-9_]/g, '_') || 'dialogue'`.</summary>
    public static string SanitizeDialogueId(string? id)
    {
        var raw = (id ?? string.Empty).Trim();
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw) sb.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        var clean = sb.ToString();
        return clean.Length > 0 ? clean : FallbackDialogueId;
    }

    /// <summary>
    /// `nextPhraseId`: первый свободный `<base>_1`, `<base>_2`, … (id реплик нового диалога).
    /// Базой служит `dialogueId` проекта, очищенный как в `createDialogue`.
    /// </summary>
    public static string NextPhraseId(IEnumerable<DialogueNode>? nodes, string? dialogueId)
    {
        var basis = SanitizeDialogueId(dialogueId);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes ?? Enumerable.Empty<DialogueNode>())
        {
            var id = node?.Data?.PhraseId;
            if (!string.IsNullOrEmpty(id)) used.Add(id!);
        }

        var n = 1;
        while (used.Contains(basis + "_" + n.ToString(CultureInfo.InvariantCulture))) n++;
        return basis + "_" + n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Позиция нового узла из палитры: `120 + n * 24` / `120 + n * 16`.</summary>
    public static (double X, double Y) PalettePosition(int nodeCount) =>
        (PaletteOffsetX + Math.Max(0, nodeCount) * PaletteStepX,
         PaletteOffsetY + Math.Max(0, nodeCount) * PaletteStepY);

    /// <summary>`createDialogue`: стартовый узел `makeNode('START', 60, 180)` с id `start`.</summary>
    public static DialogueNode CreateStartNode()
    {
        var node = DemoGraph.MakeNode(NodeTypes.Start, 60, 180);
        node.Id = "start";
        return node;
    }

    /// <summary>
    /// `addNode`: узел выбранного типа на позиции каскада; у реплики сразу проставлен `phraseId`.
    /// </summary>
    public static DialogueNode CreateNode(string? type, int nodeCount, string? dialogueId, IEnumerable<DialogueNode>? nodes)
    {
        var kind = string.IsNullOrEmpty(type) ? NodeTypes.Phrase : type;
        var (x, y) = PalettePosition(nodeCount);
        var node = DemoGraph.MakeNode(kind, x, y);
        if (kind == NodeTypes.Phrase && node.Data != null) node.Data.PhraseId = NextPhraseId(nodes, dialogueId);
        return node;
    }

    /// <summary>`duplicateNode`: копия со сдвигом 40/40, новым id и очищенным `phraseId`.</summary>
    public static DialogueNode Duplicate(DialogueNode source)
    {
        var copy = Clone(source);
        copy.Id = "node_" + RandomSuffix();
        copy.X = source.X + CopyOffset;
        copy.Y = source.Y + CopyOffset;
        if (copy.Data != null) copy.Data.PhraseId = "";
        return copy;
    }

    /// <summary>`deleteNode`: узлы и связи без указанного узла (он сам и обе стороны его связей).</summary>
    public static (List<DialogueNode> Nodes, List<DialogueEdge> Edges) WithoutNode(
        IEnumerable<DialogueNode>? nodes, IEnumerable<DialogueEdge>? edges, string? id)
    {
        var keptNodes = (nodes ?? Enumerable.Empty<DialogueNode>())
            .Where(node => node != null && !string.Equals(node.Id, id, StringComparison.Ordinal)).ToList();
        var keptEdges = (edges ?? Enumerable.Empty<DialogueEdge>())
            .Where(edge => edge != null
                           && !string.Equals(edge.Source, id, StringComparison.Ordinal)
                           && !string.Equals(edge.Target, id, StringComparison.Ordinal)).ToList();
        return (keptNodes, keptEdges);
    }

    /// <summary>`deleteEdge`: связи без записи с указанным id.</summary>
    public static List<DialogueEdge> WithoutEdge(IEnumerable<DialogueEdge>? edges, string? id) =>
        (edges ?? Enumerable.Empty<DialogueEdge>())
            .Where(edge => edge != null && !string.Equals(edge.Id, id, StringComparison.Ordinal)).ToList();

    /// <summary>
    /// `portClick`: новая связь `source → target` либо `null`, если связь уже есть или это самопетля.
    /// Id связи (`e_<время>`) при совпадении дополняется суффиксом `_1`, `_2`, ….
    /// </summary>
    public static DialogueEdge? Connect(IReadOnlyList<DialogueEdge>? edges, string? source, string? target)
    {
        var list = edges ?? Array.Empty<DialogueEdge>();
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return null;
        if (string.Equals(source, target, StringComparison.Ordinal)) return null;

        foreach (var edge in list)
            if (edge != null
                && string.Equals(edge.Source, source, StringComparison.Ordinal)
                && string.Equals(edge.Target, target, StringComparison.Ordinal)) return null;

        var id = "e_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var candidate = id;
        var k = 1;
        while (list.Any(edge => edge != null && string.Equals(edge.Id, candidate, StringComparison.Ordinal)))
            candidate = id + "_" + (k++).ToString(CultureInfo.InvariantCulture);

        return new DialogueEdge { Id = candidate, Source = source, Target = target };
    }

    /// <summary>
    /// `portClick`: первый клик по порту взводит его (`connectFrom`), второй — даёт пару
    /// `source → target` (источник — кликнутый выход, цель — кликнутый вход). Повторный клик по
    /// тому же порту или по порту того же вида снимает взвод
    /// (`connectFrom.id === id || connectFrom.kind === kind` в JS).
    /// </summary>
    /// <param name="pending">Ранее взведённый порт или <c>null</c> (второй порт ещё не выбран).</param>
    /// <param name="id">id карточки, по порту которой щёлкнули.</param>
    /// <param name="kind">Вид порта: вход (`in`) или выход (`out`).</param>
    public static GraphPortClick ResolvePortClick(GraphPortRef? pending, string? id, GraphPortKind kind)
    {
        if (string.IsNullOrEmpty(id)) return new GraphPortClick(null, null, null);
        if (pending == null) return new GraphPortClick(new GraphPortRef(id!, kind), null, null);

        if (string.Equals(pending.Id, id, StringComparison.Ordinal) || pending.Kind == kind)
            return new GraphPortClick(null, null, null);

        var source = pending.Kind == GraphPortKind.Out ? pending.Id : id;
        var target = pending.Kind == GraphPortKind.Out ? id : pending.Id;
        return new GraphPortClick(null, source, target);
    }

    /// <summary>Id узлов, попавших в рамку выделения (`up`-обработчик в App.js: карточка 210×100).</summary>
    public static List<string> MarqueeIds(
        IEnumerable<DialogueNode>? nodes, double x1, double y1, double x2, double y2)
    {
        var left = Math.Min(x1, x2);
        var right = Math.Max(x1, x2);
        var top = Math.Min(y1, y2);
        var bottom = Math.Max(y1, y2);

        return (nodes ?? Enumerable.Empty<DialogueNode>())
            .Where(node => node != null
                           && node.X + NodeWidth >= left && node.X <= right
                           && node.Y + NodeHitHeight >= top && node.Y <= bottom)
            .Select(node => node.Id)
            .ToList();
    }

    /// <summary>
    /// `onMouseDown` в DialoguesTab.js: что начинается нажатием — переключение группового выделения
    /// (`None`, Shift), перенос группы (`Group`), перенос узла (`Node`) или холст: рамка выделения
    /// с Shift (`Marquee`) либо панорама (`Pan`).
    /// </summary>
    /// <param name="onNode">Нажатие пришлось на карточку узла.</param>
    /// <param name="shift">Удерживается Shift.</param>
    /// <param name="nodeInGroup">Узел уже входит в групповое выделение.</param>
    /// <param name="groupSize">Размер группового выделения (группа переносится только при &gt; 1).</param>
    public static GraphDragMode ResolveDragMode(bool onNode, bool shift, bool nodeInGroup, int groupSize)
    {
        if (onNode && shift) return GraphDragMode.None;
        if (onNode && nodeInGroup && groupSize > 1) return GraphDragMode.Group;
        if (onNode) return GraphDragMode.Node;
        return shift ? GraphDragMode.Marquee : GraphDragMode.Pan;
    }

    /// <summary>
    /// Рамка выделения (`marquee` в App.js): нормализованные углы и размер для отрисовки.
    /// Ширина не меньше 1px (`Math.max(1, x2 − x1)` в JS), высота — как есть.
    /// </summary>
    public static (double X, double Y, double Width, double Height) MarqueeRect(
        double x1, double y1, double x2, double y2)
    {
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        return (left, top, Math.Max(1d, Math.Max(x1, x2) - left), Math.Max(y1, y2) - top);
    }

    /// <summary>
    /// `move` в App.js: экранный сдвиг курсора, переведённый в координаты графа (`/ zoom`).
    /// Некорректный (нулевой или отрицательный) зум считается единицей — деления на ноль нет.
    /// </summary>
    public static (double Dx, double Dy) DragDelta(
        double fromX, double fromY, double toX, double toY, double zoom)
    {
        var scale = zoom > 0 ? zoom : 1;
        return ((toX - fromX) / scale, (toY - fromY) / scale);
    }

    /// <summary>
    /// `setPan` при панорамировании: стартовый сдвиг плюс экранная дельта курсора
    /// (в JS панорама идёт 1:1 за курсором, без деления на зум).
    /// </summary>
    public static (double X, double Y) PanShift(
        double panX, double panY, double fromX, double fromY, double toX, double toY) =>
        (panX + toX - fromX, panY + toY - fromY);

    /// <summary>
    /// Путь связи для холста (`edges.map` в DialoguesTab.js): кривая из правого порта источника
    /// (`x + 210`, `y + 44`) в левый порт цели, контрольные точки сдвинуты по горизонтали
    /// на `dx = max(80, |x2 − x1| / 2)`. Пустая строка — один из концов связи не найден.
    /// </summary>
    public static string EdgePath(DialogueNode? source, DialogueNode? target) =>
        source == null || target == null ? string.Empty : EdgePath(source.X, source.Y, target.X, target.Y);

    /// <summary>`'M x1 y1 C (x1 + dx) y1, (x2 − dx) y2, x2 y2'` по координатам карточек.</summary>
    public static string EdgePath(double sourceX, double sourceY, double targetX, double targetY)
    {
        var x1 = sourceX + NodeWidth;
        var y1 = sourceY + PortOffsetY;
        var x2 = targetX;
        var y2 = targetY + PortOffsetY;
        var dx = Math.Max(80d, Math.Abs(x2 - x1) / 2);
        return string.Create(CultureInfo.InvariantCulture,
            $"M {x1} {y1} C {x1 + dx} {y1}, {x2 - dx} {y2}, {x2} {y2}");
    }


    /// <summary>Совпадение узла поиску по графу (`(подпись + ' ' + id)` содержит запрос).</summary>
    public static bool Matches(string? label, string? id, string? search)
    {
        var query = (search ?? string.Empty).Trim();
        if (query.Length == 0) return true;

        var haystack = ((label ?? string.Empty) + " " + (id ?? string.Empty)).ToLowerInvariant();
        return haystack.Contains(query.ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>Масштаб в границах 0.5…2 с округлением до сотых (как `+(z).toFixed(2)`).</summary>
    public static double ClampZoom(double zoom) => Math.Round(Math.Clamp(zoom, ZoomMin, ZoomMax), 2);

    /// <summary>Шаг зума: кнопки «−»/«+» и Ctrl+колесо (`±0.1` с ограничением).</summary>
    public static double ZoomBy(double zoom, double delta) => ClampZoom(zoom + delta);

    /// <summary>
    /// `centerView`: масштаб 1 и такой сдвиг, чтобы содержимое оказалось в центре окна
    /// (пустой граф — отступ 20/20, как в JS).
    /// </summary>
    public static (double X, double Y) CenterPan(
        IEnumerable<DialogueNode>? nodes, double viewportWidth, double viewportHeight)
    {
        var list = (nodes ?? Enumerable.Empty<DialogueNode>()).Where(node => node != null).ToList();
        if (list.Count == 0) return (20, 20);

        var minX = list.Min(node => node.X);
        var minY = list.Min(node => node.Y);
        var maxX = list.Max(node => node.X + NodeWidth);
        var maxY = list.Max(node => node.Y + NodeHitHeight);
        return (viewportWidth / 2 - (minX + maxX) / 2, viewportHeight / 2 - (minY + maxY) / 2);
    }

    /// <summary>Снимок графа для истории отмены/повтора (`graphHistory.push`).</summary>
    public static GraphSnapshot Capture(IEnumerable<DialogueNode>? nodes, IEnumerable<DialogueEdge>? edges) =>
        new(nodes, edges);

    /// <summary>Глубокая копия узла (правки копии исходный узел не меняют).</summary>
    public static DialogueNode Clone(DialogueNode node) => new()
    {
        Id = node.Id,
        Type = node.Type,
        X = node.X,
        Y = node.Y,
        Data = node.Data == null ? null : Clone(node.Data),
        Extra = node.Extra
    };

    /// <summary>Глубокая копия данных реплики (метаданные — новый словарь со своими списками).</summary>
    public static NodeData Clone(NodeData data) => new()
    {
        PhraseId = data.PhraseId,
        Role = data.Role,
        RoleSource = data.RoleSource,
        TextId = data.TextId,
        UseId = data.UseId,
        Text = data.Text,
        VoiceId = data.VoiceId,
        Expression = data.Expression,
        ActionType = data.ActionType,
        Params = data.Params,
        Metadata = data.Metadata.ToDictionary(
            pair => pair.Key,
            pair => pair.Value == null ? new List<string>() : new List<string>(pair.Value),
            StringComparer.Ordinal),
        Extra = data.Extra
    };

    /// <summary>Глубокая копия связи.</summary>
    public static DialogueEdge Clone(DialogueEdge edge) => new()
    {
        Id = edge.Id,
        Source = edge.Source,
        Target = edge.Target,
        Broken = edge.Broken,
        OriginalNext = edge.OriginalNext,
        Extra = edge.Extra
    };

    private static string RandomSuffix()
    {
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++) chars[i] = SuffixAlphabet[Random.Shared.Next(SuffixAlphabet.Length)];
        return new string(chars);
    }
}
