using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.ViewModels;

/// <summary>
/// Карточка узла на холсте (`w-[210px]` из `DialoguesTab.js`): шапка цвета типа (или роли при
/// «цвете по роли»), короткий id, подпись узла, бейджи озвучки/условий/действий и порты.
/// VM держит геометрию для `Canvas.Left/Top` и визуальные флаги: выделение, совпадение с
/// поиском по графу и затухание несвязанных узлов при выделении.
/// </summary>
public sealed partial class GraphNodeViewModel : ObservableObject
{
    /// <summary>Ширина карточки (`w-[210px]`), совпадает с `GraphEditing.NodeWidth`.</summary>
    public const double CardWidth = GraphEditing.NodeWidth;

    /// <summary>Минимальная высота карточки (порты на `top-10`, связи — `y + 44`).</summary>
    public const double CardMinHeight = GraphEditing.NodeHeight;

    /// <summary>Смещение портов от верха карточки (`top-10` = 40px).</summary>
    public const double PortTop = 40;

    /// <summary>Размер порта (`w-4 h-4` = 16px).</summary>
    public const double PortSize = 16;

    /// <summary>Длина превью метаданных в бейдже (`slice(0, 40)`).</summary>
    private const int BadgeLimit = 40;

    public GraphNodeViewModel(DialogueNode node, IReadOnlyList<Language>? languages, bool showText)
    {
        Node = node;
        Title = GraphLayout.NodeLabel(node, languages, showText);
        RefreshBadges();
    }

    /// <summary>Модель узла проекта: правки VM меняют её напрямую (как `patchNode` в JS).</summary>
    public DialogueNode Node { get; }

    /// <summary>Идентификатор узла (`n.id`).</summary>
    public string Id => Node.Id;

    /// <summary>Тип узла (`n.type`).</summary>
    public string Type => Node.Type;

    /// <summary>Иконка, подпись, цвет и описание типа (`NODE_TYPES[n.type] || FALLBACK_NODE`).</summary>
    public NodeTypeInfo TypeInfo => NodeTypes.InfoFor(Node.Type);

    /// <summary>Короткий id в шапке (`n.id.slice(0, 6)`).</summary>
    public string ShortId => Id.Length > 6 ? Id[..6] : Id;

    /// <summary>Подпись шапки карточки (`cfg.icon + ' ' + cfg.label`).</summary>
    public string HeaderText => TypeInfo.Icon + " " + TypeInfo.Label;

    /// <summary>Порт входа есть у всех типов, кроме START (`n.type !== 'START'`).</summary>
    public bool HasInputPort => Type != NodeTypes.Start;

    /// <summary>Порт выхода есть у всех типов, кроме END (`n.type !== 'END'`).</summary>
    public bool HasOutputPort => Type != NodeTypes.End;

    /// <summary>Позиция узла: `Canvas.Left` карточки (`n.x`).</summary>
    public double X
    {
        get => Node.X;
        set
        {
            if (Node.X.Equals(value)) return;
            Node.X = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Позиция узла: `Canvas.Top` карточки (`n.y`).</summary>
    public double Y
    {
        get => Node.Y;
        set
        {
            if (Node.Y.Equals(value)) return;
            Node.Y = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Текст узла (`nodeLabel`): перевод text_id, текст реплики или «Нет содержимого».</summary>
    [ObservableProperty]
    private string _title = "";

    /// <summary>Бейдж озвучки (`🔊 voice_id`); пустая строка — бейдж скрыт.</summary>
    [ObservableProperty]
    private string _voiceText = "";

    /// <summary>Бейдж условий (`❓ has_info, dont_has_info, precondition`, до 40 символов).</summary>
    [ObservableProperty]
    private string _conditionText = "";

    /// <summary>Бейдж действий (`⚙ action, give_info`, до 40 символов).</summary>
    [ObservableProperty]
    private string _actionText = "";

    /// <summary>Узел выделен (`selected`) — зелёная рамка с ореолом.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Узел в групповом выделении (`multiSelect`): синяя рамка (`border-blue-500`).
    /// Одиночное выделение сильнее — при <see cref="IsSelected"/> рамка остаётся зелёной,
    /// как в JS (`active ? 'border-green-500' : (multi ? 'border-blue-500' : '')`).
    /// </summary>
    [ObservableProperty]
    private bool _isMultiSelected;

    /// <summary>Узел совпал с поиском по графу (`ring-2 ring-green-400/40`).</summary>
    [ObservableProperty]
    private bool _isSearchHit;

    /// <summary>Шапка окрашена по роли реплики, а не по типу («Цвет по роли»).</summary>
    [ObservableProperty]
    private bool _useRoleColor;

    /// <summary>Непрозрачность карточки: поиск и выделение приглушают лишние узлы (0.12 / 0.25).</summary>
    [ObservableProperty]
    private double _opacity = 1;

    /// <summary>Цвет шапки: цвет роли реплики при «цвете по роли», иначе цвет типа узла.</summary>
    public string HeaderHex => UseRoleColor ? GraphLayout.RoleColor(Node) ?? TypeInfo.Color : TypeInfo.Color;

    partial void OnUseRoleColorChanged(bool value) => OnPropertyChanged(nameof(HeaderHex));

    /// <summary>Подписи обновились: смена языков, режима текста или правка реплики.</summary>
    public void RefreshLabels(IReadOnlyList<Language>? languages, bool showText)
    {
        Title = GraphLayout.NodeLabel(Node, languages, showText);
        RefreshBadges();
    }

    /// <summary>Бейджи озвучки, условий и действий (метаданные реплики).</summary>
    public void RefreshBadges()
    {
        var data = Node.Data;
        VoiceText = string.IsNullOrEmpty(data?.VoiceId) ? "" : "🔊 " + data!.VoiceId;

        var conditions = Joined(data?.Metadata, "has_info", "dont_has_info", "precondition");
        var actions = Joined(data?.Metadata, "action", "give_info");
        ConditionText = conditions.Length > 0 ? "❓ " + conditions : "";
        ActionText = actions.Length > 0 ? "⚙ " + actions : "";
    }

    /// <summary>Оповестить представление о смене координат (раскладка, undo, перетаскивание).</summary>
    public void NotifyPositionChanged()
    {
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
    }

    // --- мини-карта (шаг 6.6) ---

    /// <summary>Прямоугольник узла на мини-карте: `Canvas.Left`/`Canvas.Top`.</summary>
    [ObservableProperty]
    private double _miniMapX;

    /// <summary>Прямоугольник узла на мини-карте: `Canvas.Top`.</summary>
    [ObservableProperty]
    private double _miniMapY;

    /// <summary>Ширина прямоугольника на мини-карте (`210 × scale`).</summary>
    [ObservableProperty]
    private double _miniMapWidth;

    /// <summary>Высота прямоугольника на мини-карте (`90 × scale`).</summary>
    [ObservableProperty]
    private double _miniMapHeight;

    /// <summary>
    /// Цвет узла на мини-карте: всегда цвет типа (`NODE_TYPES[n.type].color` в JS) — «цвет по роли»
    /// на мини-карту не влияет, как и в оригинале.
    /// </summary>
    public string MiniMapHex => TypeInfo.Color;

    /// <summary>Пересчёт прямоугольника мини-карты (`MiniMapRect` в пикселях панели 176×112).</summary>
    public void SetMiniMapRect(MiniMapRect rect)
    {
        MiniMapX = rect.X;
        MiniMapY = rect.Y;
        MiniMapWidth = rect.Width;
        MiniMapHeight = rect.Height;
    }

    /// <summary>`[].concat(...).join(', ').slice(0, 40)` по списку ключей метаданных.</summary>
    private static string Joined(IReadOnlyDictionary<string, List<string>>? metadata, params string[] keys)
    {
        if (metadata == null) return "";

        var parts = new List<string>();
        foreach (var key in keys)
            if (metadata.TryGetValue(key, out var values) && values != null)
                parts.AddRange(values.Where(value => !string.IsNullOrEmpty(value)));

        var joined = string.Join(", ", parts);
        return joined.Length > BadgeLimit ? joined[..BadgeLimit] : joined;
    }
}
