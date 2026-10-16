# Миграция X-Ray Forge на WPF (.NET 9)

> Цель: перенести настольное приложение **X-Ray Forge** (редактор модов S.T.A.L.K.E.R.)
> с текущего стека **Electron + React + Vite + Tailwind** на **WPF (C# / .NET 9)**.

---

## Прогресс миграции

- [x] Этап 0. Каркас и соглашения — решение `dotnet/XRayForge.sln` создано, собирается, тесты проходят
- [x] Этап 1. Core: фундамент (history, i18n, core) — выполнено (14 тестов зелёные)
- [ ] Этап 2. Core: парсеры данных
- [ ] Этап 3. DDS-кодек
- [ ] Этап 4. Модель проекта + персистенция
- [ ] Этап 5. Каркас UI
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
| `electron/main.cjs` | IPC: save-project, open-dir, read-dir, save-file, open-file | `Services/FileSystemService` + нативные диалоги WPF |
| `electron/preload.cjs` | мост `window.xrayForge` | не нужен |
| `src/main.jsx` | точка входа React | `App.xaml` |
| `src/App.js` (2270 стр.) | монолит: состояние + рендер всех экранов | `MainWindow` + ViewModels |
| `src/lib/core.js` | пути, экранирование XML, парсинг LTX/XML, кодировки, миграция | `Core/` |
| `src/lib/ltx.js` | LTX-парсер, инклюды, предметы, категории, подсветка, `parse*` | `Core/Ltx/` |
| `src/lib/xml.js` | string_table, персонажи, локали, info_portions, encyclopedia, npc_profile, дескрипторы текстур | `Core/Xml/` |
| `src/lib/dialogs.js` | экспорт диалогов → XML/ZIP, валидация text_id | `Core/Dialogs/` |
| `src/lib/graph.js` | типы узлов, валидация графа, раскладка, `graphToSvg` | `Core/Graph/` |
| `src/lib/dds.js` | DDS-кодек: DXT1/3/5, BC4/BC5, энкодер, mip-цепочка | `Core/Imaging/DdsCodec.cs` |
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
2. **DDS-кодек** — большой, но чисто вычислительный; переносится 1:1, тесты
   уже есть (`test/dds.test.mjs`).
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

### Этап 2. Core: парсеры данных
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

**Критерий:** все Core-тесты зелёные; публичные сигнатуры стабильны.

### Этап 3. DDS-кодек
- `dds.js` → `Core/Imaging/DdsCodec.cs`: `decodeDxt1/3/5`, `decodeBc4/Bc5`,
  `encodeDds`, `buildMipChain`, `downsampleRgba`.
- Порт `dds.test.mjs`.

**Критерий:** декодирование/кодирование DXT1/3/5 и BC4/BC5 с round-trip.

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
- Настройки программы и проекта; темы (dark/light) через `ResourceDictionary`;
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



