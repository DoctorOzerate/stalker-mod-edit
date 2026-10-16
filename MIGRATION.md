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
- [ ] Этап 6. Редактор диалогов (граф)
- [ ] Этап 7. Остальные вкладки
- [ ] Этап 8. Экспорт и самопроверка

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
| `src/tabs/*.js` (8 шт.) | вынесенные вкладки `render*Tab(ctx)` | `Views/Tabs/` |
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
- Настройки программы и проекта; тема **Steel** (основная) + акцентные
  состояния **Olive/Hazard** через `ResourceDictionary` (см. «UI по референсам»
  ниже); горячие клавиши (`InputBindings`/`KeyBinding`).
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
  (проекты, сохранить, проверить, XML редактора, поиск, копии, точка сохранения,
  сканирование gamedata, настройки проекта/программы, справка), левая навигация по девяти
  разделам со счётчиками и акцентными глифами, рабочая область (панель «Обзор» с чипами
  `ProjectSummary` и списком проблем, панель раздела со строками `SectionRow`),
  welcome-оверлей (`welcomeTitle`) и статусная строка с плитками, LED занятости и метками
  сохранения/автосохранения.
- Диалоги: `InputWindow` (порт `prompt`), `SearchWindow` (глобальный поиск),
  `BackupsWindow` (точки сохранения: создать/восстановить/удалить), `SettingsWindow`
  (тема, акцент, плотность, язык, автосейв, шрифт, узлы, синхронизация, плагины,
  горячие клавиши, экспорт/импорт настроек, сброс данных), `ProjectSettingsWindow`
  (имя, `dialogueId`, серия, кодировки, показ узлов, путь и скан gamedata), `HelpWindow`.
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

### Этап 7. Остальные вкладки
- Персонажи (импорт/группировка/связь с диалогами).
- Предметы (LTX-редактор с наследованием 🔒, undo/redo, CSV-экспорт).
- Иконки (DDS + атлас: выделение, зум, панорама, сравнение, спрайт-лист).
- Скрипты (Lua-редактор с номерами строк и подсветкой, сниппеты).
- system.ltx (инклюды, дерево, циклы).
- Языки (string_table, сводная таблица).
- Квесты (граф цепочек).

### Этап 8. Экспорт и самопроверка
- Игровой ZIP, CSV, DDS/PNG, `.project.json`.
- Встроенная самопроверка (порт self-tests) + xUnit-набор.
- Пакетирование: single-file publish / MSIX (аналог portable/NSIS).

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




