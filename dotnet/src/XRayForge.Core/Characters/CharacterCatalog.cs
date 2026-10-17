using System.Text.Json;
using XRayForge.Core.Graph;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;

namespace XRayForge.Core.Characters;

/// <summary>
/// Группа списка персонажей (`groups[g]` в `CharactersTab.js`): community-подпись, иконка
/// группировки (`COMMUNITY_ICONS`) и вошедшие персонажи.
/// </summary>
public sealed class CharacterGroup
{
    public string Community { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<Character> Items { get; set; } = new();
}

/// <summary>
/// Строка панели «Изменения от оригинала»: ключ локализации подписи поля и значения
/// до/после правки (`r.a`/`r.b` в `CharactersTab.js`).
/// </summary>
public sealed record CharacterDiff(string LabelKey, string Before, string After);

/// <summary>
/// Порт логики вкладки «Персонажи» (`CharactersTab.js`) и обработчиков `App.js`
/// (`autofillCharDialogs`, `extractCharacterXml`): поиск и группировка списка, дубликаты id,
/// связанные диалоги, diff с `original`, исходный XML и автосвязь `start_dialog`.
/// Функции чистые (кроме `AutofillDialogs`, который по образцу JS правит переданных
/// персонажей), поэтому проверяются без UI; VM держит только коллекции и выбранного персонажа.
/// </summary>
public static class CharacterCatalog
{
    /// <summary>`c.community || 'Без группы'` — подпись группы персонажей без community.</summary>
    public const string NoCommunity = "Без группы";

    /// <summary>`COMMUNITY_ICONS[c] || '👤'` — иконка группы без известной иконки.</summary>
    public const string DefaultIcon = "\U0001F464";

    /// <summary>Признак персонажа, созданного во вкладке (`c.source` в JS не ставится).</summary>
    public const string EditorSource = "editor";

    /// <summary>
    /// Поля панели diff в порядке `CharactersTab.js`: имя поля в `original` и ключ локализации
    /// подписи (`ID`, `Имя`, `Группа`, `Ранг`, `Био`, `Иконка`, `Визуал`, `Старт. диалог`,
    /// `Диалог актёра`).
    /// </summary>
    public static readonly IReadOnlyList<(string Field, string LabelKey)> DiffFields = new[]
    {
        ("id", "charDiffId"),
        ("name", "charDiffName"),
        ("community", "charDiffCommunity"),
        ("rank", "charDiffRank"),
        ("bio", "charDiffBio"),
        ("icon", "charDiffIcon"),
        ("visual", "charDiffVisual"),
        ("start_dialog", "charDiffStartDialog"),
        ("actor_dialog", "charDiffActorDialog")
    };

    /// <summary>
    /// Персонаж кнопки «Создать персонажа»: те же поля, что и в JS (`id`, `name`, `community`,
    /// `rank`, `bio`, `icon`, `visual`, `start_dialog`); имя приходит из локализации.
    /// </summary>
    public static Character Create(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Community = "stalker",
        Rank = "0",
        Bio = "",
        Icon = "",
        Visual = "",
        Source = EditorSource
    };

    /// <summary>`'char_' + Date.now().toString(36)` — id нового персонажа.</summary>
    public static string NewId() => "char_" + ToBase36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>Подпись персонажа в списке (`c.name || c.id`).</summary>
    public static string DisplayName(Character? character) =>
        character == null ? "" : !string.IsNullOrWhiteSpace(character.Name) ? character.Name : character.Id;

    /// <summary>`COMMUNITY_ICONS[g.toLowerCase()] || '👤'`.</summary>
    public static string CommunityIcon(string? community) =>
        ItemCatalog.CommunityIcons.TryGetValue((community ?? "").ToLowerInvariant(), out var icon)
            ? icon
            : DefaultIcon;

    /// <summary>
    /// Строка поиска персонажа: `[name, id, bio, visual, community, start_dialog, actor_dialog]`
    /// склеиваются через пробел (массивы — через запятую, как `String(array)` в JS).
    /// </summary>
    public static string SearchText(Character? character) => character == null
        ? ""
        : string.Join(" ", new[]
        {
            character.Name, character.Id, character.Bio, character.Visual, character.Community,
            string.Join(",", character.StartDialog ?? new List<string>()),
            string.Join(",", character.ActorDialog ?? new List<string>())
        }).ToLowerInvariant();

    /// <summary>`characters.filter(...)`: поиск по всем полям карточки, регистр не важен.</summary>
    public static List<Character> Filter(IEnumerable<Character>? characters, string? query)
    {
        var list = (characters ?? Enumerable.Empty<Character>()).Where(character => character != null).ToList();
        var needle = (query ?? "").ToLowerInvariant();
        if (needle.Length == 0) return list;
        return list.Where(character => SearchText(character).Contains(needle, StringComparison.Ordinal)).ToList();
    }

    /// <summary>`dup[id] > 1`: id персонажей, встречающихся в проекте больше одного раза.</summary>
    public static HashSet<string> DuplicateIds(IEnumerable<Character>? characters)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var character in characters ?? Enumerable.Empty<Character>())
        {
            if (character == null) continue;
            var id = character.Id ?? "";
            counts.TryGetValue(id, out var count);
            counts[id] = count + 1;
        }

        return counts.Where(pair => pair.Value > 1)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// `groups[g].map(...)` + `Object.keys(groups).sort()`: персонажи раскладываются по community
    /// (пустая — «Без группы»), группы сортируются по алфавиту, при `sortByName` сортируются и
    /// карточки внутри группы (`name || id`).
    /// </summary>
    public static List<CharacterGroup> Group(IEnumerable<Character>? characters, string? query, bool sortByName)
    {
        var buckets = new Dictionary<string, CharacterGroup>(StringComparer.Ordinal);
        foreach (var character in Filter(characters, query))
        {
            var community = string.IsNullOrEmpty(character.Community) ? NoCommunity : character.Community;
            if (!buckets.TryGetValue(community, out var bucket))
            {
                bucket = new CharacterGroup { Community = community, Icon = CommunityIcon(community) };
                buckets[community] = bucket;
            }

            bucket.Items.Add(character);
        }

        if (sortByName)
            foreach (var bucket in buckets.Values)
                bucket.Items.Sort((left, right) =>
                    string.Compare(DisplayName(left), DisplayName(right), StringComparison.CurrentCulture));

        return buckets.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => buckets[key])
            .ToList();
    }

    /// <summary>
    /// `related` панели «Диалоги персонажа»: диалоги, чей `id` или `name` встречается в
    /// `start_dialog`/`actor_dialog` выбранного персонажа.
    /// </summary>
    public static List<DialogueGraph> Related(IEnumerable<DialogueGraph>? dialogues, Character? character)
    {
        var result = new List<DialogueGraph>();
        var refs = new List<string>();
        if (character != null)
        {
            refs.AddRange(character.StartDialog ?? new List<string>());
            refs.AddRange(character.ActorDialog ?? new List<string>());
        }

        if (refs.Count == 0) return result;

        foreach (var dialogue in dialogues ?? Enumerable.Empty<DialogueGraph>())
        {
            if (dialogue == null) continue;
            if (refs.Contains(dialogue.Id ?? "", StringComparer.Ordinal) ||
                refs.Contains(dialogue.Name ?? "", StringComparer.Ordinal))
                result.Add(dialogue);
        }

        return result;
    }

    /// <summary>
    /// `c.original` + `fields.map(...).filter(r.a !== r.b)`: только поля, отличающиеся от снимка
    /// `original`. Значения форматируются как `String(...)` в JS (массивы — через запятую).
    /// </summary>
    public static List<CharacterDiff> Diff(Character? character)
    {
        var rows = new List<CharacterDiff>();
        if (character?.Original is not { ValueKind: JsonValueKind.Object } original) return rows;

        foreach (var (field, labelKey) in DiffFields)
        {
            var before = Format(original.TryGetProperty(field, out var value) ? value : (JsonElement?)null);
            var after = CurrentValue(character, field);
            if (!string.Equals(before, after, StringComparison.Ordinal))
                rows.Add(new CharacterDiff(labelKey, before, after));
        }

        return rows;
    }

    /// <summary>
    /// `extractCharacterXml(id, ltxFiles)`: блок `specific_character` с `id="…"` из первого файла,
    /// где он найден; без границ блоков — весь текст файла (как в JS).
    /// </summary>
    public static string? ExtractXml(string? id, IReadOnlyDictionary<string, string>? ltxFiles)
    {
        if (string.IsNullOrEmpty(id) || ltxFiles == null) return null;

        const string close = "</specific_character>";
        foreach (var file in ltxFiles)
        {
            var text = file.Value ?? "";
            var index = text.IndexOf("id=\"" + id + "\"", StringComparison.Ordinal);
            if (index < 0) index = text.IndexOf("id='" + id + "'", StringComparison.Ordinal);
            if (index < 0) continue;

            var start = text.LastIndexOf("<specific_character", index, StringComparison.Ordinal);
            var end = text.IndexOf(close, index, StringComparison.Ordinal);
            return start != -1 && end != -1
                ? text.Substring(start, end + close.Length - start)
                : text;
        }

        return null;
    }

    /// <summary>
    /// `autofillCharDialogs`: персонажам без `start_dialog` ищется диалог, чей `id` содержит id
    /// персонажа (или наоборот) — id диалога становится стартовым. Возвращает изменённых.
    /// </summary>
    public static List<Character> AutofillDialogs(
        IEnumerable<Character>? characters,
        IEnumerable<DialogueGraph>? dialogues)
    {
        var changed = new List<Character>();
        var list = (dialogues ?? Enumerable.Empty<DialogueGraph>()).Where(dialogue => dialogue != null).ToList();

        foreach (var character in characters ?? Enumerable.Empty<Character>())
        {
            if (character == null || character.StartDialog is { Count: > 0 }) continue;

            var id = character.Id ?? "";
            var related = list.FirstOrDefault(dialogue =>
                (dialogue.Id ?? "").Contains(id, StringComparison.Ordinal) ||
                id.Contains(dialogue.Id ?? "", StringComparison.Ordinal));
            if (related == null) continue;

            character.StartDialog = new List<string> { related.Id };
            changed.Add(character);
        }

        return changed;
    }

    /// <summary>Строковое поле персонажа вне модели (`npc_rank` в `character.Extra`).</summary>
    public static string ExtraString(Character? character, string key)
    {
        if (character?.Extra == null || !character.Extra.TryGetValue(key, out var value)) return "";
        return Format(value);
    }

    /// <summary>Записать строковое поле вне модели; пустое значение убирает ключ, если его не было.</summary>
    public static void SetExtraString(Character? character, string key, string? value)
    {
        if (character == null) return;

        var text = value ?? "";
        var exists = character.Extra != null && character.Extra.ContainsKey(key);
        if (!exists && text.Length == 0) return;

        character.Extra ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        character.Extra[key] = JsonSerializer.SerializeToElement(text);
    }

    /// <summary>`String(c[field] || '')` текущего значения поля редактора.</summary>
    private static string CurrentValue(Character character, string field) => field switch
    {
        "id" => character.Id ?? "",
        "name" => character.Name ?? "",
        "community" => character.Community ?? "",
        "rank" => character.Rank ?? "",
        "bio" => character.Bio ?? "",
        "icon" => character.Icon ?? "",
        "visual" => character.Visual ?? "",
        "start_dialog" => string.Join(",", character.StartDialog ?? new List<string>()),
        "actor_dialog" => string.Join(",", character.ActorDialog ?? new List<string>()),
        _ => ""
    };

    /// <summary>Значение JSON-поля как строка (`String(c.original[f] || '')` в JS).</summary>
    private static string Format(JsonElement? element)
    {
        if (element is not { } value) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Array => string.Join(",", value.EnumerateArray().Select(item => Format(item))),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }

    private static string Format(JsonElement value) => Format((JsonElement?)value);

    /// <summary>`value.toString(36)` — нижний регистр, цифры и латиница.</summary>
    private static string ToBase36(long value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value <= 0) return "0";

        var builder = new System.Text.StringBuilder();
        while (value > 0)
        {
            builder.Insert(0, digits[(int)(value % 36)]);
            value /= 36;
        }

        return builder.ToString();
    }
}
