using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Characters;
using XRayForge.Core.Models;
using XRayForge.Core.Suggestions;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>
/// Строка списка и поля редактора одного персонажа (`CharactersTab.js`: карточка `groups[g].map`
/// и блок `setC`). Правки пишутся прямо в <see cref="Character"/> — так же, как `patch` в JS
/// заменяет объект в `project.characters`; VM только уведомляет владельца о смене полей.
/// </summary>
public sealed partial class CharacterViewModel : ObservableObject
{
    private readonly CharactersViewModel _owner;
    private bool _loading;

    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _community = "";
    [ObservableProperty] private string _rank = "";
    [ObservableProperty] private string _reputation = "";
    [ObservableProperty] private string _npcRank = "";
    [ObservableProperty] private string _bio = "";
    [ObservableProperty] private string _supplies = "";
    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private string _visual = "";
    [ObservableProperty] private string _startDialogText = "";
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isDuplicate;
    [ObservableProperty] private bool _hasWarnings;

    public CharacterViewModel(CharactersViewModel owner, Character model)
    {
        _owner = owner;
        Model = model;

        // Начальные значения полей: дальше их меняет только пользователь (On…Changed).
        ReloadFromModel();
    }

    /// <summary>Модель проекта, в которую пишутся правки полей редактора.</summary>
    public Character Model { get; }

    /// <summary>Локализованные предупреждения `validateCharacter` (ключи подставляет вкладка).</summary>
    public ObservableCollection<string> Warnings { get; } = new();

    /// <summary>`c.name || c.id` — подпись карточки списка.</summary>
    public string ListTitle => CharacterCatalog.DisplayName(Model);

    /// <summary>`c.id + (isDup ? ' · дубликат' : '')` — вторая строка карточки.</summary>
    public string ListSubtitle => Id + (IsDuplicate ? " · " + LocalizationSource.Get("charDuplicate") : "");

    /// <summary>Иконка группировки (`COMMUNITY_ICONS`) в карточке и превью.</summary>
    public string CommunityGlyph => CharacterCatalog.CommunityIcon(Community);

    /// <summary>`icon: …` в блоке «Иконка и визуал».</summary>
    public string IconText => "icon: " + (string.IsNullOrWhiteSpace(Icon) ? "—" : Icon);

    /// <summary>`visual: …` в блоке «Иконка и визуал».</summary>
    public string VisualText => "visual: " + (string.IsNullOrWhiteSpace(Visual) ? "—" : Visual);

    public IReadOnlyList<SuggestionEntry> RankSuggestions => _owner.SuggestionsFor("rank");

    public IReadOnlyList<SuggestionEntry> ReputationSuggestions => _owner.SuggestionsFor("reputation");

    public IReadOnlyList<SuggestionEntry> NpcRankSuggestions => _owner.SuggestionsFor("npc_rank");

    public IReadOnlyList<SuggestionEntry> IconSuggestions => _owner.SuggestionsFor("icon");

    public IReadOnlyList<SuggestionEntry> VisualSuggestions => _owner.SuggestionsFor("visual");

    public IReadOnlyList<SuggestionEntry> StartDialogSuggestions => _owner.SuggestionsFor("start_dialog");

    /// <summary>Есть снимок `original` — в JS панель diff показывается только для него.</summary>
    public bool HasOriginal => Model.Original != null;

    /// <summary>Перечитать поля из модели (после `AutofillDialogs` и смены проекта).</summary>
    public void ReloadFromModel()
    {
        var loading = _loading;
        _loading = true;
        try
        {
            Id = Model.Id ?? "";
            Name = Model.Name ?? "";
            Community = Model.Community ?? "";
            Rank = Model.Rank ?? "";
            Reputation = Model.Reputation ?? "";
            NpcRank = CharacterCatalog.ExtraString(Model, "npc_rank");
            Bio = Model.Bio ?? "";
            Supplies = Model.Supplies ?? "";
            Icon = Model.Icon ?? "";
            Visual = Model.Visual ?? "";
            StartDialogText = string.Join(",", Model.StartDialog ?? new List<string>());
        }
        finally
        {
            _loading = loading;
        }

        OnPropertyChanged(nameof(ListTitle));
        OnPropertyChanged(nameof(ListSubtitle));
        OnPropertyChanged(nameof(CommunityGlyph));
        OnPropertyChanged(nameof(IconText));
        OnPropertyChanged(nameof(VisualText));
        OnPropertyChanged(nameof(HasOriginal));
    }

    /// <summary>Обновить подписи карточки после смены локали.</summary>
    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(ListTitle));
        OnPropertyChanged(nameof(ListSubtitle));
        OnPropertyChanged(nameof(RankSuggestions));
        OnPropertyChanged(nameof(ReputationSuggestions));
        OnPropertyChanged(nameof(NpcRankSuggestions));
        OnPropertyChanged(nameof(IconSuggestions));
        OnPropertyChanged(nameof(VisualSuggestions));
        OnPropertyChanged(nameof(StartDialogSuggestions));
    }

    /// <summary>Заменить список предупреждений (тексты уже локализованы).</summary>
    public void SetWarnings(IEnumerable<string> warnings)
    {
        Warnings.Clear();
        foreach (var warning in warnings) Warnings.Add(warning);
        HasWarnings = Warnings.Count > 0;
    }

    partial void OnIdChanged(string value)
    {
        if (_loading) return;
        Model.Id = value ?? "";
        OnPropertyChanged(nameof(ListTitle));
        OnPropertyChanged(nameof(ListSubtitle));
        Edit(listChanged: true);
    }

    partial void OnNameChanged(string value)
    {
        if (_loading) return;
        Model.Name = value ?? "";
        OnPropertyChanged(nameof(ListTitle));
        Edit(listChanged: true);
    }

    partial void OnCommunityChanged(string value)
    {
        if (_loading) return;
        Model.Community = value ?? "";
        OnPropertyChanged(nameof(CommunityGlyph));
        Edit(listChanged: true, warningsChanged: true);
    }

    partial void OnRankChanged(string value)
    {
        if (_loading) return;
        Model.Rank = value ?? "";
        Edit();
    }

    partial void OnReputationChanged(string value)
    {
        if (_loading) return;
        Model.Reputation = value ?? "";
        Edit();
    }

    partial void OnNpcRankChanged(string value)
    {
        if (_loading) return;
        CharacterCatalog.SetExtraString(Model, "npc_rank", value);
        Edit();
    }

    partial void OnBioChanged(string value)
    {
        if (_loading) return;
        Model.Bio = value ?? "";
        Edit(listChanged: true);
    }

    partial void OnSuppliesChanged(string value)
    {
        if (_loading) return;
        Model.Supplies = value ?? "";
        Edit();
    }

    partial void OnIconChanged(string value)
    {
        if (_loading) return;
        Model.Icon = value ?? "";
        OnPropertyChanged(nameof(IconText));
        Edit(warningsChanged: true);
    }

    partial void OnVisualChanged(string value)
    {
        if (_loading) return;
        Model.Visual = value ?? "";
        OnPropertyChanged(nameof(VisualText));
        Edit(listChanged: true, warningsChanged: true);
    }

    partial void OnStartDialogTextChanged(string value)
    {
        if (_loading) return;
        Model.StartDialog = ParseReferences(value);
        Edit(listChanged: true, relatedChanged: true);
    }

    partial void OnIsDuplicateChanged(bool value) => OnPropertyChanged(nameof(ListSubtitle));

    /// <summary>Сообщить владельцу о правке: пересборка списка, предупреждений и diff.</summary>
    private void Edit(bool listChanged = false, bool warningsChanged = false, bool relatedChanged = false)
    {
        OnPropertyChanged(nameof(HasOriginal));
        _owner.OnCharacterEdited(this, listChanged, warningsChanged, relatedChanged);
    }

    /// <summary>`String(c.start_dialog || '').split(',')`: список ссылок из поля ввода.</summary>
    private static List<string> ParseReferences(string? text) =>
        (text ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToList();
}
