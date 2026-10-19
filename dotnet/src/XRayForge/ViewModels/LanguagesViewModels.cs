using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Models;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>Таблица переводов одной локали (`languages[]`).</summary>
public sealed partial class LanguageEditorViewModel : ObservableObject
{
    private readonly LanguagesViewModel _owner;
    private readonly HashSet<string> _duplicates = new(StringComparer.Ordinal);

    public LanguageEditorViewModel(LanguagesViewModel owner, Language model)
    {
        _owner = owner;
        Model = model;
        Entries = new ObservableCollection<LanguageEntryViewModel>();
        RebuildEntries();
    }

    public Language Model { get; }
    public ObservableCollection<LanguageEntryViewModel> Entries { get; }
    public LanguagesViewModel Owner => _owner;
    public string CountText => Entries.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) +
        " " + LocalizationSource.Get("scriptLinesShort");
    public string DeleteText => LocalizationSource.Get("scriptDelete");
    public string AddEntryText => LocalizationSource.Get("languagesAddEntry");
    public string LocaleLabel => LocalizationSource.Get("languagesLocale");
    public string NameLabel => LocalizationSource.Get("languagesName");

    public string Locale
    {
        get => Model.Locale;
        set
        {
            var next = value ?? "";
            if (Model.Locale == next) return;
            Model.Locale = next;
            OnPropertyChanged();
            _owner.OnLanguageChanged(this);
        }
    }

    public string Name
    {
        get => Model.Name;
        set
        {
            var next = value ?? "";
            if (Model.Name == next) return;
            Model.Name = next;
            OnPropertyChanged();
            _owner.OnLanguageChanged(this);
        }
    }

    public void RebuildEntries()
    {
        Entries.Clear();
        foreach (var entry in Model.Entries)
            Entries.Add(new LanguageEntryViewModel(this, entry));
        RefreshEntryFlags();
        OnPropertyChanged(nameof(CountText));
    }

    public void RefreshEntryFlags()
    {
        _duplicates.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Model.Entries)
        {
            if (string.IsNullOrEmpty(entry.Id)) continue;
            if (!seen.Add(entry.Id)) _duplicates.Add(entry.Id);
        }

        foreach (var entry in Entries)
        {
            entry.RefreshFlags();
        }
    }

    /// <summary>Подтянуть текст из сводной таблицы без потери объекта записи.</summary>
    public void SyncEntry(LanguageEntry entry)
    {
        var view = Entries.FirstOrDefault(e => ReferenceEquals(e.Model, entry) || e.Id == entry.Id);
        if (view == null) RebuildEntries();
        else view.Reload();
        RefreshEntryFlags();
    }

    internal bool IsDuplicate(string id) => _duplicates.Contains(id);

    public void RaiseLabels()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(AddEntryText));
        OnPropertyChanged(nameof(LocaleLabel));
        OnPropertyChanged(nameof(NameLabel));
    }

    [RelayCommand]
    private void DeleteTable() => _owner.DeleteTable(this);

    [RelayCommand]
    private void AddEntry() => _owner.AddEntry(this);
}

/// <summary>Строка перевода: id и текст, с флагами дубликата/пустого перевода.</summary>
public sealed partial class LanguageEntryViewModel : ObservableObject
{
    private readonly LanguageEditorViewModel _language;

    public LanguageEntryViewModel(LanguageEditorViewModel language, LanguageEntry model)
    {
        _language = language;
        Model = model;
    }

    public LanguageEntry Model { get; }
    public string Id
    {
        get => Model.Id;
        set
        {
            var next = value ?? "";
            if (Model.Id == next) return;
            Model.Id = next;
            OnPropertyChanged();
            _language.RefreshEntryFlags();
            _language.Owner.OnEntryChanged(_language, this);
        }
    }

    public string Text
    {
        get => Model.Text;
        set
        {
            var next = value ?? "";
            if (Model.Text == next) return;
            Model.Text = next;
            OnPropertyChanged();
            _language.Owner.OnEntryChanged(_language, this);
        }
    }

    public bool IsDuplicate => _language.IsDuplicate(Model.Id);
    public bool IsEmpty => string.IsNullOrWhiteSpace(Model.Text);
    public int Usage => _language.Owner.UsageOf(Model.Id);
    public bool HasUsage => Usage > 0;
    public string UsageText => LocalizationSource.Get("languagesUsage") + ": " + Usage;
    public string ToolTip => HasUsage
        ? LocalizationSource.Get("languagesUsedIn") + " " + Usage
        : LocalizationSource.Get("languagesUnused");

    public void RefreshFlags()
    {
        OnPropertyChanged(nameof(IsDuplicate));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Usage));
        OnPropertyChanged(nameof(HasUsage));
        OnPropertyChanged(nameof(UsageText));
        OnPropertyChanged(nameof(ToolTip));
    }

    public void Reload()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Text));
        RefreshFlags();
    }

    [RelayCommand]
    private void Remove() => _language.Owner.RemoveEntry(_language, this);
}

/// <summary>Строка сводной таблицы: id и ячейки по локалям (`Summary` в JS).</summary>
public sealed class LanguageSummaryRowViewModel
{
    public LanguageSummaryRowViewModel(LanguagesViewModel owner, string id, IEnumerable<string> locales)
    {
        Id = id;
        Cells = new ObservableCollection<LanguageSummaryCellViewModel>(
            locales.Select(locale => new LanguageSummaryCellViewModel(owner, id, locale)));
        foreach (var cell in Cells) cell.SetInitial(owner.TextFor(cell.Locale, id));
    }

    public string Id { get; }
    public ObservableCollection<LanguageSummaryCellViewModel> Cells { get; }
}

/// <summary>Ячейка сводной таблицы: правка текста нужной локали.</summary>
public sealed partial class LanguageSummaryCellViewModel : ObservableObject
{
    private readonly LanguagesViewModel _owner;

    public LanguageSummaryCellViewModel(LanguagesViewModel owner, string id, string locale)
    {
        _owner = owner;
        Id = id;
        Locale = locale;
    }

    public string Id { get; }
    public string Locale { get; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    private string _text = "";

    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? "";
            if (_text == next) return;
            _text = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
            _owner.SetSummaryCell(this, _text);
        }
    }

    public void SetInitial(string text)
    {
        _text = text ?? "";
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

/// <summary>Проблема перевода: пустой текст, дубликат id или отсутствие перевода.</summary>
public sealed class LanguageIssueViewModel
{
    public LanguageIssueViewModel(string message, string locale, string textId)
    {
        Message = message;
        Locale = locale;
        TextId = textId;
    }

    public string Message { get; }
    public string Locale { get; }
    public string TextId { get; }
    public string Text => Message + (string.IsNullOrEmpty(TextId) ? "" :
        " · " + TextId + (string.IsNullOrEmpty(Locale) ? "" : "@" + Locale));
}
