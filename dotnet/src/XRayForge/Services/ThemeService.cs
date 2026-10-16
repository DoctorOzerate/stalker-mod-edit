using System.Windows;
using System.Windows.Media;
using XRayForge.Core.Storage;
using XRayForge.Core.Ui;

namespace XRayForge.Services;

/// <summary>
/// Применение темы к ресурсам приложения: HEX-токены `ResolvedTheme` → `SolidColorBrush`,
/// метрики плотности → `sys:Double`. Ключи совпадают с `Themes/Tokens.xaml`, поэтому
/// элементы используют `DynamicResource` и перерисовываются без перезапуска.
/// </summary>
public sealed class ThemeService
{
    private readonly ResourceDictionary _target;

    public ThemeService(ResourceDictionary? target = null)
    {
        _target = target ?? Application.Current?.Resources ?? new ResourceDictionary();
        Current = ThemeCatalog.Resolve(null, null, null);
    }

    /// <summary>Текущая разобранная тема (тема + акцент + плотность).</summary>
    public ResolvedTheme Current { get; private set; }

    /// <summary>Тема применена (настройки программы → интерфейс).</summary>
    public event EventHandler<ResolvedTheme>? Changed;

    /// <summary>Применить настройки программы (`theme`, `accent`, `density`).</summary>
    public ResolvedTheme ApplySettings(ProgramSettings? settings) =>
        Apply(settings?.Theme, settings?.Accent, settings?.Density);

    /// <summary>Применить тему по идентификаторам настроек (неизвестные значения → значения по умолчанию).</summary>
    public ResolvedTheme Apply(string? themeId, string? accentId, string? densityId)
    {
        var resolved = ThemeCatalog.Resolve(themeId, accentId, densityId);

        foreach (var pair in resolved.ColorResources()) _target[pair.Key] = Brush(pair.Value);

        var metrics = resolved.Metrics;
        _target["DensityRowHeight"] = metrics.RowHeight;
        _target["DensityPanelGap"] = metrics.PanelGap;
        _target["DensityPanelPadding"] = metrics.PanelPadding;
        _target["DensityControlPaddingX"] = metrics.ControlPaddingX;
        _target["DensityControlPaddingY"] = metrics.ControlPaddingY;
        _target["DensityFontSize"] = metrics.FontSize;
        _target["DensitySmallFontSize"] = metrics.SmallFontSize;
        _target["DensityTitleFontSize"] = metrics.TitleFontSize;
        _target["DensityRadius"] = metrics.Radius;

        Current = resolved;
        Changed?.Invoke(this, resolved);
        return resolved;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
