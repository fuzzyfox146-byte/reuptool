namespace YtFolderUploader;

public static class AutoScheduleGate
{
    public static bool IsDue(bool userStarted, bool enabled, bool exactDaily, bool stopped, bool alreadyDone) =>
        userStarted && enabled && exactDaily && !stopped && !alreadyDone;
}
