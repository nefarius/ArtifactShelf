using ArtifactBrowser.Client.Utilities;

namespace ArtifactBrowser.Tests;

public sealed class PrettyUrlTests
{
    [Theory]
    [InlineData("https://example.com/", null, "https://example.com/")]
    [InlineData("https://example.com/", "", "https://example.com/")]
    [InlineData("https://example.com/", "   ", "https://example.com/")]
    [InlineData("https://example.com", "", "https://example.com/")]
    [InlineData("https://example.com/shelf/", "", "https://example.com/shelf/")]
    public void FromVirtualPath_RootUsesTrailingSlash(string baseUri, string? virtualPath, string expected)
    {
        Assert.Equal(expected, PrettyUrl.FromVirtualPath(baseUri, virtualPath));
    }

    [Theory]
    [InlineData("https://example.com/", "docs", "https://example.com/docs")]
    [InlineData("https://example.com/", "docs/guides", "https://example.com/docs/guides")]
    [InlineData("https://example.com/", "builds/app.zip", "https://example.com/builds/app.zip")]
    [InlineData("https://example.com", "docs", "https://example.com/docs")]
    [InlineData("https://example.com/shelf/", "docs/guides", "https://example.com/shelf/docs/guides")]
    public void FromVirtualPath_JoinsSegmentsToBase(string baseUri, string virtualPath, string expected)
    {
        Assert.Equal(expected, PrettyUrl.FromVirtualPath(baseUri, virtualPath));
    }

    [Fact]
    public void FromVirtualPath_EncodesSpacesAndReservedCharacters()
    {
        Assert.Equal(
            "https://example.com/foo/bar%20baz.zip",
            PrettyUrl.FromVirtualPath("https://example.com/", "foo/bar baz.zip"));
        Assert.Equal(
            "https://example.com/file%231.txt",
            PrettyUrl.FromVirtualPath("https://example.com/", "file#1.txt"));
        Assert.Equal(
            "https://example.com/a%3Fb",
            PrettyUrl.FromVirtualPath("https://example.com/", "a?b"));
    }

    [Fact]
    public void FromVirtualPath_IgnoresEmptySegments()
    {
        Assert.Equal(
            "https://example.com/docs/guides",
            PrettyUrl.FromVirtualPath("https://example.com/", "/docs//guides/"));
    }
}
