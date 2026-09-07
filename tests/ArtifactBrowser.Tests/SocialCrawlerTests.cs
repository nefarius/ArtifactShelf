using ArtifactBrowser.Features.Files;
using Microsoft.AspNetCore.Http;

namespace ArtifactBrowser.Tests;

public sealed class SocialCrawlerTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)", true)]
    [InlineData("Discordbot/2.0", true)]
    [InlineData("Slackbot-LinkExpanding 1.0 (+https://api.slack.com/robots)", true)]
    [InlineData("Twitterbot/1.0", true)]
    [InlineData("facebookexternalhit/1.1", true)]
    [InlineData("Facebot", true)]
    [InlineData("LinkedInBot/1.0", true)]
    [InlineData("TelegramBot (like TwitterBot)", true)]
    [InlineData("WhatsApp/10.0.0", true)]
    [InlineData("Iframely/1.3.1", true)]
    [InlineData("Mozilla/5.0 embedly", true)]
    [InlineData("Mattermost/7.0", true)]
    [InlineData("Redditbot/1.0", true)]
    [InlineData("Mozilla/5.0 (compatible; SkypeUriPreview/1.0)", true)]
    [InlineData("SteamChatURLLookup/1.0", true)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0", false)]
    [InlineData("curl/8.5.0", false)]
    [InlineData("*", false)]
    [InlineData("", false)]
    public void IsUnfurlBot_ReturnsExpected(string userAgent, bool expected)
    {
        var context = new DefaultHttpContext();
        if (userAgent.Length > 0)
        {
            context.Request.Headers.UserAgent = userAgent;
        }

        Assert.Equal(expected, SocialCrawler.IsUnfurlBot(context.Request));
    }

    [Fact]
    public void IsUnfurlBot_MissingUserAgent_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        Assert.False(SocialCrawler.IsUnfurlBot(context.Request));
    }
}
