namespace XRayForge.Core.GameUi;

/// <summary>
/// Разобранный файл игрового UI (`config/ui/*.xml`): корни документа, диагностика, исходный
/// текст и адресация элементов. После правок текст меняется точечно и модель пересобирается.
/// </summary>
public sealed class GameUiDocument
{
    /// <summary>Размер окна по умолчанию, если файл не задаёт геометрию.</summary>
    public const double DefaultWidth = 1024;

    /// <summary>Высота окна по умолчанию.</summary>
    public const double DefaultHeight = 768;

    /// <summary>Путь файла относительно `gamedata` (`config/ui/ui_mm_main.xml`).</summary>
    public string Path { get; set; } = "";

    /// <summary>Текущий текст файла (меняется точечными правками редактора).</summary>
    public string Text { get; internal set; } = "";

    /// <summary>Корневые элементы документа (обычно один — `window` или `w`).</summary>
    public List<GameUiElement> Roots { get; } = new();

    /// <summary>Проблемы разбора: незакрытые теги, лишние закрывающие, битые атрибуты.</summary>
    public List<GameUiDiagnostic> Diagnostics { get; } = new();

    /// <summary>Имя файла без пути.</summary>
    public string Name
    {
        get
        {
            var normalized = Path.Replace('\\', '/');
            var slash = normalized.LastIndexOf('/');
            return slash >= 0 ? normalized[(slash + 1)..] : normalized;
        }
    }

    /// <summary>В файле есть хоть что-то (даже нераспознанный текст).</summary>
    public bool HasContent => Roots.Count > 0 || Text.Trim().Length > 0;

    /// <summary>Есть ошибки разбора (предупреждения не считаются).</summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == GameUiDiagnosticSeverity.Error);

    /// <summary>Корневой элемент (`window`/`w`) или первый доступный.</summary>
    public GameUiElement? Root => Roots.FirstOrDefault();

    /// <summary>Все элементы документа в порядке файла (включая корни).</summary>
    public IEnumerable<GameUiElement> Elements()
    {
        foreach (var root in Roots)
        {
            yield return root;
            foreach (var nested in root.Descendants()) yield return nested;
        }
    }

    /// <summary>Размер «холста»: из корня, иначе по границам прямых детей, иначе 1024×768.</summary>
    public (double Width, double Height) Canvas
    {
        get
        {
            var root = Root;
            if (root == null) return (DefaultWidth, DefaultHeight);

            var width = root.Number("width");
            var height = root.Number("height");
            if (width <= 0 || height <= 0)
            {
                foreach (var child in root.Children)
                {
                    var rect = child.Rect;
                    width = Math.Max(width, rect.Right);
                    height = Math.Max(height, rect.Bottom);
                }
            }

            return (width > 0 ? width : DefaultWidth, height > 0 ? height : DefaultHeight);
        }
    }

    /// <summary>Элемент по адресу `0.2.1` (индексы в корнях и `Children`); `null`, если адрес битый.</summary>
    public GameUiElement? Find(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        GameUiElement? current = null;
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var index) || index < 0) return null;
            var siblings = i == 0 ? Roots : current?.Children;
            if (siblings == null || index >= siblings.Count) return null;
            current = siblings[index];
        }
        return current;
    }

    /// <summary>Адрес элемента в документе (`0.2.1`); пустая строка, если элемент чужой.</summary>
    public string PathOfElement(GameUiElement element)
    {
        if (!ReferenceEquals(element.Owner, this)) return "";
        var indices = new List<int>();
        var current = element;
        while (current != null)
        {
            var siblings = current.Parent?.Children ?? Roots;
            var index = siblings.IndexOf(current);
            if (index < 0) return "";
            indices.Add(index);
            current = current.Parent;
        }
        indices.Reverse();
        return string.Join('.', indices);
    }

    /// <summary>Элементы с подходящим тегом/именем/подписью (без учёта регистра).</summary>
    public IEnumerable<GameUiElement> FindAll(string query)
    {
        var text = (query ?? "").Trim();
        if (text.Length == 0) return Elements();
        return Elements().Where(e =>
            e.Tag.Contains(text, StringComparison.OrdinalIgnoreCase)
            || e.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || e.Caption.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Первый элемент с тегом (без учёта регистра).</summary>
    public GameUiElement? FindTag(string tag) =>
        Elements().FirstOrDefault(e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase));

    /// <summary>Заменить текст и пересобрать модель (вызывает редактор после точечной правки).</summary>
    internal void ReplaceText(string text)
    {
        Text = text;
        GameUiParser.ParseInto(this, text);
    }
}
