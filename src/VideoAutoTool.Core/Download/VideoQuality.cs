namespace VideoAutoTool.Core.Download;

public static class VideoQuality
{
    public static string Format(string? quality)
    {
        var height = quality switch
        {
            "360" => "360",
            "480" => "480",
            "720" => "720",
            "1080" => "1080",
            _ => "144"
        };

        return "bestvideo[vcodec=avc1][height<=" + height + "]+bestaudio[ext=m4a]/bestvideo[height<=" + height + "]+bestaudio/best[height<=" + height + "]/best";
    }
}
