# Миграция X-Ray Forge на WPF (.NET 9)

> Цель: перенести настольное приложение **X-Ray Forge** (редактор модов S.T.A.L.K.E.R.)
> с текущего стека **Electron + React + Vite + Tailwind** на **WPF (C# / .NET 9)**.

---

## Прогресс миграции

- [x] Этап 0. Каркас и соглашения — решение `dotnet/XRayForge.sln` создано, собирается, тесты проходят
- [x] Этап 1. Core: фундамент (history, i18n, core) — выполнено (14 тестов зелёные)
- [x] Этап 2. Core: парсеры данных — выполнено: Lua / Graph / Dialogs / XML / Quests / Spawn / Ltx (121 тест зелёный)
- [x] Этап 3. DDS-кодек — выполнено: DXT1/3/5 + BC4/BC5 + энкодер + mip-цепочка (214 тестов зелёные)
- [x] Этап 4. Модель проекта + персистенция — выполнено: модель схемы 3, миграция, JSON-паритет, профиль `%APPDATA%`, сканер gamedata, автосейв (249 тестов зелёные)
- [x] Этап 5. Каркас UI + UI по референсам — выполнено: окно-оболочка (`MainWindow` + свой хром), 7 диалогов, темы/токены, навигация с `Ctrl+1…9`, глобальный поиск, точки сохранения, автосейв и статусная строка, локализация RU/EN (276 тестов зелёные)
- [x] Этап 6. Редактор диалогов (граф) — выполнено: холст графа, связи и группы, палитра узлов и список диалогов, свойства реплики и метаданные, мини-карта и панели, прогон диалога, экспорт SVG, импорт и игровой экспорт `game_dialogs` (329 тестов зелёные)
- [x] Этап 7. Остальные вкладки — персонажи, предметы, иконки, скрипты, system.ltx, языки и квесты; добавлены панельные ширины, подсветка Lua прямо в редакторе, дерево/SVG инклюдов, сводная таблица `string_table`, граф цепочек квестов и автодополнение из каталога gamedata (419 тестов зелёные)
- [x] Этап 8. Экспорт и самопроверка — игровой ZIP (`GamePackage`), CSV/DDS/PNG, `.project.json`, встроенная `ProjectSelfCheck` с окном отчёта и single-file publish (`dotnet/publish.ps1`)

---

## 1. Что за программа

X-Ray Forge — редактор модов для игр на движке X-Ray («Тень Чернобыля» SoC,
«Чистое Небо» CS, «Зов Припяти» CoP). Работает с игровыми данными `gamedata`:

- диалоги (`game_dialogs*.xml` + `dialogs.ltx` + `string_table`);
- персонажи (`character_desc_*.xml`, `character_desc_general.xml`);
- предметы (`items.ltx`, `weapons.ltx`, `ammo.ltx`, … — LTX с наследованием);
- иконки/атласы (DDS-текстуры DXT1/3/5, BC4/BC5);
- скрипты (`.script`/`.lua`);
- `system.ltx` и дерево `#include`;
- локализации (`string_table` по локалям);
- квесты (`game_tasks`, `task_manager`);
- `all.spawn`.

Текущий стек: Electron 31, React 18, Vite 5, Tailwind CSS, `fflate` (ZIP),
`@xmldom/xmldom` (XML в тестах). Тесты — `node:test`.

Окружение: установлен .NET SDK **9.0.318** (`C:\Program Files\dotnet\sdk`).

---

## 2. Текущая структура кода

| Файл | Назначение | Куда в WPF |
|---|---|---|
| `electron/main.cjs` | IPC: save-project, open-dir, read-dir, save-file, open-file | `Storage/ProjectFileService`, `Storage/FileSystemService` + нативные диалоги WPF |
| `electron/preload.cjs` | мост `window.xrayForge` | не нужен |
| `src/main.jsx` | точка входа React | `App.xaml` |
| `src/App.js` (2270 стр.) | монолит: состояние + рендер всех экранов | `Projects/` (модель, миграция, целостность), `Storage/` (профиль, автосейв), `Gamedata/` (сканер) + `MainWindow` + ViewModels |
| `src/lib/core.js` | пути, экранирование XML, парсинг LTX/XML, кодировки, миграция | `Core/` + `Projects/ProjectMigration` |
| `src/lib/ltx.js` | LTX-парсер, инклюды, предметы, категории, подсветка, `parse*` | `Core/Ltx/` |
| `src/lib/xml.js` | string_table, персонажи, локали, info_portions, encyclopedia, npc_profile, дескрипторы текстур | `Core/Xml/` |
| `src/lib/dialogs.js` | экспорт диалогов → XML/ZIP, валидация text_id | `Core/Dialogs/` |
| `src/lib/graph.js` | типы узлов, валидация графа, раскладка, `graphToSvg` | `Core/Graph/` |
| `src/lib/dds.js` | DDS-кодек: DXT1/3/5, BC4/BC5, энкодер, mip-цепочка | `Core/Imaging/` (`DdsCodec`, `DdsDecode`, `DdsEncode`, `DdsModels`) |
| `src/lib/lua.js` | токенизация Lua, подсветка, баланс блоков, сниппеты | `Core/Lua/` |
| `src/lib/quests.js` | парсинг/экспорт квестов, цепочки, condlist | `Core/Quests/` |
| `src/lib/spawn.js` | разбор `all.spawn` (заголовок + строки) | `Core/Spawn/` |
| `src/lib/history.js` | undo/redo снапшоты | `Core/History.cs` |
| `src/lib/i18n.js` | словарь RU/EN интерфейса | `.resx` / `LocalizationService` |
| `src/lib/plugins.js` | плагины-экспортёры (`new Function`) | см. §5 риски |
| `src/tabs/*.js` (8 шт.) | вынесенные вкладки `render*Tab(ctx)` | `Views/DialoguesView`, `CharactersView`, `ItemsView`, `IconsView`; остальные — в работе |
| `test/*.mjs` (11 шт.) | юнит-тесты | `XRayForge.Tests` (xUnit) |

---

## 3. Доменная модель

Проект (`.project.json`, `SCHEMA_VERSION = 3`; legacy-ветка `core.js:migrateProject` пишет 4):

```
Project {
  version, projectName, dialogueId, series (soc/cs/cop), theme, gamedataPath, resourcesPath,
  nodes[], edges[], characters[], dialogues[], quests[], generalCommunities[], itemsLtx,
  scripts[], systemLtx, languages[], atlas{ columns, cellSize, cellHeight },
  settings{ importEncoding, exportEncoding, showNodeText, autosaveInterval },
  ltxFiles{path→text}, textures[], itemCategories{}, itemFiles{}, itemOrigins{},
  equipSlots{}, pinnedIcons[], pinCollections{name→[]},
  // поля, наполняемые сканером gamedata: в `.project.json` не пишутся (toPersistable)
  gameRelations, squads[], secrets, spawnSections[], actor, monsters[], npcProfiles[],
  infoPortions[], encyclopedia[], trade, surgeManager, textureMap{}
}
```

Пять «тяжёлых» gamedata-полей (`ltxFiles`, `textures`, `scripts`, `itemFiles`,
`itemOrigins`) хранятся в снапшоте gamedata (в JS — `GAMEDATA_KEY`, в порте —
`gamedata.json` профиля); `toPersistable` вычищает их перед сохранением проекта.
Незнакомые ключи файла не теряются — идут в `JsonExtensionData` (`Project.Extra`),
как в JS они просто остаются свойствами объекта.

Настройки программы (профиль, отдельно от проекта): тема, акцентный цвет,
плотность UI, отображение узлов (текст/id), автосейв (сек), горячие клавиши,
язык интерфейса, syncUrl, плагины.

Ключевые сущности:

- `DialogueNode` — типы: START, PHRASE, CONDITION, QUEST_CHECK, ITEM_CHECK,
  RANDOM, ACTION, END; данные реплики: `phraseId, role, text, text_id, use_id,
  voice_id, expression, action_type, params, metadata`.
- `metadata` — массивы: `has_info, dont_has_info, precondition, give_info,
  disable_info, action`.
- `Edge` — `{ id, source, target, broken }`.
- `Character`, `Language {locale, entries[]}`, `Script`, `Quest`, `Texture`,
  `LtxSection {name, parents[], entries[{key,value,line}]}`.

---

## 4. Соответствие JS → C#/WPF

| JS | C# / WPF |
|---|---|
| `TextDecoder('windows-1251')` + `cp1251Encode` | `Encoding.GetEncoding(1251, ...)` после `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` |
| BOM / XML-`encoding` / auto-detect (`decodeGameBytes`) | тот же алгоритм на `byte[]` |
| `DOMParser` / `@xmldom/xmldom` | `System.Xml.Linq.XDocument` (с обработкой фрагментов и `#include`-строк) |
| `fflate.zipSync` | `System.IO.Compression.ZipArchive` / `ZipFile` |
| `DataView` / `Uint8Array` (DDS) | `byte[]` + `BinaryReader`/`BinaryWriter` (little-endian) |
| SVG-строки (`graphToSvg`, `questChainSvg`, `includeGraphSvg`) | экспорт — оставить генератором строк; интерактив — `Canvas` + `Path`/`Border`/`TextBlock` |
| интерактивный канвас (pan/zoom/marquee, Shift+drag, Ctrl+wheel) | `Canvas` в `ScrollViewer` + `RenderTransform` + обработчики мыши |
| `useState`/`useEffect` | MVVM: `ObservableObject`/`INotifyPropertyChanged` (CommunityToolkit.Mvvm) |
| `createHistory` (undo/redo) | паттерн Command / снапшот-стеки |
| `localStorage` (5 ключей) | `Storage/AppDataService` → `%APPDATA%\XRayForge`: `settings.json`, `current.project.json`, `projects.json`, `backups.json`, `gamedata.json` |
| Electron-диалоги + `fs` | `Storage/ProjectFileService` (`saves/<имя>.project.json`), `Storage/FileSystemService`, `Gamedata/GamedataScanner`; нативные диалоги — `Microsoft.Win32.*Dialog` (этап 5) |
| `tr(locale, key)` | `.resx` (RU по умолчанию, EN fallback) |
| `new Function(...)` для плагинов | см. §5 |
| Tailwind-стили / темы | WPF `ResourceDictionary` + DynamicResource, dark/light |

---

## 5. Риски и ограничения

1. **Плагины-экспортёры** — `plugins.js` выполняет пользовательский JS через
   `new Function`. Варианты: (а) убрать в v1, (б) Roslyn
   (`Microsoft.CodeAnalysis.CSharp.Scripting`) с C#-скриптами, (в) встроенный
   JS-движок. **Требует решения до этапа UI.**
2. **DDS-кодек** — перенесён 1:1 (этап 3): код чисто вычислительный, тесты
   `test/dds.test.mjs` портированы. Ради побайтового паритета сохранены два
   дефекта оригинала: зацикливание `maskInfo` на маске со старшим битом
   (`aMask = 0xFF000000`) и рассинхронизация порядка байтов у 48-битных
   индексов альфы BC4/BC5/DXT5 (см. «Этап 3»).
3. **SVG-экспорт графов** — визуальная фича; можно сохранить генератор строк
   или заменить экспортом PNG через `RenderTargetBitmap`.
4. **3D-предпросмотр `visual`** — не поддерживался и в оригинале (нужны
   OGF/текстуры + WebGL); в WPF пропускаем.
5. **Монолит `App.js`** — при переносе раскладываем на ViewModels/Services.

---

## 6. Целевая структура решения

Создано в папке `dotnet/` (в корне репозитория, рядом с текущим JS-кодом):

```
dotnet/
├─ XRayForge.sln
├─ src/
│  ├─ XRayForge.Core/        # net9.0, чистая логика (перенос src/lib)
│  │  ├─ Core/      (paths, xml-escape, кодировки, миграция проекта)
│  │  ├─ Ltx/       (parser, includes, items, категории, все parse*)
│  │  ├─ Xml/       (string_table, персонажи, локали, info_portions, ...)
│  │  ├─ Dialogs/   (экспорт XML/ZIP, text_id)
│  │  ├─ Graph/     (типы, валидация, раскладка)
│  │  ├─ Imaging/   (DdsCodec)
│  │  ├─ Lua/       (tokenizer, подсветка, сниппеты)
│  │  ├─ Quests/    (парсинг/экспорт, цепочки)
│  │  ├─ Spawn/     (all.spawn)
│  │  ├─ History/   (undo/redo)
│  │  └─ Localization/
│  └─ XRayForge/              # net9.0-windows, WPF
│     ├─ Models/
│     ├─ Services/
│     ├─ ViewModels/
│     ├─ Views/Tabs/
│     ├─ App.xaml, MainWindow.xaml
│     └─ Themes/
└─ tests/
   └─ XRayForge.Tests/        # xUnit — порт test/*.mjs
```

Зависимости: Core → `System.Text.Encoding.CodePages` (Windows-1251); WPF →
`CommunityToolkit.Mvvm`; Tests → xUnit + ссылка на Core.

---

## 7. Этапы миграции

### Этап 0. Каркас и соглашения ✅
- Создано решение `dotnet/XRayForge.sln` и три проекта: `XRayForge.Core`
  (net9.0), `XRayForge` (net9.0-windows, WPF), `XRayForge.Tests` (xUnit).
- Подключены `CommunityToolkit.Mvvm`, xUnit, `System.Text.Encoding.CodePages`.
- Стиль: file-scoped namespace, `nullable enable`, `ImplicitUsings enable`.
- Точка входа: `App.xaml` → `MainWindow.xaml` (1440×900, заголовок «X-Ray Forge»).
- В `.gitignore` добавлены `dotnet/**/bin/` и `dotnet/**/obj/`.

**Итог:** `dotnet build` — 0 ошибок/предупреждений; `dotnet test` — проходит.

### Этап 1. Core: фундамент (history, i18n, core) ✅
- `History/History.cs` — `History<T>` + `HistoryState<T>` (снапшот-стек с лимитом).
- `Localization/LocalizationService.cs` — словарь RU/EN + `Tr()`.
- `Text/Paths.cs` (`Normalize`), `Text/XmlEscaping.cs` (`EscapeText/EscapeAttribute`),
  `Text/GameEncoding.cs` (`Decode`/`EncodeCp1251`, BOM/XML-declaration/auto).
- `Ltx/LtxParser.cs` — разбор LTX (секции, наследование, `#include`).
- `Models/` — `NodeTypes`, `NodeData`, `DialogueNode`, `DialogueEdge`,
  `GraphIssue`, `DialogueGraph`, `Character`, `MapIcon`.
- `Xml/XmlParsers.cs` — `ParseCharactersXml`, `ParseDialogueXml`.
- `Graph/GraphValidator.cs` — валидация графа (итеративный DFS).
- `Validation/CharacterValidator.cs` — проверка персонажа.
- Порт тестов: `HistoryTests`, `LocalizationTests`, `CoreTests` (14 тестов, зелёные).

**Отложено:** плагины (`new Function`) → решение отложено (см. §5).
`migrateProject` (core.js) реализован на этапе 4 — `Projects/ProjectMigration.MigrateLegacy`.

### Этап 2. Core: парсеры данных ✅
- `ltx.js` → `Core/Ltx` (полный парсер, инклюды, секции, `mergedItemEntries`,
  категории, `parseGameRelations`, `parseRatingScales`, `parseSquadDescr`,
  `parseSecrets`, `parseSpawnSections`, `parseMonsters`, `parseActor`,
  `parseSurgeManager`, `parseTrade`, `highlightLtx` → токены для подсветки).
- `xml.js` → `Core/Xml` (string_table, персонажи, `character_desc_general`,
  info_portions, encyclopedia, npc_profile, дескрипторы текстур, `detectLocale`,
  `mergeLanguages`).
- `dialogs.js` → `Core/Dialogs` (экспорт в игровые XML/ZIP, `dialogueToXml`,
  `dialogsLtx`, `validateTextIds`, `findTextIdIssues`, `textIdCoverage`).
- `graph.js` → `Core/Graph` (типы узлов, `safeNode`, `flattenDialogues`,
  `validateGraph`, `layoutDialogue`, `nodeLabel`, `dialogueStats`).
- `quests.js` → `Core/Quests` (парсинг/экспорт, `validateQuests`,
  `computeQuestChains`, condlist).
- `lua.js` → `Core/Lua` (токенизатор, баланс, сниппеты, `validateScriptBindings`).
- `spawn.js` → `Core/Spawn`.
- **Порт всех тестов** `ltx/dialogs/graph/xml/quests/lua/spawn.test.mjs` в xUnit.

**Прогресс от 16.10.2026**: коммиты `feat(stage 2): port data parsers (Lua, Graph, Dialogs, XML, Quests, Spawn)`
и `feat(stage 2): port full ltx.js (text ops, includes, item catalog, validators, highlight)`.

| Порт из | Классы Core | Ключевые точки входа | Тестов |
|---|---|---|---|
| `ltx.js` | `Ltx/`: `LtxParser` (+ модели `LtxEntry/LtxSection/LtxInclude/LtxDocument`), `LtxTextTools`, `IconGrid`, `LtxIncludes`, `ItemCatalog`, `LtxInheritance`, `LtxDomainParsers`, `LtxHighlight` | `Parse`, `SplitLines`, `UpdateLine`, `RemoveSection`, `AddSection`, `AddFieldToSection`, `RemoveFieldByLine`, `RenameSection`, `ReplaceFieldValues`, `ResolveIncludes`, `IsFileLoaded`, `FindIncludeSuggestion`, `IncludeTreeRows`, `IncludeGraphSvg`, `AtlasPresets`, `IconCoords`, `IsValidGridValue`, `IsValidGridWH`, `ClassifyItem`, `ItemFieldProfile`, `ItemFileCategory`, `IsItemFile`, `MergedEntries`, `InheritanceTree`, `ValidateRefs`, `FindDuplicateSections`, `FindInheritanceCycles`, `ParseRatingScales`, `ParseSquadDescr`, `ParseSecrets`, `ParseSpawnSections`, `ParseMonsters`, `ParseActor`, `ParseSurgeManager`, `ParseTrade`, `ParseGameRelations`, `Highlight`/`HighlightLine`/`ToHtml` | 43 |
| `lua.js` | `Lua/`: `LuaModels`, `LuaCatalog`, `LuaTokenizer`, `LuaFormatter`, `LuaAnalyzer` | `Tokenize`, `TokenizeLines`, `CountBrackets`, `LongBracketEnd`, `Format`, `FunctionBlocks`, `Check`, `UndeclaredGlobals`, `DuplicateFunctions`, `Unused`, `BuildCallGraph`, `Completions`, `ValidateScriptBindings` | 13 |
| `graph.js` | `Graph/`: `GraphData`, `GraphLayout`, `GraphSvg`, `DialogueGroups` + полный `GraphValidator` | `SafeNode`, `SafeData`, `SafeMetadata`, `FlattenDialogues`, `SuggestTextId`, `BracketBalanceOf`, `ValidateMetadataSyntax`, `LayoutDialogue`, `NodeLabel`, `RoleColor`, `Stats`, `GraphToSvg`, `DialogueGroup` | 9 |
| `dialogs.js` | `Dialogs/`: `DialogueExport`, `DialogueFiles`, `TextIdTools` | `DialogueToXml`, `DialogsLtx`, `GameFilesFromDialogues`, `ValidateTextIds`, `ResolveTextId`, `FindTextIdIssues`, `Coverage` | 10 |
| `xml.js` | `Xml/`: `XmlHelpers`, `StringTableParser`, `GameXmlParsers`, `GameXmlParsersExtra`, `DialogueImporter` | `XmlAttr`, `XmlTag`, `DetectLocale`, `MergeLanguages`, `ParseGeneralCharactersXml`, `Parse` (string_table), `ParseCharacterDescGeneral` и `…ToXml`, `ParseInfoPortions`, `ParseEncyclopedia`, `ParseNpcProfile`, `ParseTextureDescriptor`, `Import` | 14 |
| `quests.js` | `Quests/`: `GameTasksParser`, `TaskManagerParser`, `QuestExport`, `QuestAnalysis`, `QuestCatalog`, `QuestChainSvg` + `Models/Quest.cs` | `Parse` (game_tasks / task_manager), `QuestsToXml`, `TaskManagerLtx`, `Validate`, `ComputeChains`, `Render`, `ParseCondlistEntry`, `CondlistEntryToStr` | 13 |
| `spawn.js` | `Spawn/`: `SpawnAnalyzer` | `ParseSpawnHeader`, `ReadCString`, `ExtractSpawnStrings`, `AnalyzeSpawn` | 5 |

Плюс модели `Models/{Language,DialogueFileGroup,Quest,XmlDocuments}.cs`; в
`Text/GameEncoding.cs` добавлен `EncodeCp1251`.

**Переименования при порте `ltx.js`** (JS → C#): `validateLtxRefs` →
`LtxInheritance.ValidateRefs`, `findDuplicateSections` → `FindDuplicateSections`,
`findInheritanceCycles` → `FindInheritanceCycles`, `mergedItemEntries` →
`MergedEntries`, `ltxInheritanceTree` → `InheritanceTree`, `highlightLtx` →
`LtxHighlight.Highlight` (модель токенов) + `ToHtml` (паритетная HTML-выдача),
`ITEM_CATEGORIES`/`COMMUNITY_ICONS`/`ITEM_COMMON_FIELDS`/`ITEM_FIELD_PROFILES`/
`FIELD_DESCRIPTIONS` → `ItemCatalog.Categories/CommunityIcons/CommonFields/
FieldProfiles/FieldDescriptions`. Возвращаемые объекты — PascalCase-свойства
(ключи LTX внутри значений по-прежнему snake_case).

**Итог:** `dotnet build XRayForge.sln` — 0 ошибок / 0 предупреждений;
`dotnet test XRayForge.sln` — **121 тест, 0 падений** (`CoreTests` 10,
`LtxTests` 24, `LtxUtilsTests` 19, `LuaTests` 13, `GraphTests` 9,
`DialogsTests` 10, `XmlTests` 14, `QuestTests` 13, `SpawnTests` 5,
`HistoryTests` 3, `LocalizationTests` 1).

**Порт `test/ltx.test.mjs` выполнен целиком** — 24 кейса в `LtxTests.cs`
(плюс 19 собственных кейсов в `LtxUtilsTests.cs` на функции, которые в JS-тестах
не покрыты: правка текста/секций, сетка атласа, инклюды, дерево инклюдов и SVG,
`mergedItemEntries`, категории файлов, размеры справочников, токены подсветки,
кодовые точки эмодзи `COMMUNITY_ICONS`).

**Открытый вопрос (не блокирует этап):** структурный обход `all.spawn`
(все классы `CSE_*`, версия спавна) — текущий `SpawnAnalyzer` даёт заголовок,
читаемые строки и эвристику секций; нужен ли полный обход, решить вместе с
вкладкой спавна (этап 7).

**Критерий:** все Core-тесты зелёные; публичные сигнатуры стабильны.
*(Состояние: 121/121 зелёный — этап закрыт.)*

### Этап 3. DDS-кодек ✅
- `src/lib/dds.js` (337 стр.) → `Core/Imaging/`:
  `DdsCodec` — примитивы (`Rgb565`, `MaskInfo`, `RgbTo565`, `JsRound`);
  `DdsDecode` — `DecodeDxt1/3/5`, `DecodeBc4/Bc5`, `DecodeUncompressed`, `DecodeDds`;
  `DdsEncode` — `EncodeDxt1/3/5`, `EncodeDxtColorBlock`, `SampleBlock`,
  `DownsampleRgba`, `BuildMipChain`, `EncodeDds`; модели — `DdsImage`,
  `DdsMipLevel`, `DdsFormat`.
- Порядок байтов и текст ошибок совпадают с JS (`InvalidOperationException`
  с теми же русскими сообщениями вместо `Error`).

**Прогресс от 16.10.2026**: коммит
`feat(stage 3): port dds.js (decode/encode DXT1/3/5, BC4/BC5, mipmaps)`.

| Порт из | Классы Core | Ключевые точки входа | Тестов |
|---|---|---|---|
| `dds.js` | `Imaging/`: `DdsCodec`, `DdsDecode`, `DdsEncode`, `DdsModels` | `DecodeDds`, `DecodeDxt1/3/5`, `DecodeBc4/Bc5`, `DecodeUncompressed`, `EncodeDds`, `EncodeDxt1/3/5`, `EncodeDxtColorBlock`, `SampleBlock`, `DownsampleRgba`, `BuildMipChain`, `Rgb565`, `RgbTo565`, `MaskInfo` | 93 |

**Отклонения от JS (сознательные):**

1. `maskInfo` — в JS сдвиг `>>` знаковый, поэтому для маски со старшим битом
   (`aMask = 0xFF000000`) цикл подсчёта ширины канала не завершается вовсе.
   Порт работает с `uint` и возвращает `{24, 8}`. Для масок без старшего бита
   результат совпадает с JS.
2. `encodeDxt5` — в JS `best << (i * 3)` переполняет int32, а сдвиг маскируется
   по mod 32: индексы пикселей 11–15 попадают в младшие биты и портят индексы
   пикселей 0–4. Порт накапливает 48 бит в `ulong` и кодирует все 16 индексов
   корректно (`EncodeDxt5_keeps_alpha_index_of_high_pixels`).
3. Чтение за границей буфера — в JS `DataView` молча возвращает `undefined`,
   в порту бросается исключение с тем же текстом, что и у ветки `decodeDds`
   («Обрезанный DXT1.» и т.п.).
4. `pixelsToDataURL` не переносится: в Core нет canvas, конвертация в
   `BitmapSource` живёт в WPF-слое (этап 7).

**Наследованный дефект JS, сохранённый ради паритета:** энкодер пишет 48-битные
индексы альфы BC4/BC5/DXT5 младшими байтами вперёд, а декодер читает их
старшим байтом вперёд (`alphaBits << 8`, сдвиг `45 - i * 3`). При разных
индексах round-trip альфы переставляет пиксели внутри блока. Порт воспроизводит
обе стороны буквально (`DecodeDxt5_expands_alpha_ramp`,
`EncodeDxt5_keeps_alpha_index_of_high_pixels`); согласование сторон возможно, но
нарушит побайтовый паритет с оригиналом.

**Итог:** `dotnet test XRayForge.sln` — **214 тестов, 0 падений**; новых 93:
`DdsTests` 5 (порт `test/dds.test.mjs`: BC4 white block, BC5 R/G,
`downsampleRgba` 2×2, `encodeDds` с/без mipmaps), `DdsPrimitivesTests` 23
(`rgb565`/`rgbTo565`/`maskInfo`), `DdsDecodeTests` 9 (режимы DXT1 с 3 и 4
цветами, обрезка блоков по размеру, нибблы DXT3, уровни DXT5 для `a0 > a1` и
`a0 <= a1`, BC4/BC5), `DdsEncodeTests` 31 (размеры блоков для 1×1 / 2×3 / 5×7,
побайтовые блоки цвета, `sampleBlock` с прижатием к краю, `downsampleRgba` с
округлением половин вверх, mip-цепочка, заголовок `encodeDds`, round-trip
DXT1/DXT3/DXT5/mipmaps), `DdsContainerTests` 25 (32- и 24-битный
`decodeUncompressed`, узкие маски 5-6-5, сигнатура, размеры, обрезка,
`BC4U/BC4S/ATI1`, `BC5U/BC5S/ATI2`, несжатый контейнер).

**Критерий:** декодирование/кодирование DXT1/3/5 и BC4/BC5 с round-trip —
выполнено (DXT1 — побайтово точный на 2-цветных блоках и с толерантностью 5-6-5
на остальных, DXT3-альфа — точная, DXT5 — см. наследованный дефект выше).

### Этап 4. Модель проекта + персистенция ✅
- `Projects/Project.cs` — модель схемы 3 (`Project`, `ProjectSettings`,
  `ProjectAtlas`, `ScriptFile`, `ProjectTexture`, `PinnedIcon`, `TextureMapEntry`)
  и `ProjectSchema` (версии 3/4, лимит бэкапов 10).
- `Projects/ProjectReader.cs` + `Projects/ProjectMigration.cs` — порт `migrate`
  (App.js) и `migrateProject` (core.js): нормализация узлов/рёбер/языков/скриптов/
  квестов/атласа/настроек, незнакомые ключи — в `JsonExtensionData` (`Extra`).
- `Projects/ProjectSerializer.cs` — `.project.json`: camelCase + snake_case-ключи
  как в JS, ключи словарей (пути LTX, имена секций) пишутся без преобразования,
  `UnsafeRelaxedJsonEscaping` (кириллица не эскейпится), `ToPersistable`, `Save`/`Load`.
- `Projects/ProjectIntegrity.cs` — порт `validateProjectIntegrity` (text_id,
  start_dialog/actor_dialog, квесты, битые `#include` system.ltx).
- `Storage/AppDataService.cs` — `%APPDATA%\XRayForge` вместо `localStorage`:
  `settings.json`, `current.project.json`, `projects.json`, `backups.json`
  (лимит 10), `gamedata.json` (снапшоты по имени проекта). `Storage/ProgramSettings.cs`
  — порт `loadProgramSettings` (тема, акцент, плотность, локаль, размер шрифта,
  горячие клавиши) и `comboMatches`.
- `Storage/ProjectFileService.cs` — порт IPC `xray:save-project`:
  `saves/<sanitized-имя>.project.json` (`\w` в ECMAScript-режиме, как в JS).
- `Storage/FileSystemService.cs` — рекурсивное чтение папки (`readDirectoryFiles`),
  нормализация путей, пропуск нечитаемых файлов, `ReadGameText`.
- `Gamedata/GamedataScanner.cs` — порт `processGamedataFiles`: классификация
  `.dds/.ltx/.script/.lua/.xml`, разбор XML (переводы/персонажи/диалоги/квесты +
  контентная детекция), слияние с проектом (персонажи, языки через
  `MergeLanguages`, квесты), пост-обработка LTX (game_relations + scales, squads,
  secrets, spawn_sections, actor, monsters, surge_manager, trade_*), `textureMap`,
  слияние item-файлов в `itemsLtx`/`itemOrigins`, статистика и `skipped`.
  Результат — `GamedataPatch` с `Apply(project)` (аналог `patch(next)`);
  PNG-энкодер текстур подставляется делегатом (canvas в Core нет).

**Отклонения от JS (осознанные):**
1. Прогресс сканирования: в JS `continue` внутри `try` пропускал `done++`,
   поэтому счётчик прогресса почти не двигался; в порте считается каждый файл.
2. `NaN`/`null` в `settings.autosaveInterval` → `0` (в JS — `NaN`, а
   `JSON.stringify` пишет `null`); `null` и нечисловые значения отключают автосейв,
   как и в JS.
3. Не-скалярные значения строковых полей проекта заменяются значениями по
   умолчанию, а элементы массивов, не приводимые к модели, пропускаются (в JS они
   остаются как есть); незнакомые ключи не теряются (`Extra`), а «неразобранные»
   персонажи сохраняют `id`/`name` и остаток полей в `Extra`.
4. `validateProjectIntegrity` в JS сравнивает массив `start_dialog`/`actor_dialog`
   со множеством id и из-за этого всегда сообщает о проблеме; в порте проверяется
   каждый id (иначе проверка бесполезна).
5. `pixelsToDataURL` не переносится — вместо него `GamedataScanOptions.PngEncoder`
   (WPF-слой, этапы 5–7); без энкодера `textures[].dataURL` остаётся `null`.

**Тесты (35 новых, всего 249):** `ProjectTests` 12 (дефолты `emptyProject`, ключи
JSON и snake_case, кириллица без эскейпа, verbatim-ключи словарей, `toPersistable`,
миграция узлов/рёбер/языков/скриптов/квестов/атласа/настроек, толерантность к
битым типам, исключения `Deserialize`, legacy `migrateProject` с удалением
`speaker`, целостность проекта, round-trip реального `saves/*.project.json`),
`StorageTests` 16 (настройки программы и их нормализация, `comboMatches`, текущий
проект, список проектов и его нормализация, лимит бэкапов, снапшоты gamedata,
`resetStorage`, путь `%APPDATA%`, `sanitizeName` + `saves/*.project.json`, три
сценария автосейва), `GamedataScanTests` 7 (классификация и статистика,
item-файлы и `itemsLtx`, пропуски при ошибке чтения, `Apply` только найденных
полей, игнор чужих `.dds`, PNG-энкодер, нормализация путей при чтении каталога).

**Итог:** `dotnet build XRayForge.sln` — 0 ошибок / 0 предупреждений;
`dotnet test XRayForge.sln` — **249 тестов, 0 падений**.

**Критерий:** проект сохраняется и читается в формате JS-версии, старые файлы
мигрируются, профиль перенесён в `%APPDATA%`, сканирование gamedata воспроизводит
`processGamedataFiles` — выполнено.

### Этап 5. Каркас UI + UI по референсам ✅
> Этап 9 («UI по референсам») объединён с этим этапом: визуальный слой делается
> сразу по референсам, а не отдельной итерацией.

- `MainWindow` с левой навигацией/вкладками: Диалоги, Персонажи, Предметы,
  Иконки, Скрипты, system.ltx, Языки, Квесты, Обзор.
- Welcome-окно (создание/открытие/импорт/удаление проектов).
- Настройки программы и проекта; каталог тем **Steel / Olive / Hazard / Amber CRT /
  Cobalt Night / Arctic Light / Graphite** через `ResourceDictionary` (см. «UI по
  референсам» ниже); горячие клавиши (`InputBindings`/`KeyBinding`).
- `ViewModels` с привязками, `RelayCommand`; подключение сервисов этапа 4
  (`AppDataService`, `ProjectFileService`, `GamedataScanner`, `AutosaveService`).

**UI по референсам (скриншоты и шрифты).** Папка `dotnet/docs/ui-references/` —
реестр визуальных референсов, по которым воссоздаётся интерфейс WPF (этапы 5–7).
См. `ui-references/README.md`.

**Решение (16.10.2026):** основная тема — **Steel** (`work.png`, серый металл +
зелёный фосфор) для всего UI; **Olive** (`good-sborka.png`) и **Hazard**
(`fail-sborka.png`) — акцентные состояния для плотных редакторов и экрана
проверки/ошибок, а не отдельные скины. Палитра снята пиксельно и зафиксирована
в `ui-references/tokens/00-palette.tokens.md`.

**Возможности:**
- Анализ скриншотов (PNG/JPG/WEBP): иерархия layout, сетка, отступы и радиусы,
  состояния компонентов, типографика, иконографика.
- Сэмплирование палитры: пиксельные цвета фонов/акцентов/текста (скрипт на
  `System.Drawing`) → токены темы.
- Скачивание шрифтов по прямым URL (`Invoke-WebRequest`) с файлом лицензии;
  альтернатива — NuGet-упаковки (`MahApps.Metro.IconPacks` и др.).
- Перенос токенов в XAML: `Colors.xaml`, `Typography.xaml`, `Controls.xaml`
  (+ `ResourceDictionary` тем dark/light этого этапа).

**Раскладка папки:**
- `screenshots/` — скриншоты (имя `NN_<экран>_<тема>.png`);
- `fonts/` — шрифты `.ttf/.otf` + `LICENSE`, URL в реестре;
- `tokens/` — извлечённые палитры и размеры (`*.tokens.md`).

**Порядок работы:**
1. Пользователь добавляет скриншоты в `screenshots/`.
2. Я анализирую layout/палитру/типографику, фиксирую токены в `tokens/`.
3. Токены → `ResourceDictionary` (этап 5) → применение во вкладках (этапы 6–7).
4. При необходимости скачиваю шрифты по URL и подключаю как `Resource`.

**Что сделано (16.10.2026).** Каркас интерфейса поверх сервисов этапа 4:
- `MainWindow` — свой хром (`WindowStyle=None` + `WindowChrome`): панель инструментов
  (проекты, сохранить, проверить, XML редактора, поиск, сканирование gamedata,
  настройки проекта/программы, справка), левая навигация по девяти
  разделам со счётчиками и акцентными глифами, рабочая область (панель «Обзор» с чипами
  `ProjectSummary` и списком проблем, панель раздела со строками `SectionRow`),
  welcome-оверлей (`welcomeTitle`) и статусная строка с плитками, LED занятости и метками
  сохранения/автосохранения.
- Диалоги: `InputWindow` (порт `prompt`), `SearchWindow` (глобальный поиск),
  `BackupsWindow` (точки сохранения: создать/восстановить/удалить), `SettingsWindow`
  (тема, акцент, плотность, язык, автосейв, шрифт, узлы, синхронизация, плагины,
  горячие клавиши, экспорт/импорт настроек, сброс данных), `ProjectSettingsWindow`
  (имя, `dialogueId`, серия, кодировки, показ узлов, путь и скан gamedata,
  создание/просмотр точек сохранения), `HelpWindow`.
  Все окна — без системного хрома (перетаскивание за шапку, Esc/Enter работают).
- ViewModels: `ShellViewModel` (проект, вкладки, обзор, поиск, копии, скан, экспорт,
  автосейв, `Dispose`), `SettingsViewModel`, `ProjectSettingsViewModel`,
  `TabViewModel`/`ProjectItemViewModel`/`StatusChipViewModel`/`BackupItemViewModel`
  (`ObservableObject` + `RelayCommand` из `CommunityToolkit.Mvvm`).
- Ресурсы: `Themes/Fonts.xaml` (JetBrains Mono из `ui-references/fonts` подключается как
  `Resource` c логическим путём `Fonts/`), `Themes/Controls.xaml` (стили окна, кнопок,
  полей, флажка, списков, `NavButton`, полосы прокрутки), конвертеры
  (`HexBrush`, `LocKey`, `InverseBoolToVisibility`, `ZeroToCollapsed`).

**Отклонения от плана (осознанные):**
- Горячие клавиши обслуживает `MainWindow.OnPreviewKeyDown` через `HotkeyCatalog`
  (`MatchAction`/`TabForNumber`), а не коллекция `InputBindings`/`KeyBinding`: сочетания
  вида `1…4`/`Delete` в `KeyBinding` перехватывали бы ввод в текстовых полях, а
  переназначение из настроек требует их пересборки. Ввод текста не перехватывается
  (`TextBoxBase`).
- `MainWindow.xaml` остался в корне проекта (`App.xaml` без `StartupUri`: окно создаёт
  `App.OnStartup` после применения настроек темы); диалоги лежат в `Views/`.
- Вместо `Storage/ProjectFileService` из плана используется `AppDataService` +
  `ProjectSerializer` (этап 4); плагины-экспортёры и синхронизация — заглушки
  (статусная строка), их движок вне каркаса.
- Разделы-заглушки показывают только чтение данных проекта (`SectionRow`) — редакторы
  приходят на этапах 6–7.

**Критерий:** интерфейс WPF визуально соответствует референсам (layout,
палитра, типографика); все используемые шрифты — с открытой лицензией и
зафиксированным источником.

### Этап 6. Редактор диалогов (граф)
- Граф-канвас: `Canvas` + `ScrollViewer`, `RenderTransform` для pan/zoom,
  `MouseWheel` (Ctrl+wheel — зум), средняя кнопка — панорама, `Shift`+drag —
  рамка выделения, групповое перемещение, мини-карта, «в центр», авто-раскладка.
- Палитра узлов, панель свойств реплики, метаданные («Действия и условия»).
- Валидация (битые связи, дубли ID, циклы), импорт из `game_dialogs`, экспорт.

**Что сделано (16.10.2026).** Каркас вкладки и холст графа (шаг 6.2):
- `DialoguesView` — заголовок раздела и холст: сетка 20px (`radial-gradient` из `ui.grid`
  в масштабируемом слое), связи-кривые Безье (2.5, связи выделенного узла — `#22c55e`),
  карточки узлов 210px с шапкой цвета типа/роли, id, подписью реплики и бейджами
  озвучки/условий/действий, порты `top-10`. Выделение (`border-green-500` + свечение),
  подсветка найденных поиском узлов (`ring-green-400`) и затухание несвязанных элементов
  (`opacity`).
- Панель управления (`aside w-72`): создать диалог, Seed-демограф, поиск по графу,
  «цвет по роли», проверка графа, статистика `dialogueStats` + покрытие text_id
  (`graphStat*`), отмена/повтор, авто-раскладка, «в центр», удаление узла.
- Панель масштаба `⌖ − 100 % +` (`absolute left-4 bottom-4`), клик по карточке выделяет
  узел (`setSelected`), клик по пустому холсту снимает выделение.
- `DialogueGraphViewModel`/`GraphNodeViewModel`/`GraphEdgeViewModel` — состояние графа поверх
  `Core.Graph` (`GraphEditing`, `GraphLayout`, `GraphValidator`, `TextIdTools`), подсветка
  совпадений поиска, история правок на 50 снапшотов (порт `graphHist`), pan/zoom 0.5…2
  с центрированием по размеру холста (`GraphEditing.CenterPan`), горячие клавиши вкладки
  (`undo`, `redo`, `addStart`, `addPhrase`, `addRandom`, `addEnd`, `deleteSelected`).
- Осталось на вкладке (шаги 6.5–6.8): мини-карта, палитра узлов и список загруженных диалогов,
  свойства реплики и метаданные («Действия и условия»), прогон диалога, экспорт SVG,
  полноэкранный холст, импорт `game_dialogs`.

**Отклонения от плана (осознанные, шаг 6.2):**
- В JS пустой граф — просто пустой холст; в WPF по центру показывается подсказка
  (`graphEmptyTitle`/`graphEmptyHint`), иначе пустая вкладка неотличима от сломанной.
- Панель масштаба и подсказка живут вне transform-слоя (в JS это HTML-оверлеи поверх канваса),
  поэтому их размер не зависит от зума — как в оригинале.
- Панель управления пока покрывает только часть `aside w-72`: палитра узлов, список диалогов,
  прогон и экспорт SVG приходят на следующих шагах.

**Что сделано (16.10.2026).** Взаимодействие с холстом (шаг 6.3, обработчики `dragRef` из App.js):
- Перенос узлов: нажатие на карточку и сдвиг в координатах графа (экранная дельта / зум —
  `GraphEditing.DragDelta`), связи пересчитываются на каждом шаге, перенос отмечает проект
  изменённым (`setDirty(true)` в App.js).
- Групповое перемещение: узел группы тянет всю группу, позиции считаются от исходных (`origins`),
  поэтому ошибка округления не накапливается.
- Групповое выделение: Shift+клик переключает узел (`border-blue-500`, синюю рамку перекрывает
  одиночное зелёное выделение), обычное нажатие сбрасывает группу (`multiSelect.length > 1` в JS).
- Панорамирование: drag по пустому холсту (1:1 за курсором, без деления на зум — `PanShift`),
  а также средней кнопкой мыши.
- Рамка выделения: Shift+drag по пустому холсту, рисуется внутри transform-слоя пунктиром
  (`border-blue-400` + `bg-blue-400/10`), на отпускании отбирает накрытые узлы по hit-тесту
  карточки 210×100 (`GraphEditing.MarqueeIds`).
- Масштаб: Ctrl+колесо `ZoomByWheel` (±0.1 за щелчок, границы 0.5…2); кнопки «−»/«+» без изменений.
- Выбор режима нажатия вынесен в чистую функцию `GraphEditing.ResolveDragMode`
  (`None`/`Node`/`Group`/`Pan`/`Marquee`), геометрия — `DragDelta`, `PanShift`, `MarqueeRect`
  (всего 4 новых теста, итого 297).
- Холст захватывает мышь при нажатии, `LostMouseCapture` завершает перенос — аналог
  window-слушателей `mousemove`/`mouseup` в App.js, которые ловят курсор за пределами канваса.

**Отклонения от плана (осознанные, шаг 6.3):**
- Клик по пустому холсту больше не снимает выделение узла: он сбрасывает только групповое
  выделение и начинает панораму — как в JS (в шаге 6.2 стояло `setSelected(null)`; исправлено).
- В JS колесо мыши на холсте диалогов не делает ничего (зум только кнопками). Ctrl+колесо взято
  у общего канваса App.js (`onZoom`), панорама средней кнопкой — добавление WPF (в оригинале
  панорама только левой кнопкой по пустому месту).
- Видимые линии связей не перехватывают мышь (в JS слой рёбер `pointer-events-none`): клик по
  связи начинал панораму — на шаге 6.4 рядом появилась прозрачная hit-зона (`strokeWidth 14`).

**Что сделано (16.10.2026).** Связи (шаг 6.4, `portClick`/`deleteEdge` из `DialoguesTab.js`):
- Создание связи кликами по портам: первый клик взводит порт («Выберите второй порт. Esc — отмена.»),
  второй — по порту другого типа создаёт связь от `out` к `in`, повторный клик по тому же порту
  или по порту того же типа отменяет взведение (`ResolvePortClick`). Готовые записи —
  `GraphPortRef(id, kind)` и `GraphPortClick(source, target)`; дубль и самопетля отсекает
  `AddEdge` (без изменений журнала правок).
- Удаление связи: клик по прозрачной hit-зоне 14 px (`deleteEdge`) убирает связь с записью
  в историю; после создания/удаления статистика `dialogueStats` пересчитывается сразу.
- Esc отменяет взведённый порт (`DialogueGraph.CancelConnect`) до снятия выделения узла.
- Нажатие, попавшее в порт (`Button`) или в hit-зону связи (`Path`), холст не разбирает
  (`IsInteractiveHit`) — панорама с порта/связи не начинается (в JS порты гасят клик).
- Логика вынесена в чистую функцию `ResolvePortClick` (3 новых теста, итого 300).

**Проверка (16.10.2026).** Прогон собранного приложения через UIAutomation на «засеянном»
проекте (3 узла, 1 связь): найдено 5 кнопок-портов, после клика по первому порту появилась
подсказка «Выберите второй порт», клик по второму создал связь (статистика «связей 1» → «связей 2»,
подсказка исчезла). Удаление связи проверено внутри приложения: в середине кривой связи
(295, 254) hit-тест находит прозрачную зону (`hitTestable=True`, `IsInteractiveHit=True`,
холст её не перехватывает), поднятие того же события `MouseLeftButtonDown` удаляет связь
(«связей 2» → «связей 1»). Синтетический клик ОС-уровня в этом окружении до окна приложения
не доходит (инъекция ввода не доставляется), поэтому проверка велась через UIAutomation Invoke
и hit-тест с поднятием события.
Такой прогон требует временного «засева» профиля: `%APPDATA%\XRayForge\current.project.json`
подменяется тестовым проектом и возвращается обратно после проверки — перед запуском стоит
сохранить копию профиля, иначе при обрыве прогона в нём останется сид.

**Исправление (18.10.2026).** UIAutomation Invoke проверял только `Button.Click` и пропускал
маршрут мыши: `OnCanvasMouseUp` на `GraphLayer` всегда помечал `PreviewMouseUp` обработанным,
поэтому отпускание не доходило до кнопки-порта. `Click` не срабатывал, а `Button` оставался
в захвате мыши — приложение выглядело зависшим, связь не создавалась. Теперь холст завершает
жест только при активном перетаскивании (`DialogueGraph.IsDragging`) или собственном захвате
мыши; обычный клик по порту доходит до `Button`. WPF-зонд на собранной вкладке подтвердил
`PreviewMouseUp.Handled=False` для порта; `PortClick` создаёт связь (проверено UIAutomation
Invoke: «связей 0» → «связей 1»).

**Отклонения от плана (осознанные, шаг 6.4):**
- В JS порт — `div` с `onclick`; в WPF это `Button` со стилем `NodePort` (те же 16×16), чтобы порт
  получал клик мышью и с клавиатуры; подсветка при наведении — тот же зелёный, что `hover:bg-green-400`.
- Цвет подсветки порта `#4ADE80` задан константой в стиле `GraphPortHover`: палитра Tailwind
  в WPF не подключена, в шагах 6.2–6.3 хексы так же выписаны явно.
- Удаление связи ловит отдельная невидимая `Path` (`StrokeThickness 14`, `Stroke=Transparent`),
  а видимая линия рисования остаётся некликабельной (`IsHitTestVisible=False`) — иначе широкая
  зона съела бы нажатия карточек и рамки выделения.
- Esc обрабатывается в `ShellViewModel` (сначала отмена связи, затем снятие выделения):
  в App.js незавершённую операцию отменял глобальный `keydown`.

**Что сделано (16.10.2026).** Панели вкладки (шаг 6.5: палитра узлов, список загруженных
диалогов, свойства реплики и метаданные — левый `aside w-72` и правый `aside w-80` из
`DialoguesTab.js`):
- Палитра узлов (`filteredTypes`): поле фильтра (`paletteFilter`) ищет по ключу типа, подписи и
  описанию (регистр не важен), служебные типы (`PALETTE_HIDDEN`) скрыты, порядок — как объявлен
  `NODE_TYPES` (Старт, Реплика, Случайный выбор, Конец). Кнопка палитры — цветной квадрат с иконкой,
  подпись и описание; клик вызывает `addNode(k)`: узел встаёт каскадом, у реплики сразу проставлен
  `phraseId`, узел выделяется. Чистый отбор — `GraphPalette.Items` (порядок, скрытые типы, фильтр).
- Список загруженных диалогов: `project.dialogues` раскладываются по группам
  (`DialogueGroups.Grouped`, префикс id → локация/персонаж/тип), группы сортируются по коду символов
  (`Object.keys(groups).sort()`), заголовок группы — «Бар · 2», кнопка диалога — имя (`name || id`) и
  «N нод · M связей», в заголовке панели — счётчик загруженных диалогов. Клик —
  `loadDialogueIntoGraph`: узлы проходят `safeNode`, связи копируются, `dialogueId` берётся из `dlg.id`,
  выделение снимается; снапшот истории, как и в JS, не пишется (`patch` в App.js её не трогает).
- Свойства реплики (правая панель): заголовок `id · подпись типа` и «Удалить» (`deleteNode`). Для
  `PHRASE` — роль (`unknown`/`npc`/`player`, правка ставит `roleSource: 'manual'`), `phraseId`,
  `text_id` с кнопкой «⤵» (`suggestTextId` из phraseId/dialogueId) и предпросмотром перевода
  (`resolveTextId` → «текст» · локаль), активная локаль + поле перевода (`setTranslation`, запись
  создаётся при необходимости) и кнопка `bindId` («Создать и привязать text_id» / «Создать таблицу
  языка»), fallback-текст; для `CONDITION`/`QUEST_CHECK`/`ITEM_CHECK` — `expression`; для `ACTION` —
  `action_type` (give_info/give_item/take_item/script_call) и `params`; `voice_id` — только у реплик.
- «Действия и условия» (`metaOpen`, свёрнуто по умолчанию) — 6 сниппетов (`META_SNIPPETS`: `+info`,
  `-info`, «выдать info», «снять info», `has_info()`, `give_item`) и 6 многострочных полей
  `META_KEYS`; сниппет дописывает значение в конец списка, поле разбирает текст на строки
  (`split(/\r?\n/)`). Есть у узла любого типа, как в оригинале.
- Правка полей идёт через `patchNode`: меняются данные модели, карточка пересобирает подпись и бейджи,
  проект помечается изменённым (статусная строка и обзор), граф при этом не перезагружается — курсор
  в поле не теряется. Таблицы переводов правятся копированием (`TextIdTools.BindTextId`/`SetTranslation`).
- Логика панелей вынесена в чистые функции Core: `GraphPalette` (порядок/фильтр/сниппеты/текст
  метаданных), `DialogueGroups.Grouped`/`Name`, `TextIdTools.GeneratedTextId`/`ActiveLanguage`/
  `TranslationOf`/`BindTextId`/`SetTranslation` (11 новых тестов, итого 311).

**Проверка (16.10.2026).** Прогон собранного приложения через UIAutomation на профиле с 356
загруженными диалогами (11 узлов / 10 связей в холсте): палитра показывает 4 типа, фильтр «ста»
оставляет только «Старт» и возвращает все 4 при очистке; клик по «Реплика» добавил узел (нод 11 → 12,
реплик 7 → 8) и выделил его — панель свойств показала `phrase_9nj1m0 · Реплика`, `phraseId`
(`bar_dolg_petrenko_start_1`), пустые `text_id`/fallback/voice и 2 списка (роль и локаль); кнопка
«Создать и привязать text_id» проставила `st_phrase_9nj1m0` и подпись «Перевод · eng»; правка перевода
в поле дала предпросмотр ««Hello» · eng»; раскрытие «Действия и условия» и клик по сниппету `+info`
записали `%+quest_done%` в `has_info` (в панели стало 11 полей: 5 свойств + 6 метаданных); клик по
диалогу `aes_strelok_dialog` в списке загрузил его в холст (нод 11 → 9, связей 10 → 8, глубина 8) и
снял выделение. Как и в шаге 6.4, профиль перед прогоном копируется, а после прогона возвращается
обратно (`%APPDATA%\XRayForge\current.project.json`).

Особенности окружения: `AutomationElement.FindFirst` с условием по `Name` и обход родителей
(`TreeWalker.GetParent`) в этом окружении ненадёжны — обвязка делает снимок дерева
(`FindAll(TrueCondition)`), фильтрует по `ControlType` и ищет кнопку по прямоугольнику подписи;
имя кнопки в UIA теряет «_» из подписи (в шаблоне кнопки темы `RecognizesAccessKey=True`, «_» —
мнемоника), поэтому подпись кнопки привязки `text_id` — `TextBlock` внутри `Button`.

**Отклонения от плана (осознанные, шаг 6.5):**
- Локаль для поля перевода выбирается `ComboBox` (в JS — ряд кнопок-локалей); активная локаль всё
  равно одна на панель и хранится в VM (`activePhraseLocale`).
- «Действия и условия» — `Expander` вместо кнопки-переключателя `metaOpen`: состояние по умолчанию
  совпадает (свёрнуто), а клавиатура и мышь работают штатными средствами WPF.
- Панель свойств всегда развёрнута: свёртывание (`propsCollapsed`) и полноэкранный холст
  (`canvasFull`) — шаг 6.6, вместе с мини-картой.
- Кнопки палитры, диалогов и свойства стилизованы локально (`GraphCardButton`, `PropsLabel`,
  `PropsArea`) по мотивам `ui.card`/`ui.hover`: палитра Tailwind в WPF не подключена, хексы и
  поверхности берутся из токенов темы.
- Список загруженных диалогов пересобирается при подмене проекта (`Attach`) и смене данных проекта,
  а не на каждую правку свойств: в JS перерисовка идёт на каждый `patch`, но карточки 356 диалогов
  пересобирать на каждый символ в поле `text_id` незачем.
- Осталось на вкладке (шаги 6.6–6.8): мини-карта, свёртывание панелей (`paletteCollapsed`,
  `propsCollapsed`) и полноэкранный холст (`canvasFull`), прогон диалога, экспорт SVG и импорт
  `game_dialogs`.

**Что сделано (16.10.2026).** Панели и мини-карта (шаг 6.6: `paletteCollapsed`/`propsCollapsed`/
`canvasFull` и блок мини-карты из `DialoguesTab.js`):
- Свёртывание палитры и свойств: кнопки «‹»/«›» в шапках уводят панель в узкую полосу `w-9`
  (36px, стиль `GraphRail`) с одной кнопкой разворота. Состояние — `PaletteCollapsed`,
  `PropsCollapsed`, `ShowsPalettePanel`/`ShowsPaletteRail` (и парные для свойств), команды
  `CollapsePalette`/`ExpandPalette`/`CollapseProps`/`ExpandProps`.
- Полноэкранный холст (`canvasFull`): кнопка «⛶» в шапке палитры убирает обе панели в полосы,
  холст занимает освободившееся место, а сверху справа появляется кнопка
  «🗗 Выйти из полноэкранного» (`ExitCanvasFull`). Глиф переключателя — `CanvasFullGlyph`.
- Мини-карта (`absolute right-4 bottom-4 w-44 h-28`): панель 176×112 у правого нижнего угла холста
  вне transform-слоя. Узлы — прямоугольники `210×90 × scale` в цвете типа (`NODE_TYPES[n.type].color`;
  «цвет по роли» на мини-карту не влияет), выделенный узел — белым контуром 1.5; рамка видимой
  области (`#22c55e`, 0.85) — это `−pan / zoom` размером `холст / zoom`. Клик по панели центрирует
  холст на выбранной точке (`MiniMapClick` из `OnMiniMapClick`).
- Геометрия — чистый Core-класс `GraphMiniMap`: `Build` (границы содержимого `±20`, вписывание
  в панель как `preserveAspectRatio: xMidYMid meet`), `ToGraph`, `CenterPan`. Пересчёт идёт при
  правке графа, выделении, сдвиге, масштабе и смене размера холста (`RefreshMiniMap`), поэтому
  мини-карта скрыта ровно тогда, когда в графе нет узлов.
- Строки локализации `graphCollapsePalette`, `graphExpandPalette`, `graphCollapseProps`,
  `graphExpandProps`, `graphCanvasFull`, `graphCanvasFullExit`, `graphMiniMap` (ru/en);
  локальные стили `GraphRail`, `GraphPanelButton` и кисть `GraphMiniMapViewport` (`#22C55E`).
- Логика — 6 новых тестов (`GraphMiniMapTests`: границы, вписывание, видимая область, обратный
  пересчёт, центрирование), итого 317.

**Проверка (16.10.2026).** Внутрипроцессная обвязка в собранном приложении (профиль с 11 узлами,
обвязка удалена после прогона): мини-карта найдена в визуальном дереве (`Canvas` 176×112, отступ
18/18px от угла холста, то есть 16px `margin` + рамка), её центр перехватывает сам `Canvas`, клик
центрировал холст ровно по расчёту (`pan = 473.636,1602.015` против ожидаемых `473.636,1602.015`,
то есть `холст/2 − zoom × точка`), а рамка видимой области уехала за сдвигом (`−4.601 → −59.085`).
Настоящими кнопками («‹»/«›») палитра и свойства сворачиваются в полосы и разворачиваются обратно;
«⛶» скрывает обе панели, показывает обе полосы и кнопку выхода, холст расширяется 610 → 1088px,
а выход возвращает обе панели. Внешний UIA-смоук (снимок дерева, `FindAll(TrueCondition)`)
подтвердил то же снаружи: «‹»/«›»/«⛶» доступны по UIA, после «‹» TextBlock «Палитра» уходит из
дерева и появляется «Развернуть палитру», «⛶» убирает обе панели и показывает
«🗗 Выйти из полноэкранного», выход возвращает обе панели. Профиль, как и в шагах 6.4–6.5,
копируется перед прогоном и возвращается после (`%APPDATA%\XRayForge\current.project.json`).

Дополнение к особенностям окружения: мини-карта в дереве UIA не видна (`Border`/`Canvas` не
создают automation peer), поэтому её проверяла внутрипроцессная обвязка; в ней же выяснилось, что
`ButtonAutomationPeer.Invoke()` (и UIA `Invoke`) ставит нажатие в очередь диспетчера — без прогона
очереди (`DispatcherFrame` до `Background`) состояние VM ещё не менялось, хотя `Command` был привязан
и `CanExecute` возвращал `true`.

**Отклонения от плана (осознанные, шаг 6.6):**
- Клик по мини-карте центрирует точку с учётом масштаба (`pan = холст / 2 − zoom × точка`): в JS
  масштаб не учитывался (`cvW / 2 − gx`), и при `zoom ≠ 1` выбранная точка уезжала от центра.
- Точка клика переводится в координаты графа по той же геометрии, что и отрисовка (вписывание с
  полями); в JS та же линейная формула применяется к полному размеру элемента, что при несовпадении
  пропорций мини-карты и содержимого давало сдвиг.
- Квирк JS сохранён: под полным экраном кнопка «Развернуть палитру» снимает только
  `paletteCollapsed`, поэтому панель остаётся скрытой до выхода из полного экрана (зафиксировано
  проверкой, а не «исправлено» — поведение оригинала).
- Кнопки панелей — обычные `Button` с подсказками (`ToolTip`) и локальным стилем `GraphPanelButton`,
  полосы свёрнутых панелей — `GraphRail`; в JS это `btn` без подсказок (подсказки добавлены, чтобы
  глифы «‹»/«›»/«⛶»/«🗗» читались).
- Узлы на мини-карте рисуются без скругления (`rx: 6` в SVG — это 6 единиц viewBox, после вписывания
  меньше пикселя): прямоугольник получается визуально тем же.
- Осталось на вкладке (шаги 6.7–6.8): прогон диалога (`playthrough`), экспорт SVG (`graphToSvg`)
  и импорт `game_dialogs`.

**Что сделано (16.10.2026).** Прогон диалога, экспорт SVG и импорт `game_dialogs`
(шаги 6.7–6.8, блок `playthrough` и кнопки из `DialoguesTab.js` + обработчики `App.js`):
- Прогон (`startPlaythrough`/`stepPlaythrough`/`exitPlaythrough`): кнопка «▶ Прогон диалога»
  переключает режим (в прогоне — «■ Завершить прогон» с акцентной подсветкой), старт идёт с первой
  START-ноды (иначе — с первого узла) и выделяет её. На холсте слева сверху появляется панель
  `w-72`: тип и id текущей ноды, её текст (до 160 символов), заголовок списка («Следующие реплики» /
  «Диалог завершён» / «Тупик»), кнопки переходов «→ Тип: текст» (до 80 символов) и счётчик
  «Пройдено нод: N».
- Логика прогона — чистый Core-класс `GraphPlaythrough` (`Start`, `Step`, `Current`, `Choices`,
  `CurrentLabel`, `CurrentText`, `ChoiceLabel`, `StatusKey`); VM держит только состояние
  (`IsPlaythrough`, подписи, `PlaythroughChoices`) и пересобирает панель в `Reload`, поэтому правки
  графа, undo, раскладка и удаление узла сразу отражаются в прогоне.
- Экспорт SVG (`graphToSvg` + `download(..., bom = false)`): кнопка «Экспорт SVG» пишет разметку
  готового `GraphSvg.GraphToSvg` в файл имени диалога (`dialogueId`, иначе имя проекта) в UTF-8 без
  BOM; на пустом графе — только статус «Нет нод для экспорта».
- Импорт `game_dialogs` (шаг 6.8): кнопка «Импорт game_dialogs» в шапке списка диалогов читает
  `*.xml` в кодировке импорта проекта (`GameEncoding.Decode`) и разбирает файл
  `DialogueImporter.ImportFile` — строки `#include` снимаются (как при сканировании gamedata), узлы
  нормализуются и раскладываются (`parseDialogueXml` + `safeNode` + `layoutDialogue`), диалоги с уже
  загруженным id пропускаются, файл-источник пишется в `sourcePath`/`sourceFile`. Список диалогов
  пересобирается сразу, статус — «Импортировано диалогов: N».
- Строки локализации `graphPlaythrough*`, `graphExportSvg`, `graphNoNodesToExport`,
  `graphExportedSvg`, `graphImport*` (ru/en), локальный стиль `GraphPlaythroughButton`,
  `IDialogService.PickOpenFile` и `ShellViewModel.SavesDirectory` (стартовый каталог файловых
  диалогов вкладки).
- Логика — 12 новых тестов (`GraphPlaythroughTests`: старт, шаги, варианты, подписи и статусы;
  `DialogueImportTests`: нормализация с раскладкой, снятие `#include`, отсев уже загруженных id и
  дублей внутри файла, чужой XML), итого 329.

**Проверка (16.10.2026).** Внутрипроцессная обвязка (временный ключ `--stage67`, профиль —
временный каталог, обвязка удалена) и внешний UIA-смоук по реальному профилю (копия перед прогоном,
возврат после). Обвязка: пустой граф даёт «Нет стартовой ноды» и не открывает панель; старт
открывает панель на START-ноде («Старт · s», «Следующие реплики», один вариант «→ Реплика: Привет»,
счётчик 1), шаг в реплику без связей даёт «Тупик», шаг в END — «Диалог завершён», выход очищает
панель и возвращает подпись кнопки. Экспорт записал файл (1765 байт, BOM нет, есть `<svg`, `<path` и
текст «Привет», статус — путь файла), на пустом графе файловый диалог не вызывается. Импорт дал 2
диалога и 1 группу (`sourceFile = dialogs_check.xml`, `imported = true`), повторный импорт —
«В файле нет диалогов», битый XML — «Не удалось прочитать файл диалогов: Некорректный XML диалогов»
без добавления диалогов. Визуальный контроль (настоящая `DialoguesView` в тестовом окне): кнопка
прогона в дереве, фон `#FF303030` → в прогоне `#FF1E5B32` (акцент темы), панель видна только в
прогоне, кнопки вариантов есть в дереве. Внешний UIA: «▶ Прогон диалога», «Экспорт SVG» и
«Импорт game_dialogs» доступны, нажатие прогона открывает панель (текущая нода «Старт ·
bar_dolg_petrenko_start__start», 2 варианта, «Следующие реплики», счётчик), переход по варианту
уводит START-ноду и поднимает счётчик до «2», «■ Завершить прогон» возвращает исходный вид.

**Отклонения от плана (осознанные, шаги 6.7–6.8):**
- Импорт читает один файл `game_dialogs*.xml` со вкладки: в JS диалоги попадают в проект только
  сканированием gamedata. Файловый импорт — надстройка в духе остальных вкладок; в отличие от скана
  (он, как `patch` в JS, заменяет список целиком) импорт добавляет диалоги и пропускает уже
  загруженные id, поэтому повторный выбор файла дублей не создаёт.
- Импортированные диалоги не открываются в холсте автоматически (пересобирается список
  «Загруженные диалоги»): скан gamedata в JS тоже не переключает редактор, диалог открывается
  кликом в списке.
- Прогон не сохраняется в проекте (`playthrough` в JS — тоже только состояние UI) и сбрасывается
  при подмене проекта (`Attach`); пропавшая нода показывается прочерком — как `nodes.find(...)`.
- Панель прогона ограничена по высоте `MaxHeight="300"` вместо `max-h-[70%]`: процент от высоты
  холста в WPF требует привязки к `ActualHeight` холста, а прокрутка списка вариантов важнее.
- Кнопка прогона — одна кнопка с двумя подписями (`PlaythroughButtonLabel` + `DataTrigger` на
  акцент) вместо `playthrough ? accentBtn : btn`; поведение то же.
- Экспорт SVG пишет файл напрямую (`File.WriteAllText`, UTF-8 без BOM) — как `exportXml`
  в оболочке; в JS файл скачивается браузером.

**Шаг 6.9. Игровой экспорт диалогов (18.10.2026).** Порт `exportAllDialogues` из `App.js`
(`gameFilesFromDialogues`):
- `Core/Dialogs/DialogueGameExport.cs` собирает группы диалогов: активный граф
  (`project.nodes/edges` + `dialogueId`) идёт первым, затем `project.dialogues`; повторяющиеся id
  пропускаются. Имя `dialogs_*.xml` берётся из `sourceFile` импортированного диалога
  (`dialogs_bar.xml` → `bar`), поэтому повторный экспорт не разбивает исходный файл на несколько;
  для новых диалогов используется id. Необязательная карта имён по id оставлена для будущего UI.
- `DialogueFiles.GameFilesFromDialogues` дополняет пакет `dialogs.ltx` и `string_table` по всем
  локалям проекта; каталог `config`/`configs` выбирается по серии (`GameSeriesCatalog`).
- `DialogueGameExport.WriteFiles` пишет карту файлов в выбранный каталог. Если выбран сам
  `gamedata`, префикс пути не дублируется; кодировка — `project.settings.exportEncoding`
  (`GameEncoding.Encode`: UTF-8/UTF-16 без BOM или CP1251).
- Команда `ShellViewModel.ExportGameDialogs` (кнопки в тулбаре, обзоре и на вкладке «Диалоги»)
  строит файлы до выбора папки, показывает ошибку графа, подтверждает перезапись существующих
  файлов и пишет статус «Диалоги экспортированы · N» с путём каталога.
- Строки локализации `gameExport*` (ru/en), `GameEncoding.Encode` используется и экспортом
  `items.ltx` (удалён дублирующий `EncodeForExport`).
- Тесты: `DialogueGameExportTests` (группировка по `sourceFile`, активный граф и отсев дублей,
  `dialogs.ltx` + `string_table`, `configs`/UTF-8, пользовательские имена и пустой проект,
  запись в каталог и в сам `gamedata`), `LocalizationTests` и `CoreTests` для кодировок.

**Отклонения от плана (осознанные, шаг 6.9):**
- Экспорт пишет файлы в выбранную папку, а не в ZIP: ZIP-сборка — следующий пункт P0, и общий
  пакет сможет переиспользовать готовую карту `DialogueGameExport.GameFiles`.
- Пользовательский ввод имён `dialogs_*.xml` и выбор локалей пока не вынесены в диалог экспорта:
  имена сохраняются по `sourceFile`/id, локали берутся все из проекта.

### Этап 7. Остальные вкладки

**Шаг 7.1. Персонажи (17.10.2026).** Порт `renderCharactersTab` из `src/tabs/CharactersTab.js`
и обработчиков `App.js` (`autofillCharDialogs`, `extractCharacterXml`, `validateCharacter`):
- `Core/Characters/CharacterCatalog.cs` — чистая логика вкладки: создание персонажа
  (`char_` + base36-время), `name || id`, иконка community (`COMMUNITY_ICONS`), поиск по
  `name/id/bio/visual/community/start_dialog/actor_dialog`, дубликаты id, группировка
  (`community || 'Без группы'`, сортировка групп и карточек по имени), связанные диалоги,
  diff с `original` в порядке полей JS, извлечение блока `specific_character` из `ltxFiles`,
  автосвязь `start_dialog` и `npc_rank` в `Character.Extra`.
- `CharacterValidator` разделён на коды (`CharacterIssue`) и строки: вкладка подставляет
  локализованные подписи (`charWarn*`), а `Validate` сохраняет прежние русские строки и их
  порядок для паритета с JS и старых вызовов.
- `CharactersViewModel`/`CharacterViewModel`/`CharactersViewModels` — состояние и команды:
  `Attach` при смене проекта, `RefreshLocalization`, кэш VM по ссылке на модель (фокус полей
  не теряется при перегруппировке), выбор `selChar` с fallback на первый `project.characters[0]`,
  `Create`, `Autofill`, `Select`, `Delete`, `ShowXml`/`CloseXml`, `OpenRelated`, правка ячеек
  `game_relations`. Правки пишутся прямо в модель и помечают проект изменённым; список, warnings,
  related, diff и портрет пересобираются только по нужным событиям.
- `Views/CharactersView.xaml` — шапка раздела, левая панель (создание, автосвязь, счётчик,
  поиск с placeholder, сортировка, группы с иконками и красной пометкой дубликатов), карточка
  `game_relations` (чипы community с количеством, матрица отношений с редактируемыми ячейками,
  монстры), редактор полей `specific_character`, предупреждения, вырезка портрета из атласа
  (`textureMap` + `textures`, высота по пропорции иконки), связанные диалоги, diff, исходный XML
  и удаление. Для тёмной темы добавлены локальные шаблоны `CharCombo`/`CharComboItem`
  (системный WPF-ComboBox оставлял белый фон и терял контраст).
- Локализация: ключи `char*` (ru/en), включая подписи полей, diff, предупреждений, сортировки,
  XML и статусов. Регистрация вкладки — `CharactersTemplate` и триггер `ActiveTab.Id == "characters"`
  в `MainWindow.xaml`; `ShellViewModel.Characters` собирается из того же проекта, что граф диалогов.
- Тесты: `CharacterCatalogTests` — 17 фактов на создание/id, поиск, группы, дубликаты, related,
  diff, XML, автосвязь, `npc_rank` и коды/строки валидатора.

**Проверка (17.10.2026).** `dotnet build dotnet/src/XRayForge/XRayForge.csproj` — 0 ошибок,
0 предупреждений; `dotnet test dotnet/XRayForge.sln --no-restore` — 346/346. Визуальный и
UIA-прогон собранного приложения на временно подменённом `current.project.json` (профиль
восстановлен из копии) с 2 персонажами, `gameRelations`, `ltxFiles` и диалогами: правая панель
открывается на заголовке редактора (`VerticalScrollPercent = 0`), видны чипы/матрица/монстры,
поля, шкалы и портрет-заглушка; правка `community` в редакторе переносит карточку в другую
группу; поиск «Бармен» оставляет только группу `dolg`, исходный XML
показывает блок `<specific_character id="bar_nimble">`, кнопка связанного диалога переключает на
«Диалоги», автосвязь заполняет `bar_barman_actor`, создание/удаление меняет счётчик 2 → 3 → 2.
Нижняя прокрутка проверена отдельно: связанные диалоги, diff «Ранг 2 → 3» и исходный XML
отображаются.

**Отклонения от JS/плана (осознанные, шаг 7.1):**
- Персонажи, как и в JS, не импортируются отдельной кнопкой вкладки: `project.characters`
  наполняет общий сканер gamedata; вкладка редактирует уже загруженный список.
- Новому персонажу ставится `source = "editor"` (в JS поле не выставляется): модель C# всегда
  сериализует `source`, и маркер не даёт принять ручную запись за `gamedata`.
- Поле «Стартовый диалог» разбирается в список по запятым (`start_dialog[]`), тогда как JS после
  правки пишет в массив строку; для нескольких ссылок это сохраняет связанные диалоги и
  целостность проекта.
- `autofillCharDialogs` заполняет `start_dialog` только когда список пуст. В JS проверка
  `!c.start_dialog` для массива всегда ложна, поэтому кнопка практически не срабатывала;
  в порте исправлено на проверку `Count == 0` (покрыто тестом).
- Портрет рисуется `CroppedBitmap` в `Image` (JS — `background-image`/`background-position`);
  ширина 66 px и высота `round(66 * height / width)` совпадают с оригиналом.
- При показе вкладки правая панель принудительно прокручивается вверх: WPF восстанавливал
  прокрутку `ContentControl` от предыдущего содержимого и прятал заголовок/первые поля.
- Выбранная карточка дополнительно подсвечивается акцентной рамкой (в JS есть только hover и
  красная рамка дубликата) — визуальная надстройка, поведение выбора то же.

**Шаг 7.2. Предметы (18.10.2026).** Порт `renderItemsTab` из `src/tabs/ItemsTab.js`:
- `Core/Ltx/ItemEditor.cs` — чистая логика вкладки: группировка секций по категориям,
  поиск по имени и полям (`key=value`), фильтр категории, сортировка по имени или количеству
  полей, карта секций, цепочка наследования, битые родители, признак переопределения поля,
  дочерние секции, ссылки по `immunities_sect`/`bones_koeff_protection`/`hit_fraction`,
  подсказки значений (до 30), замена поля с сохранением комментария, генерация `new_item_xxxx`
  и CSV-экспорт с экранированием.
- `ItemsViewModel`/`ItemsViewModels` — состояние и команды: `Attach`/`RefreshLocalization`,
  сохранение выбранной секции по имени после пересборки, создание/переименование/удаление,
  undo/redo через историю `Mutate`, массовая замена значений, правка и удаление полей,
  разблокировка корневой секции (🔒) и унаследованных полей, быстрые поля по профилю
  категории, дерево наследования с переходом по родителям, блок ссылок, атлас с сеткой
  `inv_grid_x/y` и слотами экипировки, экспорт `items.csv` (UTF-8 с BOM) и `items.ltx`
  в кодировке `project.Settings.ExportEncoding`.
- `Views/ItemsView.xaml` — шапка раздела, левая панель (создание, экспорт CSV/LTX,
  переименование, удаление, undo/redo, счётчик, поиск с placeholder, фильтр категорий,
  сортировка, группы с карточками и категориями), правая панель с карточками
  `INHERITANCE_CARD`, `ATLAS_CARD`, `QUICK_CARD`, `FIELDS_CARD`, `TREE_CARD`, `REFS_CARD`,
  `EQUIP_CARD`; поля блокируются для корневой секции, унаследованные помечаются замком,
  переопределённые — янтарной рамкой; пустые состояния и подсказки совпадают с JS.
- Для тёмной темы добавлены локальные шаблоны `ItemCombo`/`ItemComboItem` (системный
  WPF-ComboBox оставлял светлый фон), `ItemFieldInput`, кнопки карточек, чипов и атласа.
- `Views/ItemsView.xaml.cs` — при показе вкладки и `Loaded` прокрутка `DetailsScroll`
  сбрасывается вверх, чтобы заголовок и первые поля не оставались за экраном.
- Локализация: ключи `item*` (ru/en), включая карточки, фильтры, быстрые поля, атлас,
  слоты, пустые состояния, экспорт и статусы; `itemRemoveField` добавлен в оба словаря.
- Тесты: `ItemEditorTests` — 7 фактов на группы/поиск/сортировку, наследование и битых
  родителей, переопределения, ссылки, сохранение комментария, подсказки, CSV и генерацию имени.

**Проверка (18.10.2026).** `dotnet build dotnet/src/XRayForge/XRayForge.csproj` — 0 ошибок,
0 предупреждений; `dotnet test` — 353/353 (346 прежних + 7 новых). Дополнительно выполнен
UI-прогон собранного приложения на временном проекте (`items_smoke`): 7 LTX-секций с цепочкой
`wpn_ak74 → wpn_base`, битым родителем и ссылкой `immunities_sect`, PNG-атлас 4×2 и слоты
`weapon`/`armor`/`helmet`. UI Automation подтвердил все семь карточек, группы и счётчики,
выбор `wpn_ak74`, наследование/переопределения/замок унаследованного поля, подсветку текущей
ячейки атласа и вырезанные иконки слотов. Прокрутка `DetailsScroll`: 0% → 100% → 0% после
переключения на другую вкладку и обратно (сброс при показе вкладки работает).

**Отклонения от JS/плана (осознанные, шаг 7.2):**
- `SetGrid` после записи `inv_grid_x` заново разбирает LTX перед записью `inv_grid_y`:
  добавление первого поля сдвигает строки, и сохранённый номер строки в JS становится неверным.
- Выбранная карточка дополнительно подсвечивается акцентной рамкой (в JS только hover) —
  визуальная надстройка, поведение выбора то же.
- При показе вкладки правая панель принудительно прокручивается вверх: WPF восстанавливал
  прокрутку `ContentControl` от предыдущего содержимого.

**Шаг 7.2a. Библиотека данных и автодополнение (18.10.2026).**
- `Core/Suggestions/SuggestionEntry.cs` — тип подсказки (`Item`, `Section`, `Class`, `Field`,
  `Script`, `ScriptFunction`, `ScriptBinding`, `LuaApi`, `TextId`, `Dialogue`, `Character`,
  `Community`, `Visual`, `Icon`, `Rank`, `Reputation`, `InfoPortion`, `Quest`, `SpawnSection`,
  `Monster`, `NpcProfile`, `Squad`, `Texture`, `Value`), значение, пояснение и глиф.
- `SuggestionMatcher` — регистронезависимый поиск с приоритетом: начало значения → начало
  сегмента (`bind_`, `ammo.`, `wpn/`) → вхождение в середину; дедупликация и лимит.
- `SuggestionLibrary.Build(project, itemSections)` — единый индекс без повторного разбора
  `ItemsLtx`: секции предметов и `system.ltx`, `class`/поля, script-файлы, функции и готовые
  `script_binding`, Lua API/глобалы/ключевые слова, `text_id` из `string_table` и узлов,
  диалоги, персонажи, сообщества, визуалы, иконки `textureMap`, шкалы, квесты, инфопорции,
  спавн-секции, монстры, NPC-профили, отряды и текстуры. `ForItemField` подбирает источники
  по имени поля (`inv_name` → text_id, `ammo_class` → боеприпасы, `immunities_sect` →
  секции, `$spawn` → спавн, `script_binding`/`logic` → скрипты), а `ForCharacterField` —
  по полям персонажа; `ForMetadataField` — info-порции для `*_info` и скрипты для
  `precondition`/`action`; `ForLua` готов для будущего Lua-редактора. В конец всегда
  добавляются уже встречавшиеся значения поля (прежний JS-`datalist`).
- `Controls/SuggestionTextBox` — WPF-контрол с popup: ↑/↓, Enter/Tab, Esc, клик мышью,
  вставка только текущего токена (поля через запятую/перевод строки), тема
  `SurfacePanel`/`AccentMain`. Подключён ко всем полям предметов, к `rank`/`reputation`/
  `npc_rank`/`icon`/`visual`/`start_dialog` персонажей, а также к `text_id`, выражению
  CONDITION и метаданным реплики в графе диалогов. Стиль `SuggestionItemTemplate` добавлен
  в `Themes/Controls.xaml`.
- Тесты: `SuggestionLibraryTests` — 6 фактов на индекс/дедупликацию, источники полей
  предметов, персонажей и метаданных, ранжирование, фильтр типов и fallback `itemFiles`.
- UI-проверка: временная цепочка `new_item_ab7z → base_item`; после ввода `new_item_t`
  popup показал `new_item_tvcd`, Enter вставил значение в поле. `current.project.json`
  восстановлен из резервной копии после прогона.

**Шаг 7.2b. Скан реальной gamedata и восстановление снапшота (18.10.2026).**
- Причина «сканирование не происходит»: `FileSystemService.ReadDirectory` читал
  `File.ReadAllBytes` **каждого** файла выбранной папки. В реальной SoC-gamedata это 25 684
  файла / 6.15 ГБ (в CoP-моде — 36 922 файла), поэтому скан зависал на чтении звуков,
  моделей и DDS задолго до разбора. Теперь `GamedataFile` хранит ленивый загрузчик и читает
  содержимое при первом обращении, как `file.arrayBuffer()` в JS; `ReadDirectory` только
  перечисляет пути. Обход каталога пропускает недоступные папки и reparse-point'ы без
  символических циклов, а ошибки попадают в `Skipped`.
- Снапшот gamedata (`gamedata.json`) сохранялся, но не применялся: `ShellViewModel`
  загружал только `current.project.json`/`projects.json`, где «тяжёлых» полей нет. Добавлен
  `GamedataSnapshot.ApplyTo` (аналог `Object.assign({}, proj, snap)`) и восстановление
  при старте, «Продолжить» и открытии проекта из списка. `ToSnapshot` теперь всегда пишет
  пустые коллекции вместо `null`, а `ApplyTo` не затирает данные `null`-полями.
- Реальные XML X-Ray часто содержат комментарии `<!--- … -->` с двойным дефисом: `@xmldom`
  в JS их пропускает, а `XDocument` считал файл битым. Из-за этого в SoC пропускались
  117 файлов персонажей (загружалось 88 вместо 889). `XmlHelpers.CleanXml` вырезает
  комментарии перед `TryParse`/`ParseCharactersXml`/`ParseDialogueXml`; оставшиеся
  `Skipped` — это include-фрагменты (`character_items.xml`, `character_drugs.xml` и т.п.),
  которые и в JS не являются самостоятельными XML-документами.
- После скана пересобираются `DialogueGraph`, `Characters` и `Items` (вместе с библиотекой
  подсказок), обновляются обзор, строки разделов и глобальный поиск — новые персонажи,
  диалоги и предметы видны сразу, без перезапуска. Исключение скана больше не уходит из
  `async`-команды: статус «Не удалось просканировать gamedata» и сообщение с причиной.
- Тесты: `Read_directory_defers_file_reads_until_content_is_requested` (ленивое чтение),
  `Gamedata_snapshot_restores_heavy_fields_without_wiping_when_empty` (восстановление
  `ltxFiles`/`scripts` и безопасные `null`), `ParseCharactersXml_tolerates_invalid_xml_comments_like_real_gamedata`.
- **Проверка (18.10.2026).** `dotnet build dotnet/src/XRayForge/XRayForge.csproj` — 0 ошибок,
  0 предупреждений; `dotnet test` — 362/362. Сквозной прогон Core на реальных путях:
  `D:\stulk_modding\STALKER ТЧ\gamedata` — 25 684 файла, 964 LTX, 442 скрипта, **889 персонажей**,
  213 диалогов, 28 item-файлов, 3 иконки, 22 языка (8 079 строк), 164 квеста, 79 пропусков,
  **0.6 с / 49 МБ**; `D:\stulk_modding\stulker_mod\gamedata` — 36 922 файла, 1 684 LTX,
  273 скрипта, **414 персонажей**, 502 диалога, 83 item-файла, 33 иконки, 1 язык (8 371 строка),
  35 пропусков, **1.0 с / 97 МБ**. Снапшот после скана восстанавливает `ltxFiles`, `scripts`,
  `textures`, `itemFiles` и `itemOrigins`.
- Дополнительно выполнен внутрипроцессный прогон `ShellViewModel` (тот же `ScanGamedataCommand`,
  временный профиль, фейковый `IDialogService`, реальный `PngDataUrlEncoder`): скан SoC дал
  889 персонажей / 213 диалогов / 28 item-файлов / 964 LTX / 3 иконки / 22 языка / 164 квеста,
  после `SaveCommand` новый экземпляр `ShellViewModel` восстановил те же счётчики и
  `gamedataPath = gamedata` — путь «скан → вкладки → перезапуск» работает целиком.

**Шаг 7.2c. Каталог gamedata (`catalog.csv`) как источник автодополнения (20.10.2026).**
- `Core/Gamedata/GamedataCatalog` — ленивый разбор встроенного `catalog.csv` (3 272 файла CoP/SoC:
  проект, путь, тип, размер, строки, ключевые элементы, аналог и MD5). CSV с разделителем `;`,
  кавычками и переводами строк; BOM заголовка и CRLF нормализуются. `ForSeries` фильтрует
  `soc`/`cop`; для `cs` и неизвестной серии отдаёт обе серии, чтобы подсказки не исчезали.
  Файл лежит в корне репозитория; без него сборка не падает, но каталог остаётся пустым.
- `GamedataCatalog.Suggestions` — LTX-секции, пути `#include`, Lua-скрипты, функции и
  `script_binding`, `text_id` из `string_table`, XML id и диалоги. Подписи-счётчики (`строк:`,
  `функций:`, `(+12)`) и копии (`~`, «копия») отбрасываются; результат кэшируется по серии.
- `GamedataCatalog.IncludeSuggestions` — только тип `Include`; `ConfigRelativeLtxPath` приводит
  путь к виду `configs\...`/`config\...` → `weapons\...`, который принимает `#include`.
- `SuggestionLibrary.Build(..., catalog)` и `ForInclude()` — значения проекта добавляются первыми,
  каталог — после них и без дублей; каталог можно подменить в тестах.
- `SystemLtxViewModel` (вкладка `system.ltx`) — `IncludeSuggestions` пересобираются в `Attach`
  по `project.Series`, каталог дополняет `AvailableLtxPaths`. `SystemLtxView` использует
  `SuggestionTextBox` с разделителями `\r\n,": `, поэтому подсказка внутри `#include "…"`
  заменяет только путь, не задевая кавычки и префикс команды.
- Тесты: `GamedataCatalogTests` — 8 фактов (CSV/кавычки/BOM, серии, нормализация, LTX/Lua/XML,
  include-only, встроенный каталог обоих серий, приоритет проекта над каталогом).
- **Проверка (20.10.2026).** `dotnet build` Debug/Release — 0 ошибок, 0 предупреждений;
  `dotnet test` — **419/419**. На встроенном каталоге CoP: 3 151 значение типа `Section`
  (LTX-секции и XML id), 1 395 include, 261 скрипт, 252 функции, 63 `text_id`.

**Шаг 7.3. Вкладка «Иконки» (`IconsTab.js`).**
- Core: `Imaging/AtlasEditor.cs` — `RgbaImage`, `AtlasGrid` (размер ячейки, вычисленные
  колонки/ряды, hit-test/клампинг выделения), `AtlasPixels` (crop/fill/alpha/
  paste/flip/rotate, отпечатки 8×8 и поиск дубликатов, спрайт-лист с зазором 2 px),
  `RgbaColor` (hex `#RGB`/`#RRGGBB`/`#RRGGBBAA`). `AtlasPreset` содержит только размер
  ячейки — без фиксированного числа колонок.
- WPF: `Controls/AtlasCanvas.cs` — nearest-neighbor атлас, сетка, overlay сравнения,
  выделение drag'ом, hover-подсветка, панорама средней кнопкой через `ScrollViewer`,
  Ctrl+колесо; `ViewModels/IconsViewModel.cs` + `IconsViewModels.cs` — загрузка DDS,
  сравнение, пресеты, коллекции закреплённых, дубликаты, undo/redo выделения, пиксельные
  операции и экспорт; `Views/IconsView.xaml(.cs)` — тёмная левая панель и холст с лупой;
  `Services/RgbaImageCodec.cs` — RGBA ↔ WPF `BitmapSource` и PNG-байты.
- Сетка строится только от размеров ячейки: колонки = `floor(width / cellW)`, ряды =
  `floor(height / cellH)`; отдельного поля «Колонок» нет. `project.atlas.columns`
  остаётся в схеме как производное значение.
- Все DDS из `textures[]` открываются вкладками над холстом (как документы в Photoshop):
  крестик скрывает вкладку только на время сессии, атлас остаётся в `textures[]` и
  доступен «Персонажам»/«Предметам»; кнопка «↺» возвращает закрытые вкладки, «+» —
  загружает DDS. Выбор сравнения — из открытых вкладок. Загрузка DDS, сравнения и
  пиксельные операции обновляют выбранный атлас, не удаляя остальные. Пустой атлас
  создаётся по ширине/высоте в пикселях; отмена любого prompt не создаёт изображение.
- Холст получает явные `Width/Height` = пиксели × zoom, поэтому масштабирование не
  растягивает изображение по одной оси. В «Предметах» показывается вырезанная иконка
  (`inv_grid_x/y` + `inv_grid_width/height`) в масштабе 1:1 с атласом: превью равно
  `inv_grid_width/height` × размер ячейки, поэтому 2×1 шире 1×1, а 2×2 больше по обеим
  осям и не подгоняется под квадрат 88×88. Поля ищутся с учётом наследования
  (`IconGrid.ResolvePlacement`), координаты и размер выводятся рядом только при заданной
  позиции; слоты экипировки вырезают иконку тем же размером, а не 1×1. Сетка атласа
  раскрывается кнопкой «Выбрать иконку» и строится только по требованию.
- Интеграция: `ShellViewModel.Icons` пересобирается при старте, открытии проекта и скане
  gamedata; `MainWindow` переключает `IconsTemplate`; ключи `icon*` добавлены в RU/EN.
- Тесты: `AtlasEditorTests` — сетка/выделение, crop, fill/alpha, paste с обрезкой,
  flip/rotate, дубликаты, спрайт-лист, hex-цвета; `ItemEditorTests` — наследование
  `inv_grid_*` и обязательность обеих координат. `dotnet test` — **375/375**.
- **Проверка (18.10.2026).** WPF-прогон на реальных атласах
  `ui_icon_equipment.dds` (1024×2048) и `ui_npc_monster.dds` (1024×512):
  сетка **20×40** и **20×10** только по ячейке 50×50; 238 ячеек-дубликатов; сравнение,
  закрепление/перезакрепление, PNG/выделение/спрайт-лист и DDS DXT1+mip записаны.
  `AtlasCanvas` при zoom 1 — 1024×2048, при 0.5 — 512×1024 (пропорции сохранены).
  Загрузка третьего DDS дала `textures=3`, отмена создания пустого атласа — 3→3,
  удаление сравнения — 3→3. Иконка `bread_a` вырезана по `inv_grid_x=11, inv_grid_y=9`;
  сетка «Предметов» открывается кнопкой. После `SaveCommand` новый `ShellViewModel`
  восстановил `textures=2` и `pinned=1`; `IconsView` отрисован в окне 1264×761 без
  ошибок XAML. Скан SoC через `FileSystemService` + `GamedataScanner` дал `textures=3`,
  `atlas=True`, сетку 20×40. Отдельный smoke-стенд вкладок: три атласа → три вкладки;
  клик по крестику (через `ButtonAutomationPeer`) дал 3→2 вкладки при `textures=3`;
  закрытие всех — 0 вкладок и пустой холст; «↺» вернула три вкладки, `textures` остался 3.
  Для `guitar_a` (4×2, ячейка 50) превью **200×100** без искажения, координаты
  `inv_grid_x=10 · inv_grid_y=19 · 4×2`; `ItemsView` отрисован в окне 1264×800 без ошибок XAML.
- Отклонение от JS: `rotateSelection` в `App.js` фактически транспонировала область;
  в порте `AtlasPixels.Rotate90` выполняет настоящий поворот на 90° (тест фиксирует это).

**Что сделано (18.10.2026).** Стартовое состояние, бесконечный холст диалогов и раскладка слотов:
- Приложение больше не восстанавливает `current.project.json` молча при запуске: старт
  пустой, `projectName`/серия/gamedata не заполнены, welcome-экран открыт. «Продолжить»
  и открытие проекта из списка по-прежнему загружают сохранённое состояние. Автосейв не
  пишет пустышку (`AutosaveService` получает `null`, пока проект не открыт), `Save` без
  проекта — no-op.
- Навигация и `Ctrl+1…9` заблокированы до открытия проекта (`IsProjectOpen`); кнопка
  «Закрыть» на welcome-экране показывается только при открытом проекте. Удаление текущего
  проекта и сброс профиля возвращают стартовое пустое состояние.
- Холст диалогов: квадратная подложка 20000×20000 заменена на `GraphCanvasBackground` —
  бесконечную точечную сетку в viewport, выровненную по `pan`/`zoom`. Панорама ловится и
  на фоне, поэтому граф можно вернуть даже после того, как все карточки уехали за экран;
  захват мыши и Ctrl+колесо работают и на фоне, и на transform-слое.
- Связи на холсте: `OnCanvasMouseUp` завершает жест только если холст ведёт перетаскивание
  или сам захватил мышь. Иначе `PreviewMouseUp` съедал отпускание до кнопки-порта: `Click`
  не срабатывал, кнопка оставалась в захвате, и приложение выглядело зависшим.
  WPF-зонд: для порта `PreviewMouseUp.Handled=False`; UIAutomation Invoke создаёт связь.
- Слоты экипировки: `equipSlotLayout[]` (ключ, подпись, x/y/width/height) с дефолтами по
  референсам `slots-for-SoC/CS/CoP.png` (3/4/5 слотов), общий прямоугольник и кнопка
  «Настройка слотов». Окно `EquipSlotsWindow` позволяет выбрать слот, править подпись,
  позицию и размер, добавлять и удалять слоты; «Отмена» откатывает снимок, «ОК» пишет
  раскладку в проект и удаляет назначения удалённых слотов. Иконки предметов вырезаются
  из атласа по `inv_grid_*` с учётом наследования.
- Тесты: `EquipSlotLayoutTests` — дефолты серий, габариты поля, нормализация/клампы,
  round-trip `equipSlotLayout`. `dotnet build` — 0 ошибок, `dotnet test` — **382/382**.

**Темы и старт со сканирования gamedata (18.10.2026).**
- `ThemeCatalog` расширен до семи тем: Steel + Phosphor, Olive Panel, Hazard Red,
  Amber CRT, Cobalt Night, Arctic Light и Graphite. Для светлой Arctic Light
  `TextInverted` — тёмный (текст на акцентной кнопке), поэтому контраст WCAG
  сохраняется. В `ThemeTokens` добавлены `ChromeBackdrop` и `GridLine`, так что
  панель инструментов и сетка графа перекрашиваются вместе с темой.
- В настройках программы список тем показывает превью палитры (поверхность/панель/
  акцент) и локализованное описание; `light` в старых проектах теперь означает
  Arctic Light, `dark`/`auto` — Steel. Все семь тем проверяются тестом контраста.
  В акцентные пресеты добавлены `amber`, `teal` и `silver`, чтобы подпись
  выбранного акцента совпадала с цветом темы по умолчанию.
- Welcome-экран без открытого проекта получил акцентный блок «Начните с gamedata».
  `ScanGamedataCommand` в этом состоянии создаёт пустой проект, берёт имя из папки
  (для `...\STALKER ТЧ\gamedata` — «STALKER ТЧ»), сканирует данные, открывает и
  сохраняет проект; при открытом проекте поведение скана прежнее.
- **Проверка (18.10.2026).** `dotnet build dotnet/XRayForge.sln` — 0 ошибок;
  `dotnet test` — **383/383**. In-process прогон `ShellViewModel` с временным профилем
  и фейковым `IDialogService` на `D:\stulk_modding\STALKER ТЧ\gamedata`:
  `open=False welcome=True` → `project=STALKER ТЧ open=True welcome=False`,
  889 персонажей / 213 диалогов / 964 LTX, снапшот и `current.project.json` записаны.
  `SettingsWindow` загрузилось, все 7 тем применились к `Application.Resources`.

**Точки сохранения в настройках проекта (18.10.2026).**
- Из глобального тулбара и быстрых действий обзора убраны кнопки «Точки сохранения»/
  «Точка сохранения». В `ProjectSettingsWindow` добавлен блок «Точки сохранения»:
  создание снимка, открытие истории и счётчик; `ProjectSettingsViewModel` проксирует
  `ShellViewModel.AddBackupCommand`/`OpenBackupsCommand`, поэтому логика профиля не
  дублируется. После восстановления снимка из открытого списка окно перечитывает поля,
  чтобы «ОК» не записал значения заменённого объекта проекта.
- Локализация: `openBackups` и `backupsHint` (ru/en). In-process WPF-зонд с временным
  профилем подтвердил загрузку окна, обе привязки команд, рост истории `0 → 1` и
  перечитывание полей после восстановления снимка; `dotnet build` — 0 ошибок,
  `dotnet test` — **383/383**.

**Исправления редактора (18.10.2026).**
- Удаление проекта из welcome-списка очищает `current.project.json`, если он указывает
  на этот проект. `LoadResumableProject` дополнительно сверяет имя со списком профиля,
  устаревший файл удаляет, а кнопка «Продолжить» скрыта, когда продолжать нечего.
  Регрессия: `StorageTests` — устаревший/удалённый текущий проект очищает файл,
  удаление другого проекта текущий файл не трогает.
- Смена пресета/размера ячейки на вкладке «Иконки» теперь сообщает вкладке «Предметы»
  (`ShellViewModel.OnAtlasChanged`). Там пересчитываются `SelectedItemIcon` и слоты
  экипировки, поэтому пресет 32×32/64×64/128×128 сразу применяется к вырезкам.
  Размеры ячейки берутся из одного `ItemsViewModel.AtlasCellSize`; пикер использует
  те же `cellSize`/`cellHeight`, что и вырезанная иконка (без растягивания остатка
  атласа), а размеры берутся из фактического bitmap, если метаданные текстуры неполные.
- `GraphPanelButton` (кнопки сворачивания панелей, полноэкранного холста и закрытия
  оверлеев) наследует тёмный шаблон `Button`; системные белые кнопки в панелях
  «Диалогов» больше не появляются.
- Базовые кнопки получили скругление 4, вкладки левой навигации — 6; подписи вкладок
  используют `DensitySmallFontSize` и `SemiBold`, как `text-xs font-semibold` в JS.
- **Проверка (18.10.2026).** `dotnet build dotnet/XRayForge.sln` — 0 ошибок;
  `dotnet test` — **386/386**. WPF-зонд на временном профиле: выбранный предмет
  `100×50` после пресета 32×32 стал `64×32`; `GraphPanelButton` наследует базовый
  тёмный стиль; у `NavButton` заданы `DensitySmallFontSize`/`SemiBold`; радиус
  базовой кнопки — 4.

**Debug/Release UI (18.10.2026).**
- `BuildFeatures.IsDebug` (`#if DEBUG`) отделяет инструменты разработки от релизного
  интерфейса. В Release скрыты кнопки «Экспорт XML редактора» (тулбар и быстрые
  действия обзора) и «Демо-граф» (welcome-экран и панель «Диалоги»), а команды
  `ExportXml`/`LoadDemoGraph` не выполняются.
- `HotkeyCatalog` в Release не показывает `exportXml` в настройках и не реагирует на
  `Ctrl+E`: действие остаётся только в Debug вместе с остальными инструментами
  разработки. В Debug поведение прежнее.
- Проверка (18.10.2026): `dotnet build` и `dotnet build -c Release` — 0 предупреждений;
  `dotnet test` в Debug и Release — **387/387** (тест
  `HotkeyCatalogResolvesBindingsAndReportsDuplicates` учитывает конфигурацию).

**Скрипты — вкладка Lua-редактора (этап 7.4, 19.10.2026).**
- Перенесён `ScriptsTab.js`: `Core/Scripts/ScriptsCatalog` (фильтр и сортировка списка,
  создание/переименование/удаление, замена во всех файлах, поиск определений,
  вставка в каретку, форматирование, разбор строк для подсветки) и
  `ScriptsViewModel` со списком `.script`/`.lua`, открытием/сканированием/экспортом
  файлов, diff «оригинал / текущая», а также
  проверкой Lua (`LuaAnalyzer`: ошибки, необъявленные глобальные, дубликаты функций),
  скобками, графом вызовов, неиспользуемыми функциями и переходом к определению
  (в том числе в другом файле).
- `ScriptsView` повторяет раскладку JS-вкладки: левая панель с поиском, заменой,
  справочником `LUA_API` и сниппетами; справа — редактор с нумерацией строк и
  подсветкой токенов прямо во время ввода (`Controls/LuaEditor`). Отдельный режим
  просмотра и свёртка функций удалены по требованию: цветной текст, каретка,
  выделение, прокрутка и собственные undo/redo работают в одном редакторе.
  Ширина панели сохраняется в настройках
  (`scriptsPaneWidth`) через `GridSplitter`.
- `MainWindow` регистрирует `ScriptsTemplate` и активирует его по `ActiveTab.Id ==
  "scripts"`; добавлены RU/EN-строки всех элементов вкладки и подсказка навигации.
  `LocalizationTests.Scripts_tab_keys_exist_in_both_locales` проверяет наличие каждого
  ключа в обеих локалях.
- **Проверка (19.10.2026).** `dotnet build dotnet/XRayForge.sln` в Debug и Release —
  0 ошибок; `dotnet test` — **406/406**. WPF-зонд с временным профилем загрузил
  `ScriptsView`, выбрал тестовый скрипт (9 строк, 2 функции, подсветка и undo),
  построил diff и подтвердил создание вкладки через `ScriptsTemplate` в `MainWindow`
  (`active=scripts`) без ошибок привязок. Ширина `scriptsPaneWidth=421`
  восстанавливается из `settings.json` и в колонке панели (`421/421`).

**Ширины панелей и раскладка (19.10.2026).**
- В `ProgramSettings` добавлены `charactersPaneWidth`, `itemsPaneWidth`,
  `iconsPaneWidth`, `dialoguesPaneWidth` и `scriptsPaneWidth` (по умолчанию
  288/288/320/250/288). Загрузка нормализует конечное значение в диапазон `180…900`;
  `StorageTests.Program_settings_pane_widths_round_trip_and_clamp` проверяет
  сохранение, ограничения и значения по умолчанию.
- Панели списков «Персонажи», «Предметы», «Иконки», «Диалоги» и «Скрипты» разделены
  общим стилем `PaneSplitter`; ширина запоминается после `DragCompleted` через
  `ShellViewModel.SavePaneWidths`. В «Диалогах» ширина палитры синхронизируется с
  `DialogueGraphViewModel.PalettePanelWidth` и сворачивается в полосу `w-9` (36 px).
- `GamedataScanner` раскладывает импортированные из XML диалоги через
  `GraphLayout.LayoutDialogue`, поэтому узлы больше не остаются в точке `(0, 0)`.
  `GraphNodeViewModel.ApplyNode` сообщает об изменении `HeaderHex`, и цвет шапки
  сразу обновляется при смене роли. Регрессии:
  `GamedataScanTests.Scan_layouts_imported_dialogue_nodes_instead_of_stacking_at_origin`
  и проверка WPF-зонда без ошибок привязок.

**system.ltx — вкладка инклюдов и секций (этап 7.5, 19.10.2026).**
- Перенесён `SystemTab.js`: `SystemLtxViewModel` + `SystemLtxView`. Редактор
  `system.ltx`, добавление `#include` из загруженных LTX и заготовки, экспорт файла,
  секции с раскрытием и правкой полей на месте (с сохранением комментариев), карта
  секций по `system.ltx`/`items.ltx`/всем `ltxFiles` с поиском и дубликатами.
- Список инклюдов подсвечивает битые, показывает номер строки, предлагает замену по
  имени файла (`findIncludeSuggestion`), переключает комментарий, открывает
  предпросмотр загруженного файла. Дерево инклюдов (`IncludeTreeRows`) и SVG-схема
  (`IncludeGraphSvg`) отображаются через новый `Controls/SvgPreview` (rect/path/text
  без WebView2).
- Проверки объединяют дубликаты секций, циклы наследования, ссылки LTX и
  `script_binding`; для привязок работает переход на вкладку «Скрипты» к нужному
  файлу. Дополнительно можно загрузить `all.spawn` и посмотреть заголовок/секции.
- RU/EN-ключи `system*`, `tabSystemHint`; шаблон `SystemLtxTemplate` в `MainWindow`.

**Языки — редактор string_table (этап 7.6, 19.10.2026).**
- Перенесён `LanguagesTab.js`: `LanguagesViewModel` + `LanguagesView`. Таблицы по
  локалям с правкой locale/name/записей, подсветкой дубликатов id и пустых переводов,
  подсказкой числа использований в репликах, добавлением/удалением строк и таблиц.
- Сводная таблица строится по всем id: колонки локалей, правка ячейки сразу
  синхронизируется с записью языка (при отсутствии — добавляет её). Импорт нескольких
  `string_table` XML объединяет локали через `XmlHelpers.MergeLanguages`, кодировка —
  из настроек проекта. Проверка `TextIdTools` показывает дубликаты, пустые переводы и
  покрытие реплик; шаблон `LanguagesTemplate` в `MainWindow`.

**Квесты — game_tasks/task_manager (этап 7.7, 19.10.2026).**
- Перенесён `QuestsTab.js`: `QuestsViewModel` + `QuestsView`. Список с поиском,
  категориями, сортировкой по id/приоритету, карточками и подсветкой проблем;
  создание квеста и шаблоны (`QuestCatalog.Templates`), undo/redo через `History<string>`
  (JSON-снимки списка).
- Редактор полей обеих схем: id/prior/title/text/requires/reward, категория,
  infoportion завершения/провала, иконка/локация/статья, цели с icon/infoportion/article,
  для `task_manager` — storyline, reward_money/item/treasure и condlist (условие и
  действие отдельными полями). Переводы title/text/целей показываются по выбранной
  локали; `bindQuestToLocales` добавляет отсутствующие id во все таблицы.
- Цепочки: `QuestAnalysis.ComputeChains` + дерево зависимостей + SVG
  `QuestChainSvg`; связанные диалоги ищутся по `give_info`/`disable_info`/`has_info`
  и открываются в графе. Проверка `QuestAnalysis.Validate`, diff по снимку `original`,
  предпросмотр XML и экспорт `game_tasks.xml`/`task_manager.xml` + `task_manager.ltx`.
- RU/EN-ключи `quests*`, `tabQuestsHint`; шаблон `QuestsTemplate` в `MainWindow`.

### Этап 8. Экспорт и самопроверка ✅
- `Core/Export/GamePackage` собирает диалоги, `string_table`, квесты, `system.ltx` и
  скрипты в карту `gamedata/...` и упаковывает её в ZIP (`ZipArchive`, кодировка
  экспорта проекта). Команда «Игровой ZIP» добавлена в панель обзора.
- `Core/Validation/ProjectSelfCheck` — встроенная самопроверка: ссылки между
  вкладками, граф диалогов, LTX/инклюды, Lua-скрипты, квесты, локализации,
  Windows-1251 round-trip и наличие игрового пакета. Результат открывается в
  `SelfCheckWindow`; техническая проверка остаётся независимой от xUnit.
- Экспорт CSV (`ItemsTab`) и DDS/PNG (иконки), сохранение `.project.json` уже
  существовали с этапов 4/7; игровой ZIP и самопроверка завершают набор экспорта.
- Пакетирование: `dotnet/publish.ps1` + `dotnet publish ... -p:PublishSingleFile=true`
  (framework-dependent `win-x64`, аналог portable-сборки). Проверено: один
  `XRayForge.exe` ~3.2 МБ в `dotnet/artifacts/win-x64`.
- **Проверка (19.10.2026).** `dotnet build` Debug/Release — 0 предупреждений;
  `dotnet test` — **411/411**; WPF-зонд с временным профилем загрузил все три новые
  вкладки: system.ltx (1 секция, 2 инклюда, дерево, SVG), языки (2 таблицы, 6 строк
  сводной таблицы), квесты (3 квеста, цепочка/дерево и SVG), редактор Lua
  (подсветка, выделение, обновление проекта, undo), окно самопроверки (9 проверок) —
  0 предупреждений привязок.


---

## 8. Стратегия тестирования

- Каждый JS-модуль `src/lib/*` → класс(ы) `Core/*` с 1:1 портом тестов из
  `test/*.mjs` в xUnit — это главный якорь корректности.
- Порядок переноса тестов определяет порядок готовности этапов 1–4.
- Формат `.project.json` проверяется round-trip'ом реальных файлов
  `saves/*.project.json` (тест `ProjectTests.Real_js_project_file_round_trips_without_field_loss`).
- UI-логика покрывается по мере возможности (сервисы + ViewModels), сложный
  граф — ручная проверка по чек-листу исходного README.

---




