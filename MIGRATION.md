# Миграция X-Ray Forge на WPF (.NET 9)

> Цель: перенести настольное приложение **X-Ray Forge** (редактор модов S.T.A.L.K.E.R.)
> с текущего стека **Electron + React + Vite + Tailwind** на **WPF (C# / .NET 9)**.

---

## Прогресс миграции

- [x] Этап 0. Каркас и соглашения — решение `dotnet/XRayForge.sln` создано, собирается, тесты проходят
- [x] Этап 1. Core: фундамент (history, i18n, core) — выполнено (14 тестов зелёные)
- [x] Этап 2. Core: парсеры данных — выполнено: Lua / Graph / Dialogs / XML / Quests / Spawn / Ltx (121 тест зелёный)
- [x] Этап 3. DDS-кодек — выполнено: DXT1/3/5 + BC4/BC5 + энкодер + mip-цепочка (214 теста зелёные)
- [ ] Этап 4. Модель проекта + персистенция
- [ ] Этап 5. Каркас UI
- [ ] Этап 6. Редактор диалогов (граф)
- [ ] Этап 7. Остальные вкладки
- [ ] Этап 8. Экспорт и самопроверка
- [ ] Этап 9. UI по референсам (скриншоты, шрифты, палитра, иконки)

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
| `electron/main.cjs` | IPC: save-project, open-dir, read-dir, save-file, open-file | `Services/FileSystemService` + нативные диалоги WPF |
| `electron/preload.cjs` | мост `window.xrayForge` | не нужен |
| `src/main.jsx` | точка входа React | `App.xaml` |
| `src/App.js` (2270 стр.) | монолит: состояние + рендер всех экранов | `MainWindow` + ViewModels |
| `src/lib/core.js` | пути, экранирование XML, парсинг LTX/XML, кодировки, миграция | `Core/` |
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

Проект (`.project.json`, `SCHEMA_VERSION = 3`):

```
Project {
  version, projectName, dialogueId, series (soc/cs/cop), theme,
  nodes[], edges[], characters[], languages[], scripts[], dialogues[],
  ltxFiles{path→text}, textures[], itemCategories{}, pinnedIcons[], quests[],
  settings{ importEncoding, exportEncoding }
}
```

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
| `localStorage` | JSON-файлы в `%APPDATA%\XRayForge` |
| Electron-диалоги + `fs` | `Microsoft.Win32.OpenFolderDialog` / `OpenFileDialog` / `SaveFileDialog` + `System.IO` |
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

**Отложено:** `migrateProject` → Этап 4 (нужна типовая модель проекта);
плагины (`new Function`) → решение отложено (см. §5).

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

### Этап 4. Модель проекта + персистенция
- `Models/Project.cs` и вложенные сущности.
- `Services/ProjectSerializer.cs` (JSON `.project.json`, миграция версий).
- `Services/AppDataService.cs` (%APPDATA% — профиль, настройки, бэкапы).
- `Services/FileSystemService.cs` + `GamedataScanner.cs` (рекурсивное чтение
  папки, определение типа `.xml/.ltx/.script/.lua/.dds`, кодировки, пропуск
  повреждённых, прогресс).
- Автосейв по таймеру (настройка интервала).

### Этап 5. Каркас UI
- `MainWindow` с левой навигацией/вкладками: Диалоги, Персонажи, Предметы,
  Иконки, Скрипты, system.ltx, Языки, Квесты, Обзор.
- Welcome-окно (создание/открытие/импорт/удаление проектов).
- Настройки программы и проекта; тема **Steel** (основная) + акцентные
  состояния **Olive/Hazard** через `ResourceDictionary` (см. §9);
  горячие клавиши (`InputBindings`/`KeyBinding`).
- `ViewModels` с привязками, `RelayCommand`.

### Этап 6. Редактор диалогов (граф)
- Граф-канвас: `Canvas` + `ScrollViewer`, `RenderTransform` для pan/zoom,
  `MouseWheel` (Ctrl+wheel — зум), средняя кнопка — панорама, `Shift`+drag —
  рамка выделения, групповое перемещение, мини-карта, «в центр», авто-раскладка.
- Палитра узлов, панель свойств реплики, метаданные («Действия и условия»).
- Валидация (битые связи, дубли ID, циклы), импорт из `game_dialogs`, экспорт.

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
- Порядок переноса тестов определяет порядок готовности этапов 1–3.
- UI-логика покрывается по мере возможности (сервисы + ViewModels), сложный
  граф — ручная проверка по чек-листу исходного README.

---

## 9. UI по референсам (скриншоты и шрифты)

Папка `dotnet/docs/ui-references/` — реестр визуальных референсов, по которым
воссоздаётся интерфейс WPF (этапы 5–7). См. `ui-references/README.md`.

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
  (+ `ResourceDictionary` тем dark/light из этапа 5).

**Раскладка папки:**
- `screenshots/` — скриншоты (имя `NN_<экран>_<тема>.png`);
- `fonts/` — шрифты `.ttf/.otf` + `LICENSE`, URL в реестре;
- `tokens/` — извлечённые палитры и размеры (`*.tokens.md`).

**Порядок работы:**
1. Пользователь добавляет скриншоты в `screenshots/`.
2. Я анализирую layout/палитру/типографику, фиксирую токены в `tokens/`.
3. Токены → `ResourceDictionary` (этап 5) → применение во вкладках (этапы 6–7).
4. При необходимости скачиваю шрифты по URL и подключаю как `Resource`.

**Критерий:** интерфейс WPF визуально соответствует референсам (layout,
палитра, типографика); все используемые шрифты — с открытой лицензией и
зафиксированным источником.



