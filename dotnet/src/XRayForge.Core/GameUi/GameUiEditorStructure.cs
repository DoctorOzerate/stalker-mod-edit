using System.Text;

namespace XRayForge.Core.GameUi;

/// <summary>Структурные правки UI: добавление, удаление и перемещение элементов, сериализация.</summary>
public static partial class GameUiEditor
{
    /// <summary>
    /// Добавить XML-фрагмент в контейнер (окно/меню/список). Возвращает добавленный элемент
    /// или `null`, если родитель не принимает детей либо фрагмент не разобрался.
    /// </summary>
    public static GameUiElement? AddChild(GameUiDocument document, GameUiElement parent, string fragment)
    {
        var target = document.Find(parent.Path);
        if (target == null || target.IsSelfClosing || target.CloseTagStart < 0) return null;
        if (!GameUiElementCatalog.CanContainChildren(target.Kind)) return null;

        var parsed = GameUiParser.Parse(fragment);
        if (parsed.Roots.Count == 0) return null;

        var childIndex = target.Children.Count;
        var parentPath = target.Path;
        var text = document.Text;
        var insertion = BuildChildInsertion(text, target, fragment);
        document.ReplaceText(text[..insertion.At] + insertion.Text + text[insertion.At..]);
        return document.Find(parentPath + "." + childIndex);
    }

    /// <summary>Удалить элемент вместе со строкой, если он занимает её целиком.</summary>
    public static bool RemoveElement(GameUiDocument document, GameUiElement element)
    {
        var target = document.Find(element.Path);
        if (target == null) return false;

        var text = document.Text;
        var start = LineStart(text, target.Start);
        var wholeLine = text[start..target.Start].All(c => c is ' ' or '\t');

        var end = target.End;
        if (wholeLine)
        {
            var newline = text.IndexOf('\n', end);
            end = newline >= 0 ? newline + 1 : text.Length;
        }

        document.ReplaceText(text[..start] + text[end..]);
        return true;
    }

    /// <summary>Переместить элемент в другой контейнер (или в конец того же).</summary>
    public static bool MoveElement(GameUiDocument document, GameUiElement element, GameUiElement newParent)
    {
        var target = document.Find(element.Path);
        var parent = document.Find(newParent.Path);
        if (target == null || parent == null) return false;
        if (ReferenceEquals(target, parent)) return false;
        if (parent.Ancestors().Contains(target)) return false;

        var fragment = document.Text[target.Start..target.End];
        var parentPath = parent.Path;
        RemoveElement(document, target);
        var freshParent = document.Find(parentPath);
        return freshParent != null && AddChild(document, freshParent, fragment) != null;
    }

    /// <summary>Сдвинуть элемент по `x`/`y` (координаты родителя).</summary>
    public static bool MoveBy(GameUiDocument document, GameUiElement element, double dx, double dy)
    {
        var path = element.Path;
        var target = document.Find(path);
        if (target == null || (dx == 0 && dy == 0)) return false;

        var moved = false;
        if (dx != 0) moved |= SetAttribute(document, target, "x", GameUiFormat.Number(target.Number("x") + dx));
        target = document.Find(path);
        if (target != null && dy != 0)
            moved |= SetAttribute(document, target, "y", GameUiFormat.Number(target.Number("y") + dy));
        return moved;
    }

    /// <summary>Изменить размер элемента (не меньше нуля).</summary>
    public static bool ResizeBy(GameUiDocument document, GameUiElement element, double dw, double dh)
    {
        var path = element.Path;
        var target = document.Find(path);
        if (target == null || (dw == 0 && dh == 0)) return false;

        var resized = false;
        if (dw != 0)
        {
            var width = Math.Max(0, target.Number("width") + dw);
            resized |= SetAttribute(document, target, "width", GameUiFormat.Number(width));
        }

        target = document.Find(path);
        if (target != null && dh != 0)
        {
            var height = Math.Max(0, target.Number("height") + dh);
            resized |= SetAttribute(document, target, "height", GameUiFormat.Number(height));
        }

        return resized;
    }

    /// <summary>
    /// Собрать текст модели заново (табы, атрибуты в порядке модели). Комментарии и исходное
    /// форматирование теряются — для точечных правок используйте методы выше.
    /// </summary>
    public static string Serialize(GameUiDocument document)
    {
        var builder = new StringBuilder();
        var newline = NewLine(document.Text);
        for (var i = 0; i < document.Roots.Count; i++)
        {
            if (i > 0) builder.Append(newline);
            Write(builder, document.Roots[i], 0, newline);
        }
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, GameUiElement element, int depth, string newline)
    {
        var indent = new string('\t', depth);
        builder.Append(indent).Append('<').Append(element.Tag);
        foreach (var attribute in element.Attributes)
        {
            builder.Append(' ').Append(attribute.Name);
            if (attribute.HasValue)
                builder.Append("=\"").Append(GameUiFormat.EscapeAttribute(attribute.Value)).Append('"');
        }

        if (element.Children.Count > 0)
        {
            builder.Append('>').Append(newline);
            for (var i = 0; i < element.Children.Count; i++)
            {
                if (i > 0) builder.Append(newline);
                Write(builder, element.Children[i], depth + 1, newline);
            }
            builder.Append(newline).Append(indent).Append("</").Append(element.Tag).Append('>');
            return;
        }

        var text = element.InnerTextTrimmed;
        if (text.Length == 0)
        {
            builder.Append("/>");
            return;
        }

        builder.Append('>').Append(GameUiFormat.EscapeText(text)).Append("</").Append(element.Tag).Append('>');
    }

    /// <summary>Вставка ребёнка перед закрывающим тегом с отступами в стиле файла.</summary>
    private static (int At, string Text) BuildChildInsertion(string text, GameUiElement parent, string fragment)
    {
        var newline = NewLine(text);
        var childIndent = ChildIndent(text, parent);
        var body = IndentFragment(fragment, childIndent);

        var lineStart = LineStart(text, parent.CloseTagStart);
        var prefix = text[lineStart..parent.CloseTagStart];
        if (lineStart > 0 && prefix.All(c => c is ' ' or '\t'))
            return (lineStart, childIndent + body + newline + prefix);

        var parentIndent = new string('\t', parent.Depth);
        return (parent.CloseTagStart, newline + childIndent + body + newline + parentIndent);
    }

    /// <summary>Отступ для нового ребёнка: как у первого существующего, иначе глубина + таб.</summary>
    private static string ChildIndent(string text, GameUiElement parent)
    {
        if (parent.Children.Count > 0)
        {
            var first = parent.Children[0];
            var start = LineStart(text, first.Start);
            var prefix = text[start..first.Start];
            if (prefix.Length > 0 && prefix.All(c => c is ' ' or '\t')) return prefix;
        }

        var parentLineStart = LineStart(text, parent.Start);
        var parentPrefix = text[parentLineStart..parent.Start];
        var baseIndent = parentPrefix.All(c => c is ' ' or '\t') ? parentPrefix : new string('\t', parent.Depth);
        return baseIndent + "\t";
    }

    /// <summary>Добавить отступ каждой непустой строке фрагмента (переносы — `\n`).</summary>
    private static string IndentFragment(string fragment, string indent)
    {
        var lines = fragment.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) builder.Append('\n');
            if (lines[i].Trim().Length == 0) continue;
            builder.Append(indent).Append(lines[i].TrimEnd());
        }
        return builder.ToString();
    }

    /// <summary>Начало строки, в которой находится позиция.</summary>
    private static int LineStart(string text, int position)
    {
        if (position <= 0) return 0;
        var newline = text.LastIndexOf('\n', Math.Min(position, text.Length) - 1);
        return newline >= 0 ? newline + 1 : 0;
    }

    /// <summary>Перенос строки файла: CRLF, если он уже используется, иначе LF.</summary>
    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
