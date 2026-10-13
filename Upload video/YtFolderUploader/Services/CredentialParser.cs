using System.Text.Json;
using System.Text.RegularExpressions;

namespace YtFolderUploader;

public static partial class CredentialParser
{
    public static ParsedCredential Parse(string? text)
    {
        var result = new ParsedCredential();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var json in ExtractJsonObjects(text))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                Read(doc.RootElement, result);
            }
            catch (JsonException)
            {
                // Ignore fragments that are not JSON. Line patterns below still apply.
            }
        }

        foreach (Match match in PairRegex().Matches(text))
        {
            Assign(result, NormalizeKey(match.Groups["k"].Value), match.Groups["v"].Value.Trim().Trim('"').Trim('\''), overwrite: false);
        }

        return result;
    }

    public static string ToPortableJson(ParsedCredential credential, string? channelId, string? channelTitle)
    {
        var payload = new Dictionary<string, string?>
        {
            ["client_id"] = credential.ClientId,
            ["client_secret"] = credential.ClientSecret,
            ["refresh_token"] = credential.RefreshToken,
            ["channel_id"] = channelId,
            ["channel_title"] = channelTitle
        };

        return JsonSerializer.Serialize(payload, SettingsStore.JsonOptions);
    }

    static void Read(JsonElement element, ParsedCredential result)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in element.EnumerateObject())
        {
            var key = NormalizeKey(property.Name);
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                Assign(result, key, property.Value.GetString() ?? "", overwrite: true);
            }
            else if (property.Value.ValueKind == JsonValueKind.Object
                && key is "installed" or "web" or "authorizeduser" or "token")
            {
                Read(property.Value, result);
            }
        }
    }

    static void Assign(ParsedCredential result, string key, string value, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        switch (key)
        {
            case "clientid":
                if (overwrite || string.IsNullOrEmpty(result.ClientId))
                    result.ClientId = value;
                break;
            case "clientsecret":
                if (overwrite || string.IsNullOrEmpty(result.ClientSecret))
                    result.ClientSecret = value;
                break;
            case "refreshtoken":
                if (overwrite || string.IsNullOrEmpty(result.RefreshToken))
                    result.RefreshToken = value;
                break;
            case "channelid":
                if (overwrite || string.IsNullOrEmpty(result.ChannelId))
                    result.ChannelId = value;
                break;
            case "channeltitle":
                if (overwrite || string.IsNullOrEmpty(result.ChannelTitle))
                    result.ChannelTitle = value;
                break;
        }
    }

    static string NormalizeKey(string key)
    {
        var chars = key.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    static IEnumerable<string> ExtractJsonObjects(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{')
                continue;

            var depth = 0;
            var inString = false;
            var escape = false;
            for (var j = i; j < text.Length; j++)
            {
                var c = text[j];
                if (inString)
                {
                    if (escape)
                        escape = false;
                    else if (c == '\\')
                        escape = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        yield return text.Substring(i, j - i + 1);
                        i = j;
                        break;
                    }
                }
            }
        }
    }

    [GeneratedRegex(@"(?im)(?:^|[\s,{])(?<k>client[\s_\-]*id|client[\s_\-]*secret|refresh[\s_\-]*token)\s*[:=]\s*[""']?(?<v>[^\s""',}\r\n]+)", RegexOptions.CultureInvariant)]
    private static partial Regex PairRegex();
}
