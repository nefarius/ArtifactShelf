using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Features.Files;

namespace ArtifactBrowser.Tests;

public sealed class MimeHelperTests
{
    [Theory]
    [InlineData(".md", MediaCategory.Markdown)]
    [InlineData(".markdown", MediaCategory.Markdown)]
    [InlineData(".png", MediaCategory.Image)]
    [InlineData(".jpg", MediaCategory.Image)]
    [InlineData(".mp3", MediaCategory.Audio)]
    [InlineData(".mp4", MediaCategory.Video)]
    [InlineData(".pdf", MediaCategory.Pdf)]
    [InlineData(".zip", MediaCategory.Archive)]
    [InlineData(".cs", MediaCategory.Code)]
    [InlineData(".unknownext", MediaCategory.Other)]
    public void Categorize_ReturnsExpectedCategory(string extension, MediaCategory expected)
    {
        Assert.Equal(expected, MimeHelper.Categorize(extension));
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".md", true)]
    [InlineData(".cs", true)]
    [InlineData(".png", false)]
    [InlineData(".zip", false)]
    public void IsPreviewableAsText_ReturnsExpected(string extension, bool expected)
    {
        Assert.Equal(expected, MimeHelper.IsPreviewableAsText(extension));
    }

    [Fact]
    public void GetContentType_UnknownExtension_ReturnsOctetStream()
    {
        Assert.Equal("application/octet-stream", MimeHelper.GetContentType(".totallyunknown"));
    }

    [Fact]
    public void GetContentType_Svg_ReturnsPlainText()
    {
        Assert.Equal("text/plain", MimeHelper.GetContentType(".svg"));
    }
}
