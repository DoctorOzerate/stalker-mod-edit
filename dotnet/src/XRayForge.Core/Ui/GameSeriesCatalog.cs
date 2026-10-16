namespace XRayForge.Core.Ui;

/// <summary>Серия игры в настройках проекта (`SERIES` из App.js).</summary>
public sealed record GameSeriesOption(string Value, string Label, string ConfigDirectory);

/// <summary>Серии X-Ray: SoC, CS, CoP и каталог конфигов (`config`/`configs`).</summary>
public static class GameSeriesCatalog
{
    public const string DefaultSeries = "soc";

    public static readonly IReadOnlyList<GameSeriesOption> Items = new[]
    {
        new GameSeriesOption("soc", "SoC · Тень Чернобыля", "config"),
        new GameSeriesOption("cs", "CS · Чистое Небо", "config"),
        new GameSeriesOption("cop", "CoP · Зов Припяти", "configs")
    };

    /// <summary>Подпись серии (неизвестная серия → первая, как в App.js).</summary>
    public static string Label(string? series)
    {
        foreach (var option in Items)
            if (option.Value == series) return option.Label;
        return Items[0].Label;
    }

    /// <summary>Каталог конфигов серии.</summary>
    public static string ConfigDirectory(string? series)
    {
        foreach (var option in Items)
            if (option.Value == series) return option.ConfigDirectory;
        return Items[0].ConfigDirectory;
    }

    /// <summary>Допустимый id серии (`soc`/`cs`/`cop`; иначе `soc`).</summary>
    public static string Normalize(string? series) =>
        Items.Any(option => option.Value == series) ? series! : DefaultSeries;
}
