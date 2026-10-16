using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XRayForge.Services;

/// <summary>
/// `pixelsToDataURL` из App.js: RGBA-буфер текстуры → `data:image/png;base64,…`.
/// В Core canvas отсутствует, поэтому конвертация живёт в WPF-слое
/// (см. MIGRATION.md, «Этап 3», отклонение 4) и подставляется в `GamedataScanOptions.PngEncoder`.
/// </summary>
public static class PngDataUrlEncoder
{
    /// <summary>Кодировать RGBA (по 4 байта на пиксель, строка сверху вниз) в PNG data URL.</summary>
    public static string FromRgba(byte[]? pixels, int width, int height)
    {
        if (pixels == null || width <= 0 || height <= 0) return "";
        var stride = width * 4;
        var expected = stride * height;
        if (pixels.Length < expected) return "";

        // WPF ждёт BGRA; альфа остаётся на месте (в JS — `ImageData`, порядок RGBA).
        var bgra = new byte[expected];
        for (var i = 0; i < expected; i += 4)
        {
            bgra[i] = pixels[i + 2];
            bgra[i + 1] = pixels[i + 1];
            bgra[i + 2] = pixels[i];
            bgra[i + 3] = pixels[i + 3];
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
}
