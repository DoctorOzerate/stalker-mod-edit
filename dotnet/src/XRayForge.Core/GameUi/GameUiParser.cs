using System.Text;

namespace XRayForge.Core.GameUi;

/// <summary>
/// Толерантный разбор игровых UI-файлов. В отличие от строгого XML-парсера допускает
/// `&lt;!--- … --&gt;` (двойной дефис в комментарии), строки `#include` вне тегов, атрибуты
/// без кавычек и незакрытые элементы: такие места попадают в <see cref="GameUiDocument.Diagnostics"/>,
/// а остальное дерево остаётся пригодным для редактирования.
/// </summary>
public static class GameUiParser
{
    /// <summary>Код диагностики: незакрытый комментарий.</summary>
    public const string UnclosedComment = "uiUnclosedComment";

    /// <summary>Код диагностики: тег не закрыт до конца файла.</summary>
    public const string UnclosedTag = "uiUnclosedTag";

    /// <summary>Код диагностики: лишний закрывающий тег.</summary>
    public const string UnexpectedClose = "uiUnexpectedClose";

    /// <summary>Код диагностики: пустое имя тега.</summary>
    public const string EmptyTag = "uiEmptyTag";

    /// <summary>Код диагностики: кавычка значения атрибута не закрыта.</summary>
    public const string UnclosedQuote = "uiUnclosedQuote";

    /// <summary>Разобрать текст файла в новый документ.</summary>
    public static GameUiDocument Parse(string? text, string? path = null)
    {
        var document = new GameUiDocument { Path = path ?? "" };
        ParseInto(document, text ?? "");
        return document;
    }

    /// <summary>Пересобрать модель документа из текста (корни и диагностика заменяются).</summary>
    internal static void ParseInto(GameUiDocument document, string text)
    {
        document.Text = text;
        document.Roots.Clear();
        document.Diagnostics.Clear();

        var stack = new List<GameUiElement>();
        var i = 0;
        while (i < text.Length)
        {
            var lt = text.IndexOf('<', i);
            var textEnd = lt < 0 ? text.Length : lt;
            if (textEnd > i) AppendInnerText(stack, text, i, textEnd);
            if (lt < 0) break;

            if (StartsWith(text, lt, "<!--"))
            {
                i = SkipComment(document, text, lt);
                continue;
            }

            if (StartsWith(text, lt, "<![CDATA["))
            {
                i = SkipCdata(stack, text, lt);
                continue;
            }

            if (StartsWith(text, lt, "<?"))
            {
                i = SkipTo(text, lt, "?>");
                continue;
            }

            if (StartsWith(text, lt, "<!"))
            {
                i = SkipTo(text, lt, ">");
                continue;
            }

            if (StartsWith(text, lt, "</"))
            {
                i = CloseTag(document, text, stack, lt);
                continue;
            }

            i = OpenTag(document, text, stack, lt);
        }

        while (stack.Count > 0)
        {
            var element = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            element.ContentEnd = text.Length;
            element.End = text.Length;
            document.Diagnostics.Add(new GameUiDiagnostic(
                GameUiDiagnosticSeverity.Warning,
                UnclosedTag,
                $"Тег <{element.Tag}> не закрыт до конца файла.",
                element.Start));
        }
    }

    /// <summary>Текст между тегами прибавляется последнему открытому элементу (прямой текстовый узел).</summary>
    private static void AppendInnerText(List<GameUiElement> stack, string text, int start, int end)
    {
        if (stack.Count == 0) return;
        stack[^1].InnerText += Decode(text.Substring(start, end - start));
    }

    /// <summary>Пропустить комментарий `&lt;!-- … --&gt;`; незакрытый — до конца файла с диагностикой.</summary>
    private static int SkipComment(GameUiDocument document, string text, int lt)
    {
        var close = text.IndexOf("-->", lt + 4, StringComparison.Ordinal);
        if (close >= 0) return close + 3;

        document.Diagnostics.Add(new GameUiDiagnostic(
            GameUiDiagnosticSeverity.Warning, UnclosedComment, "Комментарий не закрыт до конца файла.", lt));
        return text.Length;
    }

    /// <summary>Пропустить CDATA, сохранив содержимое как текст текущего элемента.</summary>
    private static int SkipCdata(List<GameUiElement> stack, string text, int lt)
    {
        const string open = "<![CDATA[";
        var start = lt + open.Length;
        var close = text.IndexOf("]]>", start, StringComparison.Ordinal);
        var end = close >= 0 ? close : text.Length;
        if (stack.Count > 0) stack[^1].InnerText += text.Substring(start, end - start);
        return close >= 0 ? close + 3 : text.Length;
    }

    /// <summary>Пропустить инструкцию/декларацию до указанного окончания.</summary>
    private static int SkipTo(string text, int lt, string terminator)
    {
        var close = text.IndexOf(terminator, lt + 2, StringComparison.Ordinal);
        return close >= 0 ? close + terminator.Length : text.Length;
    }

    private static bool StartsWith(string text, int index, string value) =>
        index >= 0 && index + value.Length <= text.Length
        && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    /// <summary>Закрывающий тег: закрывает найденный по имени элемент, промежуточные помечает незакрытыми.</summary>
    private static int CloseTag(GameUiDocument document, string text, List<GameUiElement> stack, int lt)
    {
        var gt = text.IndexOf('>', lt + 2);
        if (gt < 0)
        {
            document.Diagnostics.Add(new GameUiDiagnostic(
                GameUiDiagnosticSeverity.Error, UnexpectedClose, "Закрывающий тег обрывается до конца файла.", lt));
            return text.Length;
        }

        var name = text.Substring(lt + 2, gt - (lt + 2)).Trim();
        var match = -1;
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (string.Equals(stack[i].Tag, name, StringComparison.OrdinalIgnoreCase))
            {
                match = i;
                break;
            }
        }

        if (match < 0)
        {
            document.Diagnostics.Add(new GameUiDiagnostic(
                GameUiDiagnosticSeverity.Warning, UnexpectedClose, $"Лишний закрывающий тег </{name}>.", lt));
            return gt + 1;
        }

        while (stack.Count > match + 1)
        {
            var orphan = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            orphan.ContentEnd = lt;
            orphan.End = lt;
            document.Diagnostics.Add(new GameUiDiagnostic(
                GameUiDiagnosticSeverity.Warning, UnclosedTag, $"Тег <{orphan.Tag}> не закрыт.", orphan.Start));
        }

        var element = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        element.ContentEnd = lt;
        element.CloseTagStart = lt;
        element.End = gt + 1;
        return gt + 1;
    }

    /// <summary>Открывающий тег: имя, атрибуты (кавычки любые), самозакрытие; элемент попадает в дерево.</summary>
    private static int OpenTag(GameUiDocument document, string text, List<GameUiElement> stack, int lt)
    {
        var length = text.Length;
        var p = lt + 1;
        var nameStart = p;
        while (p < length && IsNameChar(text[p])) p++;
        if (p == nameStart)
        {
            document.Diagnostics.Add(new GameUiDiagnostic(
                GameUiDiagnosticSeverity.Warning, EmptyTag, "Пустое имя тега — символ «<» пропущен.", lt));
            return p > lt ? p : lt + 1;
        }

        var element = new GameUiElement { Tag = text.Substring(nameStart, p - nameStart), Start = lt, Owner = document };
        var selfClosing = false;

        while (p < length)
        {
            while (p < length && char.IsWhiteSpace(text[p])) p++;
            if (p >= length) break;

            if (text[p] == '>')
            {
                p++;
                break;
            }

            if (text[p] == '/' && p + 1 < length && text[p + 1] == '>')
            {
                selfClosing = true;
                p += 2;
                break;
            }

            if (text[p] == '/')
            {
                p++;
                continue;
            }

            var attrStart = p;
            while (p < length && !char.IsWhiteSpace(text[p]) && text[p] is not ('=' or '>' or '/')) p++;
            if (p == attrStart)
            {
                p++;
                continue;
            }

            var attrName = text.Substring(attrStart, p - attrStart);
            var afterName = p;
            while (p < length && char.IsWhiteSpace(text[p])) p++;
            if (p < length && text[p] == '=')
            {
                p++;
                while (p < length && char.IsWhiteSpace(text[p])) p++;
                element.Attributes.Add(ReadAttributeValue(document, text, ref p, attrName, attrStart));
            }
            else
            {
                element.Attributes.Add(new GameUiAttribute
                {
                    Name = attrName,
                    NameOffset = attrStart,
                    HasValue = false
                });
                p = afterName;
            }
        }

        element.OpenTagEnd = p;
        element.ContentStart = p;
        element.ContentEnd = p;
        element.End = p;

        if (selfClosing)
        {
            element.IsSelfClosing = true;
            Attach(document, stack, element);
            return p;
        }

        Attach(document, stack, element);
        stack.Add(element);
        return p;
    }

    /// <summary>Значение атрибута в кавычках или без них; для незакрытой кавычки — до конца файла.</summary>
    private static GameUiAttribute ReadAttributeValue(
        GameUiDocument document, string text, ref int p, string name, int nameOffset)
    {
        var length = text.Length;
        if (p < length && text[p] is '"' or '\'')
        {
            var quote = text[p];
            var valueStart = p + 1;
            var close = text.IndexOf(quote, valueStart);
            if (close < 0)
            {
                close = length;
                document.Diagnostics.Add(new GameUiDiagnostic(
                    GameUiDiagnosticSeverity.Warning, UnclosedQuote,
                    $"Значение атрибута {name} не закрыто кавычкой.", nameOffset));
            }

            var valueLength = Math.Max(0, close - valueStart);
            var value = Decode(text.Substring(valueStart, valueLength));
            p = close < length ? close + 1 : length;
            return new GameUiAttribute
            {
                Name = name,
                Value = value,
                NameOffset = nameOffset,
                ValueOffset = valueStart,
                ValueLength = valueLength
            };
        }

        var bareStart = p;
        while (p < length && !char.IsWhiteSpace(text[p]) && text[p] is not ('>' or '/')) p++;
        var bare = text.Substring(bareStart, p - bareStart);
        return new GameUiAttribute
        {
            Name = name,
            Value = Decode(bare),
            NameOffset = nameOffset,
            ValueOffset = bareStart,
            ValueLength = bare.Length
        };
    }

    private static void Attach(GameUiDocument document, List<GameUiElement> stack, GameUiElement element)
    {
        if (stack.Count == 0)
        {
            document.Roots.Add(element);
            return;
        }

        var parent = stack[^1];
        element.Parent = parent;
        element.Owner = document;
        parent.Children.Add(element);
    }

    private static bool IsNameChar(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':' or '?' or '!';

    /// <summary>Раскодировать сущности XML в значении атрибута или тексте.</summary>
    internal static string Decode(string? text)
    {
        var value = text ?? "";
        if (!value.Contains('&')) return value;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '&')
            {
                builder.Append(c);
                continue;
            }

            var semicolon = value.IndexOf(';', i + 1);
            if (semicolon < 0 || semicolon - i > 8)
            {
                builder.Append(c);
                continue;
            }

            var entity = value.Substring(i + 1, semicolon - i - 1);
            var decoded = entity switch
            {
                "lt" => "<",
                "gt" => ">",
                "quot" => "\"",
                "apos" => "'",
                "amp" => "&",
                _ => null
            };

            if (decoded == null)
            {
                builder.Append(c);
                continue;
            }

            builder.Append(decoded);
            i = semicolon;
        }

        return builder.ToString();
    }
}
