using Google;

namespace YtFolderUploader;

public static class ApiErrors
{
    public static bool IsDailyQuota(Exception exception)
    {
        var reason = Reason(exception);
        if (reason is "quotaExceeded" or "uploadLimitExceeded" or "dailyLimitExceeded")
            return true;

        var text = exception.ToString();
        return text.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase)
            || text.Contains("uploadLimitExceeded", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBadThumbFile(Exception exception)
    {
        if (exception is FileNotFoundException)
            return true;

        var reason = Reason(exception);
        return reason is "invalidImage" or "invalidImageSize" or "mediaBodyRequired";
    }

    public static bool IsAuthFailure(Exception exception)
    {
        var reason = Reason(exception);
        return reason is "forbidden" or "insufficientPermissions" or "unauthorized" or "authError";
    }

    public static bool ShouldDeferAfterThumbFailure(Exception exception)
    {
        if (exception is OperationCanceledException)
            return false;
        if (IsBadThumbFile(exception))
            return false;
        if (IsAuthFailure(exception))
            return false;
        return true;
    }

    public static bool IsTransientThumbnail(Exception exception)
    {
        var reason = Reason(exception);
        return reason is not ("invalidImage" or "invalidImageSize" or "mediaBodyRequired");
    }

    public static string? Reason(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is GoogleApiException api)
            {
                var reason = api.Error?.Errors?.FirstOrDefault()?.Reason;
                if (!string.IsNullOrWhiteSpace(reason))
                    return reason;
            }
        }

        return null;
    }

    public static string Explain(Exception exception)
    {
        if (exception is OperationCanceledException)
            return "Đã dừng.";

        var reason = Reason(exception);
        var message = ExtractMessage(exception);
        var friendly = reason switch
        {
            "invalidPublishAt" => "Giờ hẹn không hợp lệ. Video hẹn giờ phải ở trạng thái riêng tư và giờ công khai phải nằm trong tương lai.",
            "invalidTitle" => "Tiêu đề không hợp lệ. YouTube không nhận tiêu đề trống và giới hạn 100 ký tự.",
            "invalidDescription" => "Mô tả không hợp lệ hoặc dài quá 5000 ký tự.",
            "invalidTags" => "Thẻ không hợp lệ hoặc tổng thẻ dài quá 500 ký tự.",
            "invalidCategoryId" => "Thể loại không dùng được với quốc gia đang chọn. Hãy tải lại danh sách thể loại.",
            "quotaExceeded" or "uploadLimitExceeded" or "dailyLimitExceeded" => "Project Google Cloud đã hết hạn mức upload trong hôm nay.",
            "rateLimitExceeded" or "userRateLimitExceeded" => "YouTube đang giới hạn tần suất. Hãy đợi rồi đăng tiếp.",
            "uploadRateLimitExceeded" => "Kênh vừa gắn quá nhiều thumbnail. Hãy thử lại sau.",
            "forbidden" => "YouTube từ chối quyền này. Thumbnail tùy chỉnh cần kênh đã xác minh số điện thoại, và token cần scope youtube.force-ssl.",
            "insufficientPermissions" or "unauthorized" or "authError" => "Token không còn quyền. Hãy bấm Ủy quyền kênh này lần nữa.",
            "videoNotFound" => "YouTube chưa nhận ra video vừa upload. Hãy gắn lại thumbnail sau ít phút.",
            _ => null
        };

        if (friendly == null)
            return Trim(message);

        if (string.IsNullOrWhiteSpace(reason))
            return friendly;

        return $"{friendly} ({reason}: {Trim(message)})";
    }

    static string ExtractMessage(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is GoogleApiException api && !string.IsNullOrWhiteSpace(api.Error?.Message))
                return api.Error.Message;
        }

        return exception.Message;
    }

    static string Trim(string message)
    {
        var compact = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 300 ? compact : compact[..300];
    }
}
