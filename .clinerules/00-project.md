# Правила проекта X-Ray Forge (WPF)

## 1. Что это и где истина

X-Ray Forge — редактор модов S.T.A.L.K.E.R. (SoC/CS/CoP). Идёт миграция с legacy-клиента
**Electron + React** (`src/`, `electron/`, `test/`) на **WPF (.NET 9)** в `dotnet/`.

- Источник истины по WPF-версии: `dotnet/src/XRayForge*`, `dotnet/tests/XRayForge.Tests`,
  `MIGRATION.md` (прогресс и решения этапов), `dotnet/docs/FEATURE_GAP_AMK.md` (что осталось).
- `README.md` в корне описывает **старый** React/Electron-клиент — не путать.
- Legacy-код (`src/`, `test/`) — **референс для портирования**, а не место для новых фич.
  Менять его можно только по явной просьбе.
- Язык комментариев, XML-документации, коммитов и UI по умолчанию — **русский**
  (EN — вторым языком локализации).

## 2. Структура решения

```
dotnet/
├─ XRayForge.sln
├─ src/XRayForge.Core/     net9.0, чистая логика — БЕЗ System.Windows/WPF
│  ├─ Characters/ Dialogs/ Gamedata/ Graph/ History/ Imaging/ Localization/
│  ├─ Ltx/ Lua/ Models/ Projects/ Quests/ Spawn/ Storage/ Suggestions/
│  ├─ Text/ Ui/ Validation/ Xml/ Scripts (новые каталоги по домену)
├─ src/XRayForge/          net9.0-windows, WPF + CommunityToolkit.Mvvm
│  ├─ ViewModels/ Views/ Controls/ Converters/ Services/ Themes/ Localization/
├─ tests/XRayForge.Tests/  xUnit, порт test/*.mjs + тесты новых каталогов
└─ docs/                   FEATURE_GAP_AMK.md, ui-references/
```

Правило зависимостей: `Core` ничего не знает о WPF; `XRayForge` использует Core; тесты — Core и Ui.
Новую библиотеку добавлять только при реальной необходимости и с обоснованием.

## 3. Соглашения C#

- `file-scoped namespace`, `nullable enable`, `ImplicitUsings enable`, 4 пробела.
- Классы/сервисы — `sealed`, если не планируется наследование; маленькие DTO — `record`.
- Приватные поля — `_camelCase`, публичные члены — `PascalCase`, `Async`-суффикс у async-методов.
- XML-документация `///` на русском у каждого публичного типа и нетривиального члена;
  в сложных местах — комментарии «почему», а не «что».
- Стиль JS-паритета: модели проекта сохраняют **порядок и имена JSON-полей** как в
  `emptyProject()`/`.project.json`; незнакомые поля не теряются (`[JsonExtensionData] Extra`).
- `Regex` в анализаторах — с `RegexOptions.ECMAScript`, если паттерн портирован из JS.
- Никаких `Console.WriteLine`, `MessageBox` из Core, «магических» строк статуса — только
  через локализацию/сервисы оболочки.

## 4. ViewModels и Views

- VM: `ObservableObject`, `[ObservableProperty]` для состояния, `[RelayCommand]` для действий;
  публичный `Attach(Project project)` при смене проекта, `RefreshLocalization()` при смене языка.
- Кэш VM по ссылке на модель (фокус полей не теряется при перегруппировке); правки пишутся
  прямо в модель и вызывают `Shell.NotifyProjectChanged()`/`MarkDirty`-механизм оболочки.
- Списки пересобираются только по нужным событиям, не на каждый `PropertyChanged`.
- XAML: только токены темы через `{DynamicResource ...}`; строки — `{loc:Loc key}`;
  повторяющиеся стили — `Themes/Controls.xaml`, локальные шаблоны — в ресурсах View.
- Ширины панелей — `GridSplitter` + сохранение в `ProgramSettings` (см. `ItemsPaneWidth`).
- Горячие клавиши и порядок вкладок — через `NavigationCatalog`/`HotkeyCatalog`, не хардкодом.
- Заглушка `SectionStubView` допустима только для ещё не начатой вкладки; после реализации
  вкладка регистрируется `DataTemplate` + `DataTrigger` в `MainWindow.xaml`.

## 5. Локализация

- Все пользовательские строки — ключи в `Core/Localization/LocalizationService.cs` (RU-словарь и
  EN-словарь). Литералы в XAML/VM запрещены.
- Имена ключей: домен + смысл (`scriptCreate`, `scriptWarnBrackets`, `itemSearchHint`).
- RU — основная формулировка (как в legacy UI), EN — перевод; сохранять порядок групп ключей.
- Сообщения валидаторов в Core могут оставаться строками для паритета со старыми тестами,
  но UI показывает локализованные подписи по кодам.

## 6. Тесты и проверка (обязательный минимум перед отчётом)

```powershell
dotnet build dotnet/XRayForge.sln -c Debug
dotnet build dotnet/XRayForge.sln -c Release
dotnet test dotnet/XRayForge.sln -c Debug --no-build
dotnet test dotnet/XRayForge.sln -c Release --no-build
```

- 0 предупреждений и 0 ошибок в обеих конфигурациях.
- Новая логика Core → новые `Fact`-тесты (`<Domain>Tests.cs`, имена методов — предложения
  `DoesX_whenY`, допускается русская формулировка в духе существующих файлов).
- Порт из JS сопровождается тестами 1:1 с `test/*.mjs` там, где они есть.
- Для UI-изменений — ручная/зондовая проверка (временный профиль `%APPDATA%\XRayForge`,
  после проверки вернуть профиль), описание результата — в `MIGRATION.md`.
- Любая завершённая порция работы отражается в `MIGRATION.md` (что сделано, отклонения,
  числа тестов) и, при изменении полноты фич, в `dotnet/docs/FEATURE_GAP_AMK.md`.

## 7. Ограничения и запреты

- Не коммитить `saves/*.project.json`, `ebaniy-link.txt`, `bin/`, `obj/`, `node_modules/`.
- Не ломать JSON-паритет проекта и round-trip реальных `saves/*.project.json`.
- Не тащить в редактор Level Editor, 3D-рендер, погоду/шейдеры (см. P3 в gap-анализе).
- Не менять `package.json`/Vite-часть ради WPF-задач.
- Не оставлять «временные» TODO, заглушки команд и неиспользуемые ключи локализации.
