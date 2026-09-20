namespace VideoAutoTool.Core.Ffmpeg;

public sealed class FfmpegNotFoundException : Exception
{
    public FfmpegNotFoundException()
        : base(
            "Không tìm thấy ffmpeg hoặc ffprobe. " +
            "Cài ffmpeg (winget install Gyan.FFmpeg) hoặc đặt vào C:\\ffmpeg\\bin, C:\\tools\\ffmpeg\\bin, " +
            "hoặc cấu hình đường dẫn trong tab Cài đặt của ứng dụng.")
    {
    }
}
