using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace YtFolderUploader;

public readonly record struct PreparedThumb(string Path, string Mime, bool IsTemporary);

public static class ThumbnailPrep
{
    public const int MaxBytes = 2_000_000;

    public static PreparedThumb Prepare(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException("Không thấy file thumbnail.", path);

        if (info.Length > 0
            && info.Length <= MaxBytes
            && extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp")
        {
            return new PreparedThumb(path, Mime(extension), false);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "YtFolderUploader");
        Directory.CreateDirectory(tempDir);
        var dest = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".jpg");
        try
        {
            SaveJpegUnderLimit(path, dest);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Không đọc được ảnh thumbnail. Hãy dùng JPG hoặc PNG, tối đa 2MB. " + ex.Message,
                ex);
        }

        return new PreparedThumb(dest, "image/jpeg", true);
    }

    public static void Cleanup(PreparedThumb thumb)
    {
        if (!thumb.IsTemporary || !File.Exists(thumb.Path))
            return;

        try
        {
            File.Delete(thumb.Path);
        }
        catch (IOException)
        {
        }
    }

    static void SaveJpegUnderLimit(string sourcePath, string dest)
    {
        using var original = Image.FromFile(sourcePath);
        var widths = new[] { original.Width, 1920, 1280, 960 }
            .Where(width => width > 0 && width <= original.Width)
            .Distinct()
            .ToArray();
        var qualities = new long[] { 90, 80, 70, 60 };

        foreach (var width in widths)
        {
            using var bitmap = Resize(original, width);
            foreach (var quality in qualities)
            {
                SaveJpeg(bitmap, dest, quality);
                if (new FileInfo(dest).Length <= MaxBytes)
                    return;
            }
        }

        throw new InvalidOperationException("Thumbnail vẫn lớn hơn 2MB sau khi nén. YouTube chỉ nhận ảnh tối đa 2MB.");
    }

    static Bitmap Resize(Image source, int width)
    {
        if (width == source.Width)
            return new Bitmap(source);

        var height = Math.Max(1, (int)Math.Round(source.Height * (width / (double)source.Width)));
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, 0, 0, width, height);
        return bitmap;
    }

    static void SaveJpeg(Image image, string dest, long quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.MimeType == "image/jpeg");
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        image.Save(dest, encoder, parameters);
    }

    static string Mime(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };
}
