using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Ltx;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>Секция `system.ltx` со свёрнутым списком полей (`expandedSys` в JS).</summary>
public sealed partial class SystemSectionViewModel : ObservableObject
{
    private readonly SystemLtxViewModel _owner;

    public SystemSectionViewModel(SystemLtxViewModel owner, string name, IEnumerable<string> parents,
        IEnumerable<LtxEntry> entries)
    {
        _owner = owner;
        Name = name;
        Parents = parents.ToList();
        Entries = new ObservableCollection<SystemEntryViewModel>();
        foreach (var entry in entries)
            Entries.Add(new SystemEntryViewModel(owner, entry.Line, entry.Key, entry.Value));
    }

    public string Name { get; }
    public List<string> Parents { get; }
    public ObservableCollection<SystemEntryViewModel> Entries { get; }
    public string Title => "[" + Name + "]" + (Parents.Count > 0 ? ":" + string.Join(",", Parents) : "");
    public string Marker => IsExpanded ? "▾" : "▸";
    public string CountText => Entries.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    public string AddEntryText => LocalizationSource.Get("systemAddEntry");
    public string RemoveEntryText => LocalizationSource.Get("systemRemoveEntry");

    [ObservableProperty] private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Marker));

    public void RaiseLabels()
    {
        OnPropertyChanged(nameof(AddEntryText));
        OnPropertyChanged(nameof(RemoveEntryText));
    }

    [RelayCommand]
    private void Toggle() => _owner.ToggleSection(this);

    [RelayCommand]
    private void AddEntry() => _owner.AddEntry(this);
}

/// <summary>Поле секции: правка строки на месте с сохранением комментария.</summary>
public sealed partial class SystemEntryViewModel : ObservableObject
{
    private readonly SystemLtxViewModel _owner;
    private string _key;
    private string _value;

    public SystemEntryViewModel(SystemLtxViewModel owner, int line, string key, string value)
    {
        _owner = owner;
        Line = line;
        _key = key;
        _value = value;
    }

    public int Line { get; }

    public string Key
    {
        get => _key;
        set
        {
            if (_key == value) return;
            _key = value ?? "";
            OnPropertyChanged();
            _owner.UpdateEntry(Line, _key, _value);
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value ?? "";
            OnPropertyChanged();
            _owner.UpdateEntry(Line, _key, _value);
        }
    }

    [RelayCommand]
    private void Remove() => _owner.RemoveEntry(Line);
}


/// <summary>Строка списка `#include`: битая/найденная, подсказка исправления и предпросмотр.</summary>
public sealed class SystemIncludeViewModel
{
    public SystemIncludeViewModel(int line, string raw, string path, bool found, string? suggestion,
        string preview = "")
    {
        Line = line;
        Raw = raw;
        Path = path;
        Found = found;
        Suggestion = suggestion;
        Preview = preview;
    }

    public int Line { get; }
    public string Raw { get; }
    public string Path { get; }
    public bool Found { get; }
    public string? Suggestion { get; }
    public string Preview { get; }
    public bool IsBroken => !Found;
    public bool HasSuggestion => Suggestion != null;
    public bool HasPreview => Preview.Length > 0;
    public string Status => Found ? "✓" : "✗";
    public string Text => Line.ToString(System.Globalization.CultureInfo.CurrentCulture) + ": " + Raw.Trim();
    public string ToolTip => Found
        ? LocalizationSource.Get("systemIncludeFound")
        : LocalizationSource.Get("systemIncludeMissing");
    public string FixText => LocalizationSource.Get("systemIncludeFix");
    public string CommentText => LocalizationSource.Get("systemIncludeComment");
}

/// <summary>Узел дерева инклюдов (`includeTreeRows`).</summary>
public sealed class IncludeTreeRowViewModel
{
    public IncludeTreeRowViewModel(IncludeTreeRow row)
    {
        Path = row.Path;
        Depth = row.Depth;
        Cyclic = row.Cyclic;
        Found = row.Found;
    }

    public string Path { get; }
    public int Depth { get; }
    public bool Cyclic { get; }
    public bool Found { get; }
    public double Indent => Depth * 14;
    public string Marker => Cyclic ? "↻ " : "";
    public string Text => Marker + Path + (Found ? "" : " ✗");
}

/// <summary>Секция в карте всех загруженных LTX; `Files` — источники дубликата.</summary>
public sealed class SectionMapRowViewModel
{
    public SectionMapRowViewModel(string name, List<string> files)
    {
        Name = name;
        Files = files;
    }

    public string Name { get; }
    public List<string> Files { get; }
    public bool IsDuplicate => Files.Count > 1;
    public string CountText => Files.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
    public string Text => (IsDuplicate ? "⚠ " : "") + Name + " · " + CountText;
    public string ToolTip => string.Join(", ", Files);
}

/// <summary>Строка проверки system.ltx: LTX-ссылки, циклы, дубликаты и script_binding.</summary>
public sealed class SystemIssueViewModel
{
    public SystemIssueViewModel(string severity, string text, string? jumpTarget = null)
    {
        Severity = severity;
        Text = text;
        JumpTarget = jumpTarget;
    }

    public string Severity { get; }
    public string Text { get; }
    public string? JumpTarget { get; }
    public bool IsError => Severity == "error";
    public bool CanJump => !string.IsNullOrWhiteSpace(JumpTarget);
}

/// <summary>Загруженный LTX-файл (gamedata): выбор и правка текста на месте.</summary>
public sealed partial class LtxFileViewModel : ObservableObject
{
    private readonly SystemLtxViewModel _owner;

    public LtxFileViewModel(SystemLtxViewModel owner, string path, string text)
    {
        _owner = owner;
        Path = path;
        _text = text;
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string DownloadText => LocalizationSource.Get("scriptDownload");
    public string DeleteText => LocalizationSource.Get("scriptDelete");

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _text;

    partial void OnTextChanged(string value) => _owner.OnLtxFileTextChanged(this);
}
