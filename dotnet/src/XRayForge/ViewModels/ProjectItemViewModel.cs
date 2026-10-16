using System.Globalization;
using XRayForge.Core.Projects;
using XRayForge.Core.Storage;
using XRayForge.Core.Ui;

namespace XRayForge.ViewModels;

/// <summary>
/// Проект в приветственном списке (порт разметки `PROJECTS_KEY` из App.js: имя, время
/// обновления и счётчик сущностей).
/// </summary>
public sealed class ProjectItemViewModel
{
    public ProjectItemViewModel(StoredProject stored)
    {
        Name = stored.Name;
        UpdatedAt = stored.UpdatedAt;
    }

    public string Name { get; }

    /// <summary>Метка времени последнего обновления (`updatedAt`, мс от эпохи).</summary>
    public long UpdatedAt { get; }

    /// <summary>Сводка по сущностям проекта (`cnt` в списке проектов).</summary>
    public ProjectSummary Summary { get; private set; } = ProjectSummary.Empty;

    /// <summary>Локальное время обновления для подписи в списке.</summary>
    public string UpdatedText => UpdatedAt > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(UpdatedAt).LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
        : "";

    /// <summary>Счётчик сущностей проекта в списке (`cnt`).</summary>
    public string CountText => Summary.WelcomeCount.ToString(CultureInfo.CurrentCulture);

    /// <summary>Дописать сводку при загрузке списка (данные проекта лежат в профиле).</summary>
    public ProjectItemViewModel WithData(Project? project)
    {
        Summary = ProjectSummary.From(project);
        return this;
    }
}
