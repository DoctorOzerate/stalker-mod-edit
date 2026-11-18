namespace XRayForge.Core.GameUi;

/// <summary>Заготовка элемента для палитры вкладки: id, тег, ключ локализации и XML.</summary>
public sealed record GameUiTemplate(string Id, string Tag, string LabelKey, string Glyph, string Xml);

/// <summary>
/// Шаблоны новых окон и элементов. XML собирается из модели каталога (атрибуты и значения
/// по умолчанию), поэтому новые элементы сразу валидны и открываются предпросмотром.
/// </summary>
public static class GameUiTemplates
{
    /// <summary>Имя корневого окна нового файла.</summary>
    public const string NewWindowName = "new_window";

    /// <summary>Палитра элементов правой панели (порядок — от частых к редким).</summary>
    public static readonly IReadOnlyList<GameUiTemplate> Palette = new[]
    {
        new GameUiTemplate("static", "static", "uiTemplateStatic", "▨",
            "<static name=\"new_static\" x=\"0\" y=\"0\" width=\"64\" height=\"64\"><texture>ui_icons_total</texture></static>"),
        new GameUiTemplate("button", "button", "uiTemplateButton", "▶",
            "<button name=\"new_button\" x=\"0\" y=\"0\" width=\"180\" height=\"30\" hint=\"ui_hint_button\"><texture>ui_button_main_menu</texture><text>ui_button_ok</text></button>"),
        new GameUiTemplate("text", "text", "uiTemplateText", "T",
            "<text name=\"new_text\" x=\"0\" y=\"0\" width=\"200\" height=\"24\" font=\"letterica16\" alignment=\"l\" r=\"255\" g=\"255\" b=\"255\">ui_text_id</text>"),
        new GameUiTemplate("list", "list", "uiTemplateList", "☰",
            "<list name=\"new_list\" x=\"0\" y=\"0\" width=\"240\" height=\"160\" always_show_scroll=\"1\" />"),
        new GameUiTemplate("edit", "edit", "uiTemplateEdit", "✎",
            "<edit name=\"new_edit\" x=\"0\" y=\"0\" width=\"180\" height=\"24\" font=\"letterica16\" />"),
        new GameUiTemplate("progress_bar", "progress_bar", "uiTemplateProgress", "▬",
            "<progress_bar name=\"new_progress\" x=\"0\" y=\"0\" width=\"160\" height=\"12\" />"),
        new GameUiTemplate("frame", "frame", "uiTemplateFrame", "▢",
            "<frame name=\"new_frame\" x=\"0\" y=\"0\" width=\"320\" height=\"200\"><texture>ui_frame</texture></frame>")
    };

    /// <summary>Текст нового файла с корневым окном.</summary>
    public static string Window(string name = NewWindowName, double width = GameUiDocument.DefaultWidth,
        double height = GameUiDocument.DefaultHeight)
    {
        var windowName = string.IsNullOrWhiteSpace(name) ? NewWindowName : name.Trim();
        return "<window name=\"" + GameUiFormat.EscapeAttribute(windowName) + "\""
            + " x=\"0\" y=\"0\" width=\"" + GameUiFormat.Number(width) + "\" height=\"" + GameUiFormat.Number(height) + "\">\n"
            + "</window>\n";
    }

    /// <summary>Новый документ с пустым окном (для «Создать UI-файл»).</summary>
    public static GameUiDocument CreateDocument(string path = "", string name = NewWindowName,
        double width = GameUiDocument.DefaultWidth, double height = GameUiDocument.DefaultHeight) =>
        GameUiParser.Parse(Window(name, width, height), path);

    /// <summary>XML элемента с координатами и именем; для текста добавляются шрифт и выравнивание.</summary>
    public static string Element(string tag, string name, double x, double y, double width, double height)
    {
        var elementTag = string.IsNullOrWhiteSpace(tag) ? "static" : tag.Trim();
        var elementName = string.IsNullOrWhiteSpace(name) ? "new_" + elementTag : name.Trim();
        var attributes = "name=\"" + GameUiFormat.EscapeAttribute(elementName) + "\""
            + " x=\"" + GameUiFormat.Number(x) + "\""
            + " y=\"" + GameUiFormat.Number(y) + "\""
            + " width=\"" + GameUiFormat.Number(width) + "\""
            + " height=\"" + GameUiFormat.Number(height) + "\"";

        if (GameUiElementCatalog.Classify(elementTag) == GameUiElementKind.Text)
            attributes += " font=\"letterica16\" alignment=\"l\"";

        return "<" + elementTag + " " + attributes + " />";
    }

    /// <summary>XML заготовки палитры по id; неизвестный id — пустая строка.</summary>
    public static string FromPalette(string? id) =>
        Palette.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))?.Xml ?? "";

    /// <summary>Свободное имя элемента в документе: `new_static`, `new_static_2`, …</summary>
    public static string NextName(GameUiDocument document, string tag)
    {
        var baseName = "new_" + (string.IsNullOrWhiteSpace(tag) ? "element" : tag.Trim().ToLowerInvariant());
        var existing = document.Elements()
            .Select(e => e.Name)
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(baseName)) return baseName;
        for (var i = 2; i < 1000; i++)
        {
            var candidate = baseName + "_" + i;
            if (!existing.Contains(candidate)) return candidate;
        }
        return baseName + "_" + Guid.NewGuid().ToString("N")[..6];
    }
}
