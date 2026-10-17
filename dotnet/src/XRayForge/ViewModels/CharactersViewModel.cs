using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Characters;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Suggestions;
using XRayForge.Core.Ui;
using XRayForge.Core.Validation;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «Персонажи» (этап 7.1, `CharactersTab.js`): поиск и группировка списка, редактор
/// полей `specific_character`, связанные диалоги, diff с `original`, исходный XML и матрица
/// `game_relations`. Чистая логика — в <see cref="CharacterCatalog"/> и
/// <see cref="CharacterValidator"/>; VM держит коллекции, выбранного персонажа и команды.
/// </summary>
public sealed partial class CharactersViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private Project _project = ProjectMigration.Empty();
    private SuggestionLibrary _suggestions = SuggestionLibrary.Empty;
    private Dictionary<Character, CharacterViewModel> _wrappers = new();
    private string? _selectedId;
    private string? _xmlContent;

    public CharactersViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Groups = new ObservableCollection<CharacterGroupViewModel>();
        Related = new ObservableCollection<CharacterRelatedViewModel>();
        DiffRows = new ObservableCollection<CharacterDiffRow>();
        CommunityOptions = new ObservableCollection<string>();
        CommunityChips = new ObservableCollection<CommunityChipViewModel>();
        MonsterChips = new ObservableCollection<CommunityChipViewModel>();
        RankMarks = new ObservableCollection<ScaleMarkViewModel>();
        ReputationMarks = new ObservableCollection<ScaleMarkViewModel>();
        RelationColumns = new ObservableCollection<RelationColumnViewModel>();
        RelationRows = new ObservableCollection<RelationRowViewModel>();
    }

    // --- коллекции вкладки ---

    /// <summary>Группы левой панели (`Object.keys(groups).sort()`).</summary>
    public ObservableCollection<CharacterGroupViewModel> Groups { get; }

    /// <summary>Диалоги из `start_dialog`/`actor_dialog` выбранного персонажа.</summary>
    public ObservableCollection<CharacterRelatedViewModel> Related { get; }

    /// <summary>Отличия полей выбранного персонажа от снимка `original`.</summary>
    public ObservableCollection<CharacterDiffRow> DiffRows { get; }

    /// <summary>`generalCommunities` для подсказок поля «Группировка».</summary>
    public ObservableCollection<string> CommunityOptions { get; }

    /// <summary>Чипы `game_relations.communities` с числом персонажей.</summary>
    public ObservableCollection<CommunityChipViewModel> CommunityChips { get; }

    /// <summary>Чипы `game_relations.monsterCommunities`.</summary>
    public ObservableCollection<CommunityChipViewModel> MonsterChips { get; }

    /// <summary>Метки шкалы `scales.rating` (ранг).</summary>
    public ObservableCollection<ScaleMarkViewModel> RankMarks { get; }

    /// <summary>Метки шкалы `scales.reputation` (репутация).</summary>
    public ObservableCollection<ScaleMarkViewModel> ReputationMarks { get; }

    /// <summary>Столбцы матрицы отношений (`communities`, первые 8 символов).</summary>
    public ObservableCollection<RelationColumnViewModel> RelationColumns { get; }

    /// <summary>Строки матрицы отношений (`communities`, первые 12 символов).</summary>
    public ObservableCollection<RelationRowViewModel> RelationRows { get; }

    // --- состояние ---

    /// <summary>Выбранный персонаж редактора: `selChar`, иначе первый персонаж проекта (как в JS).</summary>
    [ObservableProperty] private CharacterViewModel? _selected;

    /// <summary>Поиск по всем полям карточки (`charSearch`).</summary>
    [ObservableProperty] private string _search = "";

    /// <summary>Сортировка карточек внутри групп: `name` или `default` (`listSort`).</summary>
    [ObservableProperty] private string _sortMode = "name";

    /// <summary>Открыта панель исходного XML (`charXml != null`).</summary>
    [ObservableProperty] private bool _isXmlOpen;

    /// <summary>`Персонажей: N` под кнопками панели.</summary>
    public string CountText => LocalizationSource.Get("charCount") + " " +
        _project.Characters.Count.ToString(CultureInfo.CurrentCulture);

    /// <summary>Есть персонажи — редактор можно показать (`project.characters.length`).</summary>
    public bool HasCharacters => _project.Characters.Count > 0;

    /// <summary>Есть выбранный персонаж (иначе показывается подсказка `charEmpty`).</summary>
    public bool HasSelection => Selected != null;

    /// <summary>Карточка `game_relations` видна только при непустом списке `communities`.</summary>
    public bool HasGroups => _project.GameRelations is { Communities.Count: > 0 };

    /// <summary>Матрица отношений видна только при непустом `relations`.</summary>
    public bool HasRelations => _project.GameRelations?.Relations is { Count: > 0 };

    /// <summary>Строка «Монстры» видна при непустом `monsterCommunities`.</summary>
    public bool HasMonsterChips => MonsterChips.Count > 0;

    /// <summary>`Группировки · game_relations (N)`.</summary>
    public string CommunitiesTitle => LocalizationSource.Get("charCommunitiesTitle") + " (" +
        (_project.GameRelations?.Communities.Count ?? 0).ToString(CultureInfo.CurrentCulture) + ")";

    /// <summary>`Монстры (N)`.</summary>
    public string MonstersTitle => LocalizationSource.Get("charMonstersTitle") + " (" +
        MonsterChips.Count.ToString(CultureInfo.CurrentCulture) + ")";

    /// <summary>Есть строки diff (панель скрывается, если отличий нет).</summary>
    public bool HasDiff => DiffRows.Count > 0;

    /// <summary>Есть связанные диалоги (иначе подсказка `charNoRelated`).</summary>
    public bool HasRelated => Related.Count > 0;

    /// <summary>Есть метки шкалы ранга (`scales.rating`).</summary>
    public bool HasRankMarks => RankMarks.Count > 0;

    /// <summary>Есть метки шкалы репутации (`scales.reputation`).</summary>
    public bool HasReputationMarks => ReputationMarks.Count > 0;

    /// <summary>Портрет из `textureMap`/`textures` найден — вместо заглушки показывается вырезка.</summary>
    public bool HasIconPreview => IconPreview != null;

    /// <summary>
    /// Вырезка портрета `textureMap[icon]` из атласа (в JS — `background-image` с `background-position`).
    /// </summary>
    public ImageSource? IconPreview { get; private set; }

    /// <summary>Высота вырезки: `Math.round(entry.height * (66 / entry.width))`, как в JS.</summary>
    public double IconPreviewHeight { get; private set; } = 66;

    /// <summary>Текст панели исходного XML; без найденного блока — локализованная подсказка.</summary>
    public string XmlText => _xmlContent ?? LocalizationSource.Get("charXmlNotFound");

    // --- проект, локализация, выбранный персонаж ---

    /// <summary>
    /// Принять проект (`openProject`/`createProject`/скан gamedata): пересобрать список,
    /// связи, шкалы и матрицу отношений. Поиск и сортировка остаются состоянием вкладки.
    /// </summary>
    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _suggestions = SuggestionLibrary.Build(_project);
        _selectedId = null;
        _xmlContent = null;
        IsXmlOpen = false;
        RefreshAll();
    }

    /// <summary>Подсказки для поля персонажа; запрашиваются из XAML через <see cref="CharacterViewModel"/>.</summary>
    public IReadOnlyList<SuggestionEntry> SuggestionsFor(string field) => _suggestions.ForCharacterField(field);

    /// <summary>Смена локали: подписи групп, предупреждения, чипы и шкалы пересобираются.</summary>
    public void RefreshLocalization()
    {
        RebuildGroups();
        RefreshCommunityOptions();
        RefreshRelations();
        RefreshSelectedDetails();
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(CommunitiesTitle));
        OnPropertyChanged(nameof(MonstersTitle));
        OnPropertyChanged(nameof(XmlText));
    }

    private void RefreshAll()
    {
        RebuildGroups();
        RefreshCommunityOptions();
        RefreshRelations();
        RefreshSelectedDetails();
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasCharacters));
        OnPropertyChanged(nameof(CommunitiesTitle));
        OnPropertyChanged(nameof(MonstersTitle));
        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(HasRelations));
        OnPropertyChanged(nameof(HasMonsterChips));
    }

    partial void OnSearchChanged(string value) => RebuildGroups();

    partial void OnSortModeChanged(string value) => RebuildGroups();

    partial void OnSelectedChanged(CharacterViewModel? value)
    {
        foreach (var wrapper in _wrappers.Values) wrapper.IsSelected = ReferenceEquals(wrapper, value);
        OnPropertyChanged(nameof(HasSelection));
        RefreshSelectedDetails();
    }

    // --- команды панели и редактора ---

    /// <summary>`＋ Создать персонажа`: добавить карточку и выбрать её.</summary>
    [RelayCommand]
    private void Create()
    {
        var character = CharacterCatalog.Create(CharacterCatalog.NewId(), LocalizationSource.Get("charNewName"));
        _project.Characters.Add(character);
        _selectedId = character.Id;
        RebuildGroups();
        RefreshAll();
        _shell.OnSectionEdited(LocalizationSource.Get("charCreated"));
    }

    /// <summary>`autofillCharDialogs`: персонажам без `start_dialog` подставить найденный диалог.</summary>
    [RelayCommand]
    private void Autofill()
    {
        var changed = CharacterCatalog.AutofillDialogs(_project.Characters, _project.Dialogues);
        foreach (var character in changed)
            if (_wrappers.TryGetValue(character, out var wrapper)) wrapper.ReloadFromModel();

        RebuildGroups();
        RefreshAll();
        _shell.OnSectionEdited(LocalizationSource.Get("charAutofillDone"));
    }

    /// <summary>Выбор карточки списка (`setSelChar`).</summary>
    [RelayCommand]
    private void Select(CharacterViewModel? character)
    {
        if (character == null) return;
        _selectedId = character.Id;
        Selected = character;
    }

    /// <summary>`Удалить персонажа`: убрать все карточки с тем же id (как `filter(x.id !== c.id)`).</summary>
    [RelayCommand]
    private void Delete()
    {
        var character = Selected;
        if (character == null) return;

        var id = character.Id;
        _project.Characters.RemoveAll(item => item.Id == id);
        _selectedId = null;
        _xmlContent = null;
        IsXmlOpen = false;
        OnPropertyChanged(nameof(XmlText));
        RebuildGroups();
        RefreshAll();
        _shell.OnSectionEdited(LocalizationSource.Get("charDeleted"));
    }

    /// <summary>`Исходный XML`: блок `specific_character` из `project.ltxFiles`.</summary>
    [RelayCommand]
    private void ShowXml()
    {
        var character = Selected;
        if (character == null) return;

        _xmlContent = CharacterCatalog.ExtractXml(character.Id, _project.LtxFiles);
        IsXmlOpen = true;
        OnPropertyChanged(nameof(XmlText));
    }

    /// <summary>`×` в шапке панели XML (`setCharXml(null)`).</summary>
    [RelayCommand]
    private void CloseXml()
    {
        _xmlContent = null;
        IsXmlOpen = false;
        OnPropertyChanged(nameof(XmlText));
    }

    /// <summary>`loadDialogueIntoGraph`: открыть связанный диалог на вкладке «Диалоги».</summary>
    [RelayCommand]
    private void OpenRelated(CharacterRelatedViewModel? related)
    {
        if (related == null) return;

        _shell.DialogueGraph.LoadDialogueCommand.Execute(related.Dialogue);
        _shell.ActivateTabById(NavigationCatalog.DialoguesId);
    }

    /// <summary>Правка ячейки матрицы отношений: обновить строку `gr.relations[row]`.</summary>
    public void SetRelation(int rowIndex, int columnIndex, string value)
    {
        var relations = _project.GameRelations;
        if (relations == null || rowIndex < 0 || rowIndex >= relations.Communities.Count) return;

        var row = relations.Communities[rowIndex];
        var values = relations.Relations != null && relations.Relations.TryGetValue(row, out var existing)
            ? existing.ToList()
            : new List<string>();
        while (values.Count <= columnIndex) values.Add("");
        values[columnIndex] = value ?? "";

        var next = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (relations.Relations != null)
            foreach (var pair in relations.Relations) next[pair.Key] = pair.Value;
        next[row] = values;
        relations.Relations = next;

        _shell.OnSectionEdited();
    }

    // --- список, предупреждения, связи, diff, шкалы и портрет ---

    /// <summary>
    /// Пересобрать левый список: фильтр и группировка `CharacterCatalog.Group`, дубликаты id,
    /// кэш VM по ссылке на модель (фокус полей не теряется при перегруппировке).
    /// </summary>
    private void RebuildGroups()
    {
        var next = new Dictionary<Character, CharacterViewModel>();
        foreach (var character in _project.Characters)
        {
            if (character == null) continue;
            if (!_wrappers.TryGetValue(character, out var wrapper))
                wrapper = new CharacterViewModel(this, character);
            next[character] = wrapper;
        }
        _wrappers = next;

        var duplicates = CharacterCatalog.DuplicateIds(_project.Characters);
        foreach (var wrapper in _wrappers.Values)
        {
            wrapper.IsDuplicate = duplicates.Contains(wrapper.Id);
            wrapper.RefreshLocalization();
        }

        Groups.Clear();
        foreach (var group in CharacterCatalog.Group(_project.Characters, Search, SortMode == "name"))
        {
            var items = group.Items.Where(_wrappers.ContainsKey).Select(item => _wrappers[item]).ToList();
            var label = group.Community == CharacterCatalog.NoCommunity
                ? LocalizationSource.Get("charNoGroup")
                : group.Community;
            var header = group.Icon + " " + label + " · " +
                items.Count.ToString(CultureInfo.CurrentCulture);
            Groups.Add(new CharacterGroupViewModel(header, items));
        }

        var target = _selectedId == null
            ? null
            : _wrappers.Values.FirstOrDefault(wrapper => wrapper.Id == _selectedId);
        Selected = target ?? _wrappers.Values.FirstOrDefault();
    }

    /// <summary>
    /// Правка поля персонажа из <see cref="CharacterViewModel"/>: отметка проекта, пересборка
    /// списка (имя/id/community) и панелей выбранного персонажа.
    /// </summary>
    public void OnCharacterEdited(
        CharacterViewModel character,
        bool listChanged,
        bool warningsChanged,
        bool relatedChanged)
    {
        if (listChanged)
        {
            RebuildGroups();
            // Чипы community показывают число персонажей: после смены community/состава
            // списка их счётчики пересобираются, как при каждом рендере в JS.
            RefreshRelations();
        }

        if (ReferenceEquals(Selected, character))
        {
            if (warningsChanged) RefreshWarnings();
            if (relatedChanged) RefreshRelated();
            RefreshDiff();
            RefreshIconPreview();
        }

        _shell.OnSectionEdited();
    }

    /// <summary>Пересчитать панели выбранного персонажа (выбор, attach, смена локали).</summary>
    private void RefreshSelectedDetails()
    {
        RefreshWarnings();
        RefreshRelated();
        RefreshDiff();
        RefreshScales();
        RefreshIconPreview();
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>`validateCharacter` с локализованными строками и префиксом `⚠`.</summary>
    private void RefreshWarnings()
    {
        var character = Selected;
        if (character == null)
        {
            OnPropertyChanged(nameof(HasSelection));
            return;
        }

        var textureMap = _project.TextureMap is { Count: > 0 } map
            ? map.ToDictionary(pair => pair.Key, pair => (object?)pair.Value)
            : null;
        var warnings = CharacterValidator
            .Issues(character.Model, _project.GeneralCommunities, textureMap)
            .Select(issue => "⚠ " + Describe(issue, character.Model));
        character.SetWarnings(warnings);
    }

    /// <summary>Строка проблемы (ключи `charWarn*`, значения полей — как в JS).</summary>
    private static string Describe(CharacterIssue issue, Character character) => issue switch
    {
        CharacterIssue.MissingVisual => LocalizationSource.Get("charWarnVisual"),
        CharacterIssue.MissingIcon => LocalizationSource.Get("charWarnIcon"),
        CharacterIssue.UnknownCommunity => LocalizationSource.Get("charWarnCommunity") + " " + character.Community,
        CharacterIssue.UnknownTexture => LocalizationSource.Get("charWarnTexture") + " " + character.Icon,
        _ => issue.ToString()
    };

    /// <summary>Кнопки связанных диалогов (`start_dialog`/`actor_dialog`).</summary>
    private void RefreshRelated()
    {
        Related.Clear();
        var character = Selected;
        if (character != null)
            foreach (var dialogue in CharacterCatalog.Related(_project.Dialogues, character.Model))
                Related.Add(new CharacterRelatedViewModel(dialogue));

        OnPropertyChanged(nameof(HasRelated));
    }

    /// <summary>Строки diff с `original` (только отличающиеся поля).</summary>
    private void RefreshDiff()
    {
        DiffRows.Clear();
        if (Selected != null)
            foreach (var row in CharacterCatalog.Diff(Selected.Model))
                DiffRows.Add(new CharacterDiffRow(row));

        OnPropertyChanged(nameof(HasDiff));
    }

    /// <summary>Метки `rating`/`reputation` (подсказки `Ранг`/`Репутация`).</summary>
    private void RefreshScales()
    {
        RankMarks.Clear();
        ReputationMarks.Clear();
        var scales = _project.GameRelations?.Scales;
        if (scales != null)
        {
            foreach (var mark in scales.Rating)
                RankMarks.Add(new ScaleMarkViewModel(mark, "charRankShort"));
            foreach (var mark in scales.Reputation)
                ReputationMarks.Add(new ScaleMarkViewModel(mark, "charReputationShort"));
        }

        OnPropertyChanged(nameof(HasRankMarks));
        OnPropertyChanged(nameof(HasReputationMarks));
    }

    /// <summary>`generalCommunities` для выпадающего списка поля community.</summary>
    private void RefreshCommunityOptions()
    {
        CommunityOptions.Clear();
        foreach (var community in _project.GeneralCommunities)
            if (!string.IsNullOrWhiteSpace(community) && !CommunityOptions.Contains(community))
                CommunityOptions.Add(community);
    }

    /// <summary>Чипы и матрица `game_relations` (карточка в начале правой колонки).</summary>
    private void RefreshRelations()
    {
        CommunityChips.Clear();
        MonsterChips.Clear();
        RelationColumns.Clear();
        RelationRows.Clear();

        var relations = _project.GameRelations;
        if (relations != null)
        {
            foreach (var community in relations.Communities)
            {
                var count = _project.Characters.Count(character => character.Community == community);
                CommunityChips.Add(new CommunityChipViewModel(
                    community, CharacterCatalog.CommunityIcon(community), count));
                RelationColumns.Add(new RelationColumnViewModel(community));
            }

            foreach (var community in relations.MonsterCommunities)
                MonsterChips.Add(new CommunityChipViewModel(
                    community, CharacterCatalog.CommunityIcon(community), 0));

            if (relations.Relations is { Count: > 0 })
            {
                for (var row = 0; row < relations.Communities.Count; row++)
                {
                    var name = relations.Communities[row];
                    var values = relations.Relations.TryGetValue(name, out var existing)
                        ? existing
                        : new List<string>();
                    var cells = new List<RelationCellViewModel>();
                    for (var column = 0; column < relations.Communities.Count; column++)
                        cells.Add(new RelationCellViewModel(
                            this, row, column, column < values.Count ? values[column] : ""));
                    RelationRows.Add(new RelationRowViewModel(name, row, cells));
                }
            }
        }

        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(HasRelations));
        OnPropertyChanged(nameof(HasMonsterChips));
        OnPropertyChanged(nameof(CommunitiesTitle));
        OnPropertyChanged(nameof(MonstersTitle));
    }

    /// <summary>
    /// Портрет выбранного персонажа: `textureMap[icon]` + `textures[]` ищутся так же, как в JS,
    /// затем из атласа вырезается прямоугольник иконки (масштаб `66 / entry.width`).
    /// </summary>
    private void RefreshIconPreview()
    {
        IconPreview = null;
        IconPreviewHeight = 66;
        var character = Selected;
        if (character != null && !string.IsNullOrWhiteSpace(character.Icon) &&
            _project.TextureMap != null &&
            _project.TextureMap.TryGetValue(character.Icon, out var entry) &&
            entry != null && entry.Width > 0 && entry.Height > 0)
        {
            var texture = FindTexture(_project.Textures, entry.File);
            if (texture?.DataUrl is { Length: > 0 } dataUrl && texture.Width is > 0 && texture.Height is > 0)
            {
                IconPreview = CropIcon(dataUrl, entry, texture.Width.Value, texture.Height.Value);
                if (IconPreview != null)
                    IconPreviewHeight = Math.Max(1, Math.Round(66 * entry.Height / entry.Width));
            }
        }

        OnPropertyChanged(nameof(IconPreview));
        OnPropertyChanged(nameof(IconPreviewHeight));
        OnPropertyChanged(nameof(HasIconPreview));
    }

    /// <summary>Поиск текстуры атласа по имени файла (без `.dds`, `/` и `\` эквивалентны).</summary>
    private static ProjectTexture? FindTexture(IEnumerable<ProjectTexture>? textures, string file)
    {
        if (textures == null) return null;

        var want = file.ToLowerInvariant().Replace('\\', '/');
        foreach (var texture in textures)
        {
            var name = texture.Name.ToLowerInvariant();
            if (name.EndsWith(".dds", StringComparison.Ordinal)) name = name[..^4];
            name = name.Replace('\\', '/');
            if (name == want || name.EndsWith("/" + want, StringComparison.Ordinal) ||
                want.EndsWith("/" + name, StringComparison.Ordinal))
                return texture;
        }

        return null;
    }

    /// <summary>Вырезать иконку из PNG data URL атласа; при ошибке — `null` (в JS-версии остаётся фон).</summary>
    private static ImageSource? CropIcon(string dataUrl, TextureMapEntry entry, int textureWidth, int textureHeight)
    {
        try
        {
            var comma = dataUrl.IndexOf(',');
            if (comma < 0) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(Convert.FromBase64String(dataUrl[(comma + 1)..]));
            bitmap.EndInit();
            bitmap.Freeze();

            var scale = textureWidth > 0 ? bitmap.PixelWidth / (double)textureWidth : 1.0;
            var rect = new System.Windows.Int32Rect(
                (int)Math.Round(entry.X * scale),
                (int)Math.Round(entry.Y * scale),
                (int)Math.Round(entry.Width * scale),
                (int)Math.Round(entry.Height * scale));
            if (rect.Width <= 0) rect.Width = 1;
            if (rect.Height <= 0) rect.Height = 1;
            if (rect.X < 0) rect.X = 0;
            if (rect.Y < 0) rect.Y = 0;
            if (rect.X + rect.Width > bitmap.PixelWidth) rect.Width = bitmap.PixelWidth - rect.X;
            if (rect.Y + rect.Height > bitmap.PixelHeight) rect.Height = bitmap.PixelHeight - rect.Y;
            if (rect.Width <= 0 || rect.Height <= 0) return null;

            var cropped = new CroppedBitmap(bitmap, rect);
            cropped.Freeze();
            return cropped;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
