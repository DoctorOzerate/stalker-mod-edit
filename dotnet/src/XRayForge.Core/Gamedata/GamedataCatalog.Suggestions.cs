using XRayForge.Core.Suggestions;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Gamedata;

/// <summary>
/// Подсказки из каталога gamedata: LTX-секции и пути `#include`, Lua-скрипты и функции,
/// XML id и id реплик. Значения каталога добавляются в индекс после данных проекта, поэтому
/// реальные файлы проекта имеют приоритет, а каталог заполняет пробелы.
/// </summary>
public sealed partial class GamedataCatalog
{
    private readonly Dictionary<string, IReadOnlyList<SuggestionEntry>> _suggestionsBySeries =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<SuggestionEntry>> _includesBySeries =
        new(StringComparer.Ordinal);
    private readonly object _suggestionLock = new();

    /// <summary>Подсказки каталога для серии (LTX/Lua/XML/text_id) с дедупликацией.</summary>
    public IReadOnlyList<SuggestionEntry> Suggestions(string? series)
    {
        var key = NormalizeSeries(series);
        lock (_suggestionLock)
        {
            if (_suggestionsBySeries.TryGetValue(key, out var cached)) return cached;
            var value = BuildSuggestions(series);
            _suggestionsBySeries[key] = value;
            return value;
        }
    }

    /// <summary>Только пути `#include` (для редактора `system.ltx`).</summary>
    public IReadOnlyList<SuggestionEntry> IncludeSuggestions(string? series)
    {
        var key = NormalizeSeries(series);
        lock (_suggestionLock)
        {
            if (_includesBySeries.TryGetValue(key, out var cached)) return cached;
            var value = Suggestions(series)
                .Where(entry => entry.Kind == SuggestionKind.Include)
                .ToList();
            _includesBySeries[key] = value;
            return value;
        }
    }

    private IReadOnlyList<SuggestionEntry> BuildSuggestions(string? series)
    {
        var result = new List<SuggestionEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? raw, SuggestionKind kind, string? detail = null)
        {
            var value = CleanValue(raw);
            if (value.Length is 0 or > 160) return;
            if (value.Equals("пустой файл", StringComparison.OrdinalIgnoreCase)) return;
            if (IsCountLabel(value)) return;
            if (!seen.Add(value)) return;
            result.Add(new SuggestionEntry(value, kind, detail));
        }

        foreach (var entry in ForSeries(series))
        {
            if (entry.IsCopy) continue;
            var detail = entry.FileName;

            if (entry.IsLtx)
            {
                var keyElements = CleanValue(entry.KeyElements);
                if (!keyElements.StartsWith("#include:", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var section in ParseNames(keyElements))
                        Add(section, SuggestionKind.Section, detail);
                }

                foreach (var include in ParseIncludes(keyElements))
                    Add(include, SuggestionKind.Include, "catalog.csv");

                var includePath = ConfigRelativeLtxPath(entry);
                if (includePath.Length > 0)
                    Add(includePath, SuggestionKind.Include, detail);
                continue;
            }

            if (entry.IsLua)
            {
                var script = ScriptName(entry);
                Add(script, SuggestionKind.Script, entry.Group.Length > 0 ? entry.Group : detail);
                foreach (var element in ParseNames(entry.KeyElements))
                {
                    if (string.Equals(element, script, StringComparison.OrdinalIgnoreCase)) continue;
                    Add(element, SuggestionKind.ScriptFunction, script);
                    Add(script + "." + element, SuggestionKind.ScriptBinding, detail);
                }

                continue;
            }

            if (entry.IsStringTable)
            {
                foreach (var textId in ParseNames(entry.KeyElements))
                    Add(textId, SuggestionKind.TextId, detail);
                continue;
            }

            if (!entry.IsXml) continue;
            foreach (var id in ParseNames(entry.KeyElements))
            {
                if (id.Contains('<', StringComparison.Ordinal)) continue;
                Add(id, entry.IsDialogXml ? SuggestionKind.Dialogue : SuggestionKind.Section, detail);
            }
        }

        return result;
    }

    /// <summary>Имя скрипта: имя файла без расширения (`xr_conditions.lua` → `xr_conditions`).</summary>
    private static string ScriptName(GamedataCatalogEntry entry)
    {
        var name = entry.FileName;
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>
    /// Путь LTX относительно каталога конфигов серии (`configs/` у CoP, `config/` у SoC) —
    /// именно в таком виде его принимает `#include`. Копии и не-LTX файлы пропускаются.
    /// </summary>
    private static string ConfigRelativeLtxPath(GamedataCatalogEntry entry)
    {
        if (!entry.IsLtx || entry.IsCopy) return "";
        var path = entry.Path;
        var marker = "/gamedata/";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var relative = index >= 0 ? path[(index + marker.Length)..] : path;

        var configDirectory = GameSeriesCatalog.ConfigDirectory(entry.Series) + "/";
        if (relative.StartsWith(configDirectory, StringComparison.OrdinalIgnoreCase))
            relative = relative[configDirectory.Length..];
        else if (relative.StartsWith("configs/", StringComparison.OrdinalIgnoreCase))
            relative = relative["configs/".Length..];
        else if (relative.StartsWith("config/", StringComparison.OrdinalIgnoreCase))
            relative = relative["config/".Length..];

        return relative.EndsWith(".ltx", StringComparison.OrdinalIgnoreCase)
            ? relative.Replace('/', '\\')
            : "";
    }

    /// <summary>Имена из колонки «Ключевые элементы»: `[section], name (+12)` → `section`, `name`.</summary>
    private static IEnumerable<string> ParseNames(string? keyElements)
    {
        var text = StripLeadingCount(StripSizeSuffix(CleanValue(keyElements)));
        if (text.Length == 0) yield break;
        foreach (var raw in text.Split(','))
        {
            var value = CleanValue(raw).Trim('[', ']');
            if (value.Length == 0 || value.Length > 160) continue;
            if (value.Equals("пустой файл", StringComparison.OrdinalIgnoreCase)) continue;
            if (value.All(char.IsDigit)) continue;
            if (IsCountLabel(value)) continue;
            if (value.Contains('(') || value.Contains(')')) continue;
            yield return value;
        }
    }

    /// <summary>Убирает ведущую подпись-счётчик (`41 строк: id1, id2`, `функций: 3`).</summary>
    private static string StripLeadingCount(string value)
    {
        var index = value.IndexOf(':');
        if (index <= 0) return value;
        var prefix = value[..index].Trim();
        if (IsCountLabel(prefix + ":") || IsCountLabel(value[..(index + 1)]))
            return value[(index + 1)..].Trim();
        return value;
    }

    /// <summary>Пути из `#include:`; в CSV обратные слэши удвоены, поэтому схлопываем их.</summary>
    private static IEnumerable<string> ParseIncludes(string? keyElements)
    {
        var text = CleanValue(keyElements);
        const string prefix = "#include:";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) yield break;
        text = StripSizeSuffix(text[prefix.Length..].Trim());
        foreach (var raw in text.Split(','))
        {
            var value = CleanValue(raw)
                .Replace(@"\\", @"\", StringComparison.Ordinal)
                .Replace('/', '\\');
            if (value.EndsWith(".ltx", StringComparison.OrdinalIgnoreCase))
                yield return value;
        }
    }

    /// <summary>Убирает хвост `(+44)` — число неназванных элементов, а не часть имени.</summary>
    private static string StripSizeSuffix(string value)
    {
        var text = CleanValue(value);
        var index = text.LastIndexOf("(+", StringComparison.Ordinal);
        if (index <= 0 || !text.EndsWith(')')) return text;
        var count = text[(index + 2)..^1];
        return count.Length > 0 && count.All(char.IsDigit) ? text[..index].TrimEnd() : text;
    }

    /// <summary>Служебные подписи каталога (`функций: 3`, `строк: 12`, `41 строк: …`) — не имена и не id.</summary>
    private static bool IsCountLabel(string value)
    {
        var index = value.IndexOf(':');
        if (index <= 0) return false;
        var prefix = value[..index].Trim();
        if (IsCountWord(prefix)) return true;
        var digits = prefix.TakeWhile(char.IsDigit).Count();
        return digits > 0 && IsCountWord(prefix[digits..].Trim());
    }

    private static bool IsCountWord(string value) =>
        value.Equals("функций", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("строк", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("параметров", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("элементов", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("include-файлов", StringComparison.OrdinalIgnoreCase);

    private static string CleanValue(string? value)
    {
        var text = (value ?? "").Trim().Trim('"', '\'').Trim();
        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text;
    }
}
