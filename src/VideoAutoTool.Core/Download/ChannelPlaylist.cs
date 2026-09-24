namespace VideoAutoTool.Core.Download;

public static class ChannelPlaylist
{
    public static string ToVideosUrl(string channelUrl)
    {
        var channel = channelUrl.Trim().TrimEnd('/');
        const string suffix = "/videos";
        if (channel.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            channel = channel[..^suffix.Length].TrimEnd('/');
        }

        return channel + suffix;
    }
}
