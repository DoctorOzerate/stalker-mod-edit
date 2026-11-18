namespace XRayForge.Core.GameUi;

/// <summary>Тип значения атрибута — подсказка для редактора свойств и валидации ввода.</summary>
public enum GameUiValueKind
{
    /// <summary>Произвольная строка (имя, подпись, идентификатор текста).</summary>
    Text,

    /// <summary>Целое или дробное число (`x`, `width`, `r`).</summary>
    Number,

    /// <summary>Флаг `0`/`1` (в X-Ray встречаются и `true`/`false`).</summary>
    Flag,

    /// <summary>Выравнивание `l`/`c`/`r` по горизонтали и `t`/`c`/`b` по вертикали.</summary>
    Alignment,

    /// <summary>Имя шрифта (`letterica16`, `graffiti22`).</summary>
    Font,

    /// <summary>Путь к текстуре внутри папки `textures` (`ui_button_main_menu`).</summary>
    Texture
}

/// <summary>Класс элемента игрового UI (по тегу) — для иконки, подписи и набора свойств в дереве.</summary>
public enum GameUiElementKind
{
    /// <summary>Корень файла (`window`, `w`) или вложенное окно.</summary>
    Window,

    /// <summary>Статичное изображение (`static`, `background`, `*_static`).</summary>
    Static,

    /// <summary>Кнопка (`button`, `btn`).</summary>
    Button,

    /// <summary>Текст (`text`, `caption`, `*_text`).</summary>
    Text,

    /// <summary>Текстура-лист элемента (`texture`).</summary>
    Texture,

    /// <summary>Поле ввода (`edit`, `edit_box`).</summary>
    Edit,

    /// <summary>Список (`list`, `list_box`, `*_list`).</summary>
    List,

    /// <summary>Прокручиваемая область (`*_scroll_view`, `scroll_view`).</summary>
    ScrollView,

    /// <summary>Полоса прогресса/трек (`progress_bar`, `track_bar`, `slider`).</summary>
    Progress,

    /// <summary>Рамка/фон окна (`frame`, `frame_line`).</summary>
    Frame,

    /// <summary>Контейнер без собственной отрисовки (`menu`, `menu_main`, `*_wnd`, `shniaga`).</summary>
    Container,

    /// <summary>Неизвестный тег мода — редактируется как обычный элемент.</summary>
    Unknown
}

/// <summary>Атрибут элемента UI (`x="10"`) с позицией значения в исходном тексте.</summary>
public sealed class GameUiAttribute
{
    /// <summary>Имя атрибута как в файле.</summary>
    public required string Name { get; init; }

    /// <summary>Значение без кавычек.</summary>
    public string Value { get; set; } = "";

    /// <summary>Смещение имени атрибута в исходном тексте.</summary>
    public int NameOffset { get; init; }

    /// <summary>Смещение значения (после открывающей кавычки); `-1`, если значение не задано.</summary>
    public int ValueOffset { get; init; } = -1;

    /// <summary>Длина значения в исходном тексте.</summary>
    public int ValueLength { get; init; }

    /// <summary>У атрибута есть значение (`flag` без `="…"` даёт `false`).</summary>
    public bool HasValue { get; init; } = true;
}

/// <summary>Уровень проблемы разбора UI-файла.</summary>
public enum GameUiDiagnosticSeverity
{
    Warning,
    Error
}

/// <summary>Диагностика толерантного разбора: код (для локализации), текст и смещение.</summary>
public sealed record GameUiDiagnostic(
    GameUiDiagnosticSeverity Severity,
    string Code,
    string Message,
    int Offset);

/// <summary>Прямоугольник в координатах окна (пиксели 1024×768 и их масштаб).</summary>
public readonly record struct GameUiRect(double X, double Y, double Width, double Height)
{
    /// <summary>Пустой прямоугольник (нулевой размер).</summary>
    public static GameUiRect Empty => new(0, 0, 0, 0);

    public double Right => X + Width;
    public double Bottom => Y + Height;

    /// <summary>У прямоугольника ненулевые размеры.</summary>
    public bool HasSize => Width > 0 && Height > 0;

    /// <summary>Точка попадает в прямоугольник.</summary>
    public bool Contains(double px, double py) =>
        px >= X && px <= X + Width && py >= Y && py <= Y + Height;

    /// <summary>Прямоугольник с расширением (для «толщины» линии и хит-теста).</summary>
    public GameUiRect Inflate(double d) => new(X - d, Y - d, Width + d * 2, Height + d * 2);

    /// <summary>Смещённый прямоугольник.</summary>
    public GameUiRect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);
}


/// <summary>
/// Элемент игрового UI: тег, атрибуты в порядке файла, дети, прямой текст и диапазоны
/// в исходнике. Позиции нужны редактору для точечных правок без переформатирования файла.
/// </summary>
public sealed class GameUiElement
{
    /// <summary>Имя тега (`button`, `text`, `indicator_health`).</summary>
    public string Tag { get; init; } = "";

    /// <summary>Атрибуты в порядке объявления.</summary>
    public List<GameUiAttribute> Attributes { get; } = new();

    /// <summary>Дочерние элементы в порядке файла.</summary>
    public List<GameUiElement> Children { get; } = new();

    /// <summary>Родитель; у корневых элементов `null`.</summary>
    public GameUiElement? Parent { get; internal set; }

    /// <summary>Документ, которому принадлежит элемент (нужен для адреса и правок).</summary>
    public GameUiDocument? Owner { get; internal set; }

    /// <summary>Прямой текст между тегами (для `&lt;texture&gt;`/`&lt;text&gt;`), без обрезки.</summary>
    public string InnerText { get; internal set; } = "";

    /// <summary>Смещение `&lt;` открывающего тега.</summary>
    public int Start { get; internal set; }

    /// <summary>Смещение после `&gt;` открывающего тега.</summary>
    public int OpenTagEnd { get; internal set; }

    /// <summary>Начало содержимого (после открывающего тега).</summary>
    public int ContentStart { get; internal set; }

    /// <summary>Конец содержимого (позиция `&lt;` закрывающего тега).</summary>
    public int ContentEnd { get; internal set; }

    /// <summary>Позиция `&lt;` закрывающего тега; `-1` у самозакрывающихся.</summary>
    public int CloseTagStart { get; internal set; } = -1;

    /// <summary>Смещение после элемента (для вырезания блока).</summary>
    public int End { get; internal set; }

    /// <summary>Тег записан как `&lt;tag/&gt;`.</summary>
    public bool IsSelfClosing { get; internal set; }

    /// <summary>Текст элемента без пробелов по краям (`&lt;texture&gt;` → путь к текстуре).</summary>
    public string InnerTextTrimmed => InnerText.Trim();

    /// <summary>Уровень вложенности (у корня 0).</summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            for (var parent = Parent; parent != null; parent = parent.Parent) depth++;
            return depth;
        }
    }

    /// <summary>Значение атрибута `name` или пустая строка.</summary>
    public string Name => Get("name");

    /// <summary>Подпись элемента в дереве: `name`, иначе тег.</summary>
    public string DisplayName => Name.Length > 0 ? Name : Tag;

    /// <summary>Класс элемента по тегу.</summary>
    public GameUiElementKind Kind => GameUiElementCatalog.Classify(Tag);

    /// <summary>Подпись кнопки/текста из `caption`, если `name` нет.</summary>
    public string Caption => Get("caption");

    /// <summary>Атрибут по имени (без учёта регистра) или `null`.</summary>
    public GameUiAttribute? Attribute(string name) =>
        Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Значение атрибута; отсутствующий атрибут → `fallback`.</summary>
    public string Get(string name, string fallback = "")
    {
        var attribute = Attribute(name);
        return attribute == null ? fallback : attribute.Value;
    }

    /// <summary>Числовое значение атрибута (инвариантная культура) или `fallback`.</summary>
    public double Number(string name, double fallback = 0) =>
        GameUiFormat.TryNumber(Get(name), out var value) ? value : fallback;

    /// <summary>Целочисленное значение атрибута.</summary>
    public int Int(string name, int fallback = 0) => (int)Math.Round(Number(name, fallback));

    /// <summary>Флаг атрибута: `1`/`true` → `true`, `0`/`false`/нет атрибута → `false`.</summary>
    public bool Flag(string name) => GameUiFormat.IsTrue(Get(name));

    /// <summary>Геометрия элемента в координатах родителя.</summary>
    public GameUiRect Rect => new(Number("x"), Number("y"), Number("width"), Number("height"));

    /// <summary>Строка `0.2.1` — стабильный адрес элемента между правками атрибутов.</summary>
    public string Path => Owner?.PathOfElement(this) ?? "";

    /// <summary>Все дочерние элементы рекурсивно, в порядке файла (обход в глубину).</summary>
    public IEnumerable<GameUiElement> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var nested in child.Descendants()) yield return nested;
        }
    }

    /// <summary>Цепочка родителей от ближайшего к корню.</summary>
    public IEnumerable<GameUiElement> Ancestors()
    {
        for (var parent = Parent; parent != null; parent = parent.Parent) yield return parent;
    }
}
