using System.Text;

namespace XRayForge.Core.Gamedata;

/// <summary>
/// Строка каталога gamedata (`catalog.csv`): файл игры, его назначение и ключевые элементы.
/// Каталог — только справочник для автодополнения; содержимое файлов он не подменяет.
/// </summary>
public sealed class GamedataCatalogEntry
{
    /// <summary>Проект каталога (`CoP`, `SoC`).</summary>
    public string Project { get; init; } = "";

    /// <summary>Ключ серии проекта: `soc`, `cs`, `cop`.</summary>
    public string Series { get; init; } = "";

    /// <summary>Глава каталога (крупный раздел gamedata).</summary>
    public string Chapter { get; init; } = "";

    /// <summary>Группа внутри главы (например, «Оружие»).</summary>
    public string Group { get; init; } = "";

    /// <summary>Путь в каталоге (`CoP/gamedata/configs/...`).</summary>
    public string Path { get; init; } = "";

    /// <summary>Имя файла без каталогов.</summary>
    public string FileName { get; init; } = "";

    /// <summary>Расширение в нижнем регистре, включая точку.</summary>
    public string Extension { get; init; } = "";

    /// <summary>Тип файла из каталога (`LTX`, `Lua`, `XML (строки)`, …).</summary>
    public string Type { get; init; } = "";

    /// <summary>Размер в человекочитаемом виде (`12.3 КБ`).</summary>
    public string SizeLabel { get; init; } = "";

    /// <summary>Число строк файла (0, если в каталоге не указано).</summary>
    public int Lines { get; init; }

    /// <summary>Описание назначения файла.</summary>
    public string Description { get; init; } = "";

    /// <summary>Ключевые элементы: секции, `#include`, имена функций и id.</summary>
    public string KeyElements { get; init; } = "";

    /// <summary>Аналог файла в другой серии (если указан).</summary>
    public string AnalogPath { get; init; } = "";

    /// <summary>MD5 исходного файла (если указан).</summary>
    public string Md5 { get; init; } = "";

    /// <summary>Файл LTX.</summary>
    public bool IsLtx => Extension == ".ltx";

    /// <summary>Скрипт Lua (`.lua` или `.script`).</summary>
    public bool IsLua => Extension is ".lua" or ".script";

    /// <summary>XML-файл.</summary>
    public bool IsXml => Extension == ".xml";

    /// <summary>`string_table` с локализацией.</summary>
    public bool IsStringTable =>
        IsXml && Type.StartsWith("XML (строки", StringComparison.Ordinal);

    /// <summary>Файл диалогов (`game_dialogs*.xml`) — источник id реплик.</summary>
    public bool IsDialogXml =>
        IsXml && FileName.StartsWith("game_dialogs", StringComparison.OrdinalIgnoreCase);

    /// <summary>Копия/дубль файла: такие записи не дают подсказок.</summary>
    public bool IsCopy =>
        Type.Contains("копи", StringComparison.OrdinalIgnoreCase) ||
        FileName.Contains('~');

    public override string ToString() => Path;
}


/// <summary>
/// Каталог gamedata из `catalog.csv` (3272 файла CoP/SoC). Используется как источник подсказок
/// для LTX/Lua/XML, когда файлов проекта ещё нет или нужных значений в них не хватает.
/// Разбор CSV откладывается до первого обращения, поэтому запуск без каталога дешёвый.
/// </summary>
public sealed partial class GamedataCatalog
{
    /// <summary>Логическое имя встроенного ресурса с CSV.</summary>
    public const string ResourceName = "XRayForge.Core.Gamedata.Data.gamedata-catalog.csv";

    private static readonly Lazy<GamedataCatalog> LazyDefault =
        new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IReadOnlyList<GamedataCatalogEntry> _entries;
    private readonly Dictionary<string, IReadOnlyList<GamedataCatalogEntry>> _bySeries =
        new(StringComparer.Ordinal);
    private readonly object _seriesLock = new();

    /// <summary>Каталог, встроенный в сборку; пустой, если ресурс не найден.</summary>
    public static GamedataCatalog Default => LazyDefault.Value;

    /// <summary>Пустой каталог (используется в тестах и при отсутствии `catalog.csv`).</summary>
    public static GamedataCatalog Empty { get; } = new(Array.Empty<GamedataCatalogEntry>());

    internal GamedataCatalog(IReadOnlyList<GamedataCatalogEntry> entries) => _entries = entries;

    /// <summary>Все записи каталога в порядке CSV.</summary>
    public IReadOnlyList<GamedataCatalogEntry> Entries => _entries;

    /// <summary>
    /// Записи выбранной серии: для `soc`/`cop` — только своя серия, для `cs` и неизвестной —
    /// обе серии каталога (в каталоге нет CS, но подсказки лучше показать, чем потерять).
    /// </summary>
    public IReadOnlyList<GamedataCatalogEntry> ForSeries(string? series)
    {
        var key = NormalizeSeries(series);
        lock (_seriesLock)
        {
            if (_bySeries.TryGetValue(key, out var cached)) return cached;
            IReadOnlyList<GamedataCatalogEntry> value = key is "soc" or "cop"
                ? _entries.Where(entry => entry.Series == key).ToList()
                : _entries;
            _bySeries[key] = value;
            return value;
        }
    }

    /// <summary>Нормализует ключ серии проекта (`SoC`/`CoP`/`soc` → `soc`/`cop`).</summary>
    public static string NormalizeSeries(string? series)
    {
        var value = (series ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            "soc" or "cop" or "cs" => value,
            _ when value.Contains("pripyat", StringComparison.Ordinal) ||
                   value.Contains("припят", StringComparison.Ordinal) => "cop",
            _ when value.Contains("chernobyl", StringComparison.Ordinal) ||
                   value.Contains("чернобыл", StringComparison.Ordinal) => "soc",
            _ when value.Contains("clear", StringComparison.Ordinal) ||
                   value.Contains("чистое", StringComparison.Ordinal) => "cs",
            _ => value
        };
    }

    /// <summary>Разбирает CSV каталога; пустая строка или отсутствие заголовка → пустой каталог.</summary>
    public static GamedataCatalog LoadFromCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Empty;
        var rows = ParseRows(csv.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        if (rows.Count < 2) return Empty;

        var header = rows[0].Select(cell => cell.Trim().TrimStart('\uFEFF')).ToList();
        var projectIndex = IndexOf(header, "Проект");
        var chapterIndex = IndexOf(header, "Глава");
        var groupIndex = IndexOf(header, "Группа");
        var pathIndex = IndexOf(header, "Путь");
        var typeIndex = IndexOf(header, "Тип");
        var sizeIndex = IndexOf(header, "Размер");
        var linesIndex = IndexOf(header, "Строк");
        var descriptionIndex = IndexOf(header, "Описание");
        var elementsIndex = IndexOf(header, "Ключевые элементы");
        var analogIndex = IndexOfPrefix(header, "Аналог");
        var md5Index = IndexOf(header, "MD5");
        if (pathIndex < 0) return Empty;

        var entries = new List<GamedataCatalogEntry>(rows.Count - 1);
        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace)) continue;

            string Cell(int column) => column >= 0 && column < row.Count ? row[column].Trim() : "";

            var project = Cell(projectIndex);
            var series = SeriesForProject(project);
            if (series.Length == 0) continue;

            var path = Cell(pathIndex).Replace('\\', '/');
            if (path.Length == 0) continue;
            var fileName = path[(path.LastIndexOf('/') + 1)..];
            if (fileName.Length == 0) continue;

            _ = int.TryParse(Cell(linesIndex), out var lines);
            entries.Add(new GamedataCatalogEntry
            {
                Project = project,
                Series = series,
                Chapter = Cell(chapterIndex),
                Group = Cell(groupIndex),
                Path = path,
                FileName = fileName,
                Extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant(),
                Type = Cell(typeIndex),
                SizeLabel = Cell(sizeIndex),
                Lines = lines,
                Description = Cell(descriptionIndex),
                KeyElements = Cell(elementsIndex),
                AnalogPath = Cell(analogIndex),
                Md5 = Cell(md5Index)
            });
        }

        return entries.Count == 0 ? Empty : new GamedataCatalog(entries);
    }

    /// <summary>Читает каталог из файла на диске (`catalog.csv`).</summary>
    public static GamedataCatalog LoadFromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Empty;
        return LoadFromCsv(File.ReadAllText(path, Encoding.UTF8));
    }

    private static GamedataCatalog LoadEmbedded()
    {
        try
        {
            using var stream = typeof(GamedataCatalog).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) return Empty;
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return LoadFromCsv(reader.ReadToEnd());
        }
        catch (IOException)
        {
            return Empty;
        }
    }

    private static int IndexOf(IReadOnlyList<string> header, string name)
    {
        for (var index = 0; index < header.Count; index++)
            if (string.Equals(header[index], name, StringComparison.Ordinal)) return index;
        return -1;
    }

    /// <summary>Поиск колонки по началу заголовка (`Аналог` → `Аналог в другом проекте`).</summary>
    private static int IndexOfPrefix(IReadOnlyList<string> header, string prefix)
    {
        for (var index = 0; index < header.Count; index++)
            if (header[index].StartsWith(prefix, StringComparison.Ordinal)) return index;
        return -1;
    }

    private static string SeriesForProject(string project) =>
        project.Trim().ToLowerInvariant() switch
        {
            "cop" => "cop",
            "soc" => "soc",
            _ => ""
        };


    /// <summary>
    /// Разбор CSV с точкой с запятой и кавычками (описания могут содержать `;` и переводы строк).
    /// </summary>
    private static List<List<string>> ParseRows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var ch = csv[index];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    quoted = true;
                    break;
                case ';':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
