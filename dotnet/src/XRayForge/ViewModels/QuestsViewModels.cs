using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Models;
using XRayForge.Core.Quests;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>Редактор выбранного квеста: поля обеих схем, цели, condlist и переводы.</summary>
public sealed partial class QuestEditorViewModel : ObservableObject
{
    private readonly QuestsViewModel _owner;

    public QuestEditorViewModel(QuestsViewModel owner, Quest model)
    {
        _owner = owner;
        Model = model;
        Objectives = new ObservableCollection<QuestObjectiveViewModel>();
        Condlist = new ObservableCollection<QuestCondlistViewModel>();
        RebuildChildren();
    }

    public Quest Model { get; }
    public QuestsViewModel Owner => _owner;
    public ObservableCollection<QuestObjectiveViewModel> Objectives { get; }
    public ObservableCollection<QuestCondlistViewModel> Condlist { get; }
    public bool IsTaskManager => Model.Format == "task_manager";
    public bool HasObjectives => Objectives.Count > 0;
    public bool HasCondlist => Condlist.Count > 0;
    public string AddObjectiveText => LocalizationSource.Get("questsAddObjective");
    public string RemoveObjectiveText => LocalizationSource.Get("scriptDelete");
    public string AddCondText => LocalizationSource.Get("questsAddCondition");
    public string TitleTranslation => _owner.Translation(Model.Title);
    public string TextTranslation => _owner.Translation(Model.Text);
    public string Header => Model.Id;

    public string Id
    {
        get => Model.Id;
        set => SetField(Model.Id, value, next => Model.Id = next, identity: true);
    }

    public string Prior
    {
        get => Model.Prior;
        set => SetField(Model.Prior, value, next => Model.Prior = next);
    }

    public string Title
    {
        get => Model.Title;
        set => SetField(Model.Title, value, next => Model.Title = next);
    }

    public string Text
    {
        get => Model.Text;
        set => SetField(Model.Text, value, next => Model.Text = next);
    }

    public string RequiresText
    {
        get => string.Join(", ", Model.Requires);
        set
        {
            var parts = (value ?? "").Split(new[] { ',', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (Model.Requires.SequenceEqual(parts)) return;
            Model.Requires = parts;
            OnPropertyChanged();
            _owner.OnQuestChanged(this);
        }
    }

    public string Reward
    {
        get => Model.Reward;
        set => SetField(Model.Reward, value, next => Model.Reward = next);
    }

    public string Category
    {
        get => Model.Category;
        set => SetField(Model.Category, value, next => Model.Category = next);
    }

    public string InfoportionComplete
    {
        get => Model.InfoportionComplete;
        set => SetField(Model.InfoportionComplete, value, next => Model.InfoportionComplete = next);
    }

    public string InfoportionFail
    {
        get => Model.InfoportionFail;
        set => SetField(Model.InfoportionFail, value, next => Model.InfoportionFail = next);
    }

    public string Icon
    {
        get => Model.Icon;
        set => SetField(Model.Icon, value, next => Model.Icon = next);
    }

    public string Location
    {
        get => Model.Location;
        set => SetField(Model.Location, value, next => Model.Location = next);
    }

    public string Article
    {
        get => Model.Article;
        set => SetField(Model.Article, value, next => Model.Article = next);
    }

    public string Storyline
    {
        get => Model.Storyline;
        set => SetField(Model.Storyline, value, next => Model.Storyline = next);
    }

    public string RewardMoney
    {
        get => Model.RewardMoney;
        set => SetField(Model.RewardMoney, value, next => Model.RewardMoney = next);
    }

    public string RewardItem
    {
        get => Model.RewardItem;
        set => SetField(Model.RewardItem, value, next => Model.RewardItem = next);
    }

    public string RewardTreasure
    {
        get => Model.RewardTreasure;
        set => SetField(Model.RewardTreasure, value, next => Model.RewardTreasure = next);
    }


    public void RebuildChildren()
    {
        Objectives.Clear();
        for (var i = 0; i < Model.Objectives.Count; i++)
            Objectives.Add(new QuestObjectiveViewModel(this, Model.Objectives[i], i));
        Condlist.Clear();
        var conds = Model.Condlist ?? new List<string>();
        for (var i = 0; i < conds.Count; i++)
            Condlist.Add(new QuestCondlistViewModel(this, conds[i], i));

        OnPropertyChanged(nameof(HasObjectives));
        OnPropertyChanged(nameof(HasCondlist));
        RefreshTranslations();
    }

    public void RefreshTranslations()
    {
        OnPropertyChanged(nameof(TitleTranslation));
        OnPropertyChanged(nameof(TextTranslation));
        OnPropertyChanged(nameof(Header));
        foreach (var objective in Objectives) objective.RefreshTranslation();
    }

    public void RaiseLabels()
    {
        OnPropertyChanged(nameof(AddObjectiveText));
        OnPropertyChanged(nameof(RemoveObjectiveText));
        OnPropertyChanged(nameof(AddCondText));
        RefreshTranslations();
    }

    internal void AddObjective()
    {
        Model.Objectives.Add(new QuestObjective());
        RebuildChildren();
        _owner.OnObjectiveChanged(this);
    }

    internal void RemoveObjective(QuestObjectiveViewModel objective)
    {
        Model.Objectives.Remove(objective.Model);
        RebuildChildren();
        _owner.OnObjectiveChanged(this);
    }

    internal void AddCondlist()
    {
        Model.Condlist ??= new List<string>();
        Model.Condlist.Add("");
        RebuildChildren();
        _owner.OnCondlistChanged(this);
    }

    internal void RemoveCondlist(QuestCondlistViewModel row)
    {
        Model.Condlist ??= new List<string>();
        if (row.Index >= 0 && row.Index < Model.Condlist.Count)
            Model.Condlist.RemoveAt(row.Index);
        RebuildChildren();
        _owner.OnCondlistChanged(this);
    }

    internal void UpdateObjective(QuestObjective objective) => _owner.OnObjectiveChanged(this);

    internal void UpdateCondlist(int index, string cond, string action)
    {
        Model.Condlist ??= new List<string>();
        if (index < 0 || index >= Model.Condlist.Count) return;
        Model.Condlist[index] = QuestCatalog.CondlistEntryToStr(cond, action);
        _owner.OnCondlistChanged(this);
    }

    private void SetField(string current, string? value, Action<string> set, bool identity = false)
    {
        var next = value ?? "";
        if (current == next) return;
        set(next);
        OnPropertyChanged();
        OnPropertyChanged(nameof(Header));
        _owner.OnQuestChanged(this, identity);
    }

    [RelayCommand]
    private void AddObjectiveCommand() => AddObjective();

    [RelayCommand]
    private void AddCondlistCommand() => AddCondlist();
}

/// <summary>Цель квеста: текст/иконка/infoportion/article и перевод.</summary>
public sealed partial class QuestObjectiveViewModel : ObservableObject
{
    private readonly QuestEditorViewModel _editor;

    public QuestObjectiveViewModel(QuestEditorViewModel editor, QuestObjective model, int index)
    {
        _editor = editor;
        Model = model;
        Index = index;
    }

    public QuestObjective Model { get; }
    public int Index { get; }
    public string Translation => _editor.Owner.Translation(Model.Text);
    public bool HasTranslation => !string.IsNullOrWhiteSpace(Translation);

    public string Text
    {
        get => Model.Text;
        set
        {
            if (Model.Text == value) return;
            Model.Text = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(Translation));
            OnPropertyChanged(nameof(HasTranslation));
            _editor.UpdateObjective(Model);
        }
    }

    public string Icon
    {
        get => Model.Icon;
        set
        {
            if (Model.Icon == value) return;
            Model.Icon = value ?? "";
            OnPropertyChanged();
            _editor.UpdateObjective(Model);
        }
    }

    public string Infoportion
    {
        get => Model.Infoportion;
        set
        {
            if (Model.Infoportion == value) return;
            Model.Infoportion = value ?? "";
            OnPropertyChanged();
            _editor.UpdateObjective(Model);
        }
    }

    public string Article
    {
        get => Model.Article;
        set
        {
            if (Model.Article == value) return;
            Model.Article = value ?? "";
            OnPropertyChanged();
            _editor.UpdateObjective(Model);
        }
    }

    public void RefreshTranslation()
    {
        OnPropertyChanged(nameof(Translation));
        OnPropertyChanged(nameof(HasTranslation));
    }

    [RelayCommand]
    private void Remove() => _editor.RemoveObjective(this);
}

/// <summary>Строка condlist (`{+info} complete`): условие и действие отдельно.</summary>
public sealed partial class QuestCondlistViewModel : ObservableObject
{
    private readonly QuestEditorViewModel _editor;
    private string _cond;
    private string _action;

    public QuestCondlistViewModel(QuestEditorViewModel editor, string raw, int index)
    {
        _editor = editor;
        Index = index;
        var parsed = QuestCatalog.ParseCondlistEntry(raw);
        _cond = parsed.Cond;
        _action = parsed.Action;
    }

    public int Index { get; }

    public string Cond
    {
        get => _cond;
        set
        {
            if (_cond == value) return;
            _cond = value ?? "";
            OnPropertyChanged();
            _editor.UpdateCondlist(Index, _cond, _action);
        }
    }

    public string Action
    {
        get => _action;
        set
        {
            if (_action == value) return;
            _action = value ?? "";
            OnPropertyChanged();
            _editor.UpdateCondlist(Index, _cond, _action);
        }
    }

    [RelayCommand]
    private void Remove() => _editor.RemoveCondlist(this);
}

/// <summary>Карточка квеста в списке: id, категория, формат, число целей и проблем.</summary>
public sealed partial class QuestListItemViewModel : ObservableObject
{
    private readonly QuestsViewModel _owner;

    public QuestListItemViewModel(QuestsViewModel owner, Quest quest)
    {
        _owner = owner;
        Quest = quest;
    }

    public Quest Quest { get; }
    public string Id => Quest.Id;
    public string Category => Quest.Category;
    public bool HasCategory => !string.IsNullOrWhiteSpace(Quest.Category);
    public bool IsDuplicate => _owner.IsDuplicate(Quest.Id);
    public bool IsCop => Quest.Format == "task_manager";
    public int IssueCount => _owner.IssueCount(Quest);
    public bool HasIssues => IssueCount > 0;
    public string IssuesText => "⚠ " + IssueCount;
    public string Summary => Quest.Objectives.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) +
        " " + LocalizationSource.Get("questsObjectivesShort") +
        (!string.IsNullOrWhiteSpace(Quest.Prior) ? " · prior " + Quest.Prior : "") +
        (IsCop ? " · CoP" : "") +
        (IsDuplicate ? " · " + LocalizationSource.Get("questsDuplicate") : "");

    public void RaiseSummary()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IssueCount));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(IssuesText));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(HasCategory));
        OnPropertyChanged(nameof(IsDuplicate));
    }

    [RelayCommand]
    private void Select() => _owner.SelectQuest(Quest.Id);
}

/// <summary>Проблема проверки квеста (`validateQuests`).</summary>
public sealed class QuestIssueViewModel
{
    public QuestIssueViewModel(string severity, string message)
    {
        Severity = severity;
        Message = message;
    }

    public string Severity { get; }
    public string Message { get; }
    public bool IsError => Severity == "error";
    public string Text => (IsError ? "⛔ " : "⚠ ") + Message;
}

/// <summary>Диалог, выдающий infoportion выбранного квеста.</summary>
public sealed partial class QuestRelatedViewModel : ObservableObject
{
    private readonly QuestsViewModel _owner;

    public QuestRelatedViewModel(QuestsViewModel owner, DialogueGraph dialogue, int hits)
    {
        _owner = owner;
        Dialogue = dialogue;
        Hits = hits;
    }

    public DialogueGraph Dialogue { get; }
    public int Hits { get; }
    public string Text => (!string.IsNullOrWhiteSpace(Dialogue.Name) ? Dialogue.Name : Dialogue.Id) +
        " · " + Hits + " " + LocalizationSource.Get("questsMatches");

    [RelayCommand]
    private void Open() => _owner.OpenDialogue(Dialogue);
}

/// <summary>Узел дерева цепочек: отступ, id и infoportion завершения.</summary>
public sealed partial class QuestChainRowViewModel : ObservableObject
{
    private readonly QuestsViewModel _owner;

    public QuestChainRowViewModel(QuestsViewModel owner, string id, int depth, string infoportion)
    {
        _owner = owner;
        Id = id;
        Depth = depth;
        Infoportion = infoportion;
    }

    public string Id { get; }
    public int Depth { get; }
    public string Infoportion { get; }
    public double Indent => Depth * 18;
    public string Text => Id + (!string.IsNullOrWhiteSpace(Infoportion) ? " → " + Infoportion : "");

    [RelayCommand]
    private void Select() => _owner.SelectChain(this);
}

/// <summary>Строка diff «до/после» по снимку `original`.</summary>
public sealed class QuestDiffRowViewModel
{
    public QuestDiffRowViewModel(string label, string before, string after)
    {
        Label = label;
        Before = before;
        After = after;
    }

    public string Label { get; }
    public string Before { get; }
    public string After { get; }
}
