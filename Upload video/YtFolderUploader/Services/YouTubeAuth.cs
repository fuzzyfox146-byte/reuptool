using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

namespace YtFolderUploader;

public static class YouTubeAuth
{
    public static readonly string[] Scopes =
    [
        YouTubeService.Scope.YoutubeUpload,
        YouTubeService.Scope.YoutubeForceSsl
    ];

    public static async Task<UserCredential> AuthorizeInteractiveAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        var flow = new GoogleAuthorizationCodeFlow(CreateInitializer(clientId, clientSecret, promptConsent: true));
        var app = new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver());
        return await app.AuthorizeAsync("user", cancellationToken);
    }

    public static YouTubeService CreateService(ChannelProfile channel)
    {
        if (string.IsNullOrWhiteSpace(channel.ClientId) || string.IsNullOrWhiteSpace(channel.ClientSecret))
            throw new InvalidOperationException("Kênh chưa có client_id và client_secret. Hãy dán JSON OAuth Desktop.");
        if (string.IsNullOrWhiteSpace(channel.RefreshToken))
            throw new InvalidOperationException("Kênh chưa có refresh token. Hãy bấm Ủy quyền kênh này.");

        var credential = new UserCredential(
            new GoogleAuthorizationCodeFlow(CreateInitializer(channel.ClientId, channel.ClientSecret, promptConsent: false)),
            string.IsNullOrWhiteSpace(channel.YoutubeChannelId) ? channel.Id : channel.YoutubeChannelId,
            new TokenResponse { RefreshToken = channel.RefreshToken });

        var service = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "YtFolderUploader"
        });

        try
        {
            service.HttpClient.Timeout = TimeSpan.FromMinutes(30);
        }
        catch (InvalidOperationException)
        {
            // The client starts on first use. Timeout can only be changed before that.
        }

        return service;
    }

    public static async Task<ChannelIdentity> GetMyChannelAsync(YouTubeService service, CancellationToken cancellationToken)
    {
        var request = service.Channels.List("snippet");
        request.Mine = true;
        var response = await request.ExecuteAsync(cancellationToken);
        var channel = response.Items?.FirstOrDefault();
        if (channel == null || string.IsNullOrWhiteSpace(channel.Id))
            throw new InvalidOperationException("Tài khoản Google này chưa có kênh YouTube.");

        return new ChannelIdentity(channel.Id, channel.Snippet?.Title ?? channel.Id);
    }

    public static async Task<List<CategoryChoice>> LoadCategoriesAsync(
        YouTubeService service,
        string regionCode,
        CancellationToken cancellationToken)
    {
        var request = service.VideoCategories.List("snippet");
        request.RegionCode = string.IsNullOrWhiteSpace(regionCode) ? "JP" : regionCode;
        var response = await request.ExecuteAsync(cancellationToken);
        var list = new List<CategoryChoice>();
        if (response.Items == null)
            return list;

        foreach (var item in response.Items)
        {
            if (item.Snippet?.Assignable != true || string.IsNullOrWhiteSpace(item.Id))
                continue;
            list.Add(new CategoryChoice
            {
                Id = item.Id,
                Title = item.Snippet.Title ?? item.Id
            });
        }

        return list;
    }

    static GoogleAuthorizationCodeFlow.Initializer CreateInitializer(string clientId, string clientSecret, bool promptConsent)
    {
        return new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            },
            Scopes = Scopes,
            Prompt = promptConsent ? "consent" : null
        };
    }
}
