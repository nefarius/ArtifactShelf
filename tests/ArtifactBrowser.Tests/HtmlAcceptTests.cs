using ArtifactBrowser.Features.Files;
using Microsoft.AspNetCore.Http;

namespace ArtifactBrowser.Tests;

public sealed class HtmlAcceptTests
{
    [Theory]
    [InlineData("text/html", true)]
    [InlineData("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8", true)]
    [InlineData("TEXT/HTML", true)]
    [InlineData("*/*", false)]
    [InlineData("application/json", false)]
    [InlineData("text/html;q=0", false)]
    [InlineData("", false)]
    public void PrefersHtml_ReturnsExpected(string accept, bool expected)
    {
        var context = new DefaultHttpContext();
        if (accept.Length > 0)
        {
            context.Request.Headers.Accept = accept;
        }

        Assert.Equal(expected, HtmlAccept.PrefersHtml(context.Request));
    }

    [Fact]
    public void PrefersHtml_MissingAcceptHeader_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        Assert.False(HtmlAccept.PrefersHtml(context.Request));
    }
}
