using System.Globalization;
using VideoAutoTool.Core.FrameBatch;
using VideoAutoTool.Core.Render;

namespace FrameRender;

public sealed class MainForm : Form
{
    private readonly FrameBatchEngine _engine;
    private readonly string _ffmpeg;
    private readonly TextBox _videos = new();
    private readonly TextBox _frames = new();
    private readonly TextBox _backgrounds = new();
    private readonly TextBox _output = new();
    private readonly TextBox _scale = new() { Text = "0.80", Width = 56 };
    private readonly TextBox _opacity = new() { Text = "0.90", Width = 56 };
    private readonly TextBox _crf = new() { Text = "20", Width = 48 };
    private readonly TextBox _workers = new() { Text = Math.Max(1, Environment.ProcessorCount / 2).ToString(CultureInfo.InvariantCulture), Width = 48 };
    private readonly TextBox _batch = new() { Text = FrameIntake.DefaultBatchSize.ToString(CultureInfo.InvariantCulture), Width = 48 };
    private readonly ComboBox _quality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 88 };
    private readonly CheckBox _gpu = new() { Text = "GPU (NVENC)", AutoSize = true, Checked = true };
    private readonly CheckBox _random = new() { Text = "Chọn ngẫu nhiên", AutoSize = true };
    private readonly DataGridView _main = new();
    private readonly DataGridView _waiting = new();
    private readonly Label _encoder = new() { AutoSize = true, ForeColor = Color.White, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
    private readonly TextBox _detail = new();
    private readonly ListBox _log = new();
    private readonly ProgressBar _progress = new() { Height = 16, Dock = DockStyle.Top };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.White };
    private readonly TextBox _folders = new();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 500 };
    private bool _allowPersist = true;
    private QueueController? _queue;

    public MainForm(FrameBatchEngine engine, string ffmpeg)
    {
        _engine = engine;
        _ffmpeg = ffmpeg;
        Text = FrameEdition.Title;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 740);
        Size = new Size(1180, 780);
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (!e.Control || e.KeyCode != Keys.S)
            {
                return;
            }

            SaveConfig();
            e.SuppressKeyPress = true;
            e.Handled = true;
        };
        Build();
        Load += (_, _) => Bind();
        FormClosing += (_, _) =>
        {
            _allowPersist = false;
            _clock.Stop();
            _queue?.StopForClose();
            Save();
        };
    }

    private void Bind()
    {
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FrameEdition.DataFolder, "cache");
        _queue = new QueueController(_engine, cache, SynchronizationContext.Current!);
        _queue.Logged += line => _log.Items.Insert(0, line);
        _queue.Changed += RefreshBars;
        _queue.Persist += () =>
        {
            if (_allowPersist)
            {
                Save();
            }
        };
        _main.DataSource = _queue.MainJobs;
        _waiting.DataSource = _queue.WaitingJobs;
        _main.SelectionChanged += (_, _) => ShowSelected(_main);
        _waiting.SelectionChanged += (_, _) => ShowSelected(_waiting);
        var saved = _queue.Load();
        if (saved is not null)
        {
            _videos.Text = saved.Videos;
            _frames.Text = saved.Frames;
            _backgrounds.Text = saved.Backgrounds;
            _output.Text = saved.Output;
            if (!string.IsNullOrWhiteSpace(saved.Scale)) _scale.Text = saved.Scale;
            if (!string.IsNullOrWhiteSpace(saved.Opacity)) _opacity.Text = saved.Opacity;
            if (!string.IsNullOrWhiteSpace(saved.Crf)) _crf.Text = saved.Crf;
            if (!string.IsNullOrWhiteSpace(saved.Workers)) _workers.Text = saved.Workers;
            _gpu.Checked = FrameEdition.PreferGpu || saved.Gpu;
            _random.Checked = saved.Random;
            if (saved.BatchSize > 0)
            {
                _batch.Text = saved.BatchSize.ToString(CultureInfo.InvariantCulture);
            }

            _quality.SelectedIndex = FrameDesign.NormalizeOutputHeight(saved.OutputHeight) == FrameDesign.OutputHeight480 ? 1 : 0;

            var paused = saved.Jobs.Count(row => row.State == FrameDesign.Paused);
            var cancelled = saved.Jobs.Count(row => row.State == FrameDesign.Cancelled);
            var todo = saved.Jobs.Count(row => row.State is FrameDesign.Wait or FrameDesign.Paused or FrameDesign.Failed or FrameDesign.Cancelled);
            if (_queue.DroppedSkipped > 0)
            {
                Save();
            }

            _log.Items.Insert(0, $"Đã mở lại {todo} video cần làm. Đã xóa {_queue.DroppedSkipped} video Bỏ qua khỏi hàng. Tạm dừng {paused}. Đã hủy {cancelled}. Bấm Tiếp tục, Render tiếp hoặc Bắt đầu. Ctrl+S để lưu cấu hình.");
        }
        else if (File.Exists(QueueController.QueuePath))
        {
            _log.Items.Insert(0, "Không đọc được hàng đợi đã lưu. File vẫn giữ nguyên.");
        }

        _videos.Leave += (_, _) => ApplyAutoOutput();
        _workers.Leave += (_, _) => ApplyWorkers();
        _workers.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            ApplyWorkers();
            e.SuppressKeyPress = true;
        };
        _clock.Tick += (_, _) => RefreshBars();
        _clock.Start();
        _log.Items.Insert(0, "FFmpeg: " + _ffmpeg);
        ApplyAutoOutput();
        RefreshBars();
    }

    private void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(30, 30, 30)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));

        var folders = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, BackColor = Color.FromArgb(30, 30, 30) };
        folders.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        folders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folders.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        AddFolder(folders, 0, "Video chính", _videos);
        AddFolder(folders, 1, "Khung PNG", _frames);
        AddFolder(folders, 2, "Video nền", _backgrounds);
        AddFolder(folders, 3, "Thư mục xuất", _output);

        var options = Row();
        AddNumber(options, "Scale", _scale);
        AddNumber(options, "Opacity video", _opacity);
        AddNumber(options, "CRF", _crf);
        AddNumber(options, "Số video cùng lúc", _workers);
        AddNumber(options, "Mỗi folder", _batch);
        _quality.Items.AddRange(["720p", "480p"]);
        _quality.SelectedIndex = 0;
        _quality.BackColor = Color.FromArgb(45, 45, 48);
        _quality.ForeColor = Color.White;
        _quality.FlatStyle = FlatStyle.Flat;
        options.Controls.Add(new Label { Text = "Chất lượng", AutoSize = true, ForeColor = Color.White, Padding = new Padding(8, 6, 4, 0) });
        options.Controls.Add(_quality);
        StyleCheck(_gpu);
        StyleCheck(_random);
        options.Controls.Add(_gpu);
        options.Controls.Add(_random);

        var buttons = Row();
        buttons.Controls.Add(Button("Thêm vào hàng đợi", (_, _) => AddToQueue()));
        buttons.Controls.Add(Button("Bắt đầu", (_, _) => StartQueue()));
        buttons.Controls.Add(Button("Tạm dừng", (_, _) => _queue?.Pause()));
        buttons.Controls.Add(Button("Tiếp tục", (_, _) => ContinueQueue()));
        buttons.Controls.Add(Button("Hủy", (_, _) => _queue?.Cancel(), Color.FromArgb(196, 43, 28)));
        buttons.Controls.Add(Button("Render tiếp", (_, _) => RenderAgain()));
        buttons.Controls.Add(Button("Xóa hàng đợi", (_, _) => ClearQueue()));
        buttons.Controls.Add(Button("Copy lỗi", (_, _) => CopyDetail()));

        StyleGrid(_main);
        StyleGrid(_waiting);
        _main.Columns.Add(Column("Source", "Nguồn (x/tất cả)", 28));
        _main.Columns.Add(Column("Output", "File xuất", 40));
        _main.Columns.Add(Column("Status", "Trạng thái", 18));
        _waiting.Columns.Add(Column("Source", "Nguồn (x/tất cả)", 28));
        _waiting.Columns.Add(Column("Output", "File xuất", 40));
        _waiting.Columns.Add(Column("Status", "Trạng thái", 18));
        var queues = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.FromArgb(30, 30, 30) };
        queues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        queues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        queues.Controls.Add(Wrap("Hàng chính", _main), 0, 0);
        queues.Controls.Add(Wrap("Nguồn đang chờ", _waiting), 1, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.FromArgb(30, 30, 30) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _detail.Multiline = true;
        _detail.ReadOnly = true;
        _detail.ScrollBars = ScrollBars.Vertical;
        _detail.Dock = DockStyle.Fill;
        _detail.BackColor = Color.FromArgb(45, 45, 48);
        _detail.ForeColor = Color.White;
        _detail.BorderStyle = BorderStyle.FixedSingle;
        _log.Dock = DockStyle.Fill;
        _log.BackColor = Color.FromArgb(45, 45, 48);
        _log.ForeColor = Color.White;
        _log.BorderStyle = BorderStyle.FixedSingle;
        bottom.Controls.Add(Wrap("Chi tiết / lỗi", _detail), 0, 0);
        bottom.Controls.Add(Wrap("Nhật ký", _log), 1, 0);

        var statusPanel = new TableLayoutPanel
        {
            Height = 118,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _progress.Dock = DockStyle.Fill;
        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(0, 4, 8, 0);
        _encoder.Dock = DockStyle.Fill;
        _encoder.TextAlign = ContentAlignment.MiddleRight;
        _folders.ReadOnly = true;
        _folders.Multiline = true;
        _folders.WordWrap = true;
        _folders.ScrollBars = ScrollBars.Vertical;
        _folders.BorderStyle = BorderStyle.None;
        _folders.Dock = DockStyle.Fill;
        _folders.TabStop = false;
        _folders.BackColor = Color.FromArgb(30, 30, 30);
        _folders.ForeColor = Color.White;
        statusPanel.Controls.Add(_progress, 0, 0);
        statusPanel.SetColumnSpan(_progress, 2);
        statusPanel.Controls.Add(_status, 0, 1);
        statusPanel.Controls.Add(_encoder, 1, 1);
        statusPanel.Controls.Add(_folders, 0, 2);
        statusPanel.SetColumnSpan(_folders, 2);

        root.Controls.Add(folders, 0, 0);
        root.Controls.Add(options, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        root.Controls.Add(statusPanel, 0, 3);
        root.Controls.Add(queues, 0, 4);
        root.Controls.Add(bottom, 0, 5);
        Controls.Add(root);
    }

    private bool AddToQueue()
    {
        if (_queue is null)
        {
            return false;
        }

        try
        {
            ApplyAutoOutput();
            var videos = FramePlan.ListMedia(_videos.Text, FrameDesign.VideoExtensions);
            var frames = FramePlan.ListMedia(_frames.Text, FrameDesign.FrameExtensions);
            var backgrounds = FramePlan.ListMedia(_backgrounds.Text, FrameDesign.VideoExtensions);
            if (videos.Count == 0 || frames.Count == 0 || backgrounds.Count == 0)
            {
                MessageBox.Show("Cần ít nhất 1 video, 1 PNG khung và 1 video nền.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_output.Text))
            {
                MessageBox.Show("Chọn thư mục xuất.");
                return false;
            }

            if (!int.TryParse(_batch.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var batch))
            {
                MessageBox.Show("Số video mỗi folder không hợp lệ.");
                return false;
            }

            _queue.Enqueue(videos, frames, backgrounds, _output.Text, _random.Checked, batch, _videos.Text);
            _batch.Text = _queue.BatchSize.ToString(CultureInfo.InvariantCulture);
            Save();
            return true;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message);
            return false;
        }
    }

    private void StartQueue()
    {
        if (_queue is null)
        {
            return;
        }

        if (!_queue.IsPumping)
        {
            _queue.Resume();
        }

        if (!_queue.Jobs.Any(row => row.State == FrameDesign.Wait) && !AddToQueue())
        {
            return;
        }

        if (!TryReadSettings(out var settings))
        {
            return;
        }

        try
        {
            var frames = FramePlan.ListMedia(_frames.Text, FrameDesign.FrameExtensions);
            var backgrounds = FramePlan.ListMedia(_backgrounds.Text, FrameDesign.VideoExtensions);
            _queue.Start(settings, frames, backgrounds, _output.Text);
            Save();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void ContinueQueue()
    {
        if (_queue is null)
        {
            return;
        }

        var pumping = _queue.IsPumping;
        _queue.Resume();
        if (!pumping && _queue.Jobs.Any(row => row.State == FrameDesign.Wait))
        {
            StartQueue();
        }
    }

    private void RenderAgain()
    {
        if (_queue is null)
        {
            return;
        }

        if (_queue.RenderAgain() > 0)
        {
            StartQueue();
        }
    }

    private void ClearQueue()
    {
        if (_queue is null)
        {
            return;
        }

        if (MessageBox.Show("Xóa hết video trên hàng đợi?", "Xóa hàng đợi", MessageBoxButtons.YesNo) != DialogResult.Yes)
        {
            return;
        }

        _queue.Clear();
        Save();
    }

    private void CopyDetail()
    {
        if (!string.IsNullOrWhiteSpace(_detail.Text))
        {
            Clipboard.SetText(_detail.Text);
        }
    }

    private void ShowSelected(DataGridView grid)
    {
        if (grid.CurrentRow?.DataBoundItem is not JobRow job)
        {
            return;
        }

        var logPath = FrameEdition.ErrorLog(job.OutputPath);
        var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
        _detail.Text = string.Join(Environment.NewLine, new[] { job.Status, job.VideoPath, job.OutputPath, job.Detail, log }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private void RefreshBars()
    {
        if (_queue is null || IsDisposed)
        {
            return;
        }

        var jobs = _queue.Jobs.ToList();
        var kept = _queue.KeptByFolder();
        var keptTotal = kept.Values.Sum();
        var total = jobs.Count + keptTotal;
        var done = jobs.Count(row => row.State == FrameDesign.Done) + keptTotal;
        var runningRows = jobs.Where(row => row.State == FrameDesign.Running).ToList();
        var waiting = jobs.Count(row => row.State == FrameDesign.Wait);
        var failed = jobs.Count(row => row.State == FrameDesign.Failed);
        var paused = jobs.Count(row => row.State == FrameDesign.Paused);
        var cancelled = jobs.Count(row => row.State == FrameDesign.Cancelled);
        var folderJobs = jobs.Select(row => (FrameFolderProgress.Key(row.IntakeName), row.State)).ToList();
        var folderLines = FrameFolderProgress.Format(folderJobs, kept);
        var folderLabels = FrameFolderProgress.Count(folderJobs, kept)
            .ToDictionary(item => item.Name, item => $"{item.Name} {item.Done}/{item.Total}", StringComparer.Ordinal);
        foreach (var row in jobs)
        {
            if (folderLabels.TryGetValue(FrameFolderProgress.Key(row.IntakeName), out var label))
            {
                row.Source = label;
            }
        }

        var folderText = string.IsNullOrEmpty(folderLines) ? "Từng folder: chưa có" : "Từng folder:" + Environment.NewLine + folderLines;
        if (_folders.Text != folderText)
        {
            _folders.Text = folderText;
        }
        var finished = done + runningRows.Sum(row => Math.Clamp(row.Fraction, 0, 1));
        var scale = Math.Max(1, total) * 1000;
        var value = (int)Math.Clamp(finished * 1000, 0, scale);
        if (_progress.Maximum != scale)
        {
            _progress.Value = 0;
            _progress.Maximum = scale;
        }

        _progress.Value = Math.Min(_progress.Maximum, value);
        var clock = "00:00:00";
        if (_queue.StartedAt is { } start)
        {
            var span = DateTime.Now - start;
            clock = $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        var tail = $"Xong {done}/{total}    Chờ {waiting}";
        if (paused > 0)
        {
            tail += $"    Tạm dừng {paused}";
        }

        if (cancelled > 0)
        {
            tail += $"    Đã hủy {cancelled}";
        }

        tail += $"    Lỗi {failed}";
        _status.Text = $"Đã chạy {clock}    {DescribeCurrent(runningRows)}    {DescribeTotal(jobs, runningRows)}    {tail}";
        if (_queue.IsPumping && !_workers.Focused)
        {
            var shown = _queue.WorkerLimit.ToString(CultureInfo.InvariantCulture);
            if (_workers.Text.Trim() != shown)
            {
                _workers.Text = shown;
            }
        }

        _encoder.Text = (_gpu.Checked ? "NVENC GPU" : "libx264 CPU") + (SelectedOutputHeight() == FrameDesign.OutputHeight480 ? " · 480p" : " · 720p");
        _batch.Enabled = _queue.BatchSize == 0;
    }

    private static string DescribeCurrent(IReadOnlyList<JobRow> running)
    {
        if (running.Count == 0)
        {
            return "Chưa có video đang chạy";
        }

        var parts = running.Select(row =>
        {
            var percent = (int)Math.Round(Math.Clamp(row.Fraction, 0, 1) * 100);
            var elapsed = row.StartedUtc is { } start ? DateTime.UtcNow - start : TimeSpan.Zero;
            var one = percent >= 2
                ? " · khoảng " + RenderTiming.FormatElapsed(TimeSpan.FromSeconds(elapsed.TotalSeconds / (percent / 100.0)))
                : "";
            return $"{percent}% · {RenderTiming.FormatElapsed(elapsed)}{one}";
        });
        var title = running.Count == 1 ? "Video này" : $"{running.Count} video";
        return $"{title} {string.Join(" | ", parts)}";
    }

    private static string DescribeTotal(IReadOnlyList<JobRow> jobs, IReadOnlyList<JobRow> running)
    {
        var samples = jobs
            .Where(row => row.State == FrameDesign.Done)
            .Select(row => RenderTiming.Elapsed(row.StartedUtc, row.FinishedUtc))
            .Where(span => span is { TotalSeconds: > 0 })
            .Select(span => span!.Value.TotalSeconds)
            .ToList();
        if (samples.Count == 0)
        {
            samples = running
                .Where(row => row.Fraction >= 0.02 && row.StartedUtc is not null)
                .Select(row => (DateTime.UtcNow - row.StartedUtc!.Value).TotalSeconds / row.Fraction)
                .Where(seconds => seconds > 0)
                .ToList();
        }

        if (samples.Count == 0)
        {
            return "Tổng: chờ thêm tiến độ";
        }

        var one = samples.Average();
        var waiting = jobs.Count(row => row.State is FrameDesign.Wait or FrameDesign.Paused);
        var left = waiting + running.Sum(row => 1 - Math.Clamp(row.Fraction, 0, 1));
        var parallel = Math.Max(1, running.Count);
        var oneLabel = RenderTiming.FormatElapsed(TimeSpan.FromSeconds(one));
        if (left <= 0.01)
        {
            return $"1 video ~ {oneLabel}    Còn lại ~ xong";
        }

        var eta = TimeSpan.FromSeconds(left * one / parallel);
        return $"1 video ~ {oneLabel}    Còn lại ~ {RenderTiming.FormatElapsed(eta)}";
    }

    private void ApplyAutoOutput()
    {
        if (!FrameEdition.AutoSiblingOutput || IsDisposed)
        {
            return;
        }

        var output = FramePlan.SiblingOutputFolder(_videos.Text);
        if (output is null)
        {
            return;
        }

        _output.Text = output;
        try
        {
            Directory.CreateDirectory(output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Items.Insert(0, "Không tạo được thư mục xuất: " + ex.Message);
        }
    }

    private int SelectedOutputHeight() =>
        _quality.SelectedIndex == 1 ? FrameDesign.OutputHeight480 : FrameDesign.Height;

    private void SaveConfig()
    {
        ApplyWorkers();
        Save();
        var limit = _queue?.WorkerLimit.ToString(CultureInfo.InvariantCulture) ?? _workers.Text.Trim();
        _log.Items.Insert(0, $"Đã lưu cấu hình. Số video cùng lúc: {limit}.");
    }

    private void ApplyWorkers()
    {
        if (_queue is null || IsDisposed)
        {
            return;
        }

        if (!int.TryParse(_workers.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var workers))
        {
            return;
        }

        var before = _queue.WorkerLimit;
        var applied = _queue.SetWorkerLimit(workers);
        var text = applied.ToString(CultureInfo.InvariantCulture);
        if (_workers.Text.Trim() != text)
        {
            _workers.Text = text;
        }

        if (before != applied)
        {
            Save();
        }
    }

    private bool TryReadSettings(out FrameSettings settings)
    {
        settings = new FrameSettings();
        try
        {
            var workers = FrameDesign.ParallelVideos(int.Parse(_workers.Text.Trim(), CultureInfo.InvariantCulture));
            _workers.Text = workers.ToString(CultureInfo.InvariantCulture);
            var canvas = FrameDesign.CanvasFor(SelectedOutputHeight());
            settings = new FrameSettings(
                Width: canvas.Width,
                Height: canvas.Height,
                VideoScale: double.Parse(_scale.Text.Trim(), CultureInfo.InvariantCulture),
                VideoOpacity: double.Parse(_opacity.Text.Trim(), CultureInfo.InvariantCulture),
                Crf: int.Parse(_crf.Text.Trim(), CultureInfo.InvariantCulture),
                Gpu: _gpu.Checked,
                Workers: workers,
                Threads: Math.Max(1, (Environment.ProcessorCount) / workers));
            var layout = FrameLayoutMath.Create(settings);
            _log.Items.Insert(0, $"Xuất {canvas.Width}x{canvas.Height}. Video chính {layout.VideoW}x{layout.VideoH} tại x={layout.X}, y={layout.Y}.");
            return true;
        }
        catch (FormatException)
        {
            MessageBox.Show("Scale, opacity, CRF hoặc số video không hợp lệ.");
            return false;
        }
    }

    private void Save()
    {
        if (_queue is null)
        {
            return;
        }

        _queue.Save(_queue.Capture(
            _videos.Text,
            _frames.Text,
            _backgrounds.Text,
            _output.Text,
            _scale.Text,
            _opacity.Text,
            "",
            _crf.Text,
            _workers.Text,
            _gpu.Checked,
            _random.Checked,
            SelectedOutputHeight()));
    }

    private void AddFolder(TableLayoutPanel panel, int row, string label, TextBox box)
    {
        box.Dock = DockStyle.Fill;
        box.BackColor = Color.FromArgb(45, 45, 48);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        var caption = new Label { Text = label, AutoSize = true, ForeColor = Color.White, Padding = new Padding(0, 6, 0, 0) };
        var browse = Button("Duyệt", (_, _) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (Directory.Exists(box.Text))
            {
                dialog.SelectedPath = box.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                box.Text = dialog.SelectedPath;
                if (box == _videos)
                {
                    ApplyAutoOutput();
                }
            }
        });
        panel.Controls.Add(caption, 0, row);
        panel.Controls.Add(box, 1, row);
        panel.Controls.Add(browse, 2, row);
    }

    private static FlowLayoutPanel Row() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        WrapContents = false,
        BackColor = Color.FromArgb(30, 30, 30),
        Padding = new Padding(0, 8, 0, 0)
    };

    private static void AddNumber(FlowLayoutPanel panel, string label, TextBox box)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Color.White, Padding = new Padding(8, 6, 4, 0) });
        box.BackColor = Color.FromArgb(45, 45, 48);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        panel.Controls.Add(box);
    }

    private static void StyleCheck(CheckBox box)
    {
        box.ForeColor = Color.White;
        box.BackColor = Color.FromArgb(30, 30, 30);
        box.Padding = new Padding(8, 4, 0, 0);
    }

    private static Button Button(string text, EventHandler click, Color? color = null)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = color ?? Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(10, 6, 10, 6)
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoGenerateColumns = false;
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Color.FromArgb(45, 45, 48);
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Color.FromArgb(63, 63, 70);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(62, 62, 66);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private static DataGridViewTextBoxColumn Column(string property, string header, int weight) =>
        new() { DataPropertyName = property, HeaderText = header, FillWeight = weight };

    private static Control Wrap(string title, Control body)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0), BackColor = Color.FromArgb(30, 30, 30) };
        var label = new Label { Text = title, Dock = DockStyle.Top, ForeColor = Color.White, Height = 24 };
        body.Dock = DockStyle.Fill;
        panel.Controls.Add(body);
        panel.Controls.Add(label);
        return panel;
    }
}
