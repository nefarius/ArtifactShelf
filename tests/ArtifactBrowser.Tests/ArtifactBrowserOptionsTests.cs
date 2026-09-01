using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Options;

namespace ArtifactBrowser.Tests;

public sealed class ArtifactBrowserOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveBrandTitle_WhenBlank_ReturnsDefault(string? value)
    {
        Assert.Equal(UiConfigDto.DefaultBrandTitle, ArtifactBrowserOptions.ResolveBrandTitle(value));
    }

    [Fact]
    public void ResolveBrandTitle_WhenSet_TrimsValue()
    {
        Assert.Equal("My Builds", ArtifactBrowserOptions.ResolveBrandTitle("  My Builds  "));
    }

    [Fact]
    public void ResolvedTitles_AreIndependent()
    {
        var options = new ArtifactBrowserOptions
        {
            HeaderTitle = "Header Only",
        };

        Assert.Equal("Header Only", options.ResolvedHeaderTitle);
        Assert.Equal(UiConfigDto.DefaultBrandTitle, options.ResolvedDocumentTitle);
    }
}
