namespace VideoAutoTool.Core.Download;

public sealed class MultiChannelDownloader
{
    private readonly SourceDownloader _downloader;

    public MultiChannelDownloader(SourceDownloader downloader) => _downloader = downloader;

    public async Task<IReadOnlyList<ChannelDownloadResult>> DownloadAllAsync(
        DownloadTools tools,
        IReadOnlyList<ChannelDownloadRequest> channels,
        int parallel,
        IProgress<DownloadNotice>? log,
        CancellationToken cancellationToken)
    {
        parallel = Math.Clamp(parallel, 1, 4);
        var gate = new SemaphoreSlim(parallel);
        var tasks = channels.Select(channel => RunOneAsync(tools, channel, gate, log, cancellationToken)).ToArray();
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<ChannelDownloadResult> RunOneAsync(
        DownloadTools tools,
        ChannelDownloadRequest channel,
        SemaphoreSlim gate,
        IProgress<DownloadNotice>? log,
        CancellationToken userToken)
    {
        try
        {
            await gate.WaitAsync(userToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Stopped(channel.ParentFolder, userToken);
        }

        try
        {
            var result = await _downloader.DownloadAsync(tools, channel, log, userToken).ConfigureAwait(false);
            if (result.Stop == DownloadStop.CookieDead)
            {
                log?.Report(new DownloadNotice(null, result.Detail));
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return Stopped(channel.ParentFolder, userToken);
        }
        catch (Exception ex)
        {
            return new ChannelDownloadResult(channel.ParentFolder, DownloadStop.Failed, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    private static ChannelDownloadResult Stopped(string folder, CancellationToken userToken)
    {
        if (userToken.IsCancellationRequested)
        {
            return new ChannelDownloadResult(folder, DownloadStop.Cancelled, "Đã dừng.");
        }

        return new ChannelDownloadResult(folder, DownloadStop.StoppedBecauseCookie, "Dừng vì cookie lỗi ở kênh khác.");
    }
}
