namespace XRayForge.Core.Lua;

/// <summary>Токенизация Lua для подсветки и анализа (порт tokenizeLua/tokenizeLuaLines/countLuaBrackets).</summary>
public static class LuaTokenizer
{
    private const char Nl = '\n';
    private const char DoubleQuote = '"';
    private const char SingleQuote = '\'';
    private const char Backslash = '\\';

    /// <summary>Длинная скобка Lua: [=*[ ... ]=*]. <paramref name="start"/> указывает на первую '['. Иначе -1.</summary>
    public static int LongBracketEnd(string s, int start)
    {
        if (start < 0 || start >= s.Length || s[start] != '[') return -1;

        var level = 0;
        while (start + 1 + level < s.Length && s[start + 1 + level] == '=') level++;
        if (start + 1 + level >= s.Length || s[start + 1 + level] != '[') return -1;

        var close = "]" + new string('=', level) + "]";
        var idx = s.IndexOf(close, start + 2 + level, StringComparison.Ordinal);
        return idx == -1 ? -1 : idx + close.Length;
    }

    private static bool IsAlpha(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    private static bool IsDigit(char c) => c >= '0' && c <= '9';
    private static bool IsId(char c) => IsAlpha(c) || IsDigit(c) || c == '_';

    public static List<LuaToken> Tokenize(string? code)
    {
        var tokens = new List<LuaToken>();
        var s = code ?? string.Empty;
        var n = s.Length;
        var i = 0;

        while (i < n)
        {
            var c = s[i];

            // Комментарий: --  или  --[[ ...
            if (c == '-' && i + 1 < n && s[i + 1] == '-')
            {
                if (i + 2 < n && s[i + 2] == '[')
                {
                    var stop = LongBracketEnd(s, i + 2);
                    var end = stop == -1 ? n : stop;
                    tokens.Add(new LuaToken { Text = s.Substring(i, end - i), Cls = LuaTokenClasses.Comment });
                    i = end;
                }
                else
                {
                    var j = i;
                    while (j < n && s[j] != Nl) j++;
                    tokens.Add(new LuaToken { Text = s.Substring(i, j - i), Cls = LuaTokenClasses.Comment });
                    i = j;
                }
                continue;
            }

            if (c == '[')
            {
                var stop = LongBracketEnd(s, i);
                if (stop != -1)
                {
                    tokens.Add(new LuaToken { Text = s.Substring(i, stop - i), Cls = LuaTokenClasses.StringLiteral });
                    i = stop;
                    continue;
                }
            }

            if (c == DoubleQuote || c == SingleQuote)
            {
                var q = c;
                var j = i + 1;
                var closed = false;
                while (j < n)
                {
                    if (s[j] == Backslash) j += 2;
                    else if (s[j] == q) { closed = true; j++; break; }
                    else j++;
                }
                var end = closed ? j : n;
                tokens.Add(new LuaToken { Text = s.Substring(i, end - i), Cls = LuaTokenClasses.StringLiteral });
                i = end;
                continue;
            }

            if (IsDigit(c))
            {
                var j = i;
                while (j < n && (IsDigit(s[j]) || s[j] == '.')) j++;
                tokens.Add(new LuaToken { Text = s.Substring(i, j - i), Cls = LuaTokenClasses.Number });
                i = j;
                continue;
            }

            if (IsAlpha(c) || c == '_')
            {
                var j = i;
                while (j < n && IsId(s[j])) j++;
                var word = s.Substring(i, j - i);
                tokens.Add(new LuaToken
                {
                    Text = word,
                    Cls = LuaCatalog.IsKeyword(word) ? LuaTokenClasses.Keyword : LuaTokenClasses.Ident
                });
                i = j;
                continue;
            }

            if (c is '(' or ')' or '[' or ']' or '{' or '}')
            {
                tokens.Add(new LuaToken { Text = c.ToString(), Cls = LuaTokenClasses.Bracket });
                i++;
                continue;
            }

            tokens.Add(new LuaToken { Text = c.ToString(), Cls = LuaTokenClasses.Plain });
            i++;
        }

        return tokens;
    }

    /// <summary>Токены, разложенные по строкам исходника (для построчного рендера подсветки).</summary>
    public static List<List<LuaToken>> TokenizeLines(string? code)
    {
        var s = code ?? string.Empty;
        var lines = s.Split(Nl);
        var result = new List<List<LuaToken>>();
        for (var k = 0; k < lines.Length; k++) result.Add(new List<LuaToken>());

        var li = 0;
        foreach (var tk in Tokenize(s))
        {
            var text = tk.Text;
            var seg = 0;
            while (seg < text.Length)
            {
                var nl = text.IndexOf(Nl, seg);
                if (nl == -1)
                {
                    if (seg < text.Length && li < result.Count)
                        result[li].Add(new LuaToken { Text = text.Substring(seg), Cls = tk.Cls });
                    break;
                }

                if (nl > seg && li < result.Count)
                    result[li].Add(new LuaToken { Text = text.Substring(seg, nl - seg), Cls = tk.Cls });
                li++;
                seg = nl + 1;
            }
        }

        return result;
    }

    /// <summary>Счётчик открывающих/закрывающих скобок.</summary>
    public static LuaBracketCount CountBrackets(string? code)
    {
        var s = code ?? string.Empty;
        var open = 0;
        var close = 0;
        foreach (var c in s)
        {
            if (c is '(' or '[' or '{') open++;
            else if (c is ')' or ']' or '}') close++;
        }

        return new LuaBracketCount { Open = open, Close = close };
    }
}