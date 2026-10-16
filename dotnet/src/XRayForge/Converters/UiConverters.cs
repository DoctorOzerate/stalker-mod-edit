using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using XRayForge.Localization;

namespace XRayForge.Converters;

/// <summary>`#RRGGBB` → кисть: акцентные состояния навигации, LED статусной строки, плитки тем.</summary>
public sealed class HexBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;
        if (Cache.TryGetValue(hex, out var cached)) return cached;

        SolidColorBrush brush;
        try
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
        }
        catch (FormatException) { brush = Brushes.Transparent; }
        catch (NotSupportedException) { brush = Brushes.Transparent; }

        Cache[hex] = brush;
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Ключ локализации из каталога Core → строка интерфейса (`SummaryChip`, плотность, акцент).</summary>
public sealed class LocalizationKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key && key.Length > 0 ? LocalizationSource.Get(key) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>`true` → Collapsed (скрытие приветственного экрана, когда проект открыт).</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Разметка пути связи (`M x y C …` из `GraphEditing.EdgePath`) → геометрия `Path.Data`.</summary>
public sealed class PathDataConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var data = value as string;
        if (string.IsNullOrWhiteSpace(data)) return Geometry.Empty;

        try { return Geometry.Parse(data); }
        catch (FormatException) { return Geometry.Empty; }
        catch (NotSupportedException) { return Geometry.Empty; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Непустая подпись → Visible: бейджи озвучки, условий и действий в карточке узла.</summary>
public sealed class NonEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
