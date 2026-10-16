using System.Globalization;
using XRayForge.Core.Projects;
using XRayForge.Core.Storage;
using XRayForge.Core.Ui;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>
/// Точка сохранения в окне «Копии»: снимок `StoredBackup` (метка времени `t` и данные проекта)
/// плюс сводка по сущностям — как элементы `BACKUPS_KEY` из App.js.
/// </summary>
public sealed class BackupItemViewModel
{
    public BackupItemViewModel(StoredBackup backup)
    {
        Backup = backup;
        Summary = ProjectSummary.From(backup.Data);
    }

    /// <summary>Снимок профиля (восстановление в `ShellViewModel.RestoreBackup`).</summary>
    public StoredBackup Backup { get; }

    /// <summary>Метка времени снимка (мс от эпохи — как `Date.now()` в JS).</summary>
    public long Timestamp => Backup.Timestamp;

    /// <summary>Сводка по сущностям снимка.</summary>
    public ProjectSummary Summary { get; }

    /// <summary>Имя проекта в снимке.</summary>
    public string Name => string.IsNullOrWhiteSpace(Backup.Data.ProjectName)
        ? ProjectSchema.DefaultProjectName
        : Backup.Data.ProjectName;

    /// <summary>Локальное время снимка для подписи кнопки «Восстановить».</summary>
    public string Text => Timestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
        : "";

    /// <summary>Строка-подпись списка: имя проекта, время и счётчик сущностей.</summary>
    public string Detail => Name + " · " + Summary.WelcomeCount.ToString(CultureInfo.CurrentCulture) +
        " " + LocalizationSource.Get("entities").ToLowerInvariant();
}
