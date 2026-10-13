namespace YtFolderUploader;

public sealed partial class MainForm
{
    void Scan(bool SelectedChannelsOnly)
    {
        WriteCurrent();
        _data.MaxUploadsPerRun = (int)numBatch.Value;
        _store.Save(_data);

        var channels = SelectedChannelsOnly
            ? (_current == null ? [] : new[] { _current })
            : _data.Channels.Where(channel => channel.Enabled).ToArray();
        if (channels.Length == 0)
        {
            MessageBox.Show(this, "Chưa có kênh nào được bật.", "Quét");
            return;
        }

        _items.Clear();
        grid.Rows.Clear();
        var pendingCount = 0;
        var uploadedCount = 0;
        var skippedBefore = 0;
        var missingThumb = 0;

        foreach (var channel in channels)
        {
            var pending = new List<QueueItem>();
            if (!channel.Sources.Any(source => source.Enabled))
                Log($"{channel.Name}: chưa có nguồn nào được bật.");
            foreach (var source in channel.Sources.Where(source => source.Enabled))
            {
                var scan = FolderScanner.Scan(source);
                foreach (var note in scan.Notes)
                    Log($"{channel.Name}: {note}");

                foreach (var video in scan.Videos)
                {
                    var item = new QueueItem
                    {
                        Channel = channel,
                        SourceName = source.Name,
                        Index = video.Index,
                        VideoPath = video.VideoPath,
                        ThumbPath = video.ThumbPath,
                        Title = video.Title,
                        TitleTrimmed = video.TitleTrimmed,
                        FileSize = video.FileSize,
                        FileModifiedUtc = video.FileModifiedUtc
                    };
                    var exact = _history.FindExact(channel.Id, video.VideoPath, video.FileSize);
                    if (exact != null)
                    {
                        item.Selected = false;
                        item.YoutubeVideoId = exact.YoutubeVideoId;
                        item.PublishAt = exact.PublishAt;
                        item.Status = string.IsNullOrWhiteSpace(exact.Status) ? "Đã đăng" : exact.Status;
                        uploadedCount++;
                    }
                    else if (FileNames.IsBeforeStart(video.Index, source.StartIndex))
                    {
                        item.Selected = false;
                        item.Status = $"Bỏ qua, trước số {source.StartIndex}";
                        skippedBefore++;
                    }
                    else
                    {
                        var previous = _history.FindPath(channel.Id, video.VideoPath);
                        item.Selected = true;
                        if (previous != null)
                            item.Status = "File đã đổi so với " + previous.YoutubeVideoId;
                        else if (video.MissingThumb)
                            item.Status = video.TitleTrimmed ? "Thiếu thumbnail, tiêu đề đã cắt" : "Thiếu thumbnail";
                        else if (video.TitleTrimmed)
                            item.Status = "Sẵn sàng, tiêu đề đã cắt còn 100 ký tự";
                        else
                            item.Status = "Sẵn sàng";
                        if (video.MissingThumb)
                            missingThumb++;
                        pending.Add(item);
                        pendingCount++;
                    }

                    _items.Add(item);
                }
            }
        }

        AssignSchedules();
        BindGrid();
        Log($"Quét xong: {pendingCount} video chờ, {uploadedCount} đã đăng, {skippedBefore} bỏ qua vì số nhỏ hơn, {missingThumb} thiếu thumbnail.");
    }

    void ApplyFromIndex()
    {
        WriteCurrent();
        var start = (int)numFrom.Value;
        var channels = _items.Select(item => item.Channel).Distinct().ToList();
        if (channels.Count == 0 && _current != null)
            channels.Add(_current);
        if (channels.Count == 0)
        {
            MessageBox.Show(this, "Hãy chọn một kênh trước.", "Từ số");
            return;
        }

        foreach (var channel in channels)
        {
            var names = _items.Where(item => ReferenceEquals(item.Channel, channel)).Select(item => item.SourceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var source in channel.Sources)
            {
                if (names.Count == 0 || names.Contains(source.Name))
                    source.StartIndex = start;
            }
        }

        _store.Save(_data);
        ReadSelectedSource();

        if (_items.Count == 0)
        {
            Log($"Đã đặt đăng từ số {start}. Bấm quét để áp dụng.");
            return;
        }

        var skipped = 0;
        var pending = 0;
        foreach (var item in _items)
        {
            if (!string.IsNullOrWhiteSpace(item.YoutubeVideoId))
            {
                item.Selected = false;
                continue;
            }

            item.PublishAt = null;
            if (FileNames.IsBeforeStart(item.Index, start))
            {
                item.Selected = false;
                item.Status = $"Bỏ qua, trước số {start}";
                skipped++;
                continue;
            }

            item.Selected = true;
            pending++;
            if (item.Status.StartsWith("File đã đổi", StringComparison.Ordinal))
                continue;
            if (item.ThumbPath == null)
                item.Status = item.TitleTrimmed ? "Thiếu thumbnail, tiêu đề đã cắt" : "Thiếu thumbnail";
            else if (item.TitleTrimmed)
                item.Status = "Sẵn sàng, tiêu đề đã cắt còn 100 ký tự";
            else
                item.Status = "Sẵn sàng";
        }

        AssignSchedules();
        BindGrid();
        Log(start <= 0
            ? $"Đã chọn lại mọi video chưa đăng: {pending} video."
            : $"Đã bỏ {skipped} video nhỏ hơn số {start}. Còn {pending} video được chọn. Lần quét sau vẫn giữ mốc này.");
    }

    void AssignSchedules()
    {
        foreach (var channel in _items.Select(item => item.Channel).Distinct())
        {
            var pending = _items.Where(item => ReferenceEquals(item.Channel, channel) && item.Selected && string.IsNullOrWhiteSpace(item.YoutubeVideoId)).ToList();
            if (!channel.Options.IsScheduled)
            {
                foreach (var item in pending)
                    item.PublishAt = null;
                continue;
            }

            var used = _history.Entries
                .Where(entry => entry.ChannelProfileId == channel.Id && entry.PublishAt != null && !string.IsNullOrWhiteSpace(entry.YoutubeVideoId))
                .Select(entry => entry.PublishAt!.Value);
            var plan = SchedulePlanner.Plan(pending.Count, channel.Options, used, DateTimeOffset.Now);
            if (!string.IsNullOrWhiteSpace(plan.Warning))
                Log($"{channel.Name}: {plan.Warning}");
            for (var i = 0; i < pending.Count && i < plan.Times.Count; i++)
                pending[i].PublishAt = plan.Times[i];
        }
    }

    void BindGrid()
    {
        grid.Rows.Clear();
        foreach (var item in _items)
        {
            var index = grid.Rows.Add(
                item.Selected,
                item.Channel.Name,
                item.SourceName,
                item.Index,
                item.Title,
                FormatSchedule(item),
                FileNames.FormatSize(item.FileSize),
                Path.GetFileName(item.VideoPath),
                item.ThumbPath == null ? "" : Path.GetFileName(item.ThumbPath),
                item.YoutubeVideoId,
                item.Status);
            grid.Rows[index].Tag = item;
        }

        UpdateSummary();
    }

    void UpdateRow(QueueItem item)
    {
        void write()
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!ReferenceEquals(row.Tag, item))
                    continue;
                row.Cells["select"].Value = item.Selected;
                row.Cells["title"].Value = item.Title;
                row.Cells["schedule"].Value = FormatSchedule(item);
                row.Cells["videoId"].Value = item.YoutubeVideoId;
                row.Cells["status"].Value = item.Status;
                break;
            }

            UpdateSummary();
        }

        if (grid.InvokeRequired)
            grid.BeginInvoke(write);
        else
            write();
    }

    void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not QueueItem item)
            return;
        if (grid.Columns[e.ColumnIndex].Name == "select")
            item.Selected = grid.Rows[e.RowIndex].Cells["select"].Value is bool selected && selected;
        if (grid.Columns[e.ColumnIndex].Name == "title")
            item.Title = grid.Rows[e.RowIndex].Cells["title"].Value?.ToString() ?? "";
        UpdateSummary();
    }

    void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.CellStyle == null || e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not QueueItem item)
            return;
        if (item.Status.StartsWith("Lỗi", StringComparison.Ordinal))
            e.CellStyle.ForeColor = DarkTheme.Bad;
        else if (item.Status.StartsWith("Đã đăng", StringComparison.Ordinal))
            e.CellStyle.ForeColor = DarkTheme.Ok;
        else if (item.Status.Contains("Thiếu", StringComparison.Ordinal) || item.Status.Contains("đã cắt", StringComparison.Ordinal))
            e.CellStyle.ForeColor = DarkTheme.Warn;
        else
            e.CellStyle.ForeColor = DarkTheme.Text;
    }

    void GridToolTip(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Rows[e.RowIndex].Tag is not QueueItem item)
            return;
        var name = grid.Columns[e.ColumnIndex].Name;
        e.ToolTipText = name switch
        {
            "title" => item.Title,
            "video" => item.VideoPath,
            "thumb" => item.ThumbPath ?? "",
            "status" => item.Status,
            _ => e.ToolTipText
        };
    }

    void SetChecks(Func<QueueItem, bool> predicate)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is not QueueItem item)
                continue;
            var selected = predicate(item);
            item.Selected = selected;
            row.Cells["select"].Value = selected;
        }

        UpdateSummary();
    }

    void UpdateSummary()
    {
        var pending = _items.Count(item => string.IsNullOrEmpty(item.YoutubeVideoId));
        var done = _items.Count - pending;
        var selected = _items.Count(item => item.Selected);
        lblSummary.Text = $"{_items.Count} dòng · {pending} chờ · {done} đã có video · {selected} đang chọn";
    }

    async Task UploadAsync()
    {
        WriteCurrent();
        _data.MaxUploadsPerRun = (int)numBatch.Value;
        _store.Save(_data);
        var jobs = SelectedJobs(out var skippedUploaded);
        if (skippedUploaded > 0)
            Log($"Bỏ qua {skippedUploaded} dòng đã có video trên YouTube.");
        if (jobs.Count == 0)
        {
            MessageBox.Show(this, "Chưa chọn video nào cần đăng.", "Đăng");
            return;
        }

        var limit = (int)numBatch.Value;
        if (limit > 0 && jobs.Count > limit)
        {
            Log($"Giới hạn mỗi lần chạy là {limit} video. Phần còn lại giữ trong bảng.");
            jobs = jobs.Take(limit).ToList();
        }

        foreach (var job in jobs)
        {
            job.Title = FileNames.SanitizeTitle(job.Title);
            if (string.IsNullOrWhiteSpace(job.Title))
            {
                MessageBox.Show(this, "Có video tiêu đề trống sau khi làm sạch.", "Đăng");
                return;
            }

            if (!File.Exists(job.VideoPath))
            {
                MessageBox.Show(this, "Không thấy file:\n" + job.VideoPath, "Đăng");
                return;
            }

            if (job.Channel.Options.IsScheduled)
            {
                if (job.PublishAt == null)
                {
                    MessageBox.Show(this, "Có video chưa có giờ hẹn. Hãy quét lại.", "Đăng");
                    return;
                }

                if (job.PublishAt.Value <= DateTimeOffset.UtcNow.AddMinutes(2))
                {
                    MessageBox.Show(this, "Có giờ hẹn đã qua hoặc sắp tới. Hãy quét lại để xếp giờ mới.", "Đăng");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(job.Channel.Options.PlaylistId)
                && MetadataComposer.NormalizePlaylistId(job.Channel.Options.PlaylistId) == null)
            {
                MessageBox.Show(this, $"Kênh {job.Channel.Name} có playlist không hợp lệ.", "Đăng");
                return;
            }
        }

        var lines = jobs.GroupBy(job => job.Channel.Name).Select(group => $"{group.Key}: {group.Count()} video");
        var confirm = MessageBox.Show(
            this,
            "Sẽ upload:\n" + string.Join("\n", lines) + "\n\nVideo hẹn giờ ở trạng thái riêng tư đến đúng giờ công khai.",
            "Xác nhận đăng",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;

        SetBusy(true);
        _uploadCts = new CancellationTokenSource();
        var ok = 0;
        var fail = 0;
        var blockedClients = new HashSet<string>(StringComparer.Ordinal);
        var services = new Dictionary<string, Google.Apis.YouTube.v3.YouTubeService>();
        var progress = new Progress<UploadProgress>(update =>
        {
            progressBar.Value = Math.Clamp(update.Percent, 0, 100);
            lblProgress.Text = update.Text;
        });

        try
        {
            for (var i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (_uploadCts.IsCancellationRequested)
                    break;
                if (blockedClients.Contains(job.Channel.ClientId))
                {
                    job.Status = "Bỏ qua vì project đã hết hạn mức";
                    fail++;
                    UpdateRow(job);
                    continue;
                }

                lblProgress.Text = $"{i + 1}/{jobs.Count}";
                job.Status = "Đang upload...";
                UpdateRow(job);
                try
                {
                    if (!services.TryGetValue(job.Channel.Id, out var service))
                    {
                        service = YouTubeAuth.CreateService(job.Channel);
                        services[job.Channel.Id] = service;
                    }

                    var result = await _uploader.UploadAsync(service, job, progress, Log, _uploadCts.Token);
                    if (!string.IsNullOrWhiteSpace(result.VideoId))
                    {
                        job.YoutubeVideoId = result.VideoId;
                        job.Selected = false;
                        ok++;
                    }
                    else
                    {
                        fail++;
                    }

                    job.Status = result.Status;
                    if (!string.IsNullOrWhiteSpace(result.Warning))
                        Log(result.Warning);
                    if (!string.IsNullOrWhiteSpace(result.VideoId))
                        Remember(job);
                    UpdateRow(job);
                    Log(result.Status + (string.IsNullOrWhiteSpace(result.VideoId) ? "" : " " + result.VideoId));

                    if (result.StopBatch)
                    {
                        blockedClients.Add(job.Channel.ClientId);
                        Log("Project này hết hạn mức upload hôm nay. Kênh khác dùng client_id khác vẫn tiếp tục.");
                    }
                }
                catch (OperationCanceledException)
                {
                    if (string.IsNullOrEmpty(job.YoutubeVideoId))
                    {
                        job.Status = "Đã dừng";
                        UpdateRow(job);
                    }

                    Log("Đã dừng theo yêu cầu.");
                    break;
                }
                catch (Exception ex)
                {
                    job.Status = "Lỗi: " + ApiErrors.Explain(ex);
                    fail++;
                    UpdateRow(job);
                    Log(job.Status);
                }
            }
        }
        finally
        {
            foreach (var service in services.Values)
                service.Dispose();
            _uploadCts.Dispose();
            _uploadCts = null;
            SetBusy(false);
            progressBar.Value = 0;
            lblProgress.Text = "";
            Log($"Kết thúc lần chạy: {ok} video lên YouTube, {fail} lỗi.");
        }
    }

    async Task RetryThumbnailsAsync()
    {
        WriteCurrent();
        var jobs = new List<QueueItem>();
        grid.EndEdit();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is not QueueItem item)
                continue;
            var selected = row.Cells["select"].Value is bool value && value;
            if (selected && !string.IsNullOrWhiteSpace(item.YoutubeVideoId) && !string.IsNullOrWhiteSpace(item.ThumbPath))
                jobs.Add(item);
        }

        if (jobs.Count == 0)
        {
            MessageBox.Show(this, "Hãy chọn dòng đã có Video ID và file thumbnail.", "Thumbnail");
            return;
        }

        SetBusy(true);
        _uploadCts = new CancellationTokenSource();
        var services = new Dictionary<string, Google.Apis.YouTube.v3.YouTubeService>();
        try
        {
            foreach (var job in jobs)
            {
                _uploadCts.Token.ThrowIfCancellationRequested();
                if (!services.TryGetValue(job.Channel.Id, out var service))
                {
                    service = YouTubeAuth.CreateService(job.Channel);
                    services[job.Channel.Id] = service;
                }

                try
                {
                    await _uploader.SetThumbnailOnlyAsync(service, job.YoutubeVideoId, job.ThumbPath!, _uploadCts.Token);
                    job.Status = "Đã gắn lại thumbnail";
                    Remember(job);
                    Log($"Đã gắn lại thumbnail cho {job.YoutubeVideoId}.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    job.Status = "Lỗi thumbnail: " + ApiErrors.Explain(ex);
                    Log(job.Status);
                }

                UpdateRow(job);
            }
        }
        catch (OperationCanceledException)
        {
            Log("Đã dừng gắn thumbnail.");
        }
        finally
        {
            foreach (var service in services.Values)
                service.Dispose();
            _uploadCts.Dispose();
            _uploadCts = null;
            SetBusy(false);
        }
    }

    List<QueueItem> SelectedJobs(out int skippedUploaded)
    {
        grid.EndEdit();
        skippedUploaded = 0;
        var jobs = new List<QueueItem>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is not QueueItem item)
                continue;
            item.Selected = row.Cells["select"].Value is bool selected && selected;
            item.Title = row.Cells["title"].Value?.ToString() ?? item.Title;
            if (!item.Selected)
                continue;
            if (!string.IsNullOrWhiteSpace(item.YoutubeVideoId))
            {
                skippedUploaded++;
                continue;
            }

            jobs.Add(item);
        }

        return jobs
            .OrderBy(job => job.PublishAt ?? DateTimeOffset.MaxValue)
            .ThenBy(job => job.Channel.Name, StringComparer.CurrentCulture)
            .ThenBy(job => job.Index, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    void Remember(QueueItem item)
    {
        if (string.IsNullOrWhiteSpace(item.YoutubeVideoId))
            return;

        var entry = _history.FindExact(item.Channel.Id, item.VideoPath, item.FileSize);
        if (entry == null)
        {
            entry = new HistoryEntry
            {
                ChannelProfileId = item.Channel.Id,
                VideoPath = item.VideoPath,
                FileSize = item.FileSize,
                FileModifiedUtc = item.FileModifiedUtc
            };
            _history.Entries.Add(entry);
        }

        entry.Index = item.Index;
        entry.SourceName = item.SourceName;
        entry.Title = item.Title;
        entry.YoutubeVideoId = item.YoutubeVideoId;
        entry.PublishAt = item.PublishAt;
        entry.Status = item.Status;
        entry.UploadedAt = DateTimeOffset.Now;
        _store.SaveHistory(_history);
    }

    void OpenSelectedFile(Func<QueueItem, string?> pathOf)
    {
        if (grid.CurrentRow?.Tag is not QueueItem item)
            return;
        var path = pathOf(item);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
    }

    static string FormatSchedule(QueueItem item)
    {
        if (!item.Channel.Options.IsScheduled)
            return "Đăng ngay";
        if (item.PublishAt == null)
            return "Chưa có giờ";
        var zone = TimeZones.Resolve(item.Channel.Options.TimeZoneId);
        return TimeZoneInfo.ConvertTime(item.PublishAt.Value, zone).ToString("dd/MM/yyyy HH:mm");
    }

    void SetBusy(bool busy)
    {
        _uploading = busy;
        btnUpload.Enabled = !busy;
        btnScanOne.Enabled = !busy;
        btnScanAll.Enabled = !busy;
        btnRetryThumbs.Enabled = !busy;
        btnAddChannel.Enabled = !busy;
        btnRemoveChannel.Enabled = !busy;
        btnStop.Enabled = busy;
    }
}
