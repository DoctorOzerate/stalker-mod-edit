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
        Series = new ObservableCollection<GameSeriesOption>(GameSeriesCatalog.Items);
        LoadFromProject();
    }

    /// <summary>Актуальный проект оболочки (правки применяются в <see cref="Commit"/>).</summary>
    public Project Project => _shell.Project;

    /// <summary>Серии игры (`GameSeriesCatalog.Items`: ТЧ/ЧН/ЗП).</summary>
    public ObservableCollection<GameSeriesOption> Series { get; }

    /// <summary>Кодировки ввода-вывода (`decodeGameBytes`/`cp1251Encode` в JS).</summary>
    public IReadOnlyList<string> Encodings { get; } = new[] { "auto", "utf-8", "windows-1251", "cp866", "utf-16le" };

    /// <summary>Сводка по сущностям проекта (счётчики окна).</summary>
    public ProjectSummary Summary => ProjectSummary.From(Project);

    /// <summary>Имя проекта (`projectName`).</summary>
    [ObservableProperty]
    private string _projectName = "";

    /// <summary>Идентификатор диалога проекта (`dialogueId`).</summary>
    [ObservableProperty]
    private string _dialogueId = "";

    /// <summary>Серия игры (`series`): влияет на каталог экспорта `config`/`configs`.</summary>
    [ObservableProperty]
    private GameSeriesOption _selectedSeries = GameSeriesCatalog.Items[0];

    /// <summary>Кодировка импорта (`settings.importEncoding`).</summary>
    [ObservableProperty]
    private string _importEncoding = "";

    /// <summary>Кодировка экспорта (`settings.exportEncoding`).</summary>
    [ObservableProperty]
    private string _exportEncoding = "";

    /// <summary>Показывать текст реплик в узлах графа (`settings.showNodeText`).</summary>
    [ObservableProperty]
    private bool _showNodeText;

    /// <summary>Путь к gamedata (`gamedataPath`).</summary>
    [ObservableProperty]
    private string _gamedataPath = "";

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

    /// <summary>Количество точек сохранения в истории профиля.</summary>
    public int BackupCount => _shell.Backups.Count;

    /// <summary>`saveBackups`: снимок текущего состояния проекта из настроек проекта.</summary>
    [RelayCommand]
    private void AddBackup()
    {
        _shell.AddBackupCommand.Execute(null);
        OnPropertyChanged(nameof(BackupCount));
    }

    /// <summary>`backups`: история точек сохранения профиля (окно «Копии»).</summary>
    [RelayCommand]
    private void OpenBackups()
    {
        var project = _shell.Project;
        _shell.OpenBackupsCommand.Execute(null);
        // Восстановление снимка заменяет объект проекта в оболочке — перечитываем поля,
        // чтобы «ОК» не записал значения старого проекта.
        if (!ReferenceEquals(project, _shell.Project)) LoadFromProject();
        OnPropertyChanged(nameof(BackupCount));
    }

    /// <summary>Перечитать поля окна из актуального проекта оболочки.</summary>
    private void LoadFromProject()
    {
        var project = Project;
        ProjectName = project.ProjectName;
        DialogueId = project.DialogueId;
        SelectedSeries = Series.FirstOrDefault(item => item.Value == GameSeriesCatalog.Normalize(project.Series)) ?? Series[0];
        ImportEncoding = project.Settings.ImportEncoding;
        ExportEncoding = project.Settings.ExportEncoding;
        ShowNodeText = project.Settings.ShowNodeText;
        GamedataPath = project.GamedataPath;
        OnPropertyChanged(nameof(Summary));
    }

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
