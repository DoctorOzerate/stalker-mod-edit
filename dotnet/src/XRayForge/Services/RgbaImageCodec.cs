using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XRayForge.Core.Imaging;
using XRayForge.ViewModels;

namespace XRayForge.Services;

/// <summary>
/// Мост между RGBA-буфером атласа и WPF: `pixelsToDataURL`/`getImageData` из JS.
/// Нужен вкладке «Иконки» для пиксельных операций и экспорта PNG.
/// </summary>
public static class RgbaImageCodec
{
    /// <summary>Декодировать PNG data URL в RGBA-пиксели.</summary>
    public static RgbaImage? FromDataUrl(string? dataUrl) => FromBitmap(TexturePreview.Load(dataUrl));

    /// <summary>Скопировать `BitmapSource` в RGBA (WPF внутри хранит BGRA).</summary>
    public static RgbaImage? FromBitmap(BitmapSource? bitmap)
    {
        if (bitmap == null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) return null;

        try
        {
            var converted = bitmap.Format == PixelFormats.Bgra32
                ? bitmap
                : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var stride = width * 4;
            var bgra = new byte[stride * height];
            converted.CopyPixels(bgra, stride, 0);

            var rgba = new byte[bgra.Length];
            for (var i = 0; i < bgra.Length; i += 4)
            {
                rgba[i] = bgra[i + 2];
                rgba[i + 1] = bgra[i + 1];
                rgba[i + 2] = bgra[i];
                rgba[i + 3] = bgra[i + 3];
            }

            return new RgbaImage(width, height, rgba);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Создать замороженный `BitmapSource` из RGBA для отображения в WPF.</summary>
    public static BitmapSource? ToBitmap(RgbaImage? image)
    {
        if (image == null || image.IsEmpty) return null;

        var expected = image.Width * image.Height * 4;
        var bgra = new byte[expected];
        for (var i = 0; i < expected; i += 4)
        {
            bgra[i] = image.Pixels[i + 2];
            bgra[i + 1] = image.Pixels[i + 1];
            bgra[i + 2] = image.Pixels[i];
            bgra[i + 3] = image.Pixels[i + 3];
        }

        var bitmap = BitmapSource.Create(
            image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, bgra, image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>PNG-байты для `downloadDataUrl(..., 'image/png')` из JS.</summary>
    public static byte[]? ToPngBytes(RgbaImage? image)
    {
        var bitmap = ToBitmap(image);
        if (bitmap == null) return null;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
