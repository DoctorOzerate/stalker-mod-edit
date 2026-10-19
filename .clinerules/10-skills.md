# Скиллы (проверенные рецепты работ)

## S1. Быстрая проверка состояния

```powershell
git --no-pager status --short
dotnet build dotnet/XRayForge.sln --nologo -v q
dotnet test dotnet/XRayForge.sln --no-build --nologo -v q
```

Если в дереве есть незакоммиченные правки — сначала довести их до зелёных сборки/тестов,
затем продолжать новую порцию. Число тестов фиксировать в `MIGRATION.md`.

## S2. Портирование вкладки JS → WPF (порядок, проверенный на 7.1–7.3)

1. Прочитать legacy: `src/tabs/<Name>Tab.js` + обработчики в `src/App.js`
   (поиск по именам состояния: `selScript`, `scriptSearch`, `patch`, `say`).
2. Найти готовую логику в `Core` (`Lua*`, `Ltx*`, `Xml*`, `Quests*`, `CharacterCatalog`).
   Недостающее вынести в новый каталог `Core/<Domain>/<Name>Catalog.cs` — чистые функции,
   без WPF, с XML-документацией.
3. Написать `Core`-тесты **до** ViewModel: паритет с legacy, крайние случаи, пустой проект.
4. `ViewModels/<Name>ViewModel.cs`: `Attach(project)`, `RefreshLocalization()`, коллекции
   элементов, `[ObservableProperty]`/`[RelayCommand]`; правки — в модель проекта.
   Элемент списка — отдельный маленький VM (`<Name>ItemViewModel`) с кэшем по модели.
5. Зарегистрировать VM в `ShellViewModel`: создать в конструкторе, вызвать `Attach` рядом
   с остальными, обновлять из `Adopt`/`RefreshLocalization`.
6. `Views/<Name>View.xaml` (+ `.xaml.cs` только для прокрутки/фокуса/`GridSplitter`):
   шапка раздела, левая панель списка и фильтров, правая панель редактора; стили — из темы.
7. `MainWindow.xaml`: `DataTemplate x:Key="<Name>Template"` + `DataTrigger ActiveTab.Id`.
8. Локализация: RU+EN ключи в `LocalizationService`, заменить все строки.
9. Проверка: Debug/Release build, Debug/Release tests, зонд UI на временном профиле.
10. `MIGRATION.md`: блок «Шаг 7.x …», отклонения от JS, число тестов. При необходимости —
    обновить `FEATURE_GAP_AMK.md`.

## S3. Добавление ключа локализации

1. В `LocalizationService.cs` найти RU-секцию и EN-секцию (два словаря в одном файле).
2. RU-ключ добавить в тематическую группу, EN — в такую же группу EN-словаря.
3. В XAML использовать `{loc:Loc key}`; в VM — `LocalizationSource.Get("key")`.
4. Проверить, что ключ есть в обеих локалях (тест `LocalizationTests`/`UiTests` ловит пропуски).

## S4. Панель с изменяемой шириной

1. В `ProgramSettings` добавить `<name>PaneWidth` (double, дефолт) + нормализацию в
   `ProgramSettingsCatalog.Normalize` (180…900).
2. В `ShellViewModel` — свойство-обёртка и общий `SavePaneWidths()`.
3. В XAML: `ColumnDefinition x:Name="...Column" MinWidth/MaxWidth`, `GridSplitter`
   со `Style="{StaticResource PaneSplitter}"`, обработчик `DragCompleted` в code-behind.
4. Восстанавливать ширину в `Loaded` и `IsVisibleChanged` (вкладки пересоздаются).
5. Тест в `StorageTests`: сохранение/загрузка/клампинг ширины.

## S5. Сканирование gamedata

- Новый тип файла — в `GamedataScanner` (тип по расширению, кодировка через `GameEncoding`),
  результат — в `Project` и снапшот `AppDataService.ToSnapshot`.
- Импортированные графы раскладывать `GraphLayout.LayoutDialogue`, иначе узлы липнут в (0,0).
- Тест — `GamedataScanTests` на временной папке (не на реальной `gamedata`).

## S6. Проверка WPF-интерфейса без ручного клика

- Собрать, запустить приложение на **временном** профиле (`%APPDATA%\XRayForge`):
  профиль скопировать в `.bak`, подменить `settings.json`/`current.project.json`, после
  проверки вернуть копию.
- Для UI-зонда использовать существующий подход `UiTests`/временную консольную проверку:
  создать `Application`, `ShellViewModel` с `AppDataService` на temp-каталоге, проверить
  свойства/команды, затем `Shutdown`.
- Проверять: видимость панелей, отсутствие белых системных контролов в тёмной теме,
  прокрутку правой панели (`VerticalScrollPercent == 0` при открытии), реакцию на команды.
- Результат коротко записать в `MIGRATION.md`.

## S7. Работа с legacy-ссылками и ассетами

- Пути gamedata: `config` для SoC/CS, `configs` для CoP — брать из `Paths`/`GameSeriesCatalog`,
  не собирать вручную.
- Кодировки: только `GameEncoding` (BOM/XML-декларация/UTF-8/Windows-1251), запись — с
  совпадением декларации и байтов.
- DDS — только через `DdsCodec`/`AtlasEditor`; не добавлять сторонние кодеки.

## S8. Коммиты

- Формат как в истории: `feat(<scope>): …`, `fix(<scope>): …`, `docs: …`, `chore: …`.
- Один коммит = одна законченная порция (вкладка/фикс/документация), сборка и тесты зелёные.
- В сообщении — суть изменения, без перечисления файлов; подробности — в `MIGRATION.md`.
