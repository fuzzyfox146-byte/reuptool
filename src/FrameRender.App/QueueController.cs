using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VideoAutoTool.Core.FrameBatch;
using VideoAutoTool.Core.Render;

namespace FrameRender;

public sealed class QueueController
{
    private readonly FrameBatchEngine _engine;
    private readonly string _cache;
    private readonly object _gate = new();
    private readonly SynchronizationContext _ui;
    private readonly Dictionary<string, CancellationTokenSource> _jobTokens = new(StringComparer.Ordinal);
    private readonly List<string> _intakeOrder = [];
    private CancellationTokenSource? _run;
    private volatile bool _pause;
    private int _active;
    private int _generation;
    private int _workersLimit = 1;
    private int _batchSize;
    private int _intakeSequence;
    private bool _suspendPersist;
    private bool _preserveFile;
    private readonly List<JobRow> _items = [];
    private readonly List<KeptClip> _kept = [];
    private readonly HashSet<string> _keptVideos = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _mapping = new(StringComparer.OrdinalIgnoreCase);

    public QueueController(FrameBatchEngine engine, string cacheDirectory, SynchronizationContext ui)
    {
        _engine = engine;
        _cache = cacheDirectory;
        _ui = ui;
        Jobs = [];
        MainJobs = [];
        WaitingJobs = [];
    }

    public BindingList<JobRow> Jobs { get; }

    public BindingList<JobRow> MainJobs { get; }

    public BindingList<JobRow> WaitingJobs { get; }

    public int BatchSize => _batchSize;

    public DateTime? StartedAt { get; private set; }

    public event Action<string>? Logged;

    public event Action? Changed;

    public event Action? Persist;

    public int WorkerLimit
    {
        get
        {
            lock (_gate)
            {
                return _workersLimit;
            }
        }
    }

    public int SetWorkerLimit(int workers)
    {
        var limit = FrameDesign.ParallelVideos(workers);
        var changed = false;
        lock (_gate)
        {
            if (_workersLimit != limit)
            {
                _workersLimit = limit;
                changed = true;
            }
        }

        if (changed)
        {
            Log($"Số video cùng lúc: {limit}.");
        }

        return limit;
    }

    public int DroppedSkipped { get; private set; }

    public int KeptCount => _kept.Count;

    public IReadOnlyDictionary<string, int> KeptByFolder() =>
        _kept.GroupBy(clip => FrameFolderProgress.Key(clip.IntakeName), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    public bool IsPumping
    {
        get
        {
            var run = _run;
            return run is not null && !run.IsCancellationRequested;
        }
    }

    public static string QueuePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        FrameEdition.DataFolder,
        "queue.json");

    public void Enqueue(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> frames,
        IReadOnlyList<string> backgrounds,
        string outputDirectory,
        bool random,
        int batchSize,
        string sourceFolder)
    {
        var fullSource = Path.GetFullPath(sourceFolder);
        var sameSource = _items.FirstOrDefault(row => string.Equals(row.SourceFolder, fullSource, StringComparison.OrdinalIgnoreCase));
        var intakeName = sameSource?.IntakeName ?? UniqueName(FramePlan.SourceLabel(fullSource));
        Directory.CreateDirectory(outputDirectory);
        var keptSource = _kept.FirstOrDefault(clip => string.Equals(clip.SourceFolder, fullSource, StringComparison.OrdinalIgnoreCase));
        if (sameSource is null && keptSource is not null)
        {
            intakeName = keptSource.IntakeName;
        }

        var previous = _items
            .Where(row => string.Equals(row.SourceFolder, fullSource, StringComparison.OrdinalIgnoreCase))
            .Select(row => new FramePair(row.VideoPath, row.FramePath, row.BackgroundPath, Path.GetFileName(row.OutputPath)))
            .Concat(_kept
                .Where(clip => string.Equals(clip.SourceFolder, fullSource, StringComparison.OrdinalIgnoreCase))
                .Select(clip => new FramePair(clip.VideoPath, clip.FramePath, clip.BackgroundPath, Path.GetFileName(clip.OutputPath))))
            .ToList();
        var seed = FramePlan.StableSeed(frames, backgrounds);
        var plan = FramePlan.Assign(videos, frames, backgrounds, random, seed, previous);
        var known = _items.Select(row => row.VideoPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fresh = new List<JobRow>();
        var added = new List<JobRow>();
        var rememberedNow = 0;
        var id = sameSource?.IntakeId ?? keptSource?.IntakeId ?? "";
        lock (_gate)
        {
            if (_batchSize == 0)
            {
                _batchSize = FrameIntake.ClampBatch(batchSize);
            }

            foreach (var item in plan)
            {
                if (!known.Add(item.Video))
                {
                    continue;
                }

                var output = AdoptExisting(outputDirectory, item.OutputName);
                var exists = NonEmpty(output);
                if (exists)
                {
                    if (string.IsNullOrEmpty(id))
                    {
                        id = (++_intakeSequence).ToString("D2");
                        _intakeOrder.Add(id);
                    }

                    if (Remember(new KeptClip
                    {
                        IntakeId = id,
                        IntakeName = intakeName,
                        SourceFolder = fullSource,
                        VideoPath = item.Video,
                        FramePath = item.Frame,
                        BackgroundPath = item.Background,
                        OutputPath = output
                    }))
                    {
                        rememberedNow++;
                    }

                    continue;
                }

                Forget(item.Video);
                var row = new JobRow
                {
                    Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Video)))[..16],
                    IntakeName = intakeName,
                    SourceFolder = fullSource,
                    VideoPath = item.Video,
                    FramePath = item.Frame,
                    BackgroundPath = item.Background,
                    OutputPath = output,
                    Promoted = false,
                    State = FrameDesign.Wait,
                    Status = FrameDesign.Wait
                };
                added.Add(row);
                fresh.Add(row);
            }

            if (added.Count > 0)
            {
                if (string.IsNullOrEmpty(id))
                {
                    id = (++_intakeSequence).ToString("D2");
                    _intakeOrder.Add(id);
                }

                foreach (var row in added)
                {
                    row.IntakeId = id;
                    _items.Add(row);
                }

                FrameIntake.PromoteInitial(fresh, _batchSize);
            }
        }

        if (added.Count == 0)
        {
            Log(rememberedNow == 0
                ? $"Folder {intakeName} không có video mới."
                : $"Folder {intakeName}: {rememberedNow} video đã có, không vào hàng.");
            Changed?.Invoke();
            RequestPersist();
            return;
        }

        foreach (var row in added)
        {
            Jobs.Add(row);
            Place(row);
        }

        Log($"Đã thêm nguồn {added[0].IntakeId} · {intakeName}: {fresh.Count} video cần làm. {rememberedNow} video đã có, không vào hàng. Hàng chính nhận tối đa {_batchSize} video mỗi folder.");
        Changed?.Invoke();
        RequestPersist();
    }

    public void Start(FrameSettings settings, IReadOnlyList<string> frames, IReadOnlyList<string> backgrounds, string outputDirectory)
    {
        if (!Jobs.Any(row => row.State == FrameDesign.Wait))
        {
            Log("Không còn video đang chờ.");
            return;
        }

        _pause = false;
        _run?.Cancel();
        _run = new CancellationTokenSource();
        var token = _run.Token;
        var generation = ++_generation;
        StartedAt = DateTime.Now;
        var workers = FrameDesign.ParallelVideos(settings.Workers);
        lock (_gate)
        {
            _workersLimit = workers;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                if (settings.Gpu && !await _engine.NvencAvailableAsync(token).ConfigureAwait(false))
                {
                    Log("Không dùng được NVENC. Chuyển sang libx264.");
                    settings = settings with { Gpu = false };
                }

                if (settings.Gpu)
                {
                    Log("NVENC: giải mã CUDA, scale GPU, encode GPU. Độ mờ video vẫn giữ trong công thức.");
                }

                Log("Đang làm cache, xong mới render.");
                var mapping = await _engine.PrepareCacheAsync(frames, backgrounds, _cache, settings, _workersLimit, token).ConfigureAwait(false);
                if (generation != _generation)
                {
                    return;
                }

                _mapping = new Dictionary<string, string>(mapping, StringComparer.OrdinalIgnoreCase);
                Log($"Cache xong. Cùng lúc {_workersLimit} video. Mỗi folder {_batchSize} video, rồi sang folder kế.");
                await PumpAsync(settings, outputDirectory, generation, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log("Đã hủy.");
            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }, token);
    }

    public void Pause()
    {
        CancellationTokenSource[] tokens;
        lock (_gate)
        {
            _pause = true;
            tokens = _jobTokens.Values.ToArray();
        }

        foreach (var token in tokens)
        {
            try
            {
                token.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // That encode already finished.
            }
        }

        Log("Tạm dừng. Video đang chạy sẽ render lại khi bấm Tiếp tục.");
        RequestPersist();
    }

    public void Resume()
    {
        var changed = 0;
        lock (_gate)
        {
            _pause = false;
            foreach (var job in _items)
            {
                if (job.State != FrameDesign.Paused)
                {
                    continue;
                }

                job.State = FrameDesign.Wait;
                job.Promoted = true;
                job.Detail = "";
                Publish(job);
                changed++;
            }
        }

        if (changed == 0)
        {
            return;
        }

        Log("Tiếp tục hàng đợi.");
        RequestPersist();
    }

    public void Cancel(bool keepNotice = true)
    {
        _generation++;
        _run?.Cancel();
        _pause = false;
        CancellationTokenSource[] tokens;
        lock (_gate)
        {
            foreach (var job in _items)
            {
                if (job.State is FrameDesign.Running or FrameDesign.Paused or FrameDesign.Wait)
                {
                    job.State = FrameDesign.Cancelled;
                    job.Detail = "Đã hủy.";
                    Publish(job);
                }
            }

            tokens = _jobTokens.Values.ToArray();
        }

        foreach (var token in tokens)
        {
            try
            {
                token.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // That encode already finished.
            }
        }

        if (keepNotice)
        {
            Log("Đã hủy các video đang chạy và đang chờ. Hàng đợi vẫn giữ đến khi bấm Xóa hàng đợi.");
            RequestPersist();
        }
    }

    public int RenderAgain()
    {
        var changed = 0;
        foreach (var job in _items.ToList())
        {
            if (job.State is FrameDesign.Failed or FrameDesign.Cancelled)
            {
                job.State = FrameDesign.Wait;
                job.Detail = "";
                Publish(job);
                changed++;
            }
        }

        Log($"Đưa {changed} video lỗi hoặc đã hủy về chờ.");
        if (changed > 0)
        {
            RequestPersist();
        }

        return changed;
    }

    public void StopForClose()
    {
        _generation++;
        _run?.Cancel();
        lock (_gate)
        {
            foreach (var job in _items)
            {
                if (job.State == FrameDesign.Running)
                {
                    job.State = FrameDesign.Wait;
                    job.Status = FrameDesign.Wait;
                    job.Detail = "";
                    job.Promoted = true;
                }
            }
        }
    }

    public void Clear()
    {
        _suspendPersist = true;
        _preserveFile = false;
        try
        {
            Cancel(keepNotice: false);
        }
        finally
        {
            _suspendPersist = false;
        }

        lock (_gate)
        {
            _items.Clear();
            _kept.Clear();
            _keptVideos.Clear();
            _batchSize = 0;
            _intakeSequence = 0;
            _intakeOrder.Clear();
        }

        Jobs.Clear();
        MainJobs.Clear();
        WaitingJobs.Clear();
        Changed?.Invoke();
        Log("Đã xóa hàng đợi.");
    }

    public QueueSnapshot Capture(string videos, string frames, string backgrounds, string output, string scale, string opacity, string noise, string crf, string workers, bool gpu, bool random) =>
        new(videos, frames, backgrounds, output, scale, opacity, noise, crf, workers, gpu, random, _items.ToList(), _batchSize, _intakeSequence, _intakeOrder.ToList(), _kept.ToList());

    public void Save(QueueSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(QueuePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (_preserveFile && snapshot.Jobs.Count == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        var temp = QueuePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, QueuePath, overwrite: true);
        _preserveFile = false;
    }

    public QueueSnapshot? Load()
    {
        if (!File.Exists(QueuePath))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<QueueSnapshot>(File.ReadAllText(QueuePath));
            if (snapshot?.Jobs is null)
            {
                _preserveFile = true;
                return null;
            }

            Jobs.Clear();
            MainJobs.Clear();
            WaitingJobs.Clear();
            lock (_gate)
            {
                _items.Clear();
                _kept.Clear();
                _keptVideos.Clear();
                _intakeOrder.Clear();
                _batchSize = snapshot.BatchSize;
                _intakeSequence = snapshot.IntakeSequence;
                if (snapshot.IntakeOrder is not null)
                {
                    _intakeOrder.AddRange(snapshot.IntakeOrder);
                }
            }

            if (snapshot.Kept is not null)
            {
                foreach (var clip in snapshot.Kept)
                {
                    Remember(clip);
                }
            }

            DroppedSkipped = 0;
            foreach (var job in snapshot.Jobs)
            {
                var status = FrameLayoutMath.NormalizeStatus(job.State);
                job.State = status;
                job.Status = status;
                if (string.IsNullOrEmpty(job.IntakeId))
                {
                    job.IntakeId = "01";
                    job.Promoted = true;
                }

                if (job.State == FrameDesign.Skipped)
                {
                    Remember(ToKept(job));
                    DroppedSkipped++;
                    continue;
                }

                lock (_gate)
                {
                    _items.Add(job);
                    if (!_intakeOrder.Contains(job.IntakeId))
                    {
                        _intakeOrder.Add(job.IntakeId);
                    }
                }

                Jobs.Add(job);
                Place(job);
            }

            PromoteWaitingIntoMain();
            return snapshot;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            _preserveFile = true;
            try
            {
                File.Copy(QueuePath, QueuePath + ".bak", overwrite: true);
            }
            catch (IOException)
            {
                // The original file is left in place.
            }

            return null;
        }
    }

    private async Task PumpAsync(FrameSettings settings, string outputDirectory, int generation, CancellationToken token)
    {
        while (!token.IsCancellationRequested && generation == _generation)
        {
            if (_pause)
            {
                await Task.Delay(200, token).ConfigureAwait(false);
                continue;
            }

            JobRow? job = null;
            var entered = new List<JobRow>();
            lock (_gate)
            {
                if (_active < _workersLimit)
                {
                    entered.AddRange(PromoteWave());
                    job = _items.FirstOrDefault(row => row.Promoted && row.State == FrameDesign.Wait);
                    if (job is not null)
                    {
                        _active++;
                        job.State = FrameDesign.Running;
                        job.Fraction = 0;
                        job.StartedUtc = DateTime.UtcNow;
                        job.FinishedUtc = null;
                        entered.AddRange(PromoteWave());
                    }
                }
            }

            if (entered.Count > 0)
            {
                var moved = entered;
                _ui.Post(_ =>
                {
                    foreach (var row in moved)
                    {
                        Place(row);
                    }

                    Changed?.Invoke();
                }, null);
            }

            if (job is null)
            {
                var open = false;
                lock (_gate)
                {
                    open = _active > 0 || _items.Any(row => row.State is FrameDesign.Wait or FrameDesign.Running or FrameDesign.Paused);
                }

                if (!open)
                {
                    Log("Hàng đợi đã chạy xong.");
                    _ui.Post(_ => Changed?.Invoke(), null);
                    break;
                }

                await Task.Delay(200, token).ConfigureAwait(false);
                continue;
            }

            Publish(job);
            _ = Task.Run(() => WorkAsync(job, settings, outputDirectory, generation, token));
        }
    }

    private List<JobRow> PromoteWave()
    {
        var before = _items.Where(row => row.Promoted).ToHashSet();
        FrameIntake.PromoteNextWave(_items, _intakeOrder, _batchSize);
        return _items.Where(row => row.Promoted && !before.Contains(row)).ToList();
    }

    private async Task WorkAsync(JobRow job, FrameSettings settings, string outputDirectory, int generation, CancellationToken token)
    {
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        lock (_gate)
        {
            if (generation != _generation || token.IsCancellationRequested)
            {
                Finish(job, generation, FrameDesign.Cancelled, "Đã hủy.");
                return;
            }

            if (_pause)
            {
                Finish(job, generation, FrameDesign.Paused, "Tạm dừng.");
                return;
            }

            _jobTokens[job.Id] = jobCts;
        }

        try
        {
            var background = Map(job.BackgroundPath);
            var frame = Map(job.FramePath);
            if (NonEmpty(job.OutputPath))
            {
                KeepFinished(job);
                return;
            }

            var lastTick = 0L;
            var progress = new Progress<double>(value =>
            {
                var now = Environment.TickCount64;
                if (now - lastTick < 300 && value < 1)
                {
                    return;
                }

                lastTick = now;
                job.Fraction = value;
                var label = $"{FrameDesign.Running} {(int)Math.Round(value * 100)}%";
                _ui.Post(_ =>
                {
                    job.Status = label;
                    Changed?.Invoke();
                }, null);
            });
            await _engine.RenderAsync(job.VideoPath, background, frame, job.OutputPath, settings, progress, jobCts.Token, Log).ConfigureAwait(false);
            Finish(job, generation, FrameDesign.Done, "");
        }
        catch (OperationCanceledException)
        {
            if (_pause && !token.IsCancellationRequested && generation == _generation)
            {
                Finish(job, generation, FrameDesign.Paused, "Tạm dừng.");
            }
            else
            {
                Finish(job, generation, FrameDesign.Cancelled, "Đã hủy.");
            }
        }
        catch (Exception ex)
        {
            if (TryRequeue(job, generation, ex))
            {
                return;
            }

            var log = FrameEdition.ErrorLog(job.OutputPath);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.WriteAllText(log, ex.Message);
            }
            catch (IOException)
            {
                // The grid still shows the error.
            }

            Finish(job, generation, FrameDesign.Failed, ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                _jobTokens.Remove(job.Id);
            }
        }
    }

    private bool TryRequeue(JobRow job, int generation, Exception ex)
    {
        if (!FramePressure.IsRecoverable(ex))
        {
            return false;
        }

        lock (_gate)
        {
            if (_workersLimit <= 1)
            {
                return false;
            }

            var previous = _workersLimit;
            _workersLimit--;
            _active = Math.Max(0, _active - 1);
            if (generation == _generation && job.State == FrameDesign.Running)
            {
                job.State = FrameDesign.Wait;
                job.Promoted = true;
                job.Detail = "";
                Publish(job);
            }

            Log($"Hết chỗ hoặc file đang bị khóa khi render {previous} video cùng lúc. Chuyển còn {_workersLimit} video và render lại.");
        }

        RequestPersist();
        return true;
    }

    private void Finish(JobRow job, int generation, string status, string detail)
    {
        var changed = false;
        lock (_gate)
        {
            _active = Math.Max(0, _active - 1);
            if (generation == _generation && job.State == FrameDesign.Running)
            {
                job.State = status;
                job.FinishedUtc = DateTime.UtcNow;
                if (status == FrameDesign.Done)
                {
                    job.Fraction = 1;
                }

                job.Detail = detail.Length > 2000 ? detail[..2000] : detail;
                Publish(job);
                changed = true;
            }
        }

        if (changed)
        {
            RequestPersist();
        }
    }

    private string Map(string source) =>
        _mapping.TryGetValue(Path.GetFullPath(source), out var cached) ? cached : source;

    private static string DisplayStatus(JobRow job)
    {
        if (job.State == FrameDesign.Running)
        {
            return $"{FrameDesign.Running} {(int)Math.Round(job.Fraction * 100)}%";
        }

        if (job.State == FrameDesign.Done &&
            RenderTiming.Elapsed(job.StartedUtc, job.FinishedUtc) is { } took)
        {
            return $"Xong · {RenderTiming.FormatElapsed(took)}";
        }

        return job.State;
    }

    private void PromoteWaitingIntoMain()
    {
        List<JobRow> entered;
        lock (_gate)
        {
            entered = PromoteWave();
        }

        foreach (var row in entered)
        {
            Place(row);
        }
    }

    private void Place(JobRow job)
    {
        if (job.State == FrameDesign.Skipped)
        {
            Remove(MainJobs, job);
            Remove(WaitingJobs, job);
            return;
        }

        if (!job.Promoted && job.Status == FrameDesign.Wait)
        {
            Remove(MainJobs, job);
            Add(WaitingJobs, job);
            return;
        }

        Remove(WaitingJobs, job);
        Add(MainJobs, job);
    }

    private static void Add(BindingList<JobRow> list, JobRow job)
    {
        if (!list.Contains(job))
        {
            list.Add(job);
        }
    }

    private static void Remove(BindingList<JobRow> list, JobRow job)
    {
        if (list.Contains(job))
        {
            list.Remove(job);
        }
    }

    private static string AdoptExisting(string outputDirectory, string outputName)
    {
        var fileName = Path.GetFileName(outputName);
        var flat = Path.Combine(outputDirectory, fileName);
        if (NonEmpty(flat) || !Directory.Exists(outputDirectory))
        {
            return flat;
        }

        foreach (var dir in Directory.EnumerateDirectories(outputDirectory))
        {
            var nested = Path.Combine(dir, fileName);
            if (!NonEmpty(nested))
            {
                continue;
            }

            try
            {
                File.Move(nested, flat);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
            catch (IOException)
            {
                return nested;
            }
            catch (UnauthorizedAccessException)
            {
                return nested;
            }

            return flat;
        }

        return flat;
    }

    private static bool NonEmpty(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

    private bool Remember(KeptClip clip)
    {
        if (string.IsNullOrWhiteSpace(clip.VideoPath) || !_keptVideos.Add(clip.VideoPath))
        {
            return false;
        }

        _kept.Add(clip);
        return true;
    }

    private void Forget(string videoPath)
    {
        if (!_keptVideos.Remove(videoPath))
        {
            return;
        }

        _kept.RemoveAll(clip => string.Equals(clip.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase));
    }

    private void KeepFinished(JobRow job)
    {
        lock (_gate)
        {
            _active = Math.Max(0, _active - 1);
            _items.Remove(job);
            Remember(ToKept(job));
        }

        _ui.Post(_ =>
        {
            Jobs.Remove(job);
            MainJobs.Remove(job);
            WaitingJobs.Remove(job);
            Changed?.Invoke();
        }, null);
        RequestPersist();
    }

    private static KeptClip ToKept(JobRow job) => new()
    {
        IntakeId = job.IntakeId,
        IntakeName = job.IntakeName,
        SourceFolder = job.SourceFolder,
        VideoPath = job.VideoPath,
        FramePath = job.FramePath,
        BackgroundPath = job.BackgroundPath,
        OutputPath = job.OutputPath
    };

    private string UniqueName(string name)
    {
        var used = _items.Select(row => row.IntakeName)
            .Concat(_kept.Select(clip => clip.IntakeName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (used.Add(name))
        {
            return name;
        }

        var suffix = 2;
        while (!used.Add(name + "-" + suffix))
        {
            suffix++;
        }

        return name + "-" + suffix;
    }

    private void Publish(JobRow job)
    {
        var status = DisplayStatus(job);
        var detail = job.Detail;
        var promoted = job.Promoted;
        _ui.Post(_ =>
        {
            job.Promoted = promoted;
            job.Status = status;
            job.Detail = detail;
            Place(job);
            Changed?.Invoke();
        }, null);
    }

    private void RequestPersist()
    {
        if (_suspendPersist)
        {
            return;
        }

        if (SynchronizationContext.Current == _ui)
        {
            Persist?.Invoke();
            return;
        }

        _ui.Post(_ =>
        {
            if (_suspendPersist)
            {
                return;
            }

            Persist?.Invoke();
        }, null);
    }

    private void Log(string message) => _ui.Post(_ => Logged?.Invoke(message), null);
}

public sealed record QueueSnapshot(
    string Videos,
    string Frames,
    string Backgrounds,
    string Output,
    string Scale,
    string Opacity,
    string Noise,
    string Crf,
    string Workers,
    bool Gpu,
    bool Random,
    List<JobRow> Jobs,
    int BatchSize = 0,
    int IntakeSequence = 0,
    List<string>? IntakeOrder = null,
    List<KeptClip>? Kept = null);

public sealed class KeptClip
{
    public string IntakeId { get; set; } = "";

    public string IntakeName { get; set; } = "";

    public string SourceFolder { get; set; } = "";

    public string VideoPath { get; set; } = "";

    public string FramePath { get; set; } = "";

    public string BackgroundPath { get; set; } = "";

    public string OutputPath { get; set; } = "";
}
