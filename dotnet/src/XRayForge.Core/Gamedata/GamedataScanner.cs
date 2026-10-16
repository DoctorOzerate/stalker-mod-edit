using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using XRayForge.Core.Imaging;
using XRayForge.Core.Ltx;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Quests;
using XRayForge.Core.Text;
using XRayForge.Core.Xml;

namespace XRayForge.Core.Gamedata;

/// <summary>
/// Сканирование `gamedata` — порт `processGamedataFiles` из App.js: классификация файлов
/// (`.dds`/`.ltx`/`.script`/`.lua`/`.xml`), кодировки, разбор XML/LTX, слияние с текущим
/// проектом, пост-обработка LTX (game_relations, squads, secrets, trade, …) и список пропусков.
/// </summary>
public static class GamedataScanner
{
    /// <summary>
    /// Просканировать файлы. `current` — проект, с которым сливаются данные (в JS — `project`),
    /// `topName` — имя корневой папки для `gamedataPath`.
    /// </summary>
    public static GamedataScanResult Scan(
        Project? current,
        string? topName,
        IEnumerable<GamedataFile>? files,
        GamedataScanOptions? options = null)
    {
        var context = new ScanContext(current ?? ProjectMigration.Empty(), Resolve(options, current));
        var fileList = files?.ToList() ?? new List<GamedataFile>();
        var total = fileList.Count;
        context.TotalFiles = total;
        var step = context.Options.ProgressStep > 0 ? context.Options.ProgressStep : 5;

        context.Options.Progress?.Invoke(0, total);
        var done = 0;
        foreach (var file in fileList)
        {
            try { context.ProcessFile(file); }
            catch (Exception e) { context.Skipped.Add(new SkippedFile(file.Path, e.Message)); }

            // В JS `continue` внутри try пропускал `done++`, из-за чего прогресс «застревал»;
            // в порте считается каждый обработанный файл (см. MIGRATION.md, «Этап 4»).
            done++;
            if (done % step == 0 || done == total) context.Options.Progress?.Invoke(done, total);
        }

        return context.Build(topName);
    }

    /// <summary>Строка-разделитель при слиянии item-файлов (`NL` в App.js).</summary>
    internal const string Nl = "\n";

    /// <summary>Кодировка импорта по умолчанию — из настроек проекта, как в JS.</summary>
    private static GamedataScanOptions Resolve(GamedataScanOptions? options, Project? project)
    {
        var source = options ?? new GamedataScanOptions();
        if (!string.IsNullOrEmpty(source.ImportEncoding)) return source;
        return new GamedataScanOptions
        {
            ImportEncoding = project?.Settings.ImportEncoding ?? "auto",
            PngEncoder = source.PngEncoder,
            Progress = source.Progress,
            ProgressStep = source.ProgressStep
        };
    }

    /// <summary>Накопитель результатов одного сканирования (замена замыканий из App.js).</summary>
    private sealed class ScanContext
    {
        private static readonly Regex IncludeLine = new(@"^\s*#include\b[^\n]*$", RegexOptions.Multiline);

        public ScanContext(Project project, GamedataScanOptions options)
        {
            Project = project;
            Options = options;
        }

        public Project Project { get; }
        public GamedataScanOptions Options { get; }
        public Dictionary<string, string> LtxFiles { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> ItemFiles { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> ItemOrigins { get; } = new(StringComparer.Ordinal);
        public List<ScriptFile> Scripts { get; } = new();
        public List<ProjectTexture> Textures { get; } = new();
        public List<ScannedCharacterFile> CharacterFiles { get; } = new();
        public List<ScannedDialogueFile> DialogueFiles { get; } = new();
        public List<Language> Languages { get; } = new();
        public List<Quest> Quests { get; } = new();
        public List<string> General { get; } = new();
        public List<InfoPortion> InfoPortions { get; } = new();
        public List<EncyclopediaArticle> Encyclopedia { get; } = new();
        public List<NpcProfileCharacter> NpcProfiles { get; } = new();
        public List<TextureDescriptor> TextureDescriptors { get; } = new();
        public List<SkippedFile> Skipped { get; } = new();
        public CharacterDescGeneral? GeneralDesc { get; private set; }
        public string? GeneralDescText { get; private set; }
        public string? SystemText { get; private set; }
        public string? ItemsText { get; private set; }

        /// <summary>Сколько файлов пришло на вход (`Object.keys(fileMap).length`).</summary>
        public int TotalFiles { get; set; }

        /// <summary>Классификация одного файла (ветвление `for (const path in fileMap)`).</summary>
        public void ProcessFile(GamedataFile file)
        {
            var lower = (file.Path ?? "").ToLowerInvariant();

            if (lower.EndsWith(".dds", StringComparison.Ordinal)
                && (lower.Contains("ui_icon_equipment", StringComparison.Ordinal)
                    || lower.Contains("ui_npc", StringComparison.Ordinal)
                    || lower.Contains("ui_actor", StringComparison.Ordinal)))
            {
                ProcessTexture(file);
                return;
            }

            if (lower.EndsWith(".ltx", StringComparison.Ordinal)
                || lower.EndsWith(".script", StringComparison.Ordinal)
                || lower.EndsWith(".lua", StringComparison.Ordinal))
            {
                ProcessText(file, lower);
                return;
            }

            if (lower.EndsWith(".xml", StringComparison.Ordinal)) ProcessXml(file);
            // прочие расширения игнорируются — как в JS
        }

        /// <summary>Битый DDS не пропускается, а попадает в текстуры с текстом ошибки (как в JS).</summary>
        private void ProcessTexture(GamedataFile file)
        {
            try
            {
                var image = DdsCodec.DecodeDds(file.Content);
                Textures.Add(new ProjectTexture
                {
                    Name = file.Name,
                    DataUrl = Options.PngEncoder?.Invoke(image.Pixels, image.Width, image.Height),
                    Width = image.Width,
                    Height = image.Height
                });
            }
            catch (Exception e)
            {
                Textures.Add(new ProjectTexture { Name = file.Name, Error = e.Message });
            }
        }

        /// <summary>LTX/script/lua: текст в `ltxFiles`, скрипты и item-файлы — отдельно.</summary>
        private void ProcessText(GamedataFile file, string lower)
        {
            var text = GameEncoding.Decode(file.Content, Options.ImportEncoding ?? "auto");
            var normalized = Paths.Normalize(file.Path);
            LtxFiles[normalized] = text;

            if (lower.EndsWith(".script", StringComparison.Ordinal) || lower.EndsWith(".lua", StringComparison.Ordinal))
                Scripts.Add(new ScriptFile { Name = file.Name, Path = normalized, Text = text, Original = text });
            else if (ItemCatalog.IsItemFile(file.Name))
                ItemFiles[file.Name] = text;
        }

        /// <summary>XML: снять `#include`, определить тип (переводы/персонажи/диалоги/квесты) или разобрать по содержимому.</summary>
        private void ProcessXml(GamedataFile file)
        {
            var text = IncludeLine.Replace(GameEncoding.Decode(file.Content, Options.ImportEncoding ?? "auto"), "");
            var normalized = Paths.Normalize(file.Path);
            var name = (file.Name ?? "").ToLowerInvariant();

            if (normalized.Contains("/text/", StringComparison.Ordinal))
            {
                var language = StringTableParser.Parse(text, file.Path);
                if (language != null)
                {
                    Languages.Add(language);
                    return;
                }
            }

            if (name.StartsWith("character_desc", StringComparison.Ordinal))
            {
                var characters = XmlParsers.ParseCharactersXml(text);
                if (characters.Count > 0)
                {
                    CharacterFiles.Add(new ScannedCharacterFile { File = file.Name ?? "", Path = normalized, Characters = characters });
                    return;
                }
            }

            if (name.Contains("dialog", StringComparison.Ordinal))
            {
                var dialogues = XmlParsers.ParseDialogueXml(text);
                if (dialogues.Count > 0)
                {
                    DialogueFiles.Add(new ScannedDialogueFile { File = file.Name ?? "", Path = normalized, Dialogues = dialogues });
                    return;
                }
            }

            if (name.Contains("task", StringComparison.Ordinal))
            {
                var quests = GameTasksParser.Parse(text);
                if (quests.Count > 0)
                {
                    foreach (var quest in quests) Quests.Add(WithOrigin(quest, normalized));
                    return;
                }
            }

            ProcessXmlFallback(file, text, normalized);
        }

        /// <summary>Fallback XML — контентная детекция (несколько разборов подряд, как в JS).</summary>
        private void ProcessXmlFallback(GamedataFile file, string text, string normalized)
        {
            var language = StringTableParser.Parse(text, file.Path);
            if (language != null)
            {
                Languages.Add(language);
                return;
            }

            var characters = XmlParsers.ParseCharactersXml(text);
            if (characters.Count > 0)
            {
                CharacterFiles.Add(new ScannedCharacterFile { File = file.Name, Path = normalized, Characters = characters });
                return;
            }

            var dialogues = XmlParsers.ParseDialogueXml(text);
            if (dialogues.Count > 0)
                DialogueFiles.Add(new ScannedDialogueFile { File = file.Name, Path = normalized, Dialogues = dialogues });

            var quests = GameTasksParser.Parse(text);
            if (quests.Count > 0)
                foreach (var quest in quests) Quests.Add(WithOrigin(quest, normalized));

            var general = XmlHelpers.ParseGeneralCharactersXml(text);
            if (general.Count > 0) General.AddRange(general);

            var generalDesc = GameXmlParsers.ParseCharacterDescGeneral(text);
            if (generalDesc != null)
            {
                GeneralDesc = generalDesc;
                GeneralDescText = text;
            }

            var infoPortions = GameXmlParsers.ParseInfoPortions(text);
            if (infoPortions.Count > 0) InfoPortions.AddRange(infoPortions);

            var encyclopedia = GameXmlParsers.ParseEncyclopedia(text);
            if (encyclopedia.Count > 0) Encyclopedia.AddRange(encyclopedia);

            var npcProfile = GameXmlParsers.ParseNpcProfile(text);
            if (npcProfile.Count > 0) NpcProfiles.AddRange(npcProfile);

            var descriptor = GameXmlParsers.ParseTextureDescriptor(text);
            if (descriptor != null && descriptor.Textures.Count > 0) TextureDescriptors.Add(descriptor);
        }

        /// <summary>Квест с источником и снимком `original` (снимок — без sourcePath/original).</summary>
        private static Quest WithOrigin(Quest quest, string path)
        {
            if (JsonSerializer.SerializeToNode(quest, ProjectSerializer.Options) is JsonObject snapshot)
            {
                snapshot.Remove("sourcePath");
                snapshot.Remove("original");
                quest.Original = JsonSerializer.SerializeToElement(snapshot);
            }

            quest.SourcePath = path;
            return quest;
        }

        /// <summary>Собрать патч и статистику (вторая половина `processGamedataFiles`).</summary>
        public GamedataScanResult Build(string? topName)
        {
            // item-файлы: сортировка, слияние текста и категории секций
            var mergedItems = "";
            foreach (var fileName in ItemFiles.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var text = ItemFiles[fileName];
                mergedItems += (mergedItems.Length > 0 ? Nl : "") + "; === " + fileName + " === " + Nl + text;
                var category = ItemCatalog.ItemFileCategory(fileName);
                foreach (var section in LtxParser.Parse(text).Sections)
                    if (!string.IsNullOrEmpty(section.Name))
                        ItemOrigins[section.Name] = string.IsNullOrEmpty(category) ? ItemCatalog.ClassifyItem(section) : category;
            }
            if (mergedItems.Length > 0) ItemsText = mergedItems;

            // персонажи: добавить новые (сравнение по id), сняв снимок `original`
            var mergedCharacters = new List<Character>(Project.Characters);
            foreach (var group in CharacterFiles)
                foreach (var character in group.Characters)
                {
                    if (mergedCharacters.Any(x => x.Id == character.Id)) continue;
                    character.Original = JsonSerializer.SerializeToElement(character, ProjectSerializer.Options);
                    mergedCharacters.Add(character);
                }

            // языки: сгруппировать по locale и слить с уже загруженными
            var grouped = new Dictionary<string, Language>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var language in Languages)
            {
                var key = string.IsNullOrEmpty(language.Locale) ? "unknown" : language.Locale;
                if (!grouped.TryGetValue(key, out var group))
                {
                    group = new Language { Locale = key, Name = language.Name };
                    grouped[key] = group;
                    order.Add(key);
                }

                foreach (var entry in language.Entries)
                    if (!string.IsNullOrEmpty(entry.Id)) group.Entries.Add(new LanguageEntry { Id = entry.Id, Text = entry.Text });
            }

            var mergedLanguages = XmlHelpers.MergeLanguages(Project.Languages, order.Select(key => grouped[key]));

            var mergedQuests = new List<Quest>(Project.Quests);
            foreach (var quest in Quests)
                if (!mergedQuests.Any(x => x.Id == quest.Id)) mergedQuests.Add(quest);

            var patch = new GamedataPatch
            {
                GamedataPath = string.IsNullOrEmpty(topName) ? Project.GamedataPath : topName,
                LtxFiles = LtxFiles,
                Characters = mergedCharacters,
                Languages = mergedLanguages,
                Quests = mergedQuests,
                ItemFiles = ItemFiles,
                ItemOrigins = ItemOrigins
            };

            var systemPath = LtxFiles.Keys.FirstOrDefault(p => p.ToLowerInvariant().EndsWith("system.ltx", StringComparison.Ordinal));
            if (systemPath != null) patch.SystemLtx = LtxFiles[systemPath];

            if (ItemsText != null) patch.ItemsLtx = ItemsText;
            if (Scripts.Count > 0) patch.Scripts = Scripts;

            if (Textures.Count > 0)
                patch.Textures = Textures
                    .OrderBy(t => (t.Name ?? "").ToLowerInvariant().Contains("ui_icon_equipment", StringComparison.Ordinal) ? 0 : 1)
                    .ToList();

            if (DialogueFiles.Count > 0)
            {
                var dialogues = new List<DialogueGraph>();
                foreach (var group in DialogueFiles)
                    foreach (var dialogue in group.Dialogues)
                    {
                        dialogue.SourcePath = group.Path;
                        dialogue.SourceFile = group.File;
                        dialogues.Add(dialogue);
                    }
                patch.Dialogues = dialogues;
            }

            if (General.Count > 0) patch.GeneralCommunities = General.Distinct().ToList();
            if (GeneralDesc != null)
                patch.GeneralDesc = JsonSerializer.SerializeToElement(
                    new GamedataGeneralDesc { Text = GeneralDescText ?? "", Communities = GeneralDesc.Communities },
                    ProjectSerializer.Options);

            FillLtxDomain(patch);
            return BuildResult(patch);
        }

        /// <summary>Пост-обработка LTX: game_relations, squads, secrets, spawn_sections, actor, monsters, trade, …</summary>
        private void FillLtxDomain(GamedataPatch patch)
        {
            foreach (var pair in LtxFiles)
            {
                var lower = pair.Key.ToLowerInvariant();
                if (patch.GameRelations == null && lower.EndsWith("game_relations.ltx", StringComparison.Ordinal))
                {
                    var relations = LtxDomainParsers.ParseGameRelations(pair.Value);
                    relations.Scales = LtxDomainParsers.ParseRatingScales(pair.Value);
                    patch.GameRelations = relations;
                    patch.GeneralCommunities = relations.Communities;
                }
            }

            var squads = new List<SquadDesc>();
            var spawnSections = new List<SpawnSectionInfo>();
            var monsters = new List<MonsterInfo>();
            SecretsInfo? secrets = null;
            ActorInfo? actor = null;
            SurgeManagerInfo? surgeManager = null;
            TradeInfo? trade = null;

            foreach (var pair in LtxFiles)
            {
                var lower = pair.Key.ToLowerInvariant();
                var isLtx = lower.EndsWith(".ltx", StringComparison.Ordinal);
                if (!isLtx) continue;

                if (lower.Contains("squad_descr", StringComparison.Ordinal))
                    squads.AddRange(LtxDomainParsers.ParseSquadDescr(pair.Value));
                if (lower.Contains("spawn_sections", StringComparison.Ordinal))
                    spawnSections.AddRange(LtxDomainParsers.ParseSpawnSections(pair.Value));
                if (lower.EndsWith("monsters.ltx", StringComparison.Ordinal)
                    || lower.Contains("/m_", StringComparison.Ordinal)
                    || lower.Contains("\\m_", StringComparison.Ordinal))
                    monsters.AddRange(LtxDomainParsers.ParseMonsters(pair.Value));
                if (secrets == null && lower.EndsWith("secrets.ltx", StringComparison.Ordinal))
                    secrets = LtxDomainParsers.ParseSecrets(pair.Value);
                if (actor == null && lower.EndsWith("actor.ltx", StringComparison.Ordinal))
                    actor = LtxDomainParsers.ParseActor(pair.Value);
                if (surgeManager == null && lower.EndsWith("surge_manager.ltx", StringComparison.Ordinal))
                    surgeManager = LtxDomainParsers.ParseSurgeManager(pair.Value);
                if (lower.Contains("trade_", StringComparison.Ordinal))
                {
                    var parsed = LtxDomainParsers.ParseTrade(pair.Value);
                    if (parsed.Traders.Count > 0 || parsed.Supplies.Count > 0)
                    {
                        trade ??= new TradeInfo();
                        trade.Traders.AddRange(parsed.Traders);
                        trade.Supplies.AddRange(parsed.Supplies);
                    }
                }
            }

            if (squads.Count > 0) patch.Squads = squads;
            if (secrets != null) patch.Secrets = secrets;
            if (spawnSections.Count > 0) patch.SpawnSections = spawnSections;
            if (actor != null) patch.Actor = actor;
            if (monsters.Count > 0) patch.Monsters = monsters;
            if (surgeManager != null) patch.SurgeManager = surgeManager;
            if (trade != null) patch.Trade = trade;
            if (NpcProfiles.Count > 0) patch.NpcProfiles = NpcProfiles;
            if (InfoPortions.Count > 0) patch.InfoPortions = InfoPortions;
            if (Encyclopedia.Count > 0) patch.Encyclopedia = Encyclopedia;

            if (TextureDescriptors.Count > 0)
            {
                var textureMap = new Dictionary<string, TextureMapEntry>(StringComparer.Ordinal);
                foreach (var descriptor in TextureDescriptors)
                    foreach (var texture in descriptor.Textures)
                        textureMap[texture.Id] = new TextureMapEntry
                        {
                            File = descriptor.File,
                            X = texture.X,
                            Y = texture.Y,
                            Width = texture.Width,
                            Height = texture.Height
                        };
                patch.TextureMap = textureMap;
            }
        }

        private GamedataScanResult BuildResult(GamedataPatch patch) => new()
        {
            Patch = patch,
            Skipped = Skipped,
            Stats = new GamedataScanStats
            {
                Files = TotalFiles,
                Scripts = Scripts.Count,
                Textures = Textures.Count,
                Characters = patch.Characters?.Count ?? 0,
                Dialogues = DialogueFiles.Sum(group => group.Dialogues.Count),
                Languages = patch.Languages?.Count ?? 0,
                Quests = patch.Quests?.Count ?? 0,
                Items = ItemFiles.Count,
                Skipped = Skipped.Count
            }
        };
    }
}