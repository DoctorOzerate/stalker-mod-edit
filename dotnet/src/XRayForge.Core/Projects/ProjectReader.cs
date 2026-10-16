using System.Text.Json;
using XRayForge.Core.Graph;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Quests;

namespace XRayForge.Core.Projects;

/// <summary>
/// Чтение полей JSON проекта с учётом «израсходованных» ключей: всё, что не описано
/// моделью, собирается в `Extra` (в JS такие поля просто живут в объекте проекта).
/// </summary>
internal sealed class ProjectReader
{
    private static readonly string[] ImportEncodings = { "auto", "utf-8", "windows-1251" };
    private static readonly string[] ExportEncodings = { "utf-8", "windows-1251" };

    private readonly JsonElement _raw;
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);

    public ProjectReader(JsonElement raw) => _raw = raw.ValueKind == JsonValueKind.Object ? raw : default;

    private bool Has(string name) => _raw.ValueKind == JsonValueKind.Object && _raw.TryGetProperty(name, out _);

    /// <summary>Скалярное поле строкой (числа и булевы — их текстом).</summary>
    public string? Str(string name)
    {
        if (!Has(name)) return null;
        _consumed.Add(name);
        return ProjectMigration.Scalar(_raw.GetProperty(name));
    }

    /// <summary>Как `Str`, но пустая строка и `null` считаются отсутствием (JS `||`).</summary>
    public string? Truthy(string name)
    {
        var value = Str(name);
        return ProjectMigration.IsTruthy(value) ? value : null;
    }

    public JsonElement? Element(string name)
    {
        if (!Has(name)) return null;
        _consumed.Add(name);
        return _raw.GetProperty(name).Clone();
    }

    private JsonElement? ArrayOf(string name)
    {
        if (!Has(name)) return null;
        _consumed.Add(name);
        var value = _raw.GetProperty(name);
        return value.ValueKind == JsonValueKind.Array ? value : null;
    }

    private JsonElement? ObjectOf(string name)
    {
        if (!Has(name)) return null;
        _consumed.Add(name);
        var value = _raw.GetProperty(name);
        return value.ValueKind == JsonValueKind.Object ? value : null;
    }

    public T? Typed<T>(string name) where T : class
    {
        var element = ObjectOf(name);
        if (element == null) return null;
        var result = ProjectMigration.TryDeserialize<T>(element.Value);
        if (result == null) _consumed.Remove(name); // не разобрали — значение останется в Extra
        return result;
    }

    public List<T>? TypedList<T>(string name) where T : class
    {
        var element = ArrayOf(name);
        if (element == null) return null;
        var list = new List<T>();
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var value = ProjectMigration.TryDeserialize<T>(item);
            if (value != null) list.Add(value);
        }
        return list;
    }

    public Dictionary<string, T>? TypedMap<T>(string name) where T : class
    {
        var element = ObjectOf(name);
        if (element == null) return null;
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var prop in element.Value.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            var value = ProjectMigration.TryDeserialize<T>(prop.Value);
            if (value != null) map[prop.Name] = value;
        }
        return map;
    }

    /// <summary>Список объектов (JS `filter(x =&gt; x &amp;&amp; typeof x === 'object')`) без нормализации.</summary>
    public List<T>? Objects<T>(string name) where T : class => TypedList<T>(name);

    public Dictionary<string, string>? StringMap(string name)
    {
        var element = ObjectOf(name);
        if (element == null) return null;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in element.Value.EnumerateObject()) map[prop.Name] = ProjectMigration.Scalar(prop.Value) ?? "";
        return map;
    }

    /// <summary>JS `arr.map(String)`: скаляры приводим к строке, прочее пропускаем.</summary>
    public List<string> StringList(string name)
    {
        var element = ArrayOf(name);
        var list = new List<string>();
        if (element == null) return list;
        foreach (var item in element.Value.EnumerateArray())
        {
            var s = ProjectMigration.Scalar(item);
            if (s != null) list.Add(s);
        }
        return list;
    }

    /// <summary>JS `arr.map(String)` для конкретного значения (например `quest.requires`).</summary>
    private static List<string> StringList(JsonElement obj, string name)
    {
        var list = new List<string>();
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var item in array.EnumerateArray())
        {
            var s = ProjectMigration.Scalar(item);
            if (s != null) list.Add(s);
        }
        return list;
    }

    /// <summary>`raw.nodes.map(safeNode)` — каждый элемент через `GraphData.SafeNode`.</summary>
    public List<DialogueNode> Nodes()
    {
        var element = ArrayOf("nodes");
        var list = new List<DialogueNode>();
        if (element == null) return list;
        var index = 0;
        foreach (var item in element.Value.EnumerateArray())
        {
            var node = item.ValueKind == JsonValueKind.Object ? ProjectMigration.TryDeserialize<DialogueNode>(item) : null;
            list.Add(GraphData.SafeNode(node, index));
            index++;
        }
        return list;
    }

    /// <summary>`raw.edges.filter(object).map((e, i) =&gt; Object.assign({ id: 'edge_' + i }, e))`.</summary>
    public List<DialogueEdge> Edges()
    {
        var element = ArrayOf("edges");
        var list = new List<DialogueEdge>();
        if (element == null) return list;
        var index = 0;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var edge = ProjectMigration.TryDeserialize<DialogueEdge>(item) ?? new DialogueEdge
            {
                Id = ProjectMigration.ScalarField(item, "id") ?? "",
                Source = ProjectMigration.ScalarField(item, "source") ?? "",
                Target = ProjectMigration.ScalarField(item, "target") ?? "",
                Broken = ProjectMigration.BoolField(item, "broken"),
                OriginalNext = ProjectMigration.ScalarField(item, "originalNext")
            };
            if (string.IsNullOrEmpty(edge.Id)) edge.Id = "edge_" + index;
            list.Add(edge);
            index++;
        }
        return list;
    }

    /// <summary>`flattenDialogues(raw.dialogues)` с поддержкой старого вложенного вида.</summary>
    public List<DialogueGraph> Dialogues()
    {
        var element = ArrayOf("dialogues");
        var flat = new List<DialogueGraph>();
        if (element == null) return GraphData.FlattenDialogues(flat);

        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty("dialogues", out var nested) && nested.ValueKind == JsonValueKind.Array)
            {
                foreach (var inner in nested.EnumerateArray())
                {
                    if (inner.ValueKind != JsonValueKind.Object) continue;
                    var graph = ProjectMigration.TryDeserialize<DialogueGraph>(inner);
                    if (graph != null) flat.Add(graph);
                }
                continue;
            }

            var single = ProjectMigration.TryDeserialize<DialogueGraph>(item);
            if (single != null) flat.Add(single);
        }

        return GraphData.FlattenDialogues(flat);
    }

    /// <summary>`raw.languages.filter(object).map(...)` — locale/name/entries нормализуются.</summary>
    public List<Language> Languages()
    {
        var element = ArrayOf("languages");
        var list = new List<Language>();
        if (element == null) return list;
        var index = 0;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var language = ProjectMigration.TryDeserialize<Language>(item) ?? new Language();
            var locale = ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "locale"))
                         ?? ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "code"))
                         ?? "lang" + index;
            language.Locale = locale.ToLowerInvariant();
            language.Name = ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "name"))
                            ?? ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "locale"))
                            ?? "Language " + (index + 1);

            var entries = new List<LanguageEntry>();
            if (item.TryGetProperty("entries", out var rawEntries) && rawEntries.ValueKind == JsonValueKind.Array)
                foreach (var entry in rawEntries.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    entries.Add(new LanguageEntry
                    {
                        Id = ProjectMigration.ScalarField(entry, "id") ?? "",
                        Text = ProjectMigration.ScalarField(entry, "text") ?? ""
                    });
                }

            language.Entries = entries;
            list.Add(language);
            index++;
        }
        return list;
    }

    /// <summary>`raw.scripts.filter(object).map(...)` — name/path получают шаблонные значения.</summary>
    public List<ScriptFile> Scripts()
    {
        var element = ArrayOf("scripts");
        var list = new List<ScriptFile>();
        if (element == null) return list;
        var index = 0;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var script = ProjectMigration.TryDeserialize<ScriptFile>(item) ?? new ScriptFile();
            var fallback = "script_" + index + ".script";
            script.Name = ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "name")) ?? fallback;
            script.Path = ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "path"))
                          ?? ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "name")) ?? fallback;
            script.Text = ProjectMigration.ScalarField(item, "text") ?? "";
            script.Original = ProjectMigration.ScalarField(item, "original") ?? "";
            list.Add(script);
            index++;
        }
        return list;
    }

    /// <summary>`raw.quests.filter(object).map(...).filter(x =&gt; x.id)`.</summary>
    public List<Quest> Quests()
    {
        var element = ArrayOf("quests");
        var list = new List<Quest>();
        if (element == null) return list;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var quest = ProjectMigration.TryDeserialize<Quest>(item) ?? new Quest();
            quest.Id = ProjectMigration.ScalarField(item, "id") ?? "";
            if (quest.Id.Length == 0) continue; // `.filter(function (x) { return x.id; })`

            quest.Prior = ProjectMigration.ScalarField(item, "prior") ?? "";
            quest.Title = ProjectMigration.ScalarField(item, "title") ?? "";
            quest.Text = ProjectMigration.ScalarField(item, "text") ?? "";
            quest.Objectives = QuestCatalog.NormalizeObjectives(quest.Objectives);
            quest.InfoportionComplete = ProjectMigration.ScalarField(item, "infoportion_complete") ?? "";
            quest.InfoportionFail = ProjectMigration.ScalarField(item, "infoportion_fail") ?? "";
            quest.Icon = ProjectMigration.ScalarField(item, "icon") ?? "";
            quest.Location = ProjectMigration.ScalarField(item, "location") ?? "";
            quest.Article = ProjectMigration.ScalarField(item, "article") ?? "";
            quest.Category = ProjectMigration.ScalarField(item, "category") ?? "";
            quest.Requires = StringList(item, "requires");
            quest.Reward = ProjectMigration.ScalarField(item, "reward") ?? "";
            quest.Format = ProjectMigration.Truthy(ProjectMigration.ScalarField(item, "format")) ?? "game_tasks";
            quest.SourcePath = ProjectMigration.ScalarField(item, "sourcePath") ?? "";
            quest.Original = item.TryGetProperty("original", out var original) && original.ValueKind == JsonValueKind.Object
                ? original.Clone()
                : null;

            list.Add(quest);
        }
        return list;
    }

    /// <summary>`raw.atlas` + `Math.max(1, +value || default)`.</summary>
    public ProjectAtlas Atlas()
    {
        var atlas = new ProjectAtlas();
        var element = ObjectOf("atlas");
        if (element != null)
        {
            var parsed = ProjectMigration.TryDeserialize<ProjectAtlas>(element.Value);
            if (parsed != null) atlas = parsed;
            else
            {
                atlas.Columns = ProjectMigration.NumberField(element.Value, "columns") ?? atlas.Columns;
                atlas.CellSize = ProjectMigration.NumberField(element.Value, "cellSize") ?? atlas.CellSize;
                atlas.CellHeight = ProjectMigration.NumberField(element.Value, "cellHeight") ?? atlas.CellHeight;
            }
        }

        atlas.Columns = PositiveOr(atlas.Columns, 5);
        atlas.CellSize = PositiveOr(atlas.CellSize, 50);
        atlas.CellHeight = PositiveOr(atlas.CellHeight, 50);
        if (atlas.Extra is { Count: 0 }) atlas.Extra = null;
        return atlas;
    }

    private static double PositiveOr(double value, double fallback) =>
        Math.Max(1, double.IsFinite(value) && value != 0 ? value : fallback);

    /// <summary>`raw.settings` + проверка допустимых кодировок.</summary>
    public ProjectSettings Settings()
    {
        var settings = new ProjectSettings();
        var element = ObjectOf("settings");
        if (element != null)
        {
            var parsed = ProjectMigration.TryDeserialize<ProjectSettings>(element.Value);
            if (parsed != null) settings = parsed;
            else
            {
                settings.ImportEncoding = ProjectMigration.ScalarField(element.Value, "importEncoding") ?? settings.ImportEncoding;
                settings.ExportEncoding = ProjectMigration.ScalarField(element.Value, "exportEncoding") ?? settings.ExportEncoding;
            }

            settings.AutosaveInterval = ReadAutosave(element.Value, settings.AutosaveInterval ?? 60);
        }

        if (Array.IndexOf(ImportEncodings, settings.ImportEncoding) < 0) settings.ImportEncoding = "auto";
        if (Array.IndexOf(ExportEncodings, settings.ExportEncoding) < 0) settings.ExportEncoding = "windows-1251";
        settings.AutosaveInterval = settings.AutosaveInterval is { } autosave && double.IsFinite(autosave) ? autosave : 0;
        if (settings.Extra is { Count: 0 }) settings.Extra = null;
        return settings;
    }

    /// <summary>`+(raw.autosaveInterval != null ? raw.autosaveInterval : 60)`: null и мусор отключают автосейв.</summary>
    private static double? ReadAutosave(JsonElement obj, double fallback)
    {
        if (!obj.TryGetProperty("autosaveInterval", out var raw)) return fallback;
        if (raw.ValueKind == JsonValueKind.Null) return 0;
        var value = ProjectMigration.Number(raw);
        return value.HasValue && double.IsFinite(value.Value) ? value.Value : 0;
    }

    /// <summary>`pinCollections` (нет объекта — `{ default: pinnedIcons }`).</summary>
    public Dictionary<string, List<PinnedIcon>> PinCollections(List<PinnedIcon> pinned)
    {
        var element = ObjectOf("pinCollections");
        if (element == null)
            return new Dictionary<string, List<PinnedIcon>>(StringComparer.Ordinal) { ["default"] = pinned };

        var map = new Dictionary<string, List<PinnedIcon>>(StringComparer.Ordinal);
        foreach (var prop in element.Value.EnumerateObject())
        {
            var list = new List<PinnedIcon>();
            if (prop.Value.ValueKind == JsonValueKind.Array)
                foreach (var item in prop.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var icon = ProjectMigration.TryDeserialize<PinnedIcon>(item);
                    if (icon != null) list.Add(icon);
                }
            map[prop.Name] = list;
        }
        return map;
    }

    /// <summary>Остаток полей объекта — без потерь уходит в `Extra`.</summary>
    public Dictionary<string, JsonElement>? Remaining()
    {
        if (_raw.ValueKind != JsonValueKind.Object) return null;
        Dictionary<string, JsonElement>? extra = null;
        foreach (var prop in _raw.EnumerateObject())
        {
            if (_consumed.Contains(prop.Name)) continue;
            extra ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            extra[prop.Name] = prop.Value.Clone();
        }
        return extra;
    }

    /// <summary>`raw.characters.filter(object)` без нормализации; несовместимые значения переносятся в `Extra`.</summary>
    public List<Character> Characters()
    {
        var element = ArrayOf("characters");
        var list = new List<Character>();
        if (element == null) return list;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var character = ProjectMigration.TryDeserialize<Character>(item);
            if (character != null)
            {
                list.Add(character);
                continue;
            }

            list.Add(new Character
            {
                Id = ProjectMigration.ScalarField(item, "id") ?? "",
                Name = ProjectMigration.ScalarField(item, "name") ?? "",
                Extra = RawExtra(item, "id", "name")
            });
        }
        return list;
    }

    private static Dictionary<string, JsonElement>? RawExtra(JsonElement obj, params string[] consumed)
    {
        Dictionary<string, JsonElement>? extra = null;
        foreach (var prop in obj.EnumerateObject())
        {
            if (Array.IndexOf(consumed, prop.Name) >= 0) continue;
            extra ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            extra[prop.Name] = prop.Value.Clone();
        }
        return extra;
    }

    /// <summary>`migrateProject`: `nodes.filter(Boolean).map(маппинг NPC_REPLY/PLAYER_REPLY → PHRASE)`.</summary>
    public List<DialogueNode> LegacyNodes()
    {
        var element = ArrayOf("nodes");
        var list = new List<DialogueNode>();
        if (element == null) return list;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue; // filter(Boolean)
            var oldType = ProjectMigration.ScalarField(item, "type");
            var type = oldType is "NPC_REPLY" or "PLAYER_REPLY" ? NodeTypes.Phrase : oldType ?? "";
            var hasData = item.TryGetProperty("data", out var rawData) && rawData.ValueKind == JsonValueKind.Object;
            var data = hasData ? ProjectMigration.TryDeserialize<NodeData>(rawData) ?? new NodeData() : new NodeData();
            data.Extra?.Remove("speaker"); // delete d.speaker

            if (!HasProperty(hasData, rawData, "role"))
                data.Role = type == NodeTypes.Phrase
                    ? oldType switch
                    {
                        "NPC_REPLY" => "npc",
                        "PLAYER_REPLY" => "player",
                        _ => ProjectMigration.Truthy(data.Role) ?? "unknown"
                    }
                    : "unknown";
            if (!HasProperty(hasData, rawData, "roleSource")) data.RoleSource = "migration";
            if (!HasProperty(hasData, rawData, "phraseId")) data.PhraseId = ProjectMigration.ScalarField(item, "id") ?? "";

            var node = ProjectMigration.TryDeserialize<DialogueNode>(item) ?? new DialogueNode();
            node.Type = type;
            node.Data = data;
            list.Add(node);
        }
        return list;
    }

    private static bool HasProperty(bool exists, JsonElement obj, string name) =>
        exists && obj.TryGetProperty(name, out _);

    /// <summary>`migrateProject`: `languages.filter(object).map(x =&gt; ({ ...x, entries: … }))`.</summary>
    public List<Language> LegacyLanguages()
    {
        var element = ArrayOf("languages");
        var list = new List<Language>();
        if (element == null) return list;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var language = ProjectMigration.TryDeserialize<Language>(item) ?? new Language();
            var entries = new List<LanguageEntry>();
            if (item.TryGetProperty("entries", out var rawEntries) && rawEntries.ValueKind == JsonValueKind.Array)
                entries = ProjectMigration.TryDeserialize<List<LanguageEntry>>(rawEntries) ?? new List<LanguageEntry>();
            language.Entries = entries;
            list.Add(language);
        }
        return list;
    }

    /// <summary>`migrateProject`: настройки с дефолтными кодировками, но без проверки допустимых.</summary>
    public ProjectSettings LegacySettings()
    {
        var settings = new ProjectSettings();
        var element = ObjectOf("settings");
        if (element == null) return settings;
        var parsed = ProjectMigration.TryDeserialize<ProjectSettings>(element.Value);
        if (parsed != null) return parsed;
        settings.ImportEncoding = ProjectMigration.ScalarField(element.Value, "importEncoding") ?? settings.ImportEncoding;
        settings.ExportEncoding = ProjectMigration.ScalarField(element.Value, "exportEncoding") ?? settings.ExportEncoding;
        return settings;
    }
}