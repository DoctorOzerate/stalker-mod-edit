using System.Text;

namespace XRayForge.Core.Imaging;

/// <summary>
/// RGBA-изображение атласа: 4 байта на пиксель, строки сверху вниз (порядок DDS-декодера
/// и `ImageData` из JS).
/// </summary>
public sealed class RgbaImage
{
    public RgbaImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0 || Pixels.Length < Width * Height * 4;
}

/// <summary>Ячейка сетки атласа (`{x,y}` в JS).</summary>
public readonly record struct AtlasCell(int X, int Y);

/// <summary>Прямоугольная область атласа: ячейки или пиксели (контекст задаёт вызывающий).</summary>
public readonly record struct AtlasRegion(int X, int Y, int W, int H)
{
    public bool IsEmpty => W <= 0 || H <= 0;
}

/// <summary>Спрайт для экспорта листа: ячейка и её размер в ячейках.</summary>
public readonly record struct AtlasSprite(int X, int Y, int W, int H);

/// <summary>Цвет RGBA для заливки выделения.</summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A)
{
    /// <summary>
    /// Разбор hex-цвета как в поле ввода JS: `#RGB`, `#RRGGBB` или `#RRGGBBAA`.
    /// Возвращает `false` для невалидной строки.
    /// </summary>
    public static bool TryParseHex(string? value, out RgbaColor color)
    {
        color = default;
        var text = (value ?? "").Trim();
        if (text.StartsWith('#')) text = text[1..];
        if (text.Length == 3)
            text = string.Concat(text.Select(ch => new string(ch, 2)));
        if (text.Length is not (6 or 8)) return false;
        if (!int.TryParse(text[..6], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
            return false;

        byte alpha = 255;
        if (text.Length == 8 &&
            !byte.TryParse(text[6..], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out alpha))
            return false;

        color = new RgbaColor((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), alpha);
        return true;
    }
}

/// <summary>
/// Сетка атласа (порт вычислений `project.atlas` из `IconsTab.js`): размер ячейки,
/// настроенное число колонок и вычисленное число рядов.
/// </summary>
public sealed class AtlasGrid
{
    private AtlasGrid(int imageWidth, int imageHeight, int cellWidth, int cellHeight, int columns)
    {
        ImageWidth = Math.Max(0, imageWidth);
        ImageHeight = Math.Max(0, imageHeight);
        CellWidth = Math.Max(1, cellWidth);
        CellHeight = Math.Max(1, cellHeight);
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, ImageHeight / CellHeight);
    }

    public int ImageWidth { get; }
    public int ImageHeight { get; }
    public int CellWidth { get; }
    public int CellHeight { get; }
    public int Columns { get; }
    public int Rows { get; }

    /// <summary>`Math.floor(width / cellSize)` / `Math.floor(height / cellHeight)` в JS.</summary>
    public static AtlasGrid Create(int imageWidth, int imageHeight, double cellWidth, double cellHeight, double columns) =>
        new(imageWidth, imageHeight,
            (int)Math.Round(cellWidth > 0 ? cellWidth : 50),
            (int)Math.Round(cellHeight > 0 ? cellHeight : cellWidth > 0 ? cellWidth : 50),
            (int)Math.Round(columns > 0 ? columns : 5));

    /// <summary>Ячейка под точкой изображения (пиксели) или `null` вне сетки/картинки.</summary>
    public AtlasCell? HitTest(double pixelX, double pixelY)
    {
        if (pixelX < 0 || pixelY < 0 || pixelX >= ImageWidth || pixelY >= ImageHeight) return null;
        var x = (int)Math.Floor(pixelX / CellWidth);
        var y = (int)Math.Floor(pixelY / CellHeight);
        if (x < 0 || y < 0 || x >= Columns || y >= Rows) return null;
        return new AtlasCell(x, y);
    }

    /// <summary>Прижать ячейку к допустимому диапазону (drag за пределы атласа).</summary>
    public AtlasCell ClampCell(AtlasCell cell) => new(
        Math.Clamp(cell.X, 0, Columns - 1),
        Math.Clamp(cell.Y, 0, Rows - 1));

    /// <summary>Нормализованный прямоугольник выделения между двумя ячейками включительно.</summary>
    public static AtlasRegion Selection(AtlasCell start, AtlasCell end) => new(
        Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
        Math.Abs(start.X - end.X) + 1, Math.Abs(start.Y - end.Y) + 1);

    /// <summary>Ограничить выделение сеткой (как проверки `x &lt; columns` в `AtlasViewer`).</summary>
    public AtlasRegion ClampSelection(AtlasRegion selection)
    {
        var x = Math.Clamp(selection.X, 0, Columns - 1);
        var y = Math.Clamp(selection.Y, 0, Rows - 1);
        var w = Math.Clamp(selection.W <= 0 ? 1 : selection.W, 1, Columns - x);
        var h = Math.Clamp(selection.H <= 0 ? 1 : selection.H, 1, Rows - y);
        return new AtlasRegion(x, y, w, h);
    }

    /// <summary>Пиксельный прямоугольник выделения (может выходить за картинку — операции обрежут).</summary>
    public AtlasRegion PixelRegion(AtlasRegion selection)
    {
        var clamped = ClampSelection(selection);
        return new AtlasRegion(clamped.X * CellWidth, clamped.Y * CellHeight,
            clamped.W * CellWidth, clamped.H * CellHeight);
    }
}

/// <summary>
/// Пиксельные операции вкладки «Иконки» (порт обработчиков canvas из `IconsTab.js`).
/// Все методы работают с RGBA и не зависят от WPF.
/// </summary>
public static class AtlasPixels
{
    /// <summary>Создать прозрачное изображение (пустой атлас).</summary>
    public static RgbaImage Transparent(int width, int height)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        return new RgbaImage(w, h, new byte[w * h * 4]);
    }

    /// <summary>`getImageData` + `putImageData` в новый canvas: вырезать область.</summary>
    public static RgbaImage Crop(RgbaImage source, AtlasRegion region)
    {
        var rect = Intersect(source, region);
        if (rect.IsEmpty) return new RgbaImage(0, 0, Array.Empty<byte>());

        var pixels = new byte[rect.W * rect.H * 4];
        for (var y = 0; y < rect.H; y++)
        {
            var src = ((rect.Y + y) * source.Width + rect.X) * 4;
            var dst = y * rect.W * 4;
            Buffer.BlockCopy(source.Pixels, src, pixels, dst, rect.W * 4);
        }

        return new RgbaImage(rect.W, rect.H, pixels);
    }

    /// <summary>`ctx.fillRect(x, y, w, h)` в пределах изображения.</summary>
    public static void Fill(RgbaImage image, AtlasRegion region, RgbaColor color)
    {
        var rect = Intersect(image, region);
        for (var y = rect.Y; y < rect.Y + rect.H; y++)
        {
            for (var x = rect.X; x < rect.X + rect.W; x++)
            {
                var i = (y * image.Width + x) * 4;
                image.Pixels[i] = color.R;
                image.Pixels[i + 1] = color.G;
                image.Pixels[i + 2] = color.B;
                image.Pixels[i + 3] = color.A;
            }
        }
    }

    /// <summary>`id.data[i + 3] = val` для всех пикселей выделения.</summary>
    public static void SetAlpha(RgbaImage image, AtlasRegion region, byte alpha)
    {
        var rect = Intersect(image, region);
        for (var y = rect.Y; y < rect.Y + rect.H; y++)
        {
            for (var x = rect.X; x < rect.X + rect.W; x++)
                image.Pixels[(y * image.Width + x) * 4 + 3] = alpha;
        }
    }

    /// <summary>`ctx.putImageData(clipboard, x, y)` с обрезкой по краю атласа.</summary>
    public static void Paste(RgbaImage destination, RgbaImage source, int x, int y)
    {
        for (var sy = 0; sy < source.Height; sy++)
        {
            var dy = y + sy;
            if (dy < 0 || dy >= destination.Height) continue;
            for (var sx = 0; sx < source.Width; sx++)
            {
                var dx = x + sx;
                if (dx < 0 || dx >= destination.Width) continue;
                var si = (sy * source.Width + sx) * 4;
                var di = (dy * destination.Width + dx) * 4;
                destination.Pixels[di] = source.Pixels[si];
                destination.Pixels[di + 1] = source.Pixels[si + 1];
                destination.Pixels[di + 2] = source.Pixels[si + 2];
                destination.Pixels[di + 3] = source.Pixels[si + 3];
            }
        }
    }

    /// <summary>`flipSelection(horizontal)`: отражение области на месте.</summary>
    public static void Flip(RgbaImage image, AtlasRegion region, bool horizontal)
    {
        var rect = Intersect(image, region);
        if (rect.IsEmpty) return;
        var copy = Crop(image, rect);
        for (var yy = 0; yy < rect.H; yy++)
        {
            for (var xx = 0; xx < rect.W; xx++)
            {
                var sx = horizontal ? rect.W - 1 - xx : xx;
                var sy = horizontal ? yy : rect.H - 1 - yy;
                var si = (sy * rect.W + sx) * 4;
                var di = ((rect.Y + yy) * image.Width + rect.X + xx) * 4;
                image.Pixels[di] = copy.Pixels[si];
                image.Pixels[di + 1] = copy.Pixels[si + 1];
                image.Pixels[di + 2] = copy.Pixels[si + 2];
                image.Pixels[di + 3] = copy.Pixels[si + 3];
            }
        }
    }

    /// <summary>`rotateSelection(clockwise)`: поворот квадратной области на 90°.</summary>
    public static void Rotate90(RgbaImage image, AtlasRegion region, bool clockwise)
    {
        var rect = Intersect(image, region);
        if (rect.IsEmpty || rect.W != rect.H) return;
        var n = rect.W;
        var copy = Crop(image, rect);
        for (var yy = 0; yy < n; yy++)
        {
            for (var xx = 0; xx < n; xx++)
            {
                // В JS формула давала транспонирование; здесь поворот действительно на 90°.
                var sx = clockwise ? yy : n - 1 - yy;
                var sy = clockwise ? n - 1 - xx : xx;
                var si = (sy * n + sx) * 4;
                var di = ((rect.Y + yy) * image.Width + rect.X + xx) * 4;
                image.Pixels[di] = copy.Pixels[si];
                image.Pixels[di + 1] = copy.Pixels[si + 1];
                image.Pixels[di + 2] = copy.Pixels[si + 2];
                image.Pixels[di + 3] = copy.Pixels[si + 3];
            }
        }
    }

    /// <summary>
    /// `findDuplicateIcons`: каждая ячейка уменьшается до 8×8, одинаковые отпечатки
    /// считаются дубликатами. Возвращает все ячейки групп с повторами в порядке обхода.
    /// </summary>
    public static List<AtlasCell> FindDuplicateCells(RgbaImage image, int cellWidth, int cellHeight)
    {
        var result = new List<AtlasCell>();
        var cw = Math.Max(1, cellWidth);
        var ch = Math.Max(1, cellHeight);
        var cols = image.Width / cw;
        var rows = image.Height / ch;
        if (cols <= 0 || rows <= 0) return result;

        var groups = new Dictionary<string, List<AtlasCell>>(StringComparer.Ordinal);
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                var key = Fingerprint(image, x * cw, y * ch, cw, ch);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<AtlasCell>();
                    groups[key] = list;
                }

                list.Add(new AtlasCell(x, y));
            }
        }

        foreach (var list in groups.Values)
            if (list.Count > 1)
                result.AddRange(list);
        return result;
    }

    /// <summary>
    /// `exportSpritesheet`: спрайты в ряд с зазором 2 px (как canvas в `App.js`).
    /// </summary>
    public static RgbaImage BuildSpritesheet(
        RgbaImage source, IReadOnlyList<AtlasSprite> sprites, int cellWidth, int cellHeight)
    {
        var cw = Math.Max(1, cellWidth);
        var ch = Math.Max(1, cellHeight);
        const int gap = 2;
        if (sprites.Count == 0) return new RgbaImage(0, 0, Array.Empty<byte>());

        var width = gap;
        foreach (var sprite in sprites)
            width += Math.Max(1, sprite.W) * cw + gap;
        var height = sprites.Max(sprite => Math.Max(1, sprite.H) * ch);
        var sheet = new RgbaImage(width, Math.Max(1, height), new byte[width * Math.Max(1, height) * 4]);

        var x = gap;
        foreach (var sprite in sprites)
        {
            var w = Math.Max(1, sprite.W) * cw;
            var h = Math.Max(1, sprite.H) * ch;
            CopyRect(source, sprite.X * cw, sprite.Y * ch, w, h, sheet, x, 0);
            x += w + gap;
        }

        return sheet;
    }

    private static void CopyRect(
        RgbaImage source, int sx, int sy, int width, int height, RgbaImage destination, int dx, int dy)
    {
        for (var y = 0; y < height; y++)
        {
            var srcY = sy + y;
            var dstY = dy + y;
            if (srcY < 0 || srcY >= source.Height || dstY < 0 || dstY >= destination.Height) continue;
            for (var x = 0; x < width; x++)
            {
                var srcX = sx + x;
                var dstX = dx + x;
                if (srcX < 0 || srcX >= source.Width || dstX < 0 || dstX >= destination.Width) continue;
                var si = (srcY * source.Width + srcX) * 4;
                var di = (dstY * destination.Width + dstX) * 4;
                Buffer.BlockCopy(source.Pixels, si, destination.Pixels, di, 4);
            }
        }
    }

    private static AtlasRegion Intersect(RgbaImage image, AtlasRegion region)
    {
        var x = Math.Max(0, region.X);
        var y = Math.Max(0, region.Y);
        var right = Math.Min(image.Width, region.X + Math.Max(0, region.W));
        var bottom = Math.Min(image.Height, region.Y + Math.Max(0, region.H));
        return new AtlasRegion(x, y, right - x, bottom - y);
    }

    /// <summary>8×8 nearest-neighbor отпечаток ячейки (аналог `drawImage` в canvas).</summary>
    private static string Fingerprint(RgbaImage image, int x0, int y0, int cw, int ch)
    {
        var text = new StringBuilder(8 * 8 * 4);
        for (var sy = 0; sy < 8; sy++)
        {
            var y = y0 + Math.Min(ch - 1, (sy * 2 + 1) * ch / 16);
            for (var sx = 0; sx < 8; sx++)
            {
                var x = x0 + Math.Min(cw - 1, (sx * 2 + 1) * cw / 16);
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                {
                    text.Append('\0').Append('\0').Append('\0').Append('\0');
                    continue;
                }

                var i = (y * image.Width + x) * 4;
                text.Append((char)image.Pixels[i])
                    .Append((char)image.Pixels[i + 1])
                    .Append((char)image.Pixels[i + 2])
                    .Append((char)image.Pixels[i + 3]);
            }
        }

        return text.ToString();
    }
}
