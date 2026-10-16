using XRayForge.Core.Models;

namespace XRayForge.Core.Graph;

/// <summary>Элемент палитры узлов: тип и его отображение (`Object.entries(NODE_TYPES)` в graph.js).</summary>
public sealed class PaletteItem
{
    public string Type { get; set; } = "";
    public NodeTypeInfo Info { get; set; } = NodeTypes.FallbackNode;
}

/// <summary>Сниппет метаданных реплики (`META_SNIPPETS` в DialoguesTab.js).</summary>
public sealed class MetaSnippet
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// Палитра узлов и метаданные реплики (порт `filteredTypes`, `META_SNIPPETS` и `patchNode`
/// из `DialoguesTab.js`/`App.js`): порядок и фильтр типов, сниппеты «Действий и условий»
/// и перевод списка значений метаданных в текст многострочного поля и обратно.
/// </summary>
public static class GraphPalette
{
    /// <summary>Порядок типов в палитре — как объявлен `NODE_TYPES` в graph.js.</summary>
    public static readonly IReadOnlyList<string> Order = new List<string>
    {
        NodeTypes.Start, NodeTypes.Phrase, NodeTypes.Condition, NodeTypes.QuestCheck,
        NodeTypes.ItemCheck, NodeTypes.Random, NodeTypes.Action, NodeTypes.End
    };

    /// <summary>`META_SNIPPETS`: подпись кнопки и значение, которое она добавляет в метаданные.</summary>
    public static readonly IReadOnlyList<MetaSnippet> Snippets = new List<MetaSnippet>
    {
        new() { Key = "has_info", Label = "+info", Value = "%+quest_done%" },
        new() { Key = "dont_has_info", Label = "-info", Value = "%+quest_done%" },
        new() { Key = "give_info", Label = "выдать info", Value = "quest_given" },
        new() { Key = "disable_info", Label = "снять info", Value = "quest_given" },
        new() { Key = "precondition", Label = "has_info()", Value = "has_info(\"%+quest_done%\")" },
        new() { Key = "action", Label = "give_item", Value = "give_item:medkit" }
    };

    /// <summary>
    /// `filteredTypes`: типы палитры в порядке объявления без служебных (`PALETTE_HIDDEN`),
    /// у которых ключ, подпись или описание содержат подстроку фильтра (регистр не важен).
    /// </summary>
    public static List<PaletteItem> Items(string? filter)
    {
        var query = (filter ?? string.Empty).ToLowerInvariant();
        var result = new List<PaletteItem>();
        foreach (var type in Order)
        {
            if (NodeTypes.PaletteHidden.Contains(type)) continue;

            var info = NodeTypes.InfoFor(type);
            var haystack = (type + info.Label + info.Desc).ToLowerInvariant();
            if (!haystack.Contains(query, StringComparison.Ordinal)) continue;

            result.Add(new PaletteItem { Type = type, Info = info });
        }

        return result;
    }

    /// <summary>
    /// `m[key] = (m[key] || []).concat([value])` — метаданные с добавленным сниппетом
    /// (значение дописывается в конец, остальные ключи, включая нестандартные, сохраняются).
    /// </summary>
    public static Dictionary<string, List<string>> WithSnippet(
        IDictionary<string, List<string>>? metadata, MetaSnippet? snippet)
    {
        var result = Clone(metadata);
        if (snippet == null || snippet.Key.Length == 0) return result;

        if (!result.TryGetValue(snippet.Key, out var values))
        {
            values = new List<string>();
            result[snippet.Key] = values;
        }

        values.Add(snippet.Value);
        return result;
    }

    /// <summary>`(metadata[key] || []).join('\n')` — содержимое поля метаданных.</summary>
    public static string Text(IDictionary<string, List<string>>? metadata, string? key)
    {
        if (key == null || metadata == null || !metadata.TryGetValue(key, out var values) || values == null) return "";
        return string.Join("\n", values);
    }

    /// <summary>`e.target.value.split(/\r?\n/)` — строки метаданных из текста поля.</summary>
    public static List<string> Lines(string? text) =>
        System.Text.RegularExpressions.Regex.Split(text ?? string.Empty, "\r?\n").ToList();

    /// <summary>`patchNode(id, { metadata })`: метаданные с заменённым текстом одного ключа.</summary>
    public static Dictionary<string, List<string>> WithText(
        IDictionary<string, List<string>>? metadata, string? key, string? text)
    {
        var result = Clone(metadata);
        if (key == null || key.Length == 0) return result;

        result[key] = Lines(text);
        return result;
    }

    /// <summary>Копия словаря метаданных со всеми ключами (`Object.assign({}, metadata)`).</summary>
    public static Dictionary<string, List<string>> Clone(IDictionary<string, List<string>>? metadata)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var (key, values) in metadata ?? new Dictionary<string, List<string>>())
            result[key] = values == null ? new List<string>() : new List<string>(values);
        return result;
    }
}
