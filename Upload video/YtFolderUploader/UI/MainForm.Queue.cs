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
            : _data.Channels.Where(channel => channel.Enabled && (!_autoTickRunning || _autoArmed.Contains(channel.Id))).ToArray();
        if (channels.Length == 0)
        {
            if (_autoTickRunning)
                return;
            MessageBox.Show(this, "Chưa có kênh nào được bật.", "Quét");
            return;
        }

        if (_uploading || _autoTickRunning)
        {
            channels = channels.Where(channel => !IsBusy(channel)).ToArray();
            if (channels.Length == 0)
                return;
            var drop = channels.Select(channel => channel.Id).ToHashSet();
            _items.RemoveAll(item => drop.Contains(item.Channel.Id));
        }
        else
        {
            _items.Clear();
        }

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
                    else if (video.MissingThumb)
                    {
                        missingThumb++;
                        continue;
                    }
                    else if (FileNames.IsBeforeStart(video.Index, source.StartIndex))
                    {
                        skippedBefore++;
                        continue;
                    }
                    else
                    {
                        var previous = _history.FindPath(channel.Id, video.VideoPath);
                        item.Selected = false;
                        item.Status = previous != null
                            ? "File đã đổi so với " + previous.YoutubeVideoId
                            : "Chờ ngày sau";
                        pending.Add(item);
                        pendingCount++;
                    }

                    _items.Add(item);
                }
            }
        }

        AssignSchedules();
        BindGrid();
        Log($"Quét xong: {pendingCount} video có ảnh sẽ đăng, {uploadedCount} đã đăng, {skippedBefore} bỏ vì số nhỏ hơn, {missingThumb} thiếu ảnh nên không vào hàng.");
    }

    void RefreshQueueFromSettings()
    {
        if (Loading || _current == null || IsBusy(_current))
            return;

        WriteCurrent();
        var start = (int)numFrom.Value;
        foreach (var source in _current.Sources)
            source.StartIndex = start;
        if (numSourceStart != null && numSourceStart.Value != start)
        {
            BeginLoad();
            numSourceStart.Value = Math.Clamp(start, (int)numSourceStart.Minimum, (int)numSourceStart.Maximum);
            EndLoad();
        }

        _data.MaxUploadsPerRun = (int)numBatch.Value;
        _store.Save(_data);
        if (_current.Sources.Count == 0)
            return;

        Scan(SelectedChannelsOnly: true);
    }

    void AssignSchedules(bool includeBusy = false)
    {
        foreach (var channel in _items.Select(item => item.Channel).Distinct())
        {
            if (!includeBusy && IsBusy(channel))
                continue;
            var pending = _items.Where(item => ReferenceEquals(item.Channel, channel) && string.IsNullOrWhiteSpace(item.YoutubeVideoId)).ToList();
            if (!channel.Options.IsScheduled)
            {
                foreach (var item in pending)
                    item.PublishAt = null;
                continue;
            }

            var used = _history.Entries
                .Where(entry => entry.ChannelProfileId == channel.Id && entry.PublishAt != null && !string.IsNullOrWhiteSpace(entry.YoutubeVideoId))
                .Select(entry => entry.PublishAt!.Value);
            var plan = SchedulePlanner.Plan(pending.Count, channel.Options, used, DateTimeOffset.Now, channel.UploadDeferredTo);
            channel.ShortSource = plan.ShortSource;
            channel.DayAlreadySet = plan.DayAlreadySet;
            channel.PlannedDay = plan.TargetDay;
            channel.ScheduleNote = plan.Warning;
            if (!string.IsNullOrWhiteSpace(plan.Warning))
                Log($"{channel.Name}: {plan.Warning}");

            if (channel.Options.ExactDailyTimes)
            {
                for (var i = 0; i < pending.Count; i++)
                {
                    if (i < plan.Times.Count)
                    {
                        pending[i].PublishAt = plan.Times[i];
                        pending[i].Selected = true;
                        if (!pending[i].Status.StartsWith("File đã đổi", StringComparison.Ordinal))
                            pending[i].Status = "Sẵn sàng, ngày " + plan.TargetDay?.ToString("dd/MM");
                    }
                    else
                    {
                        pending[i].PublishAt = null;
                        pending[i].Selected = false;
                        pending[i].Status = plan.ShortSource
                            ? "Không đủ video cho một ngày, không đăng"
                            : plan.DayAlreadySet
                                ? "Ngày này đã có lịch"
                                : "Chờ ngày sau";
                    }
                }
            }
            else
            {
                var keep = Math.Max(1, channel.Options.VideosPerDay);
                for (var i = 0; i < pending.Count; i++)
                {
                    if (i < keep && i < plan.Times.Count)
                    {
                        pending[i].PublishAt = plan.Times[i];
                        pending[i].Selected = true;
                        pending[i].Status = "Sẵn sàng";
                    }
                    else
                    {
                        pending[i].PublishAt = i < plan.Times.Count ? plan.Times[i] : null;
                        pending[i].Selected = false;
                        if (string.IsNullOrWhiteSpace(pending[i].YoutubeVideoId))
                            pending[i].Status = "Chờ ngày sau";
                    }
                }
            }
        }

        lstChannels.Invalidate();
    }

    void ClearChannelQueue()
    {
        if (_current == null || IsBusy(_current))
        {
            if (IsBusy(_current))
                MessageBox.Show(this, "Kênh này đang upload. Hết lượt đăng rồi hãy xóa hàng.", "Xóa hàng đợi");
            return;
        }

        var count = _items.Count(item => item.Channel.Id == _current.Id);
        if (count == 0)
        {
            MessageBox.Show(this, "Hàng đợi kênh này đang trống.", "Xóa hàng đợi");
            return;
        }

        if (MessageBox.Show(
                this,
                $"Xóa {count} dòng trong hàng đợi của \"{_current.Name}\"?\nFile video và thumbnail không bị xóa. Bấm Quét lại thì danh sách hiện lại.",
                "Xóa hàng đợi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var channelId = _current.Id;
        _items.RemoveAll(item => item.Channel.Id == channelId);
        BindGrid();
        UpdateSummary();
        Log($"Đã xóa hàng đợi {_current.Name}: {count} dòng. File trên đĩa vẫn còn.");
    }

    void ShowChannelQueue()
    {
        if (_autoTickRunning || _current == null)
            return;
        if (IsBusy(_current))
        {
            BindGrid();
            UpdateSummary();
            return;
        }
        if (_current.Sources.Any(source => source.Enabled && !string.IsNullOrWhiteSpace(source.VideoFolder)))
            Scan(SelectedChannelsOnly: true);
        else
        {
            if (_uploading)
                _items.RemoveAll(item => item.Channel.Id == _current.Id);
            else
                _items.Clear();
            grid.Rows.Clear();
            UpdateSummary();
        }
    }

    IEnumerable<QueueItem> VisibleItems() =>
        _current == null ? _items : _items.Where(item => item.Channel.Id == _current.Id);

    void BindGrid()
    {
        grid.Rows.Clear();
        foreach (var item in VisibleItems())
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
        var visible = VisibleItems().ToList();
        var pending = visible.Count(item => string.IsNullOrEmpty(item.YoutubeVideoId));
        var done = visible.Count - pending;
        var selected = visible.Count(item => item.Selected);
        lblSummary.Text = $"{visible.Count} dòng · {pending} chờ · {done} đã có video · {selected} đang chọn";
    }

    async Task UploadTomorrowAllAsync()
    {
        if (_uploading)
        {
            MessageBox.Show(this, "Đang đăng. Hãy đợi xong hoặc bấm Dừng.", "Đăng hết ngày mai");
            return;
        }

        Scan(SelectedChannelsOnly: false);
        LogMissingDayChannels();
        LogChannelsAlreadyPostedTomorrow();
        await UploadAsync(confirm: true, allChannels: true, keepChannelOrder: true);
    }

    void LogChannelsAlreadyPostedTomorrow()
    {
        foreach (var channel in _data.Channels.Where(AlreadyPostedTomorrow))
        {
            var day = channel.PlannedDay ?? SchedulePlanner.TargetDay(channel.Options, DateTimeOffset.Now, channel.UploadDeferredTo);
            Log($"{channel.Name}: đã đăng ngày {day:dd/MM}, không đăng nữa.");
        }
    }

    bool AlreadyPostedTomorrow(ChannelProfile channel)
    {
        var used = _history.Entries
            .Where(entry => entry.ChannelProfileId == channel.Id && entry.PublishAt != null && !string.IsNullOrWhiteSpace(entry.YoutubeVideoId))
            .Select(entry => entry.PublishAt!.Value)
            .Concat(_items
                .Where(item => item.Channel.Id == channel.Id && item.PublishAt != null && !string.IsNullOrWhiteSpace(item.YoutubeVideoId))
                .Select(item => item.PublishAt!.Value));
        return channel.DayAlreadySet
            || SchedulePlanner.HasUploadOnTargetDay(channel.Options, used, DateTimeOffset.Now, channel.UploadDeferredTo);
    }

    void LogMissingDayChannels()
    {
        foreach (var channel in _data.Channels.Where(channel => channel.ShortSource))
        {
            Log($"{channel.Name}: thiếu video cho một ngày. Bỏ qua kênh này, không đăng nửa ngày.");
            _autoStopped.Add(channel.Id);
        }

        lstChannels.Invalidate();
    }

    async Task UploadAsync(bool confirm = true, bool allChannels = false, bool keepChannelOrder = false)
    {
        WriteCurrent();
        _data.MaxUploadsPerRun = (int)numBatch.Value;
        _store.Save(_data);
        if (_items.Any(item => item.Channel.Options.ExactDailyTimes))
        {
            AssignSchedules();
            BindGrid();
        }

        var jobs = SelectedJobs(allChannels, out var skippedUploaded);
        if (keepChannelOrder)
        {
            var alreadyPosted = _data.Channels.Where(AlreadyPostedTomorrow).Select(channel => channel.Id).ToHashSet();
            jobs = jobs.Where(job => !alreadyPosted.Contains(job.Channel.Id)).ToList();
            jobs = ChannelQueueOrder.InListOrder(
                jobs,
                _data.Channels.Select(channel => channel.Id).ToList(),
                job => job.Channel.Id,
                job => job.PublishAt,
                job => job.Index);
        }
        if (!confirm)
            jobs = jobs.Where(job => _autoArmed.Contains(job.Channel.Id)).ToList();
        if (skippedUploaded > 0)
            Log($"Bỏ qua {skippedUploaded} dòng đã có video trên YouTube.");
        if (jobs.Count == 0)
        {
            foreach (var channel in _items.Select(item => item.Channel).Distinct())
            {
                if (channel.DayAlreadySet && channel.PlannedDay != null)
                    _autoDone[channel.Id] = channel.PlannedDay.Value;
            }

            NoteShortChannels();
            if (confirm)
            {
                var note = keepChannelOrder
                    ? "Không còn kênh nào cần đăng ngày mai. Kênh đã có lịch ngày mai thì bỏ qua."
                    : _items.Select(item => item.Channel).FirstOrDefault(channel => channel.ShortSource)?.ScheduleNote;
                MessageBox.Show(this, note ?? "Chưa chọn video nào cần đăng.", "Đăng");
            }

            return;
        }

        var exactRun = jobs.All(job => job.Channel.Options.ExactDailyTimes);
        var limit = (int)numBatch.Value;
        if (!keepChannelOrder && !exactRun && limit > 0 && jobs.Count > limit)
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
        var missing = _data.Channels.Where(channel => channel.ShortSource).Select(channel => channel.Name).ToList();
        var posted = _data.Channels.Where(AlreadyPostedTomorrow).Select(channel => channel.Name).ToList();
        var prompt = keepChannelOrder
            ? "Đăng lần lượt từ kênh trên xuống dưới, không đăng cùng lúc.\nMỗi kênh chỉ đăng đủ video của ngày mai.\nKênh đã đăng ngày mai thì không đăng nữa.\nKênh thiếu video thì bỏ qua.\n\n"
                + string.Join("\n", lines)
                + (posted.Count == 0 ? "" : "\n\nĐã đăng ngày mai, bỏ qua: " + string.Join(", ", posted))
                + (missing.Count == 0 ? "" : "\n\nThiếu video, không đăng: " + string.Join(", ", missing))
            : exactRun
            ? "Chỉ đăng ngày mai, đúng các giờ đã nhập.\nApp còn mở thì ngày sau tự đăng tiếp đúng kênh này.\nKênh thêm sau không tự đăng cho đến khi bấm Đăng.\nThiếu video cho một ngày thì dừng, không đăng nửa ngày.\n\n" + string.Join("\n", lines)
            : "Sẽ upload:\n" + string.Join("\n", lines) + "\n\nVideo hẹn giờ ở trạng thái riêng tư đến đúng giờ công khai.";
        if (confirm && MessageBox.Show(this, prompt, "Xác nhận đăng", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        if (exactRun && confirm)
        {
            foreach (var channelId in jobs.Select(job => job.Channel.Id).Distinct())
                _autoArmed.Add(channelId);
            ArmAuto();
        }

        _busyChannelIds.Clear();
        foreach (var channelId in jobs.Select(job => job.Channel.Id).Distinct())
            _busyChannelIds.Add(channelId);
        SetBusy(true);
        _uploadCts = new CancellationTokenSource();
        var ok = 0;
        var fail = 0;
        var blockedClients = new HashSet<string>(StringComparer.Ordinal);
        var deferredClients = new HashSet<string>(StringComparer.Ordinal);
        var thumbChecked = new HashSet<string>(StringComparer.Ordinal);
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
                    if (!deferredClients.Contains(job.Channel.ClientId))
                    {
                        job.Status = "Bỏ qua vì project đã hết hạn mức";
                        fail++;
                        UpdateRow(job);
                    }

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

                    if (thumbChecked.Add(job.Channel.Id) && FindThumbProbe(job.Channel) is { } probe)
                    {
                        Log($"{job.Channel.Name}: kiểm tra gắn thumbnail trước khi đăng.");
                        try
                        {
                            await _uploader.ProbeThumbnailAsync(service, probe.VideoId, probe.ThumbPath, Log, _uploadCts.Token);
                            Log($"{job.Channel.Name}: thumbnail gắn được. Bắt đầu đăng.");
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex) when (ApiErrors.ShouldDeferAfterThumbFailure(ex))
                        {
                            Log($"{job.Channel.Name}: không gắn được thumbnail. " + ApiErrors.Explain(ex));
                            DeferAfterThumbLimit(job.Channel, job.PublishAt);
                            blockedClients.Add(job.Channel.ClientId);
                            deferredClients.Add(job.Channel.ClientId);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Log($"{job.Channel.Name}: ảnh thumbnail cũ không dùng để thử. " + ApiErrors.Explain(ex));
                        }
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
                    var originalPublish = job.PublishAt;
                    if (!string.IsNullOrWhiteSpace(result.VideoId))
                    {
                        if (result.DeferSchedule && originalPublish != null)
                        {
                            var moved = ScheduleDefer.AddOneDay(originalPublish.Value, job.Channel.Options.TimeZoneId);
                            try
                            {
                                await _uploader.MovePublishAsync(service, result.VideoId, moved, _uploadCts.Token);
                                job.PublishAt = moved;
                                Log($"Đã chuyển giờ công khai của {result.VideoId} sang {FormatSchedule(job)}.");
                            }
                            catch (Exception moveEx) when (moveEx is not OperationCanceledException)
                            {
                                Log("Không đổi được giờ công khai trên YouTube: " + ApiErrors.Explain(moveEx));
                            }
                        }

                        Remember(job);
                        if (job.Channel.Options.DeleteSourceAfterUpload && result.ThumbAttached && !result.DeferSchedule)
                            DeleteUploadedFile(job);
                    }
                    UpdateRow(job);
                    Log(result.Status + (string.IsNullOrWhiteSpace(result.VideoId) ? "" : " " + result.VideoId));

                    if (result.DeferSchedule)
                    {
                        DeferAfterThumbLimit(job.Channel, originalPublish);
                        blockedClients.Add(job.Channel.ClientId);
                        deferredClients.Add(job.Channel.ClientId);
                    }
                    else if (result.StopBatch)
                    {
                        blockedClients.Add(job.Channel.ClientId);
                        Log("Dừng đăng tiếp vì không gắn được thumbnail hoặc hết hạn mức. Kênh khác dùng client_id khác vẫn tiếp tục.");
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
                    if (ApiErrors.IsDailyQuota(ex))
                    {
                        DeferAfterThumbLimit(job.Channel, job.PublishAt);
                        blockedClients.Add(job.Channel.ClientId);
                        deferredClients.Add(job.Channel.ClientId);
                    }
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
            if (deferredClients.Count > 0)
            {
                AssignSchedules();
                BindGrid();
            }

            Log($"Kết thúc lần chạy: {ok} video lên YouTube, {fail} lỗi.");
            if (exactRun)
                RememberAutoDay(jobs);
        }
    }

    void ArmAuto()
    {
        var started = !_autoDaily;
        _autoDaily = true;
        _autoTimer ??= new System.Windows.Forms.Timer { Interval = 60_000 };
        _autoTimer.Tick -= OnAutoTick;
        _autoTimer.Tick += OnAutoTick;
        _autoTimer.Start();
        if (started)
            Log("Lịch tự động chỉ chạy cho kênh vừa bấm Đăng. Kênh thêm sau không tự đăng.");
    }

    void StopAuto()
    {
        if (_autoDaily)
            Log("Đã dừng lịch tự động.");
        _autoDaily = false;
        _autoArmed.Clear();
        _autoTimer?.Stop();
        _uploadCts?.Cancel();
    }

    async void OnAutoTick(object? sender, EventArgs e)
    {
        if (_autoTickRunning || _uploading || !_autoDaily || DateTimeOffset.Now < _autoNotBefore)
            return;

        var now = DateTimeOffset.Now;
        var due = _data.Channels.Any(channel =>
        {
            var alreadyDone = _autoDone.TryGetValue(channel.Id, out var done)
                && done == SchedulePlanner.TargetDay(channel.Options, now, channel.UploadDeferredTo);
            return AutoScheduleGate.IsDue(
                _autoArmed.Contains(channel.Id),
                channel.Enabled,
                channel.Options.ExactDailyTimes,
                _autoStopped.Contains(channel.Id),
                alreadyDone);
        });
        if (!due)
            return;

        _autoTickRunning = true;
        try
        {
            Log("Tới lượt ngày mới. Đang xếp lịch ngày kế tiếp.");
            Scan(SelectedChannelsOnly: false);
            await UploadAsync(confirm: false, allChannels: true);
        }
        finally
        {
            _autoTickRunning = false;
        }
    }

    void NoteShortChannels()
    {
        foreach (var channel in _items.Select(item => item.Channel).Distinct().Where(channel => channel.ShortSource))
        {
            if (!_autoStopped.Add(channel.Id))
                continue;
            Log($"{channel.Name}: thiếu video cho một ngày. Dừng kênh này, không đăng tiếp.");
        }

        lstChannels.Invalidate();
    }

    void RememberAutoDay(IReadOnlyList<QueueItem> jobs)
    {
        foreach (var channel in jobs.Select(job => job.Channel).Distinct())
        {
            if (!channel.Options.ExactDailyTimes || channel.PlannedDay == null)
                continue;

            var failed = jobs.Any(job => ReferenceEquals(job.Channel, channel) && string.IsNullOrWhiteSpace(job.YoutubeVideoId));
            if (failed)
            {
                var backoff = DateTimeOffset.Now.AddMinutes(15);
                if (_autoNotBefore < backoff)
                    _autoNotBefore = backoff;
            }
            else
                _autoDone[channel.Id] = channel.PlannedDay.Value;
        }

        NoteShortChannels();
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

    List<QueueItem> SelectedJobs(bool allChannels, out int skippedUploaded)
    {
        grid.EndEdit();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is not QueueItem item)
                continue;
            item.Selected = row.Cells["select"].Value is bool selected && selected;
            item.Title = row.Cells["title"].Value?.ToString() ?? item.Title;
        }

        skippedUploaded = 0;
        var jobs = new List<QueueItem>();
        var source = allChannels || _current == null
            ? _items
            : _items.Where(item => item.Channel.Id == _current.Id);
        foreach (var item in source)
        {
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

    void DeleteUploadedFiles()
    {
        if (_uploading)
        {
            MessageBox.Show(this, "Đang đăng video. Hãy đợi xong rồi xóa file.", "Xóa file");
            return;
        }

        var jobs = VisibleItems().Where(item => !string.IsNullOrWhiteSpace(item.YoutubeVideoId) && HasLocalSource(item)).ToList();
        if (jobs.Count == 0)
        {
            MessageBox.Show(this, "Kênh này không còn file video hoặc thumbnail đã đăng trên máy.", "Xóa file");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Xóa vĩnh viễn video và thumbnail của {jobs.Count} video đã đăng trên kênh này?\nFile không vào thùng rác. Video trên YouTube vẫn còn.",
            "Xóa video và thumb",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
            return;

        var removed = 0;
        foreach (var job in jobs)
        {
            if (DeleteUploadedFile(job))
            {
                _items.Remove(job);
                removed++;
            }
        }

        BindGrid();
        Log($"Đã xóa vĩnh viễn video và thumbnail của {removed} video đã đăng.");
    }

    void DeleteAllUploadedFiles()
    {
        if (_uploading)
        {
            MessageBox.Show(this, "Đang đăng video. Hãy đợi xong rồi xóa file.", "Xóa file");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<(string Path, string Kind)>();
        foreach (var entry in _history.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.YoutubeVideoId))
                continue;
            Add(entry.VideoPath, "video");
            if (!string.Equals(entry.ThumbPath, entry.VideoPath, StringComparison.OrdinalIgnoreCase))
                Add(entry.ThumbPath, "thumbnail");
        }

        if (files.Count == 0)
        {
            MessageBox.Show(this, "Không còn file video hoặc thumbnail đã đăng trên máy.", "Xóa file");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Xóa vĩnh viễn {files.Count} file video và thumbnail đã đăng của mọi kênh?\nFile không vào thùng rác. Video trên YouTube vẫn còn.",
            "Xóa video đã đăng",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
            return;

        var removed = 0;
        var dropped = new HashSet<QueueItem>();
        foreach (var file in files)
        {
            if (!DeleteOne(file.Path, file.Kind))
                continue;
            removed++;
            foreach (var job in _items)
            {
                if (string.Equals(job.ThumbPath, file.Path, StringComparison.OrdinalIgnoreCase))
                    job.ThumbPath = null;
                if (string.Equals(job.VideoPath, file.Path, StringComparison.OrdinalIgnoreCase))
                {
                    job.Status = "Đã đăng, đã xóa file";
                    dropped.Add(job);
                }
            }
        }

        _items.RemoveAll(dropped.Contains);

        BindGrid();
        Log($"Đã xóa vĩnh viễn {removed} file đã đăng của mọi kênh.");

        void Add(string? path, string kind)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !seen.Add(path))
                return;
            files.Add((path, kind));
        }
    }

    static bool HasLocalSource(QueueItem item) =>
        File.Exists(item.VideoPath) || (!string.IsNullOrWhiteSpace(item.ThumbPath) && File.Exists(item.ThumbPath));

    bool DeleteUploadedFile(QueueItem job)
    {
        var ok = DeleteOne(job.VideoPath, "video");
        if (!string.IsNullOrWhiteSpace(job.ThumbPath)
            && !string.Equals(job.ThumbPath, job.VideoPath, StringComparison.OrdinalIgnoreCase))
            ok = DeleteOne(job.ThumbPath, "thumbnail") && ok;

        if (!ok)
            return false;

        job.Status = "Đã đăng, đã xóa file";
        job.ThumbPath = null;
        UpdateRow(job);
        return true;
    }

    bool DeleteOne(string? path, string kind)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;
        if (!SourceDeleter.TryDeletePermanent(path, out var error))
        {
            Log($"Không xóa được {kind} {path}. {error}");
            return false;
        }

        Log($"Đã xóa vĩnh viễn {kind} {path}");
        return true;
    }

    (string VideoId, string ThumbPath)? FindThumbProbe(ChannelProfile channel)
    {
        var fromHistory = _history.Entries
            .Where(entry => entry.ChannelProfileId == channel.Id && !string.IsNullOrWhiteSpace(entry.YoutubeVideoId))
            .OrderByDescending(entry => entry.UploadedAt)
            .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.ThumbPath) && File.Exists(entry.ThumbPath));
        if (fromHistory != null)
            return (fromHistory.YoutubeVideoId, fromHistory.ThumbPath!);

        var row = _items.LastOrDefault(item =>
            item.Channel.Id == channel.Id
            && !string.IsNullOrWhiteSpace(item.YoutubeVideoId)
            && !string.IsNullOrWhiteSpace(item.ThumbPath)
            && File.Exists(item.ThumbPath));
        return row == null ? null : (row.YoutubeVideoId, row.ThumbPath!);
    }

    void DeferAfterThumbLimit(ChannelProfile channel, DateTimeOffset? publishAt)
    {
        var day = ScheduleDefer.NextPublicDay(publishAt, channel.Options.TimeZoneId, DateTimeOffset.Now);
        foreach (var other in _data.Channels)
        {
            var sameProject = other.Id == channel.Id
                || (!string.IsNullOrWhiteSpace(channel.ClientId)
                    && string.Equals(other.ClientId, channel.ClientId, StringComparison.Ordinal));
            if (!sameProject)
                continue;
            if (other.UploadDeferredTo == null || other.UploadDeferredTo < day)
                other.UploadDeferredTo = day;
        }

        _store.Save(_data);
        var until = ScheduleDefer.StartOfDay(day, channel.Options.TimeZoneId);
        if (_autoNotBefore < until)
            _autoNotBefore = until;
        Log($"{channel.Name}: hết hạn mức khi gắn thumbnail. Không đăng thêm. Lịch công khai chuyển sang {day:dd/MM}.");
        AssignSchedules(includeBusy: true);
        BindGrid();
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
        entry.ThumbPath = item.ThumbPath;
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
        if (!busy)
            _busyChannelIds.Clear();
        btnUpload.Enabled = !busy;
        btnUploadAll.Enabled = !busy;
        btnDeleteUploaded.Enabled = !busy;
        btnScanAll.Enabled = !busy;
        btnRetryThumbs.Enabled = !busy;
        btnAddChannel.Enabled = true;
        btnStop.Enabled = busy || _autoDaily;
        ApplyEditorLock();
        if (busy && _current != null && IsBusy(_current))
            Log($"{_current.Name} đang upload. Bấm kênh khác để xem nguồn và cấu hình. Kênh đang upload thì khóa sửa.");
    }
}
