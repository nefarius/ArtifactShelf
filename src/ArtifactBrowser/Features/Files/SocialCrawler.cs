namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Detects well-known link-unfurl crawlers that scrape Open Graph / Twitter Card tags
/// and do not execute Blazor WebAssembly.
/// </summary>
public static class SocialCrawler
{
    private static readonly string[] Tokens =
    [
        "Discordbot",
        "Slackbot",
        "Twitterbot",
        "facebookexternalhit",
        "Facebot",
        "LinkedInBot",
        "TelegramBot",
        "WhatsApp",
        "Iframely",
        "embedly",
        "Mattermost",
        "Redditbot",
        "SkypeUriPreview",
        "SteamChatURLLookup",
    ];

    /// <summary>
    /// True when <c>User-Agent</c> contains a known unfurl-bot token.
    /// A missing or empty header is treated as a regular client (curl, browsers, HttpClient).
    /// </summary>
    public static bool IsUnfurlBot(HttpRequest request)
    {
        var userAgent = request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        foreach (var token in Tokens)
        {
            if (userAgent.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
