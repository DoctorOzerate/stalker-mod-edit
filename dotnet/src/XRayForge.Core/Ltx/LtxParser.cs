using System.Text.RegularExpressions;

namespace XRayForge.Core.Ltx;

public sealed class LtxEntry
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public int Line { get; set; }
}

public sealed class LtxSection
{
    public string Name { get; set; } = "";
    public List<string> Parents { get; set; } = new();
    public int StartLine { get; set; }
    public List<LtxEntry> Entries { get; set; } = new();
    public List<string> RawLines { get; set; } = new();
}

public sealed class LtxInclude
{
    public string Path { get; set; } = "";
    public int Line { get; set; }
    public string Raw { get; set; } = "";
}

public sealed class LtxDocument
{
    public List<string> Lines { get; set; } = new();
    public List<LtxSection> Sections { get; set; } = new();
    public List<LtxInclude> Includes { get; set; } = new();
}

/// <summary>Разбор LTX-текста: секции с наследованием, пары «ключ = значение» и #include.</summary>
public static class LtxParser
{
    public static LtxDocument Parse(string? text)
    {
        var lines = SplitLines(text ?? string.Empty);
        var sections = new List<LtxSection>();
        var includes = new List<LtxInclude>();
        LtxSection? current = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var lineNo = i + 1;
            var code = raw.Trim().Split(';')[0].Trim();

            if (code.Length == 0)
            {
                current?.RawLines.Add(raw);
                continue;
            }

            if (code.StartsWith("#include", StringComparison.OrdinalIgnoreCase) && code.Contains('"'))
            {
                var a = code.IndexOf('"');
                var b = code.LastIndexOf('"');
                if (a != b)
                    includes.Add(new LtxInclude { Path = code.Substring(a + 1, b - a - 1), Line = lineNo, Raw = raw });
                continue;
            }

            var m = Regex.Match(code, @"^\[([^\]]+)\]\s*(?::\s*(.*))?$");
            if (m.Success)
            {
                current = new LtxSection
                {
                    Name = m.Groups[1].Value.Trim(),
                    Parents = m.Groups[2].Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList(),
                    StartLine = lineNo,
                    RawLines = { raw }
                };
                sections.Add(current);
                continue;
            }

            var eq = code.IndexOf('=');
            if (eq > 0)
            {
                var key = code.Substring(0, eq).Trim();
                var value = code.Substring(eq + 1).Trim();
                if (key.Length > 0 && !key.Contains(' '))
                {
                    var entry = new LtxEntry { Key = key, Value = value, Line = lineNo };
                    if (current != null)
                    {
                        current.Entries.Add(entry);
                        current.RawLines.Add(raw);
                    }
                    else
                    {
                        var orphan = sections.Find(s => s.Name.Length == 0);
                        if (orphan == null)
                        {
                            orphan = new LtxSection { Name = "", StartLine = 0 };
                            sections.Insert(0, orphan);
                        }
                        orphan.Entries.Add(entry);
                    }
                    continue;
                }
            }

            current?.RawLines.Add(raw);
        }

        return new LtxDocument { Lines = lines, Sections = sections, Includes = includes };
    }

    private static List<string> SplitLines(string text) =>
        text.Split('\n').Select(l => l.Replace("\r", "")).ToList();
}
