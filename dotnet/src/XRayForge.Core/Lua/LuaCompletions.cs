using XRayForge.Core.Suggestions;

namespace XRayForge.Core.Lua;

/// <summary>
/// Автодополнение Lua-редактора: определение вводимого токена под каретой, проверка
/// «курсор в комментарии» и подбор подсказок. Отделено от WPF, чтобы правила ввода
/// (`db.actor:give_info_portion`, `level.actor`, info-порции `+quest_done`) тестировались без UI.
/// </summary>
public static class LuaCompletions
{
    /// <summary>Минимальная длина введённого токена, после которой показываются подсказки.</summary>
    public const int MinimumPrefixLength = 2;

    /// <summary>Сколько подсказок максимум держать в выпадающем списке.</summary>
    public const int DefaultLimit = 40;

    /// <summary>Символы, из которых состоит имя API/функции (`db.actor:give_item`).</summary>
    private static bool IsTokenChar(char symbol) =>
        char.IsLetterOrDigit(symbol) || symbol is '_' or '.' or ':';

    /// <summary>
    /// Начало и текст вводимого токена перед каретой. `+`/`-` (info-порции вида `+quest_done`)
    /// в токен не входят, чтобы подсказка подставлялась сразу после знака.
    /// </summary>
    public static (int Start, string Prefix) PrefixAt(string? text, int caret)
    {
        var source = text ?? "";
        var end = Math.Clamp(caret, 0, source.Length);
        var start = end;
        while (start > 0 && IsTokenChar(source[start - 1])) start--;
        return (start, source[start..end]);
    }

    /// <summary>Конец токена после каретки — остаток заменяется при принятии подсказки.</summary>
    public static int TokenEnd(string? text, int caret)
    {
        var source = text ?? "";
        var index = Math.Clamp(caret, 0, source.Length);
        while (index < source.Length && IsTokenChar(source[index])) index++;
        return index;
    }

    /// <summary>
    /// Каретка внутри комментария строки (`-- ...`). В строковых литералах `--` комментарием
    /// не считается, поэтому подсказки для `give_info("+quest")` продолжают работать.
    /// </summary>
    public static bool IsComment(string? text, int caret)
    {
        var source = text ?? "";
        var end = Math.Clamp(caret, 0, source.Length);
        var lineStart = source.LastIndexOf('\n', Math.Max(0, end - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var quote = '\0';
        for (var i = lineStart; i < end; i++)
        {
            var symbol = source[i];
            if (quote != '\0')
            {
                if (symbol == '\\')
                {
                    i++;
                    continue;
                }

                if (symbol == quote) quote = '\0';
                continue;
            }

            if (symbol is '"' or '\'')
            {
                quote = symbol;
                continue;
            }

            if (symbol == '-' && i + 1 < source.Length && source[i + 1] == '-') return true;
        }

        return false;
    }

    /// <summary>
    /// Подсказки для текста под каретой: `null`, если показывать нечего (короткий токен,
    /// комментарий или нет совпадений). При равенстве значения префиксу подсказка скрывается.
    /// </summary>
    public static IReadOnlyList<SuggestionEntry>? Suggest(
        string? text, int caret, IEnumerable<SuggestionEntry>? source, int limit = DefaultLimit)
    {
        if (source == null) return null;
        if (IsComment(text, caret)) return null;

        var (_, prefix) = PrefixAt(text, caret);
        if (prefix.Length < MinimumPrefixLength) return null;

        var tail = LastSegment(prefix);
        var matches = SuggestionMatcher
            .Filter(source, prefix, Math.Max(1, limit))
            .Where(entry => !string.Equals(entry.Value, prefix, StringComparison.OrdinalIgnoreCase))
            // `db.actor:give_info` → `db.actor:give_info_portion` раньше прочих методов `db.actor`.
            .OrderBy(entry => TailMatches(entry.Value, tail) ? 0 : 1)
            .ToList();

        return matches.Count == 0 ? null : matches;
    }

    /// <summary>Разделители сегментов имени API (`db.actor:give_info_portion`).</summary>
    private static readonly char[] SegmentSeparators = { '.', ':' };

    /// <summary>Хвост токена после последнего `.`/`:` (`db.actor:give_info` → `give_info`).</summary>
    private static string LastSegment(string value)
    {
        var index = value.LastIndexOfAny(SegmentSeparators);
        return index >= 0 ? value[(index + 1)..] : value;
    }

    /// <summary>Последний сегмент значения начинается с хвоста введённого токена.</summary>
    private static bool TailMatches(string value, string tail) =>
        tail.Length > 0 && LastSegment(value).StartsWith(tail, StringComparison.OrdinalIgnoreCase);
}
