namespace VideoAutoTool.Core.Subtitles;

public sealed record Cue(TimeSpan Start, TimeSpan End, string Text);
