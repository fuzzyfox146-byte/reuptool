using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoTool.App.ViewModels;

public sealed partial class IntakeGroupViewModel : ObservableObject
{
    public IntakeGroupViewModel(string id, string sourceName)
    {
        Id = id;
        SourceName = sourceName;
        IsExpanded = true;
        Header = $"{id}  {sourceName}";
    }

    public string Id { get; }

    public string SourceName { get; }

    public ObservableCollection<string> WaitingFiles { get; } = new();

    [ObservableProperty]
    private string _header;

    [ObservableProperty]
    private bool _isExpanded;

    public void Update(IReadOnlyList<string> waiting, int done, int total)
    {
        Header = $"{Id}  {SourceName}    chờ {waiting.Count} · xong {done}/{total}";
        if (waiting.Count == WaitingFiles.Count && waiting.SequenceEqual(WaitingFiles))
        {
            return;
        }

        WaitingFiles.Clear();
        foreach (var file in waiting)
        {
            WaitingFiles.Add(file);
        }
    }
}
