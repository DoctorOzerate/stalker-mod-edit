namespace XRayForge.Core.GameUi;

/// <summary>Описание известного атрибута UI: имя, тип значения и подсказка по умолчанию.</summary>
public sealed record GameUiAttributeInfo(string Name, GameUiValueKind Kind, string Default = "");

/// <summary>
/// Каталог тегов и атрибутов игрового UI: классификация элементов для дерева, типы значений
/// для редактора свойств и типовые наборы атрибутов для новых элементов. Набор тегов открытый:
/// моды добавляют свои (`indicator_booster_psy`), поэтому неизвестный тег редактируется как обычно.
/// </summary>
public static class GameUiElementCatalog
{
    /// <summary>Допустимые значения выравнивания (`alignment`/`vert_alignment`).</summary>
    public static readonly IReadOnlyList<string> AlignmentValues = new[] { "l", "c", "r", "t", "b" };

    /// <summary>Геометрические атрибуты — по ним двигают и масштабируют элемент.</summary>
    public static readonly IReadOnlyList<string> GeometryAttributes = new[] { "x", "y", "width", "height" };

    private static readonly Dictionary<string, GameUiValueKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = GameUiValueKind.Text,
        ["caption"] = GameUiValueKind.Text,
        ["hint"] = GameUiValueKind.Text,
        ["text"] = GameUiValueKind.Text,
        ["id"] = GameUiValueKind.Text,
        ["x"] = GameUiValueKind.Number,
        ["y"] = GameUiValueKind.Number,
        ["width"] = GameUiValueKind.Number,
        ["height"] = GameUiValueKind.Number,
        ["dx"] = GameUiValueKind.Number,
        ["dy"] = GameUiValueKind.Number,
        ["heading"] = GameUiValueKind.Number,
        ["min_height"] = GameUiValueKind.Number,
        ["max_height"] = GameUiValueKind.Number,
        ["btn_height"] = GameUiValueKind.Number,
        ["cell_width"] = GameUiValueKind.Number,
        ["cell_height"] = GameUiValueKind.Number,
        ["rows"] = GameUiValueKind.Number,
        ["cols"] = GameUiValueKind.Number,
        ["spacing"] = GameUiValueKind.Number,
        ["right_ident"] = GameUiValueKind.Number,
        ["left_ident"] = GameUiValueKind.Number,
        ["top_ident"] = GameUiValueKind.Number,
        ["bottom_ident"] = GameUiValueKind.Number,
        ["vert_interval"] = GameUiValueKind.Number,
        ["r"] = GameUiValueKind.Number,
        ["g"] = GameUiValueKind.Number,
        ["b"] = GameUiValueKind.Number,
        ["a"] = GameUiValueKind.Number,
        ["stretch"] = GameUiValueKind.Flag,
        ["always_show_scroll"] = GameUiValueKind.Flag,
        ["complex_mode"] = GameUiValueKind.Flag,
        ["light_anim"] = GameUiValueKind.Flag,
        ["la_cyclic"] = GameUiValueKind.Flag,
        ["la_texture"] = GameUiValueKind.Flag,
        ["la_text"] = GameUiValueKind.Flag,
        ["la_alpha"] = GameUiValueKind.Flag,
        ["inverse_dir"] = GameUiValueKind.Flag,
        ["flip_vert"] = GameUiValueKind.Flag,
        ["vertical"] = GameUiValueKind.Flag,
        ["alignment"] = GameUiValueKind.Alignment,
        ["align"] = GameUiValueKind.Alignment,
        ["vert_alignment"] = GameUiValueKind.Alignment,
        ["vert_align"] = GameUiValueKind.Alignment,
        ["font"] = GameUiValueKind.Font
    };

    /// <summary>Подсказки для нового элемента: базовая геометрия и имя.</summary>
    public static readonly IReadOnlyList<GameUiAttributeInfo> CommonAttributes = new[]
    {
        new GameUiAttributeInfo("name", GameUiValueKind.Text),
        new GameUiAttributeInfo("x", GameUiValueKind.Number, "0"),
        new GameUiAttributeInfo("y", GameUiValueKind.Number, "0"),
        new GameUiAttributeInfo("width", GameUiValueKind.Number, "0"),
        new GameUiAttributeInfo("height", GameUiValueKind.Number, "0")
    };

    /// <summary>Тип значения атрибута; неизвестный атрибут — свободный текст.</summary>
    public static GameUiValueKind KindOf(string? name) =>
        name != null && Kinds.TryGetValue(name, out var kind) ? kind : GameUiValueKind.Text;

    /// <summary>Атрибут известен каталогу (есть подсказка типа и значения).</summary>
    public static bool IsKnownAttribute(string? name) => name != null && Kinds.ContainsKey(name);

    /// <summary>Атрибут управляет геометрией элемента (`x`/`y`/`width`/`height`).</summary>
    public static bool IsGeometry(string? name) =>
        name != null && GeometryAttributes.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Атрибут задаёт цвет (`r`/`g`/`b`/`a`).</summary>
    public static bool IsColorChannel(string? name) =>
        name != null && name.Length == 1 && "rgba".Contains(name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Атрибут задаёт выравнивание.</summary>
    public static bool IsAlignment(string? name) => KindOf(name) == GameUiValueKind.Alignment;

    /// <summary>Нормализовать выравнивание: `left`→`l`, `center`→`c`, `right`→`r`, иначе пустая строка.</summary>
    public static string NormalizeAlignment(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "l" or "left" => "l",
        "c" or "center" or "centre" => "c",
        "r" or "right" => "r",
        "t" or "top" => "t",
        "b" or "bottom" => "b",
        _ => ""
    };

    /// <summary>Класс элемента по тегу.</summary>
    public static GameUiElementKind Classify(string? tag)
    {
        var name = (tag ?? "").Trim().ToLowerInvariant();
        if (name.Length == 0) return GameUiElementKind.Unknown;

        if (name is "window" or "w" or "dialog" or "ui" || name.EndsWith("_window", StringComparison.Ordinal)
            || name.EndsWith("_wnd", StringComparison.Ordinal))
            return GameUiElementKind.Window;

        if (name == "texture" || name.EndsWith("_texture", StringComparison.Ordinal))
            return GameUiElementKind.Texture;

        if (name is "text" or "caption" or "string" || name.EndsWith("_text", StringComparison.Ordinal))
            return GameUiElementKind.Text;

        if (name is "button" or "btn" or "button_hint" || name.EndsWith("_button", StringComparison.Ordinal))
            return GameUiElementKind.Button;

        if (name is "static" or "background" or "image" or "icon" || name.EndsWith("_static", StringComparison.Ordinal)
            || name.StartsWith("indicator_", StringComparison.Ordinal))
            return GameUiElementKind.Static;

        if (name is "edit" or "edit_box" or "edit_text") return GameUiElementKind.Edit;

        if (name is "list" or "list_box" or "combo_box" or "table" || name.EndsWith("_list", StringComparison.Ordinal))
            return GameUiElementKind.List;

        if (name is "scroll_view" || name.EndsWith("_scroll_view", StringComparison.Ordinal))
            return GameUiElementKind.ScrollView;

        if (name is "progress_bar" or "track_bar" or "slider" or "progress"
            || name.EndsWith("_bar", StringComparison.Ordinal))
            return GameUiElementKind.Progress;

        if (name is "frame" or "frame_line" or "border" || name.EndsWith("_frame", StringComparison.Ordinal))
            return GameUiElementKind.Frame;

        if (name is "menu" or "panel" or "container" or "group" || name.StartsWith("menu_", StringComparison.Ordinal)
            || name.EndsWith("_panel", StringComparison.Ordinal))
            return GameUiElementKind.Container;

        return GameUiElementKind.Unknown;
    }

    /// <summary>Символ для дерева элементов (визуальное различие классов без картинок).</summary>
    public static string Glyph(GameUiElementKind kind) => kind switch
    {
        GameUiElementKind.Window => "▭",
        GameUiElementKind.Static => "▨",
        GameUiElementKind.Button => "▶",
        GameUiElementKind.Text => "T",
        GameUiElementKind.Texture => "▩",
        GameUiElementKind.Edit => "✎",
        GameUiElementKind.List => "☰",
        GameUiElementKind.ScrollView => "↕",
        GameUiElementKind.Progress => "▬",
        GameUiElementKind.Frame => "▢",
        GameUiElementKind.Container => "▾",
        _ => "◇"
    };

    /// <summary>Ключ локализации подписи класса (`uiKindWindow`, …).</summary>
    public static string LabelKey(GameUiElementKind kind) => kind switch
    {
        GameUiElementKind.Window => "uiKindWindow",
        GameUiElementKind.Static => "uiKindStatic",
        GameUiElementKind.Button => "uiKindButton",
        GameUiElementKind.Text => "uiKindText",
        GameUiElementKind.Texture => "uiKindTexture",
        GameUiElementKind.Edit => "uiKindEdit",
        GameUiElementKind.List => "uiKindList",
        GameUiElementKind.ScrollView => "uiKindScrollView",
        GameUiElementKind.Progress => "uiKindProgress",
        GameUiElementKind.Frame => "uiKindFrame",
        GameUiElementKind.Container => "uiKindContainer",
        _ => "uiKindUnknown"
    };

    /// <summary>Элемент может содержать детей (окно, контейнер, список, прокрутка, рамка).</summary>
    public static bool CanContainChildren(GameUiElementKind kind) => kind
        is GameUiElementKind.Window or GameUiElementKind.Container or GameUiElementKind.List
        or GameUiElementKind.ScrollView or GameUiElementKind.Frame or GameUiElementKind.Unknown;

    /// <summary>Типовые атрибуты для класса элемента (для новых элементов и подсказок свойств).</summary>
    public static IReadOnlyList<GameUiAttributeInfo> AttributesFor(GameUiElementKind kind) => kind switch
    {
        GameUiElementKind.Button => new[]
        {
            new GameUiAttributeInfo("name", GameUiValueKind.Text),
            new GameUiAttributeInfo("x", GameUiValueKind.Number, "0"),
            new GameUiAttributeInfo("y", GameUiValueKind.Number, "0"),
            new GameUiAttributeInfo("width", GameUiValueKind.Number, "0"),
            new GameUiAttributeInfo("height", GameUiValueKind.Number, "0"),
            new GameUiAttributeInfo("hint", GameUiValueKind.Text),
            new GameUiAttributeInfo("caption", GameUiValueKind.Text)
        },
        GameUiElementKind.Text => new[]
        {
            new GameUiAttributeInfo("font", GameUiValueKind.Font, "letterica16"),
            new GameUiAttributeInfo("alignment", GameUiValueKind.Alignment, "c"),
            new GameUiAttributeInfo("vert_alignment", GameUiValueKind.Alignment, "c"),
            new GameUiAttributeInfo("r", GameUiValueKind.Number, "255"),
            new GameUiAttributeInfo("g", GameUiValueKind.Number, "255"),
            new GameUiAttributeInfo("b", GameUiValueKind.Number, "255"),
            new GameUiAttributeInfo("a", GameUiValueKind.Number, "255")
        },
        _ => CommonAttributes
    };

    /// <summary>Атрибуты, которых у элемента ещё нет и которые стоит предложить в редакторе свойств.</summary>
    public static IReadOnlyList<GameUiAttributeInfo> SuggestedFor(GameUiElement element) =>
        AttributesFor(element.Kind).Where(info => element.Attribute(info.Name) == null).ToList();
}
