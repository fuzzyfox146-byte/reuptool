namespace VideoAutoTool.Core.Download;

public static class CookieFailure
{
    public static bool IsFatal(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.Contains("cookies are no longer valid", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase)
            || line.Contains("not a bot", StringComparison.OrdinalIgnoreCase)
            || line.Contains("HTTP Error 401", StringComparison.OrdinalIgnoreCase)
            || line.Contains("LOGIN_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || line.Contains("account cookies", StringComparison.OrdinalIgnoreCase);
    }
}
