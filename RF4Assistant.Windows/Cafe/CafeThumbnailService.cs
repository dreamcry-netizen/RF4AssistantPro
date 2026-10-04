using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using RF4AssistantPro.Services;

namespace RF4AssistantPro.Cafe;

public sealed class CafeThumbnailService
{
    public string CreateThumbnail(string fullImagePath)
    {
        if (!File.Exists(fullImagePath))
        {
            throw new FileNotFoundException("Скриншот кафе не найден.", fullImagePath);
        }

        var directory = Path.Combine(
            PortableDataPaths.GetScreenshotDirectory("rf4_cafe"),
            "Thumbnails");
        Directory.CreateDirectory(directory);

        var thumbnailPath = Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(fullImagePath)}_thumb.png");

        using var source = new Bitmap(fullImagePath);
        var targetWidth = 360;
        var targetHeight = Math.Max(1, source.Height * targetWidth / source.Width);
        using var thumbnail = new Bitmap(targetWidth, targetHeight);
        using var graphics = Graphics.FromImage(thumbnail);

        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
        thumbnail.Save(thumbnailPath, ImageFormat.Png);

        return thumbnailPath;
    }
}