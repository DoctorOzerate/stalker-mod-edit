using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Dialogs;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Text;
using XRayForge.Core.Ui;
using XRayForge.Core.Xml;
using XRayForge.Localization;
using XRayForge.Services;

namespace XRayForge.ViewModels;

/// <summary>
/// Вкладка «Языки» (этап 7.6, `LanguagesTab.js`): таблицы `string_table` по локалям,
/// сводная таблица всех локалей, импорт XML, подсветка пустых переводов/дубликатов,
/// контекст использования в репликах и покрытие text_id.
/// </summary>
public sealed partial class LanguagesViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private Project _project = ProjectMigration.Empty();
    private bool _loading;

    public LanguagesViewModel(ShellViewModel shell, IDialogService dialogs)
    {
        _shell = shell;
        _dialogs = dialogs;
        Languages = new ObservableCollection<LanguageEditorViewModel>();
        SummaryRows = new ObservableCollection<LanguageSummaryRowViewModel>();
        Issues = new ObservableCollection<LanguageIssueViewModel>();
        Attach(ProjectMigration.Empty());
    }

    public ObservableCollection<LanguageEditorViewModel> Languages { get; }
    public ObservableCollection<LanguageSummaryRowViewModel> SummaryRows { get; }
    public ObservableCollection<LanguageIssueViewModel> Issues { get; }

    [ObservableProperty] private bool _summaryMode;

    public bool HasLanguages => Languages.Count > 0;
    public bool HasIssues => Issues.Count > 0;
    public bool HasSummaryRows => SummaryRows.Count > 0;
    public string ModeText => SummaryMode
        ? LocalizationSource.Get("languagesByLocale")
        : LocalizationSource.Get("languagesSummaryMode");
    public string IssuesTitle => LocalizationSource.Get("languagesIssues") + " · " +
        Issues.Count.ToString(CultureInfo.CurrentCulture);
    public string StatsText => LocalizationSource.Get("languagesStats") + ": " +
        TotalEntries.ToString(CultureInfo.CurrentCulture) + " · " +
        LocalizationSource.Get("languagesCoverage") + ": " + Coverage.Pct.ToString(CultureInfo.CurrentCulture) + "%";
    public string TablesText => LocalizationSource.Get("languagesTables") + ": " +
        Languages.Count.ToString(CultureInfo.CurrentCulture);
    public int TotalEntries => Languages.Sum(language => language.Entries.Count);
    public TextIdCoverage Coverage { get; private set; } = new() { Pct = 100 };

    public void Attach(Project project)
    {
        _project = project ?? ProjectMigration.Empty();
        _project.Languages ??= new List<Language>();

        _loading = true;
        BuildEditors();
        RefreshSummary();
        RefreshIssues();
        _loading = false;
        NotifyAll();
    }

    public void RefreshLocalization()
    {
        foreach (var language in Languages) language.RaiseLabels();
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(IssuesTitle));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(TablesText));
    }

    partial void OnSummaryModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeText));
        if (value) RefreshSummary();
    }

    [RelayCommand]
    private void ToggleSummary() => SummaryMode = !SummaryMode;

    [RelayCommand]
    private void AddTable()
    {
        var language = new Language
        {
            Locale = UniqueLocale("rus"),
            Name = TextIdTools.DefaultLocaleName,
            Entries = new List<LanguageEntry> { new() { Id = "st_new", Text = "" } }
        };
        _project.Languages.Add(language);
        BuildEditors();
        RefreshSummary();
        RefreshIssues();
        OnChanged(LocalizationSource.Get("languagesTableCreated"));
    }

    internal void DeleteTable(LanguageEditorViewModel language)
    {
        if (!_dialogs.Confirm(LocalizationSource.Get("languagesDeleteTable") + ": " + language.Locale)) return;
        _project.Languages.Remove(language.Model);
        BuildEditors();
        RefreshSummary();
        RefreshIssues();
        OnChanged(LocalizationSource.Get("languagesTableDeleted"));
    }

    [RelayCommand]
    private void ImportXml()
    {
        var files = _dialogs.PickOpenFiles(LocalizationSource.Get("languagesImport"),
            "string_table (*.xml)|*.xml|*.*|*.*", _project.GamedataPath);
        if (files.Count == 0) return;

        var incoming = new List<Language>();
        foreach (var file in files)
        {
            try
            {
                var text = GameEncoding.Decode(File.ReadAllBytes(file), _project.Settings.ImportEncoding);
                var language = StringTableParser.Parse(text, file);
                if (language != null) incoming.Add(language);
            }
            catch
            {
                // Повреждённый XML пропускается, как при сканировании gamedata.
            }
        }

        if (incoming.Count == 0)
        {
            _shell.StatusText = LocalizationSource.Get("languagesImportEmpty");
            return;
        }

        _project.Languages = XmlHelpers.MergeLanguages(_project.Languages, incoming);
        BuildEditors();
        RefreshSummary();
        RefreshIssues();
        OnChanged(LocalizationSource.Get("languagesImported") + ": " +
            incoming.Count.ToString(CultureInfo.CurrentCulture));
    }

    internal void AddEntry(LanguageEditorViewModel language)
    {
        var id = UniqueEntryId(language.Model, "st_new");
        language.Model.Entries.Add(new LanguageEntry { Id = id, Text = "" });
        language.RebuildEntries();
        RefreshSummary();
        RefreshIssues();
        OnChanged(LocalizationSource.Get("languagesEntryAdded"));
    }

    internal void RemoveEntry(LanguageEditorViewModel language, LanguageEntryViewModel entry)
    {
        language.Model.Entries.Remove(entry.Model);
        language.RebuildEntries();
        RefreshSummary();
        RefreshIssues();
        OnChanged(LocalizationSource.Get("languagesEntryRemoved"));
    }

    internal void OnLanguageChanged(LanguageEditorViewModel language)
    {
        if (_loading) return;
        language.RefreshEntryFlags();
        RefreshIssues();
        OnChanged();
    }

    internal void OnEntryChanged(LanguageEditorViewModel language, LanguageEntryViewModel entry)
    {
        if (_loading) return;
        language.RefreshEntryFlags();
        entry.RefreshFlags();
        RefreshIssues();
        OnChanged();
    }

    internal string TextFor(string locale, string id) =>
        _project.Languages.FirstOrDefault(l => l.Locale == locale)?.Entries
            .FirstOrDefault(e => e.Id == id)?.Text ?? "";

    internal int UsageOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        var count = 0;
        foreach (var node in _project.Nodes)
            if (node.Type == NodeTypes.Phrase && node.Data?.TextId == id) count++;
        foreach (var dialogue in _project.Dialogues)
            foreach (var node in dialogue.Nodes)
                if (node.Type == NodeTypes.Phrase && node.Data?.TextId == id) count++;
        return count;
    }

    internal void SetSummaryCell(LanguageSummaryCellViewModel cell, string? value)
    {
        var text = value ?? "";
        var language = _project.Languages.FirstOrDefault(l => l.Locale == cell.Locale);
        if (language == null) return;

        var entry = language.Entries.FirstOrDefault(e => e.Id == cell.Id);
        if (entry == null)
        {
            entry = new LanguageEntry { Id = cell.Id, Text = text };
            language.Entries.Add(entry);
        }
        else
        {
            entry.Text = text;
        }

        var editor = Languages.FirstOrDefault(l => ReferenceEquals(l.Model, language));
        editor?.SyncEntry(entry);
        RefreshIssues();
        OnChanged();
    }

    private void BuildEditors()
    {
        Languages.Clear();
        foreach (var language in _project.Languages)
            Languages.Add(new LanguageEditorViewModel(this, language));
    }

    private void RefreshSummary()
    {
        SummaryRows.Clear();
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var language in _project.Languages)
            foreach (var entry in language.Entries)
                if (!string.IsNullOrEmpty(entry.Id)) ids.Add(entry.Id);

        var locales = _project.Languages.Where(l => !string.IsNullOrEmpty(l.Locale))
            .Select(l => l.Locale).Distinct(StringComparer.Ordinal).ToList();
        foreach (var id in ids)
            SummaryRows.Add(new LanguageSummaryRowViewModel(this, id, locales));

        OnPropertyChanged(nameof(HasSummaryRows));
    }

    private void RefreshIssues()
    {
        Issues.Clear();
        Coverage = TextIdTools.Coverage(_project.Dialogues, _project.Languages);
        foreach (var issue in TextIdTools.FindTextIdIssues(_project.Languages, _project.Dialogues))
            Issues.Add(new LanguageIssueViewModel(issue.Message, issue.Locale ?? "", issue.TextId));

        foreach (var issue in TextIdTools.ValidateTextIds(_project.Dialogues, _project.Languages))
            Issues.Add(new LanguageIssueViewModel(issue.Message, "", issue.TextId));

        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(IssuesTitle));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(TablesText));
    }

    private void OnChanged(string? status = null)
    {
        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(TablesText));
        _shell.OnSectionEdited(status);
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(HasLanguages));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasSummaryRows));
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(IssuesTitle));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(TablesText));
        OnPropertyChanged(nameof(TotalEntries));
    }

    private string UniqueLocale(string baseLocale)
    {
        var locale = baseLocale;
        var index = 2;
        while (_project.Languages.Any(l => l.Locale == locale)) locale = baseLocale + index++;
        return locale;
    }

    private static string UniqueEntryId(Language language, string baseId)
    {
        var id = baseId;
        var index = language.Entries.Count;
        while (language.Entries.Any(e => e.Id == id)) id = baseId + "_" + index++;
        return id;
    }
}
