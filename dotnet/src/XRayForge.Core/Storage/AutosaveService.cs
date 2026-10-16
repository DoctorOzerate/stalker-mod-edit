using XRayForge.Core.Projects;

namespace XRayForge.Core.Storage;

/// <summary>
/// Автосохранение по таймеру (порт `setInterval` из App.js): пишет текущий проект,
/// список проектов и опционально файл в `saves/`, помнит время последнего сохранения.
/// </summary>
public sealed class AutosaveService : IDisposable
{
    private readonly AppDataService _store;
    private readonly Func<Project?> _currentProject;
    private readonly Func<ProgramSettings?> _settings;
    private readonly Func<Project, bool>? _afterSave;
    private Timer? _timer;
    private double _intervalSeconds;

    public AutosaveService(
        AppDataService store,
        Func<Project?> currentProject,
        Func<ProgramSettings?> settings,
        Func<Project, bool>? afterSave = null)
    {
        _store = store;
        _currentProject = currentProject;
        _settings = settings;
        _afterSave = afterSave;
    }

    /// <summary>Время последнего автосохранения (в JS — `lastAutosave`).</summary>
    public DateTimeOffset? LastAutosave { get; private set; }

    /// <summary>Есть ли несохранённые изменения (в JS — `dirty`).</summary>
    public bool Dirty { get; set; }

    /// <summary>Текущий интервал в секундах (0 — автосейв выключен).</summary>
    public double IntervalSeconds => _intervalSeconds;

    /// <summary>Автосохранение выполнено (для статусной строки/тестов).</summary>
    public event EventHandler<Project>? Saved;

    /// <summary>Взять интервал из настроек и (пере)запустить таймер.</summary>
    public void ApplySettings()
    {
        var interval = _settings()?.AutosaveInterval ?? 0;
        if (!double.IsFinite(interval) || interval <= 0)
        {
            Stop();
            return;
        }

        if (_timer != null && Math.Abs(interval - _intervalSeconds) < 0.0001) return;
        Start(interval);
    }

    /// <summary>Запустить таймер с указанным интервалом (секунды).</summary>
    public void Start(double intervalSeconds)
    {
        Stop();
        if (!double.IsFinite(intervalSeconds) || intervalSeconds <= 0) return;
        _intervalSeconds = intervalSeconds;
        var period = TimeSpan.FromSeconds(intervalSeconds);
        _timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }, null, period, period);
    }

    /// <summary>Остановить автосохранение.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _intervalSeconds = 0;
    }

    /// <summary>Одна итерация автосохранения; `false`, если проекта нет.</summary>
    public bool Tick()
    {
        var project = _currentProject();
        if (project == null) return false;

        _store.SaveCurrentProject(project);
        _store.SaveProject(project);
        _afterSave?.Invoke(project);
        LastAutosave = DateTimeOffset.Now;
        Dirty = false;
        Saved?.Invoke(this, project);
        return true;
    }

    public void Dispose() => Stop();
}