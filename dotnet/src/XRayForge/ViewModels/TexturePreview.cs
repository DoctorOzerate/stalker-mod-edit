using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XRayForge.Core.Projects;

namespace XRayForge.ViewModels;

/// <summary>
/// Вырезка иконки из PNG data URL атласа (`background-image`/`background-position` в JS).
/// Общая логика вкладок «Персонажи» и «Предметы»; ошибки декодирования не пробрасываются —
/// вызывающая сторона показывает заглушку, как и JS при битой текстуре.
/// </summary>
internal static class TexturePreview
{
    /// <summary>Поиск текстуры атласа по имени файла (без `.dds`, `/` и `\` эквивалентны).</summary>
    public static ProjectTexture? FindTexture(IEnumerable<ProjectTexture>? textures, string file)
    {
        if (textures == null) return null;

        var want = (file ?? "").ToLowerInvariant().Replace('\\', '/');
        foreach (var texture in textures)
        {
            var name = (texture.Name ?? "").ToLowerInvariant();
            if (name.EndsWith(".dds", StringComparison.Ordinal)) name = name[..^4];
            name = name.Replace('\\', '/');
            if (name == want || name.EndsWith("/" + want, StringComparison.Ordinal) ||
                want.EndsWith("/" + name, StringComparison.Ordinal))
                return texture;
        }

        return null;
    }

    /// <summary>Декодировать PNG data URL в замороженный <see cref="BitmapImage"/>.</summary>
    public static BitmapImage? Load(string? dataUrl)
    {
        try
        {
            var comma = dataUrl?.IndexOf(',') ?? -1;
            if (dataUrl == null || comma < 0) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(Convert.FromBase64String(dataUrl[(comma + 1)..]));
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Вырезать прямоугольник из PNG data URL атласа. Масштаб берётся из фактических пикселей
    /// картинки и заявленных `textureWidth/Height` (в JS `background-size` растягивает атлас).
    /// </summary>
    public static ImageSource? Crop(
        BitmapImage? bitmap, double x, double y, double width, double height,
        int textureWidth, int textureHeight)
    {
        if (bitmap == null || width <= 0 || height <= 0) return null;

        try
        {
            var scale = textureWidth > 0 ? bitmap.PixelWidth / (double)textureWidth : 1.0;
            var rect = new Int32Rect(
                (int)Math.Round(x * scale),
                (int)Math.Round(y * scale),
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale));
            if (rect.Width <= 0) rect.Width = 1;
            if (rect.Height <= 0) rect.Height = 1;
            if (rect.X < 0) rect.X = 0;
            if (rect.Y < 0) rect.Y = 0;
            if (rect.X + rect.Width > bitmap.PixelWidth) rect.Width = bitmap.PixelWidth - rect.X;
            if (rect.Y + rect.Height > bitmap.PixelHeight) rect.Height = bitmap.PixelHeight - rect.Y;
            if (rect.Width <= 0 || rect.Height <= 0) return null;

            var cropped = new CroppedBitmap(bitmap, rect);
            cropped.Freeze();
            return cropped;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Вырезка иконки по data URL (атлас перечитывается на каждый вызов).</summary>
    public static ImageSource? Crop(
        string? dataUrl, double x, double y, double width, double height,
        int textureWidth, int textureHeight) =>
        Crop(Load(dataUrl), x, y, width, height, textureWidth, textureHeight);
}
