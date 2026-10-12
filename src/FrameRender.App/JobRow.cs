using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoAutoTool.Core.FrameBatch;

namespace FrameRender;

public sealed class JobRow : INotifyPropertyChanged, IFrameQueueSlot
{
    public string Id { get; init; } = "";

    public string IntakeId { get; set; } = "";

    public string IntakeName { get; set; } = "";

    public string SourceFolder { get; set; } = "";

    public bool Promoted { get; set; }

    public string VideoPath { get; init; } = "";

    public string FramePath { get; init; } = "";

    public string BackgroundPath { get; init; } = "";

    public string OutputPath { get; set; } = "";

    public string State { get; set; } = VideoAutoTool.Core.FrameBatch.FrameDesign.Wait;

    public string Detail { get; set; } = "";

    public double Fraction { get; set; }

    public DateTime? StartedUtc { get; set; }

    public DateTime? FinishedUtc { get; set; }

    private string _source = "";

    public string Source
    {
        get => string.IsNullOrEmpty(_source) ? IntakeName : _source;
        set
        {
            if (_source == value)
            {
                return;
            }

            _source = value;
            OnPropertyChanged();
        }
    }

    string IFrameQueueSlot.Status => State;

    public string Video => Path.GetFileName(VideoPath);

    public string Frame => Path.GetFileName(FramePath);

    public string Background => Path.GetFileName(BackgroundPath);

    public string Output => Path.GetFileName(OutputPath);

    private string _status = VideoAutoTool.Core.FrameBatch.FrameDesign.Wait;

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
