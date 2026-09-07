using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace ArtifactBrowser.Tests;

public sealed class SocialEmbedTests : IDisposable
{
    private readonly TempContentRoot _root = new();
    private readonly SocialEmbedService _service;

    public SocialEmbedTests()
    {
        _service = new SocialEmbedService(new PathGuard(_root.CreateOptions()), _root.CreateOptions());
    }

    [Fact]
    public void DescribeFolder_Root_UsesSiteBranding()
    {
        var embed = _service.DescribeFolder(CreateRequest("/"));

        Assert.Equal("Artifact Browser", embed.Title);
        Assert.Equal("Artifact Browser", embed.DocumentTitle);
        Assert.Equal("Artifact Browser | /", embed.Description);
        Assert.Equal("https://artifacts.example.com/", embed.Url);
        Assert.Null(embed.ImageUrl);
        Assert.Equal("summary", embed.TwitterCard);
    }

    [Fact]
    public void DescribeFolder_ExistingDirectory_UsesFolderName()
    {
        var embed = _service.DescribeFolder(CreateRequest("/docs"));

        Assert.Equal("docs", embed.Title);
        Assert.Equal("docs — Artifact Browser", embed.DocumentTitle);
        Assert.Equal("Artifact Browser | /docs", embed.Description);
        Assert.Equal("https://artifacts.example.com/docs", embed.Url);
        Assert.Null(embed.ImageUrl);
    }

    [Fact]
    public void DescribeFolder_MissingPath_FallsBackToBranding()
    {
        var embed = _service.DescribeFolder(CreateRequest("/does-not-exist"));

        Assert.Equal("Artifact Browser", embed.Title);
        Assert.Equal("Artifact Browser", embed.DocumentTitle);
        Assert.Equal("Artifact Browser", embed.Description);
    }

    [Fact]
    public void DescribeFolder_ReservedPath_FallsBackToBranding()
    {
        var embed = _service.DescribeFolder(CreateRequest("/health"));

        Assert.Equal("Artifact Browser", embed.Title);
        Assert.Equal("Artifact Browser", embed.Description);
    }

    [Fact]
    public void DescribeFolder_FilePath_FallsBackToBranding()
    {
        var embed = _service.DescribeFolder(CreateRequest("/docs/notes.txt"));

        Assert.Equal("Artifact Browser", embed.Title);
        Assert.DoesNotContain("notes.txt", embed.Title);
    }

    [Fact]
    public void TryDescribeFile_ExistingFile_UsesMetadata()
    {
        var embed = _service.TryDescribeFile("docs/notes.txt", CreateRequest("/docs/notes.txt"));

        Assert.NotNull(embed);
        Assert.Equal("notes.txt", embed!.Title);
        Assert.Equal("notes.txt — Artifact Browser", embed.DocumentTitle);
        Assert.Contains("Code", embed.Description);
        Assert.Contains("12 B", embed.Description);
        Assert.Contains("Modified", embed.Description);
        Assert.Contains("UTC", embed.Description);
        Assert.Equal("https://artifacts.example.com/docs/notes.txt", embed.Url);
        Assert.Null(embed.ImageUrl);
    }

    [Fact]
    public void TryDescribeFile_Image_IncludesThumbnailUrl()
    {
        File.WriteAllBytes(Path.Combine(_root.ContentRoot, "docs", "logo.png"), [0x89, 0x50, 0x4E, 0x47]);

        var embed = _service.TryDescribeFile("docs/logo.png", CreateRequest("/docs/logo.png"));

        Assert.NotNull(embed);
        Assert.Equal("logo.png", embed!.Title);
        Assert.Contains("Image", embed.Description);
        Assert.Equal(
            "https://artifacts.example.com/api/files/thumbnail?path=docs%2Flogo.png",
            embed.ImageUrl);
        Assert.Equal("summary_large_image", embed.TwitterCard);
    }

    [Fact]
    public void TryDescribeFile_Svg_OmitsThumbnailUrl()
    {
        File.WriteAllText(Path.Combine(_root.ContentRoot, "docs", "icon.svg"), "<svg></svg>");

        var embed = _service.TryDescribeFile("docs/icon.svg", CreateRequest("/docs/icon.svg"));

        Assert.NotNull(embed);
        Assert.Null(embed!.ImageUrl);
        Assert.Equal("summary", embed.TwitterCard);
    }

    [Fact]
    public void TryDescribeFile_HiddenFile_StillUnfurls()
    {
        var embed = _service.TryDescribeFile(".hidden-file.txt", CreateRequest("/.hidden-file.txt"));

        Assert.NotNull(embed);
        Assert.Equal(".hidden-file.txt", embed!.Title);
    }

    [Fact]
    public void TryDescribeFile_Missing_ReturnsNull()
    {
        Assert.Null(_service.TryDescribeFile("docs/missing.txt", CreateRequest("/docs/missing.txt")));
    }

    [Fact]
    public void TryDescribeFile_Traversal_ReturnsNull()
    {
        Assert.Null(_service.TryDescribeFile("../secret", CreateRequest("/../secret")));
    }

    [Fact]
    public void ToHtml_EncodesQuotesAndAngleBrackets()
    {
        var embed = new SocialEmbed(
            Title: """say "hi" & <b>.txt""",
            DocumentTitle: """say "hi" & <b>.txt — Artifact Browser""",
            Description: "Code | 1 B | Modified 2026-01-01 00:00 UTC",
            SiteName: "Artifact Browser",
            Url: "https://artifacts.example.com/docs/file.txt",
            ImageUrl: null);

        var html = embed.ToHtml();
        Assert.Contains("say &quot;hi&quot; &amp; &lt;b&gt;.txt", html);
        Assert.DoesNotContain("content=\"say \"hi\"", html);
        Assert.DoesNotContain("<b>", html);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(12, "12 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    public void FormatBytes_UsesInvariantUnits(long bytes, string expected)
    {
        Assert.Equal(expected, SocialEmbedService.FormatBytes(bytes));
    }

    [Fact]
    public void FormatUtc_MinValue_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SocialEmbedService.FormatUtc(DateTimeOffset.MinValue));
    }

    [Fact]
    public void IsReservedPath_MatchesKnownPrefixes()
    {
        Assert.True(SocialEmbedService.IsReservedPath("/health"));
        Assert.True(SocialEmbedService.IsReservedPath("/api/files/raw"));
        Assert.True(SocialEmbedService.IsReservedPath("/_framework/blazor.web.js"));
        Assert.False(SocialEmbedService.IsReservedPath("/docs"));
        Assert.False(SocialEmbedService.IsReservedPath("/"));
    }

    public void Dispose() => _root.Dispose();

    private static HttpRequest CreateRequest(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("artifacts.example.com");
        context.Request.Path = path;
        return context.Request;
    }
}
