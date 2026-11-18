using System.Text;

namespace XRayForge.Core.GameUi;

/// <summary>
/// Точечные правки UI-файла: меняется только нужный диапазон исходного текста, форматирование
/// и комментарии остальных элементов сохраняются. После каждой правки модель документа
/// пересобирается, поэтому адреса (`0.2.1`) остаются актуальными, а старые ссылки на элементы
/// нужно брать заново через <see cref="GameUiDocument.Find"/>.
/// </summary>
public static partial class GameUiEditor
{
    /// <summary>Задать значение атрибута; `null` — удалить атрибут. `false`, если ничего не изменилось.</summary>
    public static bool SetAttribute(GameUiDocument document, GameUiElement element, string name, string? value)
    {
        if (value == null) return RemoveAttribute(document, element, name);
        var target = document.Find(element.Path);
        if (target == null || name.Length == 0) return false;

        var escaped = GameUiFormat.EscapeAttribute(value);
        var attribute = target.Attribute(name);
        var text = document.Text;

        if (attribute != null)
        {
            if (attribute.HasValue && attribute.ValueOffset >= 0)
            {
                if (attribute.Value == value) return false;
                var updated = text[..attribute.ValueOffset] + escaped
                    + text[(attribute.ValueOffset + attribute.ValueLength)..];
                document.ReplaceText(updated);
                return true;
            }

            // Атрибут без значения: дописываем `="…"` сразу после имени.
            var at = attribute.NameOffset + attribute.Name.Length;
            document.ReplaceText(text[..at] + "=\"" + escaped + "\"" + text[at..]);
            return true;
        }

        var insertAt = OpenTagAttributeEnd(target);
        var addition = target.IsSelfClosing
            ? name + "=\"" + escaped + "\" "
            : " " + name + "=\"" + escaped + "\"";
        document.ReplaceText(text[..insertAt] + addition + text[insertAt..]);
        return true;
    }

    /// <summary>Удалить атрибут вместе с отступом перед ним.</summary>
    public static bool RemoveAttribute(GameUiDocument document, GameUiElement element, string name)
    {
        var target = document.Find(element.Path);
        var attribute = target?.Attribute(name);
        if (target == null || attribute == null) return false;

        var text = document.Text;
        var start = attribute.NameOffset;
        while (start > target.Start && text[start - 1] is ' ' or '\t') start--;

        var end = attribute.NameOffset + attribute.Name.Length;
        if (attribute.HasValue && attribute.ValueOffset >= 0)
        {
            end = attribute.ValueOffset + attribute.ValueLength;
            if (end < text.Length && text[end] is '"' or '\'') end++;
        }

        document.ReplaceText(text[..start] + text[end..]);
        return true;
    }

    /// <summary>Переименовать тег в открывающем и закрывающем тегах.</summary>
    public static bool RenameTag(GameUiDocument document, GameUiElement element, string tag)
    {
        var target = document.Find(element.Path);
        var name = (tag ?? "").Trim();
        if (target == null || name.Length == 0 || string.Equals(target.Tag, name, StringComparison.Ordinal))
            return false;

        var text = document.Text;
        if (!target.IsSelfClosing && target.CloseTagStart >= 0)
        {
            var closeAt = target.CloseTagStart + 2;
            text = text[..closeAt] + name + text[(closeAt + target.Tag.Length)..];
        }

        var openAt = target.Start + 1;
        text = text[..openAt] + name + text[(openAt + target.Tag.Length)..];
        document.ReplaceText(text);
        return true;
    }

    /// <summary>Заменить прямой текст элемента (`texture`/`text`); у контейнеров с детьми — `false`.</summary>
    public static bool SetInnerText(GameUiDocument document, GameUiElement element, string value)
    {
        var target = document.Find(element.Path);
        if (target == null || target.Children.Count > 0) return false;
        if (target.InnerTextTrimmed == (value ?? "").Trim()) return false;

        var text = document.Text;
        var updated = text[..target.ContentStart]
            + GameUiFormat.EscapeText(value ?? "")
            + text[target.ContentEnd..];
        document.ReplaceText(updated);
        return true;
    }

    /// <summary>Позиция вставки нового атрибута: перед `>` или перед `/>` открывающего тега.</summary>
    private static int OpenTagAttributeEnd(GameUiElement element) =>
        element.IsSelfClosing && element.OpenTagEnd >= 2 ? element.OpenTagEnd - 2 : element.OpenTagEnd - 1;
}
