namespace VideoAutoTool.Core.Scanning;

public sealed record ScannedFile(string AbsolutePath, string RelativePath, int? Number, string FileName);
