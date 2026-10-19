using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Lua;
using XRayForge.Core.Projects;
using XRayForge.Core.Scripts;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>Карточка списка скриптов и текущий редактируемый файл (`selScript` в JS).</summary>
public sealed partial class ScriptItemViewModel : ObservableObject
{
    private readonly ScriptsViewModel _owner;
    private bool _loading;

    public ScriptItemViewModel(ScriptsViewModel owner, ScriptFile file)
    {
        _owner = owner;
        File = file;
        _text = ScriptsCatalog.Text(file);
    }

    /// <summary>Источник данных проекта (`scripts[]`).</summary>
    public ScriptFile File { get; }

    public string Name => ScriptsCatalog.Name(File);
    public string Path => string.IsNullOrWhiteSpace(File.Path) ? Name : File.Path;
    public string Subtitle => Path + " · " + ScriptsCatalog.LineCount(File.Text).ToString() + " " +
        LocalizationSource.Get("scriptLinesShort");
    public bool HasChanges => ScriptsCatalog.HasChanges(File);
    public string ChangeMark => HasChanges ? "● " + LocalizationSource.Get("scriptModified") : "";
    public string Original => ScriptsCatalog.OriginalText(File);
    public string LineNumbers => ScriptsCatalog.LineNumbers(Text);
    public bool HasOriginal => Original.Length > 0;

    [ObservableProperty] private bool _isSelected;

    /// <summary>Текст редактора; каждая правка сразу попадает в проект (`updateScript`).</summary>
    [ObservableProperty] private string _text;

    partial void OnTextChanged(string value)
    {
        if (_loading) return;

        File.Text = value ?? "";
        RaiseMeta();
        _owner.OnScriptTextChanged(this);
    }

    [RelayCommand]
    private void Select() => _owner.SelectItem(this);

    [RelayCommand]
    private void Rename() => _owner.RenameScript(this);

    [RelayCommand]
    private void Delete() => _owner.DeleteScript(this);

    /// <summary>Показать текст без повторной правки документа (замена, формат, вставка).</summary>
    internal void LoadText(string? text)
    {
        var value = text ?? "";
        _loading = true;
        try
        {
            Text = value;
        }
        finally
        {
            _loading = false;
        }

        File.Text = value;
        RaiseMeta();
    }

    /// <summary>Пересчитать подписи после переименования или замены во всех файлах.</summary>
    internal void RaiseMeta()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ChangeMark));
        OnPropertyChanged(nameof(Original));
        OnPropertyChanged(nameof(LineNumbers));
        OnPropertyChanged(nameof(HasOriginal));
    }
}

/// <summary>Строка проверки Lua (`luaCheck` + `luaUndeclaredGlobals` + дубликаты функций).</summary>
public sealed class ScriptDiagnosticViewModel
{
    public ScriptDiagnosticViewModel(LuaIssue issue)
    {
        IsError = issue.Severity == "error";
        var message = !string.IsNullOrWhiteSpace(issue.Message) ? issue.Message! : issue.Code;
        Text = (IsError ? "⛔ " : "⚠ ") +
            (issue.Line is > 0 and int line ? LocalizationSource.Get("scriptLine") + " " + line + ": " : "") +
            message;
    }

    public ScriptDiagnosticViewModel(string message)
    {
        IsError = false;
        Text = "⚠ " + message;
    }

    public bool IsError { get; }
    public string Text { get; }
}

/// <summary>Ребро графа вызовов (`from → to`).</summary>
public sealed class ScriptEdgeViewModel
{
    public ScriptEdgeViewModel(LuaCallEdge edge)
    {
        Text = edge.From + " → " + edge.To;
    }

    public string Text { get; }
}

/// <summary>Функция текущего скрипта: переход к определению и число вызовов.</summary>
public sealed class ScriptFunctionViewModel
{
    public ScriptFunctionViewModel(string name, int calls)
    {
        Name = name;
        Calls = calls;
    }

    public string Name { get; }
    public int Calls { get; }
    public string Text => Name + " (" + Calls + ")";
    public string Tooltip => LocalizationSource.Get("scriptJumpDefinition") + " · " +
        LocalizationSource.Get("scriptCalls") + ": " + Calls;
}
