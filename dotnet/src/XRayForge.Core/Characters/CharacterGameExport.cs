using System.Text.Json;
using System.Text.RegularExpressions;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;

namespace XRayForge.Core.Characters;

/// <summary>
/// Экспорт персонажей в игровой XML (остаток P0): персонажи проекта пишутся в
/// <c>gamedata/&lt;config|configs&gt;/gameplay/character_desc_&lt;id&gt;.xml</c> — рядом с
/// оригинальными <c>character_desc_*.xml</c>. Формат повторяет файлы игры (SoC/CoP):
/// корень <c>&lt;xml&gt;</c>, блок <c>specific_character</c> с полями в порядке оригинала.
/// Незнакомые поля модели (<see cref="Character.Extra"/>) сохраняются элементами с теми же
/// именами, а <c>supplies</c> пишется без экранирования — внутри живут <c>#include</c> и
/// <c>[spawn]</c>, которые обрабатывает препроцессор игры.
/// </summary>
public static class CharacterGameExport
{
    /// <summary>Имя файла, если id диалога пустой (или в нём нет латиницы).</summary>
    public const string DefaultFile = "character_desc_mod.xml";

    /// <summary>Префикс имени игрового файла персонажей.</summary>
    public const string FilePrefix = "character_desc_";

    /// <summary>Каталог файлов персонажей внутри `config`/`configs` (как в игре).</summary>
    public const string GameDirectory = "gameplay";

    /// <summary>Поля модели персонажа: в XML они раскладываются по элементам явно.</summary>
    private static readonly string[] ModelFields =
    {
        "id", "name", "community", "rank", "reputation", "class", "terrain_sect", "snd_config",
        "map_icon", "bio", "icon", "visual", "supplies", "start_dialog", "actor_dialog",
        "source", "original"
    };

    /// <summary>
    /// Карта «путь в gamedata → текст» с файлом персонажей. Пустой список персонажей —
    /// пустая карта: файл не создаётся, иначе мод перезаписал бы всех NPC.
    /// </summary>
    /// <param name="project">Проект редактора.</param>
    /// <param name="fileName">Необязательное имя файла (иначе <see cref="FileName"/> по id диалога).</param>
    public static Dictionary<string, string> GameFiles(Project? project, string? fileName = null)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (project == null || project.Characters.Count == 0) return files;

        var name = string.IsNullOrWhiteSpace(fileName) ? FileName(project.DialogueId) : Path.GetFileName(fileName);
        var path = Paths.Normalize($"gamedata/{GameSeriesCatalog.ConfigDirectory(project.Series)}/{GameDirectory}/{name}");
        if (path.Length == 0) return files;

        files[path] = CharactersToXml(project.Characters);
        return files;
    }

    /// <summary>
    /// Имя файла персонажей по id диалога: `bar_quest` → `character_desc_bar_quest.xml`.
    /// Кириллица и прочие символы схлопываются в `_`, пустой результат — <see cref="DefaultFile"/>.
    /// </summary>
    public static string FileName(string? id)
    {
        var slug = Regex.Replace((id ?? string.Empty).ToLowerInvariant(), "[^a-z0-9_]+", "_").Trim('_');
        if (slug.StartsWith(FilePrefix, StringComparison.Ordinal)) slug = slug.Substring(FilePrefix.Length);
        return slug.Length == 0 ? DefaultFile : FilePrefix + slug + ".xml";
    }

    /// <summary>
    /// XML с блоками <c>specific_character</c> в корне <c>&lt;xml&gt;</c>. Повторяющиеся id
    /// пишутся один раз (движок не терпит дублей), поля — в порядке оригинальных файлов.
    /// </summary>
    public static string CharactersToXml(IEnumerable<Character>? characters)
    {
        var lines = new List<string>
        {
            "<?xml version=\"1.0\" encoding=\"windows-1251\"?>",
            "<xml>"
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in characters ?? Enumerable.Empty<Character>())
        {
            if (character == null || string.IsNullOrWhiteSpace(character.Id)) continue;
            if (!seen.Add(character.Id)) continue;

            lines.Add("\t<specific_character id=\"" + XmlEscaping.EscapeAttribute(character.Id) + "\">");
            AddText(lines, "name", character.Name);
            AddText(lines, "icon", character.Icon);
            if (character.MapIcon != null)
            {
                lines.Add("\t\t<map_icon x=\"" + XmlEscaping.EscapeAttribute(character.MapIcon.X) +
                          "\" y=\"" + XmlEscaping.EscapeAttribute(character.MapIcon.Y) + "\"></map_icon>");
            }

            AddText(lines, "bio", character.Bio);
            AddText(lines, "class", character.Class);
            AddText(lines, "community", character.Community);
            AddText(lines, "terrain_sect", character.TerrainSect);
            AddText(lines, "snd_config", character.SndConfig);
            AddText(lines, "rank", character.Rank);
            AddText(lines, "reputation", character.Reputation);
            AddText(lines, "visual", character.Visual);
            AddSupplies(lines, character.Supplies);
            AddTexts(lines, "start_dialog", character.StartDialog);
            AddTexts(lines, "actor_dialog", character.ActorDialog);
            AddExtra(lines, character);

            lines.Add("\t</specific_character>");
        }

        lines.Add("</xml>");
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Элемент с экранированным текстом; пустое значение не пишется.</summary>
    private static void AddText(List<string> lines, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        lines.Add("\t\t<" + name + ">" + XmlEscaping.EscapeText(value) + "</" + name + ">");
    }

    /// <summary>Повторяющиеся элементы (`start_dialog`/`actor_dialog`).</summary>
    private static void AddTexts(List<string> lines, string name, IEnumerable<string>? values)
    {
        foreach (var value in values ?? Enumerable.Empty<string>()) AddText(lines, name, value);
    }

    /// <summary>
    /// Блок <c>supplies</c> как есть: внутри строки `[spawn] \n …` и `#include`-директивы,
    /// которые нельзя экранировать — их читает препроцессор XML до разбора.
    /// </summary>
    private static void AddSupplies(List<string> lines, string? supplies)
    {
        if (string.IsNullOrWhiteSpace(supplies)) return;

        lines.Add("\t\t<supplies>");
        foreach (var line in supplies.Replace("\r\n", "\n").Trim('\n').Split('\n'))
            lines.Add("\t\t\t" + line.TrimEnd('\r'));
        lines.Add("\t\t</supplies>");
    }

    /// <summary>
    /// Поля вне модели (`npc_rank` и пр.): скаляры и массивы скаляров пишутся элементами с тем
    /// же именем, объекты пропускаются — в XML у них нет прямого аналога.
    /// </summary>
    private static void AddExtra(List<string> lines, Character character)
    {
        if (character.Extra == null) return;

        foreach (var pair in character.Extra)
        {
            if (Array.IndexOf(ModelFields, pair.Key) >= 0) continue;

            if (pair.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pair.Value.EnumerateArray())
                    if (Scalar(item) is { } text) AddText(lines, pair.Key, text);
                continue;
            }

            if (Scalar(pair.Value) is { } value) AddText(lines, pair.Key, value);
        }
    }

    /// <summary>Скалярное значение JSON как строка (`null` для объектов, массивов и `null`).</summary>
    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };
}
