using System.Buffers.Binary;
using System.Text.RegularExpressions;
using XRayForge.Core.Text;

namespace XRayForge.Core.Spawn;

public sealed class SpawnHeader
{
    public uint Version { get; set; }
    public uint SpawnCount { get; set; }
    public uint LevelCount { get; set; }
}

public sealed class SpawnString
{
    public int Offset { get; set; }
    public string Text { get; set; } = "";
}

public sealed class SpawnCString
{
    public string Text { get; set; } = "";
    public int Next { get; set; }
}

public sealed class SpawnAnalysis
{
    public SpawnHeader? Header { get; set; }
    public List<SpawnString> Strings { get; set; } = new();
    public List<string> Sections { get; set; } = new();
}

/// <summary>
/// Разбор бинарного all.spawn (#10): заголовок + извлечение читаемых строк
/// (секции спавна, имена, уровни). Полный структурный парсинг всех CSE_*
/// (SPAWN_VERSION=128, десятки классов) — вне объёма, здесь надёжная основа.
/// </summary>
public static class SpawnAnalyzer
{
    /// <summary>Заголовок спавна: version / spawnCount / levelCount (uint32 little-endian).</summary>
    public static SpawnHeader? ParseSpawnHeader(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 12) return null;
        return new SpawnHeader
        {
            Version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)),
            SpawnCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)),
            LevelCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4))
        };
    }

    /// <summary>Прочитать строку, завершённую нулём (windows-1251): текст и следующий индекс.</summary>
    public static SpawnCString ReadCString(byte[] bytes, int? offset)
    {
        if (offset == null || offset.Value >= bytes.Length)
            return new SpawnCString { Text = "", Next = bytes.Length };

        var end = offset.Value;
        while (end < bytes.Length && bytes[end] != 0) end++;
        return new SpawnCString
        {
            Text = GameEncoding.DecodeCp1251(bytes, offset.Value, end - offset.Value),
            Next = end + 1
        };
    }

    /// <summary>Извлечь все печатные ASCII-строки (длина ≥ minLen) с их смещениями.</summary>
    public static List<SpawnString> ExtractSpawnStrings(byte[] bytes, int minLen = 3)
    {
        var result = new List<SpawnString>();
        var start = -1;
        for (var i = 0; i <= bytes.Length; i++)
        {
            var b = i < bytes.Length ? bytes[i] : (byte)0;
            var printable = b >= 32 && b < 127;
            if (printable && start == -1)
            {
                start = i;
            }
            else if (!printable && start != -1)
            {
                if (i - start >= minLen)
                    result.Add(new SpawnString
                    {
                        Offset = start,
                        Text = GameEncoding.DecodeCp1251(bytes, start, i - start)
                    });
                start = -1;
            }
        }

        return result;
    }

    /// <summary>Эвристика: вероятные секции спавна — короткие строки из [a-z0-9_].</summary>
    public static SpawnAnalysis AnalyzeSpawn(byte[] bytes)
    {
        var header = ParseSpawnHeader(bytes);
        var strings = ExtractSpawnStrings(bytes, 3);
        var sections = new List<string>();
        var seen = new HashSet<string>();
        foreach (var s in strings)
        {
            if (s.Text.Length >= 3 && s.Text.Length <= 40
                && Regex.IsMatch(s.Text, "^[a-z0-9_]+$") && seen.Add(s.Text))
                sections.Add(s.Text);
        }

        return new SpawnAnalysis { Header = header, Strings = strings, Sections = sections };
    }
}