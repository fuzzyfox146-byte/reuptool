using System.Resources;

namespace HdDownloader.Resources;

public static class UiText
{
    private static readonly ResourceManager Manager = new("HdDownloader.Resources.Strings", typeof(UiText).Assembly);

#if HD_VIEW_EDITION
    public static string Title => "Tải video theo view";
    public static string Blurb => "Giống bản tải hiện tại. Bật Theo view: trong khoảng Từ–Đến, video nhiều view nhất ghi số ở ô Số (1 ra 001), rồi giảm dần đến view thấp nhất. Tắt thì vẫn theo thứ tự trên kênh.";
#else
    public static string Title => S(nameof(Title));
    public static string Blurb => S(nameof(Blurb));
#endif
    public static string YtDlp => S(nameof(YtDlp));
    public static string Cookies => S(nameof(Cookies));
    public static string Root => S(nameof(Root));
    public static string Quality => S(nameof(Quality));
    public static string Parallel => S(nameof(Parallel));
    public static string ByViews => S(nameof(ByViews));
    public static string DownloadSubtitles => S(nameof(DownloadSubtitles));
    public static string SubtitleLanguages => S(nameof(SubtitleLanguages));
    public static string SubLangEnJaPt => S(nameof(SubLangEnJaPt));
    public static string SubLangEn => S(nameof(SubLangEn));
    public static string SubLangJa => S(nameof(SubLangJa));
    public static string SubLangPt => S(nameof(SubLangPt));
    public static string SubLangEnJa => S(nameof(SubLangEnJa));
    public static string SubLangJaPt => S(nameof(SubLangJaPt));
    public static string SubLangEnPt => S(nameof(SubLangEnPt));
    public static string SubLangKo => S(nameof(SubLangKo));
    public static string SubLangZh => S(nameof(SubLangZh));
    public static string SubLangEs => S(nameof(SubLangEs));
    public static string SubLangFr => S(nameof(SubLangFr));
    public static string SubLangDe => S(nameof(SubLangDe));
    public static string SubLangVi => S(nameof(SubLangVi));
    public static string SubLangTh => S(nameof(SubLangTh));
    public static string SubLangAll => S(nameof(SubLangAll));
    public static string AddChannel => S(nameof(AddChannel));
    public static string StartAll => S(nameof(StartAll));
    public static string StopAll => S(nameof(StopAll));
    public static string Folder => S(nameof(Folder));
    public static string Link => S(nameof(Link));
    public static string From => S(nameof(From));
    public static string To => S(nameof(To));
    public static string Number => S(nameof(Number));
    public static string Progress => S(nameof(Progress));
    public static string Browse => S(nameof(Browse));
    public static string Start => S(nameof(Start));
    public static string Remove => S(nameof(Remove));
    public static string Log => S(nameof(Log));
    public static string PickYtDlp => S(nameof(PickYtDlp));
    public static string PickCookies => S(nameof(PickCookies));
    public static string PickRoot => S(nameof(PickRoot));
    public static string PickChannel => S(nameof(PickChannel));
    public static string Idle => S(nameof(Idle));
    public static string Busy => S(nameof(Busy));
    public static string Ready => S(nameof(Ready));
    public static string MissingYtDlp => S(nameof(MissingYtDlp));
    public static string MissingCookies => S(nameof(MissingCookies));
    public static string MissingFfmpeg => S(nameof(MissingFfmpeg));
    public static string AlreadyRunning => S(nameof(AlreadyRunning));
    public static string Waiting => S(nameof(Waiting));
    public static string Running => S(nameof(Running));
    public static string Stopped => S(nameof(Stopped));
    public static string Error => S(nameof(Error));
    public static string VideoIdle => S(nameof(VideoIdle));
    public static string ThumbIdle => S(nameof(ThumbIdle));
    public static string FileFilterExe => S(nameof(FileFilterExe));
    public static string FileFilterCookie => S(nameof(FileFilterCookie));

    public static string VideoCount(int count, int total, string number) =>
        total > 0 ? $"Video {count}/{total} · {number}" : $"Video {number}";

    public static string ThumbCount(int count, int total, string number) =>
        total > 0 ? $"Thumb {count}/{total} · {number}" : $"Thumb {number}";

    private static string S(string name) => Manager.GetString(name) ?? name;
}
