using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Projects;
using XRayForge.Core.Ui;

namespace XRayForge.ViewModels;

/// <summary>
/// Модель окна «Настройки проекта» (`projectSettings`): имя проекта, серия игры,
/// кодировки импорта/экспорта, показ текста узлов и путь к gamedata.
/// Значения записываются в проект при «Сохранить» (<see cref="Commit"/>).
/// </summary>
public sealed partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    public ProjectSettingsViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Project = shell.Project;
        Series = new ObservableCollection<GameSeriesOption>(GameSeriesCatalog.Items);

        _projectName = Project.ProjectName;
        _dialogueId = Project.DialogueId;
        _selectedSeries = Series.FirstOrDefault(item => item.Value == GameSeriesCatalog.Normalize(Project.Series)) ?? Series[0];
        _importEncoding = Project.Settings.ImportEncoding;
        _exportEncoding = Project.Settings.ExportEncoding;
        _showNodeText = Project.Settings.ShowNodeText;
        _gamedataPath = Project.GamedataPath;
    }

    /// <summary>Проект оболочки (правки применяются в <see cref="Commit"/>).</summary>
    public Project Project { get; }

    /// <summary>Серии игры (`GameSeriesCatalog.Items`: ТЧ/ЧН/ЗП).</summary>
    public ObservableCollection<GameSeriesOption> Series { get; }

    /// <summary>Кодировки ввода-вывода (`decodeGameBytes`/`cp1251Encode` в JS).</summary>
    public IReadOnlyList<string> Encodings { get; } = new[] { "auto", "utf-8", "windows-1251", "cp866", "utf-16le" };

    /// <summary>Сводка по сущностям проекта (счётчики окна).</summary>
    public ProjectSummary Summary => ProjectSummary.From(Project);

    /// <summary>Имя проекта (`projectName`).</summary>
    [ObservableProperty]
    private string _projectName;

    /// <summary>Идентификатор диалога проекта (`dialogueId`).</summary>
    [ObservableProperty]
    private string _dialogueId;

    /// <summary>Серия игры (`series`): влияет на каталог экспорта `config`/`configs`.</summary>
    [ObservableProperty]
    private GameSeriesOption _selectedSeries;

    /// <summary>Кодировка импорта (`settings.importEncoding`).</summary>
    [ObservableProperty]
    private string _importEncoding;

    /// <summary>Кодировка экспорта (`settings.exportEncoding`).</summary>
    [ObservableProperty]
    private string _exportEncoding;

    /// <summary>Показывать текст реплик в узлах графа (`settings.showNodeText`).</summary>
    [ObservableProperty]
    private bool _showNodeText;

    /// <summary>Путь к gamedata (`gamedataPath`).</summary>
    [ObservableProperty]
    private string _gamedataPath;

    /// <summary>`showDirectoryPicker`: выбрать каталог gamedata.</summary>
    [RelayCommand]
    private void ChooseFolder()
    {
        var folder = _shell.SelectFolder(GamedataPath);
        if (!string.IsNullOrWhiteSpace(folder)) GamedataPath = folder!;
    }

    /// <summary>`processGamedataMap`: запустить сканирование (окно настроек остаётся открытым).</summary>
    [RelayCommand]
    private void ScanGamedata() => _shell.ScanGamedataCommand.Execute(null);

    /// <summary>Записать значения полей в проект (вызывает окно при «Сохранить»).</summary>
    public void Commit()
    {
        Project.ProjectName = string.IsNullOrWhiteSpace(ProjectName) ? ProjectSchema.DefaultProjectName : ProjectName.Trim();
        Project.DialogueId = string.IsNullOrWhiteSpace(DialogueId) ? Project.ProjectName : DialogueId.Trim();
        Project.Series = SelectedSeries?.Value ?? GameSeriesCatalog.DefaultSeries;
        Project.Settings.ImportEncoding = ImportEncoding;
        Project.Settings.ExportEncoding = ExportEncoding;
        Project.Settings.ShowNodeText = ShowNodeText;
        Project.GamedataPath = GamedataPath ?? "";
    }
}
