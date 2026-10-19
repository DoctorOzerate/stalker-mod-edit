using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Dialogs;
using XRayForge.Core.History;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Quests;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;
using XRayForge.Localization;
using XRayForge.Services;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «Квесты» (этап 7.7, `QuestsTab.js`): список с поиском/категориями/шаблонами,
/// редактор game_tasks/task_manager, цели и condlist, цепочки с SVG, проверки, diff,
/// предпросмотр переводов, связанные диалоги и экспорт XML/LTX.
/// </summary>
public sealed partial class QuestsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly History<string> _history = new(50);
    private HistoryState<string> _historyState = HistoryState<string>.Empty;
    private Project _project = ProjectMigration.Empty();
    private string? _selectedId;

    public QuestsViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;
        Quests = new ObservableCollection<QuestListItemViewModel>();
        Categories = new ObservableCollection<string> { AllCategories };
        foreach (var category in QuestCatalog.Categories) Categories.Add(category);
        Issues = new ObservableCollection<QuestIssueViewModel>();
        Related = new ObservableCollection<QuestRelatedViewModel>();
        DiffRows = new ObservableCollection<QuestDiffRowViewModel>();
        ChainRows = new ObservableCollection<QuestChainRowViewModel>();
        LanguageLocales = new ObservableCollection<string>();
        Attach(ProjectMigration.Empty());
    }

    public const string AllCategories = "Все";

    public ObservableCollection<QuestListItemViewModel> Quests { get; }
    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<QuestIssueViewModel> Issues { get; }
    public ObservableCollection<QuestRelatedViewModel> Related { get; }
    public ObservableCollection<QuestDiffRowViewModel> DiffRows { get; }
    public ObservableCollection<QuestChainRowViewModel> ChainRows { get; }
    public ObservableCollection<string> LanguageLocales { get; }
    public IReadOnlyList<QuestTemplate> Templates => QuestCatalog.Templates;

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _category = AllCategories;
    [ObservableProperty] private string _sortMode = "name";
    [ObservableProperty] private string _viewMode = "edit";
    [ObservableProperty] private QuestTemplate? _selectedTemplate;
    [ObservableProperty] private string _previewLocale = "";
    [ObservableProperty] private QuestListItemViewModel? _selected;
    [ObservableProperty] private QuestEditorViewModel? _selectedEditor;

    public bool HasQuests => _project.Quests.Count > 0;
    public bool HasSelection => SelectedEditor != null;
    public bool IsEditView => ViewMode != "chains";
    public bool IsChainsView => ViewMode == "chains";
    public bool IsTaskManager => Selected?.Quest.Format == "task_manager";
    public bool HasIssues => Issues.Count > 0;
    public bool HasRelated => Related.Count > 0;
    public bool HasDiff => DiffRows.Count > 0;
    public bool HasChains => ChainRows.Count > 0;
    public bool HasChainSvg => ChainSvg.Length > 0;
    public bool CanUndo => _historyState.Pos > 0;
    public bool CanRedo => _historyState.Pos >= 0 && _historyState.Pos < _historyState.Hist.Length - 1;
    public string ChainSvg { get; private set; } = "";
    public string TotalText => LocalizationSource.Get("questsTotal") + ": " +
        _project.Quests.Count.ToString(CultureInfo.CurrentCulture) + " (" +
        LocalizationSource.Get("questsCop") + ": " +
        _project.Quests.Count(q => q.Format == "task_manager").ToString(CultureInfo.CurrentCulture) + ")";
    public string IssuesTitle => LocalizationSource.Get("questsChecks") + " · " +
        Issues.Count.ToString(CultureInfo.CurrentCulture);
    public string RelatedTitle => LocalizationSource.Get("questsRelated") + " (" +
        Related.Count.ToString(CultureInfo.CurrentCulture) + ")";
    public string DiffTitle => LocalizationSource.Get("questsDiff");
    public string ChainsTitle => LocalizationSource.Get("questsChains") + " · " +
        ChainRows.Count.ToString(CultureInfo.CurrentCulture);
    public string PreviewTitle => LocalizationSource.Get("questsPreview");
    public string CurrentFormatText => IsTaskManager
        ? LocalizationSource.Get("questsCopFormat")
        : LocalizationSource.Get("questsSocFormat");
    public string EditTabText => LocalizationSource.Get("questsEditor");
    public string ChainsTabText => LocalizationSource.Get("questsChains");
    public string NoQuestsText => HasQuests
        ? LocalizationSource.Get("questsNoMatches")
        : LocalizationSource.Get("questsEmpty");

    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _project.Quests ??= new List<Quest>();
        _project.Languages ??= new List<Language>();
        _project.Dialogues ??= new List<DialogueGraph>();

        _historyState = HistoryState<string>.Empty;
        _selectedId = null;
        BuildLanguageLocales();
        RefreshList();
        RefreshChains();
        Selected = Quests.FirstOrDefault();
        BuildSelection();
        NotifyAll();
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(IssuesTitle));
        OnPropertyChanged(nameof(RelatedTitle));
        OnPropertyChanged(nameof(DiffTitle));
        OnPropertyChanged(nameof(ChainsTitle));
        OnPropertyChanged(nameof(PreviewTitle));
        OnPropertyChanged(nameof(CurrentFormatText));
        OnPropertyChanged(nameof(EditTabText));
        OnPropertyChanged(nameof(ChainsTabText));
        OnPropertyChanged(nameof(NoQuestsText));
        SelectedEditor?.RaiseLabels();
    }

    partial void OnSearchChanged(string value) => RefreshList();

    partial void OnCategoryChanged(string value) => RefreshList();

    partial void OnSortModeChanged(string value) => RefreshList();

    partial void OnPreviewLocaleChanged(string value)
    {
        SelectedEditor?.RefreshTranslations();
        OnPropertyChanged(nameof(PreviewTitle));
    }

    partial void OnViewModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsEditView));
        OnPropertyChanged(nameof(IsChainsView));
        if (IsChainsView) RefreshChains();
    }

    partial void OnSelectedChanged(QuestListItemViewModel? value)
    {
        _selectedId = value?.Quest.Id;
        BuildSelection();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsTaskManager));
        OnPropertyChanged(nameof(CurrentFormatText));
    }

    private void BuildSelection()
    {
        var quest = Selected?.Quest ?? _project.Quests.FirstOrDefault();
        SelectedEditor = quest != null ? new QuestEditorViewModel(this, quest) : null;
        RefreshDerived();
    }

    // --- список ---

    private void RefreshList()
    {
        var previous = _selectedId ?? Selected?.Quest.Id;
        var search = (Search ?? "").Trim().ToLowerInvariant();
        var filtered = _project.Quests.Where(q =>
        {
            if (Category != AllCategories && q.Category != Category) return false;
            if (search.Length == 0) return true;
            var text = string.Join(" ", new[] { q.Id, q.Title, q.Text }
                .Concat(q.Objectives.Select(o => o.Text))).ToLowerInvariant();
            return text.Contains(search, StringComparison.Ordinal);
        });

        var sorted = SortMode == "count"
            ? filtered.OrderBy(q => int.TryParse(q.Prior, out var prior) ? prior : 0)
                .ThenBy(q => q.Id, StringComparer.Ordinal)
            : filtered.OrderBy(q => q.Id, StringComparer.Ordinal);

        Quests.Clear();
        foreach (var quest in sorted)
            Quests.Add(new QuestListItemViewModel(this, quest));

        var next = Quests.FirstOrDefault(q => q.Quest.Id == previous) ?? Quests.FirstOrDefault();
        if (!ReferenceEquals(Selected, next)) Selected = next;
        else if (next != null) BuildSelection();

        OnPropertyChanged(nameof(HasQuests));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(NoQuestsText));
    }

    /// <summary>Пересчёт проверки, связанных диалогов, diff и предпросмотра текущего квеста.</summary>
    private void RefreshDerived()
    {
        Issues.Clear();
        Related.Clear();
        DiffRows.Clear();

        var quest = SelectedEditor?.Model;
        if (quest != null)
        {
            foreach (var issue in QuestAnalysis.Validate(new[] { quest }, _project.Languages))
                Issues.Add(new QuestIssueViewModel(issue.Severity, issue.Message));

            BuildRelated(quest);
            BuildDiff(quest);
            SelectedEditor?.RefreshTranslations();
        }

        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(IssuesTitle));
        OnPropertyChanged(nameof(HasRelated));
        OnPropertyChanged(nameof(RelatedTitle));
        OnPropertyChanged(nameof(HasDiff));
        OnPropertyChanged(nameof(DiffTitle));
        Selected?.RaiseSummary();
    }

    private void BuildRelated(Quest quest)
    {
        var refs = new[] { quest.InfoportionComplete, quest.InfoportionFail, quest.Id }
            .Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        foreach (var dialogue in _project.Dialogues)
        {
            var hits = dialogue.Nodes.Count(node =>
            {
                var metadata = node.Data?.Metadata ?? new Dictionary<string, List<string>>();
                var values = new[] { "give_info", "disable_info", "has_info" }
                    .Where(metadata.ContainsKey).SelectMany(key => metadata[key]);
                return values.Any(value => refs.Any(reference =>
                    value.Contains(reference, StringComparison.Ordinal) ||
                    reference.Contains(value, StringComparison.Ordinal)));
            });
            if (hits > 0) Related.Add(new QuestRelatedViewModel(this, dialogue, hits));
        }
    }

    private void BuildDiff(Quest quest)
    {
        if (quest.Original == null) return;
        Quest? original = null;
        try
        {
            original = JsonSerializer.Deserialize<Quest>(quest.Original.Value.GetRawText(),
                ProjectSerializer.Options);
        }
        catch
        {
            return;
        }
        if (original == null) return;

        void Row(string label, string before, string after)
        {
            if (string.Equals(before ?? "", after ?? "", StringComparison.Ordinal)) return;
            DiffRows.Add(new QuestDiffRowViewModel(label, before ?? "", after ?? ""));
        }

        Row("ID", original.Id, quest.Id);
        Row(LocalizationSource.Get("questsPriority"), original.Prior, quest.Prior);
        Row(LocalizationSource.Get("questsTitleField"), original.Title, quest.Title);
        Row(LocalizationSource.Get("questsTextField"), original.Text, quest.Text);
        Row(LocalizationSource.Get("questsComplete"), original.InfoportionComplete, quest.InfoportionComplete);
        Row(LocalizationSource.Get("questsFail"), original.InfoportionFail, quest.InfoportionFail);
        Row(LocalizationSource.Get("questsIcon"), original.Icon, quest.Icon);
        Row(LocalizationSource.Get("questsLocation"), original.Location, quest.Location);
        Row(LocalizationSource.Get("questsArticle"), original.Article, quest.Article);

        static string Objectives(List<QuestObjective> list) => string.Join("; ",
            list.Select(o => o.Text + "|" + o.Icon + "|" + o.Infoportion + "|" + o.Article));
        Row(LocalizationSource.Get("questsObjectives"), Objectives(original.Objectives),
            Objectives(quest.Objectives));
    }

    // --- цепочки ---

    private void RefreshChains()
    {
        ChainRows.Clear();
        var list = _project.Quests;
        var completeTo = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var quest in list)
            if (!string.IsNullOrEmpty(quest.InfoportionComplete))
                completeTo[quest.InfoportionComplete] = quest.Id;

        var parentOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var quest in list)
            foreach (var requirement in quest.Requires)
                if (completeTo.TryGetValue(requirement, out var parent) && parent != quest.Id)
                    parentOf[quest.Id] = parent;

        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var pair in parentOf)
        {
            if (!children.TryGetValue(pair.Value, out var kids))
                children[pair.Value] = kids = new List<string>();
            kids.Add(pair.Key);
        }

        var roots = list.Where(q => !parentOf.ContainsKey(q.Id)).Select(q => q.Id).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Walk(string id, int depth)
        {
            if (depth > 50 || !seen.Add(id)) return;
            var quest = list.FirstOrDefault(q => q.Id == id);
            ChainRows.Add(new QuestChainRowViewModel(this, id, depth, quest?.InfoportionComplete ?? ""));
            if (children.TryGetValue(id, out var kids))
                foreach (var kid in kids) Walk(kid, depth + 1);
        }

        foreach (var root in roots) Walk(root, 0);
        ChainSvg = QuestChainSvg.Render(QuestAnalysis.ComputeChains(list), list);
        OnPropertyChanged(nameof(ChainSvg));
        OnPropertyChanged(nameof(HasChains));
        OnPropertyChanged(nameof(HasChainSvg));
        OnPropertyChanged(nameof(ChainsTitle));
    }

    // --- правки ---

    internal void OnQuestChanged(QuestEditorViewModel editor, bool identityChanged = false)
    {
        if (identityChanged) _selectedId = editor.Model.Id;
        RefreshDerived();
        if (identityChanged) RefreshChains();
        _shell.OnSectionEdited();
    }

    internal void OnObjectiveChanged(QuestEditorViewModel editor) => OnQuestChanged(editor);

    internal void OnCondlistChanged(QuestEditorViewModel editor) => OnQuestChanged(editor);

    internal void SelectQuest(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var item = Quests.FirstOrDefault(q => q.Quest.Id == id);
        if (item == null)
        {
            Search = "";
            Category = AllCategories;
            RefreshList();
            item = Quests.FirstOrDefault(q => q.Quest.Id == id);
        }
        if (item != null)
        {
            Selected = item;
            ViewMode = "edit";
        }
    }

    internal void SelectChain(QuestChainRowViewModel row) => SelectQuest(row.Id);

    internal void OpenDialogue(DialogueGraph dialogue)
    {
        if (dialogue == null) return;
        _shell.DialogueGraph.LoadDialogueCommand.Execute(dialogue);
        _shell.ActivateTabById(NavigationCatalog.DialoguesId);
    }

    internal int IssueCount(Quest quest) =>
        QuestAnalysis.Validate(new[] { quest }, _project.Languages).Count;

    internal bool IsDuplicate(string id) =>
        _project.Quests.Count(q => q.Id == id) > 1;

    internal string Translation(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
        if (!string.IsNullOrWhiteSpace(PreviewLocale))
        {
            var language = _project.Languages.FirstOrDefault(l => l.Locale == PreviewLocale);
            var entry = language?.Entries.FirstOrDefault(e => e.Id == id);
            return entry?.Text ?? "";
        }

        foreach (var language in _project.Languages)
        {
            var entry = language.Entries.FirstOrDefault(e => e.Id == id && !string.IsNullOrWhiteSpace(e.Text));
            if (entry != null) return entry.Text;
        }
        return "";
    }

    // --- команды ---

    [RelayCommand]
    private void ShowEdit() => ViewMode = "edit";

    [RelayCommand]
    private void ShowChains() => ViewMode = "chains";

    [RelayCommand]
    private void CreateQuest()
    {
        var id = UniqueQuestId("quest");
        var quest = NewQuest(id, "10", id + "_complete");
        MutateQuests(() =>
        {
            _project.Quests.Add(quest);
            _selectedId = id;
        }, LocalizationSource.Get("questsCreated"));
    }

    [RelayCommand]
    private void CreateFromTemplate()
    {
        if (SelectedTemplate == null) return;
        var template = SelectedTemplate;
        var id = UniqueQuestId("quest_" + template.Prefix);
        var quest = NewQuest(id, string.IsNullOrWhiteSpace(template.Prior) ? "10" : template.Prior,
            id + "_complete");
        MutateQuests(() =>
        {
            _project.Quests.Add(quest);
            _selectedId = id;
        }, LocalizationSource.Get("questsCreated") + ": " + template.Label);
        SelectedTemplate = null;
    }

    [RelayCommand]
    private void DeleteQuest()
    {
        var quest = SelectedEditor?.Model;
        if (quest == null) return;
        MutateQuests(() =>
        {
            _project.Quests.Remove(quest);
            _selectedId = null;
        }, LocalizationSource.Get("questsDeleted"));
    }

    [RelayCommand]
    private void Undo()
    {
        var next = _history.Move(_historyState, -1, ApplyHistory);
        if (next.Pos == _historyState.Pos) return;
        _historyState = next;
        AfterHistoryChange();
    }

    [RelayCommand]
    private void Redo()
    {
        var next = _history.Move(_historyState, +1, ApplyHistory);
        if (next.Pos == _historyState.Pos) return;
        _historyState = next;
        AfterHistoryChange();
    }

    private void MutateQuests(Action action, string? status)
    {
        var snapshot = JsonSerializer.Serialize(_project.Quests, ProjectSerializer.Options);
        _historyState = _history.Push(_historyState, snapshot);
        action();
        _selectedId ??= SelectedEditor?.Model.Id;
        RefreshList();
        RefreshChains();
        RefreshDerived();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(TotalText));
        _shell.OnSectionEdited(status);
    }

    private void ApplyHistory(string json)
    {
        try
        {
            _project.Quests = JsonSerializer.Deserialize<List<Quest>>(json, ProjectSerializer.Options)
                              ?? new List<Quest>();
        }
        catch
        {
            _project.Quests = new List<Quest>();
        }
    }

    private void AfterHistoryChange()
    {
        RefreshList();
        RefreshChains();
        RefreshDerived();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(TotalText));
        _shell.OnSectionEdited();
    }

    [RelayCommand]
    private void BindToLocales()
    {
        var quest = SelectedEditor?.Model;
        if (quest == null) return;
        var ids = new[] { quest.Title, quest.Text }
            .Concat(quest.Objectives.Select(o => o.Text))
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            _shell.StatusText = LocalizationSource.Get("questsNoTranslationIds");
            return;
        }

        if (_project.Languages.Count == 0)
            _project.Languages.Add(new Language
            {
                Locale = TextIdTools.DefaultLocale,
                Name = TextIdTools.DefaultLocaleName,
                Entries = new List<LanguageEntry>()
            });

        foreach (var language in _project.Languages)
            foreach (var id in ids)
                if (!language.Entries.Any(e => e.Id == id))
                    language.Entries.Add(new LanguageEntry { Id = id, Text = "" });

        _shell.Languages.Attach(_project);
        BuildLanguageLocales();
        RefreshDerived();
        _shell.OnSectionEdited(LocalizationSource.Get("questsBound") + " (" +
            ids.Count.ToString(CultureInfo.CurrentCulture) + ")");
    }

    [RelayCommand]
    private void ExportXml()
    {
        if (_project.Quests.Count == 0)
        {
            _shell.StatusText = LocalizationSource.Get("questsEmpty");
            return;
        }

        var encoding = _project.Settings.ExportEncoding ?? "windows-1251";
        var name = _project.Quests.Any(q => q.Format == "task_manager")
            ? "task_manager.xml"
            : "game_tasks.xml";
        var path = _dialogs.PickSaveFile(LocalizationSource.Get("questsExportXml"), name,
            "XML (*.xml)|*.xml|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var xml = QuestExport.QuestsToXml(_project.Quests, encoding);
            File.WriteAllBytes(path, GameEncoding.Encode(xml, encoding));
            _shell.StatusText = LocalizationSource.Get("questsExported");
            _shell.StatusDetail = path;
        }
        catch (Exception e)
        {
            _shell.StatusText = LocalizationSource.Get("questsExportFailed") + ": " + e.Message;
        }
    }

    [RelayCommand]
    private void ExportTaskManager()
    {
        var text = QuestExport.TaskManagerLtx(_project.Quests);
        if (string.IsNullOrWhiteSpace(text))
        {
            _shell.StatusText = LocalizationSource.Get("questsNoTaskManager");
            return;
        }

        var path = _dialogs.PickSaveFile(LocalizationSource.Get("questsExportLtx"),
            "task_manager.ltx", "LTX (*.ltx)|*.ltx|*.*|*.*", _shell.SavesDirectory);
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            File.WriteAllBytes(path, GameEncoding.Encode(text, "windows-1251"));
            _shell.StatusText = LocalizationSource.Get("questsExported");
            _shell.StatusDetail = path;
        }
        catch (Exception e)
        {
            _shell.StatusText = LocalizationSource.Get("questsExportFailed") + ": " + e.Message;
        }
    }

    [RelayCommand]
    private void OpenRelated(QuestRelatedViewModel? related)
    {
        if (related != null) OpenDialogue(related.Dialogue);
    }

    private Quest NewQuest(string id, string prior, string complete) => new()
    {
        Id = id,
        Prior = prior,
        Title = id + "_title",
        Text = id + "_text",
        Objectives = new List<QuestObjective>
        {
            new() { Text = id + "_objective", Icon = "", Infoportion = id + "_complete", Article = "" }
        },
        InfoportionComplete = complete,
        InfoportionFail = "",
        Icon = "",
        Location = "",
        Article = "",
        Category = "",
        Requires = new List<string>(),
        Reward = "",
        Format = "game_tasks",
        SourcePath = "",
        Original = null
    };

    private string UniqueQuestId(string prefix)
    {
        var id = prefix + "_" + Guid.NewGuid().ToString("N")[..4];
        while (_project.Quests.Any(q => q.Id == id))
            id = prefix + "_" + Guid.NewGuid().ToString("N")[..4];
        return id;
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(HasQuests));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasRelated));
        OnPropertyChanged(nameof(HasDiff));
        OnPropertyChanged(nameof(HasChains));
        OnPropertyChanged(nameof(HasChainSvg));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(NoQuestsText));
        OnPropertyChanged(nameof(IsTaskManager));
        OnPropertyChanged(nameof(CurrentFormatText));
    }

    private void BuildLanguageLocales()
    {
        LanguageLocales.Clear();
        foreach (var locale in _project.Languages.Select(l => l.Locale)
                     .Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal))
            LanguageLocales.Add(locale);
        OnPropertyChanged(nameof(LanguageLocales));
    }
}
