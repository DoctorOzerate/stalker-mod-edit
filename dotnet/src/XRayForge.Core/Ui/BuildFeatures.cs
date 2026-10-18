namespace XRayForge.Core.Ui;

/// <summary>
/// Признаки конфигурации сборки для отладочных элементов интерфейса.
/// <c>DEBUG</c> задаётся MSBuild только в Debug; Release-сборка оставляет
/// пользовательские действия и убирает инструменты, нужные при разработке.
/// </summary>
public static class BuildFeatures
{
#if DEBUG
    /// <summary>Текущая сборка — Debug (`dotnet build` без `-c Release`).</summary>
    public static readonly bool IsDebug = true;
#else
    /// <summary>Текущая сборка — Release (`dotnet build -c Release`).</summary>
    public static readonly bool IsDebug = false;
#endif

    /// <summary>
    /// Действие доступно только в Debug-сборке. Сейчас это внутренний XML редактора
    /// (`exportXml`): в Release он не нужен ни в тулбаре, ни в горячих клавишах.
    /// </summary>
    public static bool IsDebugOnlyAction(string? actionId) =>
        string.Equals(actionId, "exportXml", StringComparison.Ordinal);
}
