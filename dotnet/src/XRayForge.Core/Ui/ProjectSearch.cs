using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;

namespace XRayForge.Core.Ui;

/// <summary>Совпадение глобального поиска: вкладка, категория, подпись записи и фрагмент текста.</summary>
public sealed record ProjectSearchHit(string TabId, string Kind, string Label, string Snippet);

/// <summary>
/// Глобальный поиск по проекту (этап 5): диалоги, персонажи, предметы (LTX-секции), иконки,
/// скрипты, system.ltx, языки, квесты и info-порции. Поиск по подстроке без учёта регистра;
/// результат несёт вкладку, куда ведёт переход (переходы по результату — этапы 6–7).
/// </summary>
public static class ProjectSearch
{
    /// <summary>Предел результатов одного поиска (список в окне поиска).</summary>
    public const int DefaultLimit = 200;

    /// <summary>Найти совпадения; пустой запрос или `null`-проект → пустой список.</summary>
    public static IReadOnlyList<ProjectSearchHit> Search(Project? project, string? query, int limit = DefaultLimit)
    {
        var hits = new List<ProjectSearchHit>();
        var needle = (query ?? "").Trim();
        if (project == null || needle.Length == 0 || limit <= 0) return hits;

        var collector = new Collector(hits, needle, limit);
        collector.ScanDialogues(project);
        collector.ScanCharacters(project);
        collector.ScanItems(project);
        collector.ScanIcons(project);
        collector.ScanScripts(project);
        collector.ScanSystem(project);
        collector.ScanLanguages(project);
        collector.ScanQuests(project);
        return hits;
    }

    /// <summary>Содержит ли значение подстроку (без учёта регистра; пустой запрос — нет).</summary>
    public static bool Matches(string? value, string? query)
    {
        var needle = (query ?? "").Trim();
        if (needle.Length == 0 || string.IsNullOrEmpty(value)) return false;
        return value.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Фрагмент вокруг совпадения: пробелы сжимаются, края обрезаются многоточием.</summary>
    public static string Snippet(string? text, string? query, int radius = 40)
    {
        var flat = Collapse(text);
        if (flat.Length == 0) return "";
        if (radius < 0) radius = 0;

        var needle = (query ?? "").Trim();
        var at = needle.Length == 0 ? -1 : flat.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return Truncate(flat, radius * 2);

        var start = Math.Max(0, at - radius);
        var end = Math.Min(flat.Length, at + needle.Length + radius);
        var slice = flat[start..end];
        return (start > 0 ? "…" : "") + slice + (end < flat.Length ? "…" : "");
    }

    /// <summary>Пробельные символы (включая переводы строк) сжимаются в один пробел.</summary>
    public static string Collapse(string? text)
    {
        var source = text ?? "";
        var builder = new System.Text.StringBuilder(source.Length);
        var pending = false;
        foreach (var ch in source)
        {
            if (char.IsWhiteSpace(ch))
            {
                pending = builder.Length > 0;
                continue;
            }

            if (pending)
            {
                builder.Append(' ');
                pending = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string Truncate(string text, int max) =>
        max > 0 && text.Length > max ? text[..max] + "…" : text;

    /// <summary>Накопитель результатов: следит за пределом и обрезает фрагменты.</summary>
    private sealed class Collector
    {
        private readonly List<ProjectSearchHit> _hits;
        private readonly string _query;
        private readonly int _limit;

        public Collector(List<ProjectSearchHit> hits, string query, int limit)
        {
            _hits = hits;
            _query = query;
            _limit = limit;
        }

        private bool Full => _hits.Count >= _limit;

        private void Add(string tabId, string kind, string? label, string? text)
        {
            if (Full) return;
            _hits.Add(new ProjectSearchHit(tabId, kind, label ?? "", Snippet(text, _query)));
        }

        /// <summary>Поле совпало с запросом → запись (подпись и фрагмент берутся из других полей).</summary>
        private void AddField(string tabId, string kind, string? label, string? field, string? text)
        {
            if (!Matches(field, _query)) return;
            Add(tabId, kind, label, text);
        }

        /// <summary>Секции LTX-документа: имя секции и строки `ключ = значение`.</summary>
        private void ScanLtxSections(string tabId, string? text, string source)
        {
            foreach (var section in LtxParser.Parse(text).Sections)
            {
                if (Full) return;
                AddField(tabId, source, section.Name, section.Name, source);
                foreach (var entry in section.Entries)
                {
                    if (Full) return;
                    AddField(tabId, source, section.Name + "." + entry.Key, entry.Key, entry.Value);
                    AddField(tabId, source, section.Name + "." + entry.Key, entry.Value, entry.Value);
                }
            }
        }

        public void ScanSystem(Project project) =>
            ScanLtxSections(NavigationCatalog.SystemId, project.SystemLtx, "system.ltx");

        public void ScanLanguages(Project project)
        {
            foreach (var language in project.Languages)
            {
                if (Full) return;
                var label = Label(language.Locale, language.Name);
                var count = $"{language.Entries.Count} строк";
                AddField(NavigationCatalog.LanguagesId, "language", label, language.Locale, count);
                AddField(NavigationCatalog.LanguagesId, "language", label, language.Name, count);

                foreach (var entry in language.Entries)
                {
                    if (Full) return;
                    AddField(NavigationCatalog.LanguagesId, "string", entry.Id, entry.Id, entry.Text);
                    AddField(NavigationCatalog.LanguagesId, "string", entry.Id, entry.Text, entry.Text);
                }
            }
        }

        public void ScanQuests(Project project)
        {
            foreach (var quest in project.Quests)
            {
                if (Full) return;
                var label = Label(quest.Id, quest.Title);
                AddField(NavigationCatalog.QuestsId, "quest", label, quest.Id, quest.Title);
                AddField(NavigationCatalog.QuestsId, "quest", label, quest.Title, quest.Title);
                AddField(NavigationCatalog.QuestsId, "quest", label, quest.Text, quest.Text);
                AddField(NavigationCatalog.QuestsId, "quest", label, quest.Category, quest.Title);

                foreach (var objective in quest.Objectives)
                {
                    if (Full) return;
                    AddField(NavigationCatalog.QuestsId, "objective", label, objective.Text, objective.Text);
                    AddField(NavigationCatalog.QuestsId, "objective", label, objective.Infoportion, objective.Text);
                }
            }

            foreach (var portion in project.InfoPortions ?? new List<InfoPortion>())
            {
                if (Full) return;
                AddField(NavigationCatalog.QuestsId, "info_portion", portion.Id, portion.Id, string.Join(", ", portion.Tasks));
                AddField(NavigationCatalog.QuestsId, "info_portion", portion.Id, string.Join(" ", portion.Dialogs), portion.Id);
            }
        }

        public void ScanItems(Project project)
        {
            ScanLtxSections(NavigationCatalog.ItemsId, project.ItemsLtx, "items.ltx");
            if (project.ItemFiles == null) return;
            foreach (var file in project.ItemFiles)
            {
                if (Full) return;
                ScanLtxSections(NavigationCatalog.ItemsId, file.Value, file.Key);
            }
        }

        public void ScanIcons(Project project)
        {
            foreach (var texture in project.Textures ?? new List<ProjectTexture>())
            {
                if (Full) return;
                var info = texture.Width is { } w && texture.Height is { } h
                    ? $"иконка {w}×{h}"
                    : texture.DataUrl != null ? "иконка (png)" : texture.Error ?? "";
                AddField(NavigationCatalog.IconsId, "texture", texture.Name, texture.Name, info);
            }

            if (project.TextureMap == null) return;
            foreach (var pair in project.TextureMap)
            {
                if (Full) return;
                var info = $"атлас {pair.Value.Width:0}×{pair.Value.Height:0} @ {pair.Value.X:0},{pair.Value.Y:0}";
                AddField(NavigationCatalog.IconsId, "texture_map", pair.Key, pair.Key, info);
                AddField(NavigationCatalog.IconsId, "texture_map", pair.Key, pair.Value.File, pair.Key + " → " + info);
            }
        }

        public void ScanScripts(Project project)
        {
            foreach (var script in project.Scripts ?? new List<ScriptFile>())
            {
                if (Full) return;
                var label = Label(script.Name, script.Path);
                AddField(NavigationCatalog.ScriptsId, "script", label, script.Name, script.Path);
                AddField(NavigationCatalog.ScriptsId, "script", label, script.Path, label);

                var lines = (script.Text ?? "").Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    if (Full) return;
                    if (!Matches(lines[i], _query)) continue;
                    Add(NavigationCatalog.ScriptsId, "script", label + ":" + (i + 1), lines[i]);
                }
            }
        }

        public void ScanDialogues(Project project)
        {
            foreach (var dialogue in project.Dialogues)
            {
                if (Full) return;
                var id = Label(dialogue.Id, dialogue.Name);
                AddField(NavigationCatalog.DialoguesId, "graph", id, dialogue.Name, id);
                AddField(NavigationCatalog.DialoguesId, "graph", id, dialogue.Id,
                    $"{dialogue.Nodes.Count} узлов · {dialogue.Edges.Count} связей");

                foreach (var node in dialogue.Nodes)
                {
                    if (Full) return;
                    ScanNode(NavigationCatalog.DialoguesId, node, id);
                }
            }

            // Плоские узлы проекта (`project.nodes`) — как в графе App.js.
            foreach (var node in project.Nodes)
            {
                if (Full) return;
                ScanNode(NavigationCatalog.DialoguesId, node, node.Id);
            }
        }

        private void ScanNode(string tabId, DialogueNode node, string fallbackLabel)
        {
            var label = Label(node.Id, fallbackLabel);
            var data = node.Data;
            AddField(tabId, node.Type, label, node.Id, data?.Text);
            AddField(tabId, node.Type, label, data?.Text, data?.Text);
            AddField(tabId, node.Type, label, data?.TextId, data?.Text);
            AddField(tabId, node.Type, label, data?.PhraseId, data?.Text);
            AddField(tabId, node.Type, label, data?.VoiceId, data?.Text);
            AddField(tabId, node.Type, label, data?.ActionType, data?.Params);
        }

        public void ScanCharacters(Project project)
        {
            foreach (var character in project.Characters)
            {
                if (Full) return;
                var id = Label(character.Id, character.Name);
                AddField(NavigationCatalog.CharactersId, "character", id, character.Id, character.Name);
                AddField(NavigationCatalog.CharactersId, "character", id, character.Name, character.Name);
                AddField(NavigationCatalog.CharactersId, "character", id, character.Community, character.Bio);
                AddField(NavigationCatalog.CharactersId, "character", id, character.Class, character.Bio);
                AddField(NavigationCatalog.CharactersId, "character", id, character.Bio, character.Bio);
            }

            foreach (var profile in project.NpcProfiles ?? new List<NpcProfileCharacter>())
            {
                if (Full) return;
                AddField(NavigationCatalog.CharactersId, "npc_profile", profile.Id, profile.Id, profile.Class);
                AddField(NavigationCatalog.CharactersId, "npc_profile", profile.Id, profile.SpecificCharacter, profile.Id);
            }

            foreach (var community in project.GeneralCommunities)
            {
                if (Full) return;
                AddField(NavigationCatalog.CharactersId, "community", community, community, "сообщество");
            }
        }

        private static string Label(string? primary, string? secondary) =>
            !string.IsNullOrEmpty(primary) ? primary! : secondary ?? "";
    }
}
