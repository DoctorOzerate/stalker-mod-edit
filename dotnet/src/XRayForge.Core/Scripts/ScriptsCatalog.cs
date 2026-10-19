using XRayForge.Core.Lua;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Scripts;

/// <summary>Строка предпросмотра скрипта: номер, маркер блока и токены подсветки.</summary>
public sealed class ScriptPreviewLine
{
    /// <summary>Номер строки исходника (0-based); `-1` для свёрнутой заглушки.</summary>
    public int Line { get; set; } = -1;

    /// <summary>Подпись номера строки (пустая для заглушки).</summary>
    public string Number { get; set; } = "";

    /// <summary>Маркер раскрытия функции (`▾`/`▸`); пустой для обычной строки.</summary>
    public string Marker { get; set; } = "";

    /// <summary>Строка начинает блок функции — по маркеру можно свернуть/развернуть.</summary>
    public bool IsBlockStart { get; set; }

    /// <summary>Строка-заглушка свёрнутого блока.</summary>
    public bool IsSummary { get; set; }

    /// <summary>Сколько строк исходника скрыто (для заглушки).</summary>
    public int CollapsedCount { get; set; }

    /// <summary>Токены подсветки строки; у заглушки формируются ViewModel.</summary>
    public List<LuaToken> Tokens { get; set; } = new();
}

/// <summary>Найденное определение функции (`jumpToDefinition`).</summary>
public sealed class ScriptDefinition
{
    public ScriptFile File { get; set; } = new();
    public int Index { get; set; }
    public int Line { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Результат вставки текста в позицию курсора.</summary>
public sealed class ScriptInsertResult
{
    public string Text { get; set; } = "";
    public int Caret { get; set; }
}

/// <summary>
/// Операции вкладки «Скрипты» без WPF: список и фильтр, создание/переименование/удаление,
/// замена по всем файлам, переход к определению, вставка в курсор, предпросмотр блоков.
/// Порт `ScriptsTab.js` (`updateScript`, `replaceAllInScripts`, `jumpToDefinition`,
/// `toggleBlock`, `deleteScript`, `renameScript`, `formatCurrentScript`, `insertLuaAtCursor`).
/// </summary>
public static class ScriptsCatalog
{
    /// <summary>Имя файла по умолчанию (`new_script.lua`).</summary>
    public const string DefaultName = "new_script.lua";

    /// <summary>Начальный текст нового файла.</summary>
    public const string DefaultText = "-- X-Ray Forge script\n\nfunction on_game_start()\n\t\nend\n";

    /// <summary>Имя скрипта: `name`, затем имя файла из `path`.</summary>
    public static string Name(ScriptFile? file)
    {
        if (file == null) return "";
        if (!string.IsNullOrWhiteSpace(file.Name)) return file.Name;
        if (!string.IsNullOrWhiteSpace(file.Path)) return Path.GetFileName(file.Path);
        return "";
    }

    /// <summary>Текущий текст скрипта (`s.text`, отсутствие — пустая строка).</summary>
    public static string Text(ScriptFile? file) => file?.Text ?? "";

    /// <summary>Исходный текст для diff (`s.original`).</summary>
    public static string OriginalText(ScriptFile? file) => file?.Original ?? "";

    /// <summary>Текст отличается от исходного (`updateScript` рисует маркер «изменён»).</summary>
    public static bool HasChanges(ScriptFile? file) =>
        file != null && !string.Equals(file.Text ?? "", file.Original ?? "", StringComparison.Ordinal);

    /// <summary>Строк в скрипте: пустой текст — одна строка (`split('\n')`).</summary>
    public static int LineCount(string? text) => (text ?? "").Split('\n').Length;

    /// <summary>Номера строк для редактора (`1\n2\n…`).</summary>
    public static string LineNumbers(string? text)
    {
        var count = LineCount(text);
        var builder = new System.Text.StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            if (i > 1) builder.Append('\n');
            builder.Append(i);
        }

        return builder.ToString();
    }

    /// <summary>Смещение начала строки (`0-based`) в тексте с `\n` или `\r\n`.</summary>
    public static int LineOffset(string? text, int zeroBasedLine)
    {
        var source = text ?? "";
        if (zeroBasedLine <= 0) return 0;

        var line = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '\n') continue;
            line++;
            if (line == zeroBasedLine) return i + 1;
        }

        return source.Length;
    }

    /// <summary>Номер строки (0-based) для смещения курсора.</summary>
    public static int LineAtOffset(string? text, int offset)
    {
        var source = text ?? "";
        var limit = Math.Min(Math.Max(offset, 0), source.Length);
        var line = 0;
        for (var i = 0; i < limit; i++)
            if (source[i] == '\n') line++;
        return line;
    }

    /// <summary>
    /// Фильтр списка (`updateScriptsList`): поиск по имени, пути и тексту без учёта регистра;
    /// `sortByName` — сортировка по имени (иначе порядок проекта).
    /// </summary>
    public static List<ScriptFile> Filter(IEnumerable<ScriptFile>? scripts, string? query, bool sortByName = false)
    {
        var needle = (query ?? "").Trim();
        var list = new List<ScriptFile>();
        foreach (var script in scripts ?? Array.Empty<ScriptFile>())
            if (script != null && Matches(script, needle)) list.Add(script);

        if (sortByName)
            list = list
                .OrderBy(Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(script => script.Path ?? "", StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        return list;
    }

    /// <summary>Совпадает ли скрипт с поисковой строкой (пустая строка — все).</summary>
    public static bool Matches(ScriptFile? file, string? query)
    {
        var needle = (query ?? "").Trim();
        if (needle.Length == 0) return file != null;
        if (file == null) return false;
        var comparison = StringComparison.CurrentCultureIgnoreCase;
        return Name(file).Contains(needle, comparison)
               || (file.Path ?? "").Contains(needle, comparison)
               || (file.Text ?? "").Contains(needle, comparison);
    }

    /// <summary>Новый файл (`new_script.lua` + текущий текст в качестве исходника).</summary>
    public static ScriptFile Create(string? name = null, string? text = null)
    {
        var fileName = NormalizeFileName(name, DefaultName);
        var content = text ?? DefaultText;
        return new ScriptFile { Name = fileName, Path = fileName, Text = content, Original = content };
    }

    /// <summary>Имя файла: без каталогов и недопустимых символов, с расширением `.lua`.</summary>
    public static string NormalizeFileName(string? name, string fallback = DefaultName)
    {
        var candidate = (name ?? "").Trim();
        if (candidate.Length == 0) return fallback;

        candidate = Path.GetFileName(candidate);
        foreach (var invalid in Path.GetInvalidFileNameChars())
            candidate = candidate.Replace(invalid, '_');
        candidate = candidate.Trim();
        if (candidate.Length == 0 || candidate == "." || candidate == "..") return fallback;

        if (Path.GetExtension(candidate).Length == 0) candidate += ".lua";
        return candidate;
    }

    /// <summary>Переименовать файл (`renameScript`); возвращает `false` без изменений.</summary>
    public static bool Rename(ScriptFile? file, string? newName)
    {
        if (file == null) return false;
        var normalized = NormalizeFileName(newName, Name(file));
        if (string.Equals(Name(file), normalized, StringComparison.Ordinal)) return false;
        file.Name = normalized;
        return true;
    }

    /// <summary>Удалить скрипт из проекта (`deleteScript`).</summary>
    public static bool Remove(IList<ScriptFile>? scripts, ScriptFile? file) =>
        file != null && scripts != null && scripts.Remove(file);

    /// <summary>
    /// Заменить подстроку во всех скриптах (`replaceAllInScripts`, `String.replaceAll`).
    /// Возвращает число заменённых вхождений.
    /// </summary>
    public static int ReplaceAll(IEnumerable<ScriptFile>? scripts, string? search, string? replacement)
    {
        if (string.IsNullOrEmpty(search)) return 0;

        var count = 0;
        foreach (var script in scripts ?? Array.Empty<ScriptFile>())
        {
            var text = script.Text ?? "";
            if (text.Length == 0 || !text.Contains(search, StringComparison.Ordinal)) continue;

            count += CountOccurrences(text, search);
            script.Text = text.Replace(search, replacement ?? "", StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Число неперекрывающихся вхождений подстроки.</summary>
    public static int CountOccurrences(string? text, string? value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) return 0;

        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    /// <summary>Форматировать текст (`formatCurrentScript`) через <see cref="LuaFormatter"/>.</summary>
    public static string Format(string? text) => LuaFormatter.Format(text ?? "");

    /// <summary>Проверка баланса блоков (`luaCheck`).</summary>
    public static List<LuaIssue> Check(string? text) => LuaAnalyzer.Check(text);

    /// <summary>Граф вызовов текущего скрипта.</summary>
    public static LuaCallGraph CallGraph(string? text) => LuaAnalyzer.BuildCallGraph(text);

    /// <summary>Функции, которые нигде не вызываются.</summary>
    public static List<string> Unused(string? text) => LuaAnalyzer.Unused(text);

    /// <summary>Баланс скобок текущего скрипта.</summary>
    public static LuaBracketCount Brackets(string? text) => LuaTokenizer.CountBrackets(text);

    /// <summary>
    /// Найти определение функции (`jumpToDefinition`): сначала в текущем скрипте, затем
    /// в остальных файлах проекта в исходном порядке.
    /// </summary>
    public static ScriptDefinition? FindDefinition(
        IReadOnlyList<ScriptFile>? scripts, string? functionName, int currentIndex = -1)
    {
        var name = TrimFunctionName(functionName);
        if (name.Length == 0 || scripts == null) return null;

        if (currentIndex >= 0 && currentIndex < scripts.Count)
        {
            var line = DefinitionLine(Text(scripts[currentIndex]), name);
            if (line >= 0)
                return new ScriptDefinition { File = scripts[currentIndex], Index = currentIndex, Line = line, Name = name };
        }

        for (var index = 0; index < scripts.Count; index++)
        {
            if (index == currentIndex) continue;
            var line = DefinitionLine(Text(scripts[index]), name);
            if (line >= 0)
                return new ScriptDefinition { File = scripts[index], Index = index, Line = line, Name = name };
        }

        return null;
    }

    /// <summary>Строка объявления функции (0-based) или `-1`.</summary>
    public static int DefinitionLine(string? text, string? functionName)
    {
        var name = TrimFunctionName(functionName);
        if (name.Length == 0) return -1;

        var lines = (text ?? "").Split('\n');
        for (var index = 0; index < lines.Length; index++)
            if (string.Equals(DeclaredFunctionName(lines[index]), name, StringComparison.Ordinal))
                return index;
        return -1;
    }

    /// <summary>Имя функции из строки `function f(...)` / `local function f(...)`.</summary>
    public static string DeclaredFunctionName(string? line)
    {
        var trimmed = (line ?? "").TrimStart();
        if (trimmed.StartsWith("local ", StringComparison.Ordinal)) trimmed = trimmed[6..].TrimStart();
        if (!trimmed.StartsWith("function ", StringComparison.Ordinal)) return "";

        var rest = trimmed[9..];
        var end = rest.IndexOf('(');
        if (end < 0) return "";
        return rest[..end].Trim();
    }

    /// <summary>Имя функции без вызова (`f()` → `f`).</summary>
    public static string TrimFunctionName(string? value)
    {
        var name = (value ?? "").Trim();
        var paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];
        return name.Trim();
    }

    /// <summary>
    /// Вставить API/сниппет в позицию курсора (`insertLuaAtCursor`). Выделение заменяется,
    /// многострочный текст получает отступ текущей строки; возвращается новая позиция курсора.
    /// </summary>
    public static ScriptInsertResult InsertAtCursor(
        string? text, string? snippet, int caret, int selectionLength = 0)
    {
        var source = text ?? "";
        var start = Math.Min(Math.Max(caret, 0), source.Length);
        var length = Math.Min(Math.Max(selectionLength, 0), source.Length - start);

        var lineStart = start == 0 ? -1 : source.LastIndexOf('\n', start - 1);
        var linePrefix = source.Substring(lineStart + 1, start - lineStart - 1);
        var indent = linePrefix[..(linePrefix.Length - linePrefix.TrimStart(' ', '\t').Length)];

        var body = (snippet ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (indent.Length > 0) body = body.Replace("\n", "\n" + indent, StringComparison.Ordinal);

        var result = source[..start] + body + source[(start + length)..];
        return new ScriptInsertResult { Text = result, Caret = start + body.Length };
    }

    /// <summary>
    /// Строки предпросмотра с подсветкой и сворачиванием функций (`toggleBlock`).
    /// <paramref name="collapsed"/> — 0-based строки начал функций, которые свернуты.
    /// </summary>
    public static List<ScriptPreviewLine> BuildPreview(string? text, IEnumerable<int>? collapsed = null)
    {
        var source = text ?? "";
        if (source.Length == 0) return new List<ScriptPreviewLine>();
        var lines = source.Split('\n');
        var blocks = LuaFormatter.FunctionBlocks(lines);
        var byStart = new Dictionary<int, LuaBlock>();
        foreach (var block in blocks) byStart[block.Start] = block;

        var collapsedStarts = new HashSet<int>(collapsed ?? Array.Empty<int>());
        var tokenLines = LuaTokenizer.TokenizeLines(source);
        var rows = new List<ScriptPreviewLine>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (byStart.TryGetValue(index, out var block))
            {
                var isCollapsed = collapsedStarts.Contains(index);
                rows.Add(new ScriptPreviewLine
                {
                    Line = index,
                    Number = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
                    Marker = isCollapsed ? "▸" : "▾",
                    IsBlockStart = true,
                    Tokens = TokenLine(tokenLines, index)
                });

                if (isCollapsed)
                {
                    rows.Add(new ScriptPreviewLine
                    {
                        Line = -1,
                        IsSummary = true,
                        CollapsedCount = Math.Max(1, block.End - block.Start)
                    });
                    index = block.End;
                }

                continue;
            }

            rows.Add(new ScriptPreviewLine
            {
                Line = index,
                Number = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
                Tokens = TokenLine(tokenLines, index)
            });
        }

        // Хвостовая пустая строка в предпросмотре не нужна (нумерация редактора её сохраняет).
        if (rows.Count > 1 && rows[^1].Tokens.Count == 0 && rows[^1].Number.Length > 0)
            rows.RemoveAt(rows.Count - 1);
        return rows;
    }

    private static List<LuaToken> TokenLine(IReadOnlyList<List<LuaToken>> lines, int index) =>
        index >= 0 && index < lines.Count ? lines[index] : new List<LuaToken>();
}
