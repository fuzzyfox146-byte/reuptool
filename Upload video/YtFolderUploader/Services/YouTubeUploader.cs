using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace YtFolderUploader;

public sealed class YouTubeUploader
{
    public async Task<UploadOneResult> UploadAsync(
        YouTubeService service,
        QueueItem item,
        IProgress<UploadProgress>? progress,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var options = item.Channel.Options;
        var title = FileNames.SanitizeTitle(item.Title);
        if (string.IsNullOrWhiteSpace(title))
            return new UploadOneResult { Status = "Lỗi: tiêu đề trống." };

        item.Title = title;
        string? videoId = null;

        try
        {
            progress?.Report(new UploadProgress(0, "Đang upload video..."));
            videoId = await UploadVideoAsync(service, item, title, progress, cancellationToken);
            log?.Invoke($"Đã upload {title} → https://youtu.be/{videoId}");

            var thumb = await TryThumbnailAsync(service, item, videoId, log, cancellationToken);
            var playlist = thumb.StopBatch
                ? new StepResult(null, false)
                : await TryPlaylistAsync(service, item, videoId, log, cancellationToken);
            var warning = Join(thumb.Warning, playlist.Warning);
            var status = "Đã đăng";
            if (thumb.Defer)
                status = "Đã đăng, hết hạn mức khi gắn thumbnail";
            else if (thumb.Warning != null && playlist.Warning != null)
                status = "Đã đăng, lỗi thumbnail và playlist";
            else if (thumb.Warning != null)
                status = "Đã đăng, chưa gắn được thumbnail";
            else if (playlist.StopBatch)
                status = "Đã đăng, hết hạn mức khi thêm playlist";
            else if (playlist.Warning != null)
                status = "Đã đăng, lỗi playlist";

            var stop = thumb.StopBatch || playlist.StopBatch;
            var defer = thumb.Defer || playlist.StopBatch;

            progress?.Report(new UploadProgress(100, status));
            return new UploadOneResult
            {
                VideoUploaded = true,
                VideoId = videoId,
                StopBatch = stop,
                DeferSchedule = defer,
                ThumbAttached = thumb.Warning == null,
                Status = status,
                Warning = warning
            };
        }
        catch (OperationCanceledException)
        {
            if (videoId == null)
                throw;

            return new UploadOneResult
            {
                VideoUploaded = true,
                VideoId = videoId,
                Status = "Đã đăng, dừng trước khi hoàn tất thumbnail",
                Warning = "Video đã lên YouTube. Có thể gắn lại thumbnail sau."
            };
        }
        catch (Exception ex)
        {
            if (videoId != null)
            {
                var limited = ApiErrors.IsDailyQuota(ex);
                return new UploadOneResult
                {
                    VideoUploaded = true,
                    VideoId = videoId,
                    StopBatch = limited,
                    DeferSchedule = limited,
                    Status = "Đã đăng, lỗi sau upload",
                    Warning = ApiErrors.Explain(ex)
                };
            }

            var quota = ApiErrors.IsDailyQuota(ex);
            return new UploadOneResult
            {
                StopBatch = quota,
                DeferSchedule = quota,
                Status = "Lỗi: " + ApiErrors.Explain(ex)
            };
        }
    }

    public async Task SetThumbnailOnlyAsync(
        YouTubeService service,
        string videoId,
        string thumbPath,
        CancellationToken cancellationToken)
    {
        await SetThumbnailAsync(service, videoId, thumbPath, log: null, cancellationToken, retry: true);
    }

    public Task ProbeThumbnailAsync(
        YouTubeService service,
        string videoId,
        string thumbPath,
        Action<string>? log,
        CancellationToken cancellationToken) =>
        SetThumbnailAsync(service, videoId, thumbPath, log, cancellationToken, retry: false);

    public async Task MovePublishAsync(
        YouTubeService service,
        string videoId,
        DateTimeOffset publishAt,
        CancellationToken cancellationToken)
    {
        var video = new Video
        {
            Id = videoId,
            Status = new VideoStatus
            {
                PrivacyStatus = "private",
                PublishAtDateTimeOffset = publishAt
            }
        };
        await service.Videos.Update(video, "status").ExecuteAsync(cancellationToken);
    }

    async Task<string> UploadVideoAsync(
        YouTubeService service,
        QueueItem item,
        string title,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var options = item.Channel.Options;
        var scheduled = options.IsScheduled;
        if (scheduled && item.PublishAt == null)
            throw new InvalidOperationException("Thiếu giờ hẹn. Hãy quét lại sau khi chỉnh lịch.");

        var description = MetadataComposer.Description(
            options.DescriptionTemplate,
            title,
            item.Index,
            item.SourceName,
            Path.GetFileName(item.VideoPath));
        var tags = MetadataComposer.Tags(options.Tags);

        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = title,
                Description = description,
                CategoryId = string.IsNullOrWhiteSpace(options.CategoryId) ? "24" : options.CategoryId
            },
            Status = new VideoStatus
            {
                PrivacyStatus = scheduled ? "private" : Privacy(options.PrivacyMode),
                SelfDeclaredMadeForKids = options.MadeForKids,
                ContainsSyntheticMedia = options.ContainsSyntheticMedia,
                Embeddable = options.Embeddable,
                License = options.License == "creativeCommon" ? "creativeCommon" : "youtube",
                PublicStatsViewable = options.PublicStatsViewable
            }
        };

        if (tags.Count > 0)
            video.Snippet.Tags = tags.ToList();
        if (!string.IsNullOrWhiteSpace(options.DefaultLanguage))
            video.Snippet.DefaultLanguage = options.DefaultLanguage;
        var audioLanguage = string.IsNullOrWhiteSpace(options.DefaultAudioLanguage)
            ? options.DefaultLanguage
            : options.DefaultAudioLanguage;
        if (!string.IsNullOrWhiteSpace(audioLanguage))
            video.Snippet.DefaultAudioLanguage = audioLanguage;

        if (scheduled)
            video.Status.PublishAtDateTimeOffset = item.PublishAt;

        var parts = new List<string> { "snippet", "status" };
        if (options.SendRecordingDate)
        {
            var zone = TimeZones.Resolve(options.TimeZoneId);
            var local = options.RecordingDate.ToDateTime(TimeOnly.MinValue);
            video.RecordingDetails = new VideoRecordingDetails
            {
                RecordingDateDateTimeOffset = new DateTimeOffset(local, zone.GetUtcOffset(local))
            };
            parts.Add("recordingDetails");
        }

        if (options.HasPaidProductPlacement)
        {
            video.PaidProductPlacementDetails = new VideoPaidProductPlacementDetails
            {
                HasPaidProductPlacement = true
            };
            parts.Add("paidProductPlacementDetails");
        }

        if (!string.IsNullOrWhiteSpace(options.LocalizationLanguage))
        {
            var fileName = Path.GetFileName(item.VideoPath);
            var localizedTitle = FileNames.SanitizeTitle(MetadataComposer.Description(
                string.IsNullOrWhiteSpace(options.LocalizationTitle) ? "{title}" : options.LocalizationTitle,
                title,
                item.Index,
                item.SourceName,
                fileName));
            if (string.IsNullOrWhiteSpace(localizedTitle))
                localizedTitle = title;
            video.Localizations = new Dictionary<string, VideoLocalization>
            {
                [options.LocalizationLanguage] = new VideoLocalization
                {
                    Title = localizedTitle,
                    Description = MetadataComposer.Description(options.LocalizationDescription, title, item.Index, item.SourceName, fileName)
                }
            };
            parts.Add("localizations");
        }

        await using var stream = new FileStream(
            item.VideoPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            useAsync: true);

        var insert = service.Videos.Insert(video, string.Join(",", parts), stream, VideoMime(item.VideoPath));
        insert.NotifySubscribers = options.NotifySubscribers;
        if (options.AutoLevels)
            insert.AutoLevels = true;
        if (options.Stabilize)
            insert.Stabilize = true;
        insert.ChunkSize = ResumableUpload.MinimumChunkSize * 32;
        var length = stream.Length;
        insert.ProgressChanged += uploadProgress =>
        {
            if (uploadProgress.Status != UploadStatus.Uploading || length <= 0)
                return;
            var percent = (int)Math.Clamp(uploadProgress.BytesSent * 100 / length, 0, 99);
            progress?.Report(new UploadProgress(percent, $"Đang upload {percent}%"));
        };

        var completed = await insert.UploadAsync(cancellationToken);
        if (completed.Status != UploadStatus.Completed || string.IsNullOrWhiteSpace(insert.ResponseBody?.Id))
        {
            if (completed.Exception != null)
                throw completed.Exception;
            throw new InvalidOperationException("YouTube không trả về mã video.");
        }

        return insert.ResponseBody.Id;
    }

    async Task<StepResult> TryThumbnailAsync(
        YouTubeService service,
        QueueItem item,
        string videoId,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.ThumbPath))
            return new StepResult(null, false);

        try
        {
            log?.Invoke("Đang gắn thumbnail...");
            await SetThumbnailAsync(service, videoId, item.ThumbPath, log, cancellationToken, retry: true);
            log?.Invoke("Đã gắn thumbnail.");
            return new StepResult(null, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = ApiErrors.Explain(ex);
            log?.Invoke("Thumbnail: " + message);
            var defer = ApiErrors.ShouldDeferAfterThumbFailure(ex);
            return new StepResult(message, true, defer);
        }
    }

    async Task SetThumbnailAsync(
        YouTubeService service,
        string videoId,
        string thumbPath,
        Action<string>? log,
        CancellationToken cancellationToken,
        bool retry)
    {
        var prepared = ThumbnailPrep.Prepare(thumbPath);
        try
        {
            var delays = retry ? new[] { 0, 10, 20, 40 } : new[] { 0 };
            Exception? last = null;
            for (var attempt = 0; attempt < delays.Length; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (delays[attempt] > 0)
                {
                    log?.Invoke($"Thumbnail chưa gắn được, thử lại sau {delays[attempt]} giây.");
                    await Task.Delay(TimeSpan.FromSeconds(delays[attempt]), cancellationToken);
                }

                try
                {
                    await using var stream = new FileStream(prepared.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var request = service.Thumbnails.Set(videoId, stream, prepared.Mime);
                    var progress = await request.UploadAsync(cancellationToken);
                    if (progress.Status == UploadStatus.Completed)
                        return;
                    last = progress.Exception ?? new InvalidOperationException(progress.Status.ToString());
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    last = ex;
                }

                if (last != null && (ApiErrors.ShouldDeferAfterThumbFailure(last) || !ApiErrors.IsTransientThumbnail(last)))
                    break;
            }

            throw last ?? new InvalidOperationException("Không gắn được thumbnail.");
        }
        finally
        {
            ThumbnailPrep.Cleanup(prepared);
        }
    }

    async Task<StepResult> TryPlaylistAsync(
        YouTubeService service,
        QueueItem item,
        string videoId,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var playlistId = MetadataComposer.NormalizePlaylistId(item.Channel.Options.PlaylistId);
        if (playlistId == null)
            return new StepResult(null, false);

        try
        {
            var playlistItem = new PlaylistItem
            {
                Snippet = new PlaylistItemSnippet
                {
                    PlaylistId = playlistId,
                    ResourceId = new ResourceId
                    {
                        Kind = "youtube#video",
                        VideoId = videoId
                    }
                }
            };
            await service.PlaylistItems.Insert(playlistItem, "snippet").ExecuteAsync(cancellationToken);
            log?.Invoke("Đã thêm vào playlist.");
            return new StepResult(null, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = ApiErrors.Explain(ex);
            log?.Invoke("Playlist: " + message);
            return new StepResult(message, ApiErrors.IsDailyQuota(ex));
        }
    }

    readonly record struct StepResult(string? Warning, bool StopBatch, bool Defer = false);

    static string Privacy(string mode) => mode switch
    {
        "public" => "public",
        "unlisted" => "unlisted",
        "private" => "private",
        _ => "private"
    };

    static string VideoMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".avi" => "video/x-msvideo",
        ".wmv" => "video/x-ms-wmv",
        ".flv" => "video/x-flv",
        ".webm" => "video/webm",
        ".mkv" => "video/x-matroska",
        ".mpeg" or ".mpg" => "video/mpeg",
        ".3gp" => "video/3gpp",
        _ => "video/*"
    };

    static string? Join(string? left, string? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return left + " | " + right;
    }
}
