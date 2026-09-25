namespace VideoAutoTool.Core.Render;

/// <summary>
/// Deletes a render .part file. A just-exited ffmpeg can keep the handle for a moment;
/// failing the job because that delete throws hides the real encode error.
/// </summary>
public static class PartFile
{
    public static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                File.Delete(path);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 19)
                {
                    return;
                }

                Thread.Sleep(100);
            }
        }
    }
}
