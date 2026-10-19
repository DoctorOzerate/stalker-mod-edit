using System.Text.RegularExpressions;
using XRayForge.Core.Gamedata;
using XRayForge.Core.Ltx;
using XRayForge.Core.Lua;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Suggestions;

/// <summary>
/// Библиотека данных для автодополнения. Собирается из проекта (и уже разобранных секций
/// предметов), не зависит от WPF и переиспользуется всеми вкладками. В индекс попадают:
/// секции предметов и system.ltx, скрипты и их функции, Lua API, text_id, диалоги,
/// персонажи, сообщества, визуалы, иконки, шкалы, квесты и прочие справочники gamedata.
/// </summary>
public sealed class SuggestionLibrary
{
    private readonly List<SuggestionEntry> _all;
    private readonly Dictionary<SuggestionKind, List<SuggestionEntry>> _byKind;

    private SuggestionLibrary(List<SuggestionEntry> entries)
    {
        _all = entries;
        _byKind = entries
            .GroupBy(entry => entry.Kind)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    /// <summary>Пустая библиотека: используется до загрузки проекта и в тестах.</summary>
    public static SuggestionLibrary Empty { get; } = new(new List<SuggestionEntry>());

    public IReadOnlyList<SuggestionEntry> All => _all;

    public IReadOnlyList<SuggestionEntry> OfKind(SuggestionKind kind) =>
        _byKind.TryGetValue(kind, out var entries) ? entries : Array.Empty<SuggestionEntry>();

    public IReadOnlyList<SuggestionEntry> OfKinds(params SuggestionKind[] kinds)
    {
        var result = new List<SuggestionEntry>();
        foreach (var kind in kinds) result.AddRange(OfKind(kind));
        return SuggestionMatcher.Distinct(result);
    }

    /// <summary>Подсказки по префиксу; можно ограничить типы (например, только диалоги).</summary>
    public IReadOnlyList<SuggestionEntry> Suggest(
        string? prefix, IEnumerable<SuggestionKind>? kinds = null, int limit = 20)
    {
        if (kinds == null) return SuggestionMatcher.Filter(_all, prefix, limit);
        var allowed = kinds.ToHashSet();
        return SuggestionMatcher.Filter(_all.Where(entry => allowed.Contains(entry.Kind)), prefix, limit);
    }

    /// <summary>
    /// Подсказки для конкретного поля предмета. Ссылочные поля получают секции, текстовые —
    /// text_id, `script_binding`/`logic` — скрипты и функции, `$spawn` — спавн-секции.
    /// В конец всегда добавляются уже встречавшиеся значения поля (как JS `datalist`).
    /// </summary>
    public IReadOnlyList<SuggestionEntry> ForItemField(
        string? key, IEnumerable<string>? existingValues = null)
    {
        var result = new List<SuggestionEntry>();
        var name = (key ?? "").Trim().ToLowerInvariant();

        void AddKinds(params SuggestionKind[] kinds) => result.AddRange(OfKinds(kinds));

        if (name is "$spawn" or "spawn")
            AddKinds(SuggestionKind.SpawnSection, SuggestionKind.Item, SuggestionKind.Section);
        else if (name is "visual" or "actor_visual")
            AddKinds(SuggestionKind.Visual, SuggestionKind.Texture);
        else if (name.Contains("script", StringComparison.Ordinal) ||
                 name.Contains("binding", StringComparison.Ordinal) ||
                 name is "logic" or "on_death" or "action" or "condition" ||
                 name.EndsWith("_action", StringComparison.Ordinal) ||
                 name.EndsWith("_condition", StringComparison.Ordinal))
            AddKinds(SuggestionKind.ScriptBinding, SuggestionKind.ScriptFunction,
                SuggestionKind.Script, SuggestionKind.LuaApi);
        else if (name is "scope_name" or "silencer_name" or "grenade_launcher_name" or
                 "hud" or "upgrades" or "installed_upgrades" or "parent" or "death_items" or
                 "bones_koeff_protection" ||
                 name.EndsWith("_sect", StringComparison.Ordinal) ||
                 name.EndsWith("_section", StringComparison.Ordinal) ||
                 name.Contains("immunit", StringComparison.Ordinal))
            AddKinds(SuggestionKind.Item, SuggestionKind.Section);
        else if (name.Contains("icon", StringComparison.Ordinal) ||
                 name.Contains("texture", StringComparison.Ordinal))
            AddKinds(SuggestionKind.Icon, SuggestionKind.Texture);
        else if (name is "ammo_class" or "grenade_class")
            result.AddRange(OfKind(SuggestionKind.Item).Where(entry =>
                entry.Detail == "Боеприпасы" ||
                entry.Value.StartsWith("ammo", StringComparison.OrdinalIgnoreCase) ||
                entry.Value.StartsWith("grenade", StringComparison.OrdinalIgnoreCase)));
        else if (name is "inv_name" or "inv_name_short" or "description" or "name" or
                 "title" or "text" or "text_id" or "voice_id" ||
                 name.EndsWith("_text", StringComparison.Ordinal) ||
                 name.EndsWith("_name", StringComparison.Ordinal))
            AddKinds(SuggestionKind.TextId);
        else if (name is "class")
            AddKinds(SuggestionKind.Class);
        else if (name.Contains("dialog", StringComparison.Ordinal))
            AddKinds(SuggestionKind.Dialogue);
        else if (name.Contains("community", StringComparison.Ordinal) ||
                 name.Contains("faction", StringComparison.Ordinal))
            AddKinds(SuggestionKind.Community);
        else if (name.Contains("rank", StringComparison.Ordinal))
            AddKinds(SuggestionKind.Rank);
        else if (name.Contains("reputation", StringComparison.Ordinal))
            AddKinds(SuggestionKind.Reputation);

        foreach (var value in existingValues ?? Enumerable.Empty<string>())
            result.Add(new SuggestionEntry(value, SuggestionKind.Value));

        return SuggestionMatcher.Distinct(result);
    }

    /// <summary>Подсказки для полей редактора персонажа.</summary>
    public IReadOnlyList<SuggestionEntry> ForCharacterField(string? field)
    {
        return (field ?? "").Trim().ToLowerInvariant() switch
        {
            "community" => OfKind(SuggestionKind.Community),
            "rank" or "npc_rank" => OfKind(SuggestionKind.Rank),
            "reputation" => OfKind(SuggestionKind.Reputation),
            "icon" => OfKinds(SuggestionKind.Icon, SuggestionKind.Texture),
            "visual" => OfKind(SuggestionKind.Visual),
            "start_dialog" or "actor_dialog" => OfKind(SuggestionKind.Dialogue),
            "id" => OfKind(SuggestionKind.Character),
            "class" => OfKind(SuggestionKind.Class),
            "terrain_sect" or "snd_config" => OfKinds(SuggestionKind.Item, SuggestionKind.Section),
            _ => Array.Empty<SuggestionEntry>()
        };
    }

    /// <summary>Подсказки для Lua-редактора (этап 7.3): API, глобалы и функции скриптов.</summary>
    public IReadOnlyList<SuggestionEntry> ForLua() =>
        OfKinds(SuggestionKind.LuaApi, SuggestionKind.ScriptFunction,
            SuggestionKind.ScriptBinding, SuggestionKind.Script);

    /// <summary>Подсказки путей для `#include` (редактор `system.ltx`).</summary>
    public IReadOnlyList<SuggestionEntry> ForInclude() => OfKind(SuggestionKind.Include);

    /// <summary>Подсказки для метаданных реплики: info-порции и скрипты условий/действий.</summary>
    public IReadOnlyList<SuggestionEntry> ForMetadataField(string? key)
    {
        return (key ?? "").Trim().ToLowerInvariant() switch
        {
            "has_info" or "dont_has_info" or "give_info" or "disable_info" =>
                OfKind(SuggestionKind.InfoPortion),
            "precondition" or "action" =>
                OfKinds(SuggestionKind.ScriptBinding, SuggestionKind.ScriptFunction, SuggestionKind.LuaApi),
            _ => Array.Empty<SuggestionEntry>()
        };
    }

    /// <summary>
    /// Собрать индекс по проекту. `itemSections` передаёт уже разобранные секции
    /// `ItemsLtx`, чтобы вкладка «Предметы» не парсила один и тот же текст повторно.
    /// `catalog` — справочник gamedata; по умолчанию встроенный `catalog.csv`.
    /// </summary>
    public static SuggestionLibrary Build(
        Project? project, IEnumerable<LtxSection>? itemSections = null,
        GamedataCatalog? catalog = null)
    {
        if (project == null) return Empty;

        var builder = new Builder();
        var items = itemSections?
            .Where(section => section != null && !string.IsNullOrWhiteSpace(section.Name))
            .ToList();
        if (items == null || items.Count == 0) items = ParseItemSections(project);

        foreach (var section in items) builder.AddItemSection(section, project);
        builder.AddFieldCatalog();
        foreach (var section in LtxParser.Parse(project.SystemLtx).Sections)
            builder.Add(new SuggestionEntry(section.Name, SuggestionKind.Section, "system.ltx"));

        builder.AddScripts(project.Scripts);
        builder.AddLua();
        builder.AddLanguages(project);
        builder.AddDialogues(project);
        builder.AddCharacters(project);
        builder.AddWorld(project);

        if (items.Count == 0 && project.ItemOrigins is { Count: > 0 } origins)
            foreach (var pair in origins)
                builder.Add(new SuggestionEntry(pair.Key, SuggestionKind.Item, pair.Value));

        builder.AddCatalog(catalog ?? GamedataCatalog.Default, project.Series);

        return new SuggestionLibrary(builder.Entries.ToList());
    }

    private static readonly Regex FunctionDeclaration = new(
        @"\b(?:local\s+)?function\s+([A-Za-z_][\w.:]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LuaComment = new(
        @"--\[\[.*?\]\]|--[^\r\n]*",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static List<LtxSection> ParseItemSections(Project project)
    {
        var text = project.ItemsLtx;
        if (string.IsNullOrWhiteSpace(text) && project.ItemFiles is { Count: > 0 } files)
            text = string.Join("\n", files.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value));
        return LtxParser.Parse(text).Sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Name))
            .ToList();
    }

    private static string ScriptName(string? name, string? path)
    {
        var file = !string.IsNullOrWhiteSpace(name) ? name : path ?? "";
        file = file.Replace('\\', '/');
        var slash = file.LastIndexOf('/');
        if (slash >= 0) file = file[(slash + 1)..];
        var dot = file.LastIndexOf('.');
        if (dot > 0) file = file[..dot];
        return file.Trim();
    }

    /// <summary>Накопитель индекса: отбрасывает пустые и повторяющиеся значения.</summary>
    private sealed class Builder
    {
        private readonly List<SuggestionEntry> _entries = new();
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SuggestionEntry> Entries => _entries;

        public void Add(SuggestionEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value)) return;
            if (!_seen.Add(entry.Value)) return;
            _entries.Add(entry);
        }

        public void AddItemSection(LtxSection section, Project project)
        {
            var name = section.Name.Trim();
            var detail = project.ItemOrigins != null &&
                         project.ItemOrigins.TryGetValue(name, out var origin) &&
                         !string.IsNullOrWhiteSpace(origin)
                ? origin
                : ItemCatalog.ClassifyItem(section);

            Add(new SuggestionEntry(name, SuggestionKind.Item, detail));
            foreach (var entry in section.Entries)
            {
                if (string.Equals(entry.Key, "class", StringComparison.Ordinal))
                    Add(new SuggestionEntry(entry.Value, SuggestionKind.Class, "item class"));
                Add(new SuggestionEntry(entry.Key, SuggestionKind.Field, "item field"));
            }
        }

        public void AddFieldCatalog()
        {
            foreach (var field in ItemCatalog.CommonFields)
                Add(new SuggestionEntry(field, SuggestionKind.Field, "common field"));
            foreach (var profile in ItemCatalog.FieldProfiles.Values)
                foreach (var field in profile)
                    Add(new SuggestionEntry(field, SuggestionKind.Field, "item field profile"));
        }

        public void AddScripts(IEnumerable<ScriptFile>? scripts)
        {
            foreach (var script in scripts ?? Enumerable.Empty<ScriptFile>())
            {
                if (script == null) continue;
                var file = !string.IsNullOrWhiteSpace(script.Name) ? script.Name : script.Path;
                var name = ScriptName(script.Name, script.Path);
                if (name.Length == 0) continue;

                Add(new SuggestionEntry(name + ".script", SuggestionKind.Script, file));
                Add(new SuggestionEntry(name, SuggestionKind.Script, file));

                var code = script.Text ?? script.Original ?? "";
                if (string.IsNullOrWhiteSpace(code)) continue;
                var clean = LuaComment.Replace(code, "");
                foreach (Match match in FunctionDeclaration.Matches(clean))
                {
                    var function = match.Groups[1].Value.Trim();
                    if (function.Length == 0) continue;
                    Add(new SuggestionEntry(function, SuggestionKind.ScriptFunction, file));
                    Add(new SuggestionEntry(name + "." + function, SuggestionKind.ScriptBinding, file));
                }
            }
        }

        public void AddLua()
        {
            foreach (var api in LuaCatalog.Api)
                Add(new SuggestionEntry(api, SuggestionKind.LuaApi, "Lua API"));
            foreach (var global in LuaCatalog.EngineGlobals)
                Add(new SuggestionEntry(global, SuggestionKind.LuaApi, "Lua global"));
            foreach (var keyword in LuaCatalog.Keywords)
                Add(new SuggestionEntry(keyword, SuggestionKind.LuaApi, "Lua keyword"));
        }

        public void AddLanguages(Project project)
        {
            foreach (var language in project.Languages ?? new List<Language>())
                foreach (var entry in language.Entries)
                    Add(new SuggestionEntry(entry.Id, SuggestionKind.TextId,
                        string.IsNullOrWhiteSpace(entry.Text) ? language.Locale : entry.Text));

            foreach (var node in project.Nodes.Concat(
                         (project.Dialogues ?? new List<DialogueGraph>()).SelectMany(dialogue => dialogue.Nodes)))
                Add(new SuggestionEntry(node.Data?.TextId, SuggestionKind.TextId, "dialogue node"));

            foreach (var article in project.Encyclopedia ?? new List<EncyclopediaArticle>())
                Add(new SuggestionEntry(article.Id, SuggestionKind.TextId, article.Name));
        }

        public void AddDialogues(Project project)
        {
            Add(new SuggestionEntry(project.DialogueId, SuggestionKind.Dialogue, "project"));
            foreach (var dialogue in project.Dialogues ?? new List<DialogueGraph>())
            {
                if (dialogue == null) continue;
                var detail = !string.IsNullOrWhiteSpace(dialogue.Name)
                    ? dialogue.Name
                    : dialogue.SourceFile ?? "";
                Add(new SuggestionEntry(dialogue.Id, SuggestionKind.Dialogue, detail));
            }
        }

        public void AddCharacters(Project project)
        {
            foreach (var community in project.GeneralCommunities ?? new List<string>())
                Add(new SuggestionEntry(community, SuggestionKind.Community, "general"));

            if (project.GameRelations != null)
            {
                foreach (var community in project.GameRelations.Communities ?? new List<string>())
                    Add(new SuggestionEntry(community, SuggestionKind.Community, "game_relations"));
                foreach (var community in project.GameRelations.MonsterCommunities ?? new List<string>())
                    Add(new SuggestionEntry(community, SuggestionKind.Community, "monster"));
            }

            foreach (var character in project.Characters ?? new List<Character>())
            {
                if (character == null) continue;
                Add(new SuggestionEntry(character.Id, SuggestionKind.Character, character.Name));
                Add(new SuggestionEntry(character.Community, SuggestionKind.Community, character.Id));
                Add(new SuggestionEntry(character.Visual, SuggestionKind.Visual, character.Id));
                Add(new SuggestionEntry(character.Icon, SuggestionKind.Icon, character.Id));
                Add(new SuggestionEntry(character.Class, SuggestionKind.Class, character.Id));
                Add(new SuggestionEntry(character.TerrainSect, SuggestionKind.Section, character.Id));
            }

            foreach (var profile in project.NpcProfiles ?? new List<NpcProfileCharacter>())
            {
                if (profile == null) continue;
                Add(new SuggestionEntry(profile.Id, SuggestionKind.NpcProfile, profile.Class));
                Add(new SuggestionEntry(profile.SpecificCharacter, SuggestionKind.Character, profile.Id));
            }
        }

        public void AddWorld(Project project)
        {
            if (project.Actor != null)
            {
                Add(new SuggestionEntry(project.Actor.Visual, SuggestionKind.Visual, "actor"));
                Add(new SuggestionEntry(project.Actor.Class, SuggestionKind.Class, "actor"));
                Add(new SuggestionEntry(project.Actor.ScriptBinding, SuggestionKind.ScriptBinding, "actor"));
            }

            foreach (var monster in project.Monsters ?? new List<MonsterInfo>())
            {
                if (monster == null) continue;
                Add(new SuggestionEntry(monster.Id, SuggestionKind.Monster, monster.Class));
                Add(new SuggestionEntry(monster.Visual, SuggestionKind.Visual, monster.Id));
                Add(new SuggestionEntry(monster.Icon, SuggestionKind.Icon, monster.Id));
                Add(new SuggestionEntry(monster.ScriptBinding, SuggestionKind.ScriptBinding, monster.Id));
            }

            foreach (var section in project.SpawnSections ?? new List<SpawnSectionInfo>())
            {
                if (section == null) continue;
                Add(new SuggestionEntry(section.Id, SuggestionKind.SpawnSection, section.Base));
                Add(new SuggestionEntry(section.Community, SuggestionKind.Community, section.Id));
                Add(new SuggestionEntry(section.CharacterProfile, SuggestionKind.Character, section.Id));
                Add(new SuggestionEntry(section.Logic, SuggestionKind.ScriptBinding, section.Id));
            }

            foreach (var squad in project.Squads ?? new List<SquadDesc>())
            {
                if (squad == null) continue;
                Add(new SuggestionEntry(squad.Id, SuggestionKind.Squad, squad.Faction));
            }

            foreach (var portion in project.InfoPortions ?? new List<InfoPortion>())
            {
                if (portion == null) continue;
                Add(new SuggestionEntry(portion.Id, SuggestionKind.InfoPortion,
                    portion.Tasks is { Count: > 0 } ? "quest" : ""));
            }

            foreach (var quest in project.Quests ?? new List<Quest>())
            {
                if (quest == null) continue;
                Add(new SuggestionEntry(quest.Id, SuggestionKind.Quest, quest.Title));
                Add(new SuggestionEntry(quest.InfoportionComplete, SuggestionKind.InfoPortion, quest.Id));
                Add(new SuggestionEntry(quest.InfoportionFail, SuggestionKind.InfoPortion, quest.Id));
                foreach (var objective in quest.Objectives ?? new List<QuestObjective>())
                {
                    Add(new SuggestionEntry(objective.Infoportion, SuggestionKind.InfoPortion, quest.Id));
                    Add(new SuggestionEntry(objective.Article, SuggestionKind.TextId, quest.Id));
                }
            }

            foreach (var texture in project.Textures ?? new List<ProjectTexture>())
            {
                if (texture == null) continue;
                Add(new SuggestionEntry(texture.Name, SuggestionKind.Texture, texture.Error ?? "texture"));
            }
            if (project.TextureMap != null)
                foreach (var pair in project.TextureMap)
                    if (pair.Value != null)
                        Add(new SuggestionEntry(pair.Key, SuggestionKind.Icon, pair.Value.File));

            if (project.Secrets?.Treasures != null)
                foreach (var treasure in project.Secrets.Treasures)
                    if (treasure != null)
                        Add(new SuggestionEntry(treasure.Id, SuggestionKind.Section, "secret"));

            var scales = project.GameRelations?.Scales;
            if (scales != null)
            {
                foreach (var mark in scales.Rating ?? new List<ScaleMark>())
                    Add(new SuggestionEntry(mark.Value, SuggestionKind.Rank, mark.Name));
                foreach (var mark in scales.Reputation ?? new List<ScaleMark>())
                    Add(new SuggestionEntry(mark.Value, SuggestionKind.Reputation, mark.Name));
            }

            if (project.EquipSlots != null)
                foreach (var slot in project.EquipSlots)
                    Add(new SuggestionEntry(slot.Key, SuggestionKind.Field, "equip slot"));
        }

        /// <summary>Подсказки из каталога gamedata: секции, include, скрипты, функции и id.</summary>
        public void AddCatalog(GamedataCatalog catalog, string? series)
        {
            foreach (var entry in catalog.Suggestions(series)) Add(entry);
        }
    }
}
