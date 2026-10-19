using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace XRayForge.Controls;

/// <summary>
/// Минимальный SVG-просмотрщик для схем инклюдов и цепочек квестов: rect, path
/// (M/C/L), polygon и text. Достаточно для разметки, которую строят Core-модули,
/// и не требует WebView2. Масштабирует рисунок по доступной области.
/// </summary>
public sealed class SvgPreview : FrameworkElement
{
    private static readonly Regex PathTokens = new(@"[MCLZ]|-?\d*\.?\d+(?:[eE][-+]?\d+)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(SvgPreview),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value ?? string.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = TryReadSize();
        if (size == null) return new Size(0, 0);
        var width = double.IsInfinity(availableSize.Width) ? size.Value.Width : availableSize.Width;
        var scale = width / Math.Max(1, size.Value.Width);
        var height = size.Value.Height * scale;
        if (double.IsInfinity(availableSize.Height)) return new Size(width, height);

        scale = Math.Min(scale, availableSize.Height / Math.Max(1, size.Value.Height));
        return new Size(size.Value.Width * scale, size.Value.Height * scale);
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (string.IsNullOrWhiteSpace(Source)) return;

        XDocument document;
        try
        {
            document = XDocument.Parse(Source);
        }
        catch
        {
            return;
        }

        var root = document.Root;
        if (root == null) return;
        var box = ReadViewBox(root);
        if (box.Width <= 0 || box.Height <= 0) return;

        var scale = Math.Min(RenderSize.Width / box.Width, RenderSize.Height / box.Height);
        if (!double.IsFinite(scale) || scale <= 0) return;

        context.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        context.PushTransform(new ScaleTransform(scale, scale));
        foreach (var element in root.Elements())
            DrawElement(context, element);
        context.Pop();
        context.Pop();
    }

    private void DrawElement(DrawingContext context, XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "rect":
                DrawRect(context, element);
                break;
            case "path":
                DrawPath(context, element);
                break;
            case "polygon":
                DrawPolygon(context, element);
                break;
            case "text":
                DrawText(context, element);
                break;
        }
    }

    private static void DrawRect(DrawingContext context, XElement element)
    {
        var x = Number(element, "x");
        var y = Number(element, "y");
        var width = Number(element, "width");
        var height = Number(element, "height");
        if (width <= 0 || height <= 0) return;

        var radius = Number(element, "rx");
        var fill = Brush(element, "fill");
        var pen = Pen(element, "stroke", "stroke-width");
        var rect = new Rect(x, y, width, height);
        if (radius > 0)
            context.DrawRoundedRectangle(fill, pen, rect, radius, radius);
        else
            context.DrawRectangle(fill, pen, rect);
    }

    private static void DrawPath(DrawingContext context, XElement element)
    {
        var geometry = BuildPath((string?)element.Attribute("d"));
        if (geometry == null) return;
        context.DrawGeometry(Brush(element, "fill"), Pen(element, "stroke", "stroke-width"), geometry);
    }

    private static void DrawPolygon(DrawingContext context, XElement element)
    {
        var points = (string?)element.Attribute("points");
        if (string.IsNullOrWhiteSpace(points)) return;

        var numbers = ParseNumbers(points);
        if (numbers.Count < 4) return;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(numbers[0], numbers[1]), true, true);
            for (var i = 2; i + 1 < numbers.Count; i += 2)
                g.LineTo(new Point(numbers[i], numbers[i + 1]), true, false);
        }
        geometry.Freeze();
        context.DrawGeometry(Brush(element, "fill"), Pen(element, "stroke", "stroke-width"), geometry);
    }

    private static void DrawText(DrawingContext context, XElement element)
    {
        var text = element.Value;
        if (string.IsNullOrWhiteSpace(text)) return;

        var size = Number(element, "font-size", 11);
        var weight = ((string?)element.Attribute("font-weight"))?.Contains("bold", StringComparison.OrdinalIgnoreCase) == true
            ? FontWeights.Bold
            : FontWeights.Normal;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, weight, FontStretches.Normal),
            size, Brush(element, "fill") ?? Brushes.White, 1.0);
        context.DrawText(formatted,
            new Point(Number(element, "x"), Number(element, "y") - formatted.Height * 0.82));
    }


    private static StreamGeometry? BuildPath(string? d)
    {
        if (string.IsNullOrWhiteSpace(d)) return null;
        var tokens = PathTokens.Matches(d).Select(m => m.Value).ToList();
        if (tokens.Count == 0) return null;

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var i = 0;
            var started = false;
            var current = new Point();
            char command = 'M';
            while (i < tokens.Count)
            {
                if (char.IsLetter(tokens[i][0]))
                {
                    command = char.ToUpperInvariant(tokens[i][0]);
                    i++;
                    if (command == 'Z')
                    {
                        if (started) g.Close();
                        continue;
                    }
                }

                if (command == 'M' || command == 'L')
                {
                    if (i + 1 >= tokens.Count) break;
                    current = new Point(Num(tokens[i]), Num(tokens[i + 1]));
                    if (!started)
                    {
                        g.BeginFigure(current, false, false);
                        started = true;
                    }
                    else
                    {
                        g.LineTo(current, true, false);
                    }
                    i += 2;
                    command = 'L'; // дополнительные пары после M — линии
                }
                else if (command == 'C')
                {
                    if (i + 5 >= tokens.Count) break;
                    var c1 = new Point(Num(tokens[i]), Num(tokens[i + 1]));
                    var c2 = new Point(Num(tokens[i + 2]), Num(tokens[i + 3]));
                    current = new Point(Num(tokens[i + 4]), Num(tokens[i + 5]));
                    if (!started)
                    {
                        g.BeginFigure(current, false, false);
                        started = true;
                    }
                    g.BezierTo(c1, c2, current, true, false);
                    i += 6;
                }
                else
                {
                    i++;
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private Size? TryReadSize()
    {
        try
        {
            var root = XDocument.Parse(Source).Root;
            if (root == null) return null;
            return ReadViewBox(root);
        }
        catch
        {
            return null;
        }
    }

    private static Size ReadViewBox(XElement root)
    {
        var box = (string?)root.Attribute("viewBox");
        if (!string.IsNullOrWhiteSpace(box))
        {
            var parts = ParseNumbers(box);
            if (parts.Count >= 4 && parts[2] > 0 && parts[3] > 0)
                return new Size(parts[2], parts[3]);
        }

        var width = Number(root, "width", 320);
        var height = Number(root, "height", 180);
        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    private static double Number(XElement element, string name, double fallback = 0) =>
        double.TryParse((string?)element.Attribute(name), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static double Num(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static List<double> ParseNumbers(string text) =>
        Regex.Matches(text, @"-?\d*\.?\d+(?:[eE][-+]?\d+)?")
            .Select(m => Num(m.Value)).ToList();

    private static Brush? Brush(XElement element, string attribute)
    {
        var value = (string?)element.Attribute(attribute);
        if (string.IsNullOrWhiteSpace(value) || value == "none") return null;
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return Brushes.Transparent;
        if (value.StartsWith('#') && value.Length is 7 or 4)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Gray;
            }
        }
        return Brushes.Gray;
    }

    private static Pen? Pen(XElement element, string attribute, string widthAttribute)
    {
        var brush = Brush(element, attribute);
        if (brush == null) return null;
        var width = Number(element, widthAttribute, 1);
        var pen = new Pen(brush, Math.Max(0.5, width));
        pen.Freeze();
        return pen;
    }
}
