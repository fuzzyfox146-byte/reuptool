namespace VideoAutoTool.Core.Ffmpeg;

public sealed class FfmpegNotFoundException : Exception
{
    public FfmpegNotFoundException()
        : base(
            "Không tìm thấy ffmpeg hoặc ffprobe. " +
            "App đã kèm ffmpeg trong tools\\ffmpeg cạnh file .exe; nếu thiếu hãy chạy scripts\\fetch-ffmpeg.ps1 rồi build lại.")
    {
    }
}
