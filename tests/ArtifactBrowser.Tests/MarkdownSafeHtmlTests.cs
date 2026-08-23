using ArtifactBrowser.Client.Utilities;

namespace ArtifactBrowser.Tests;

public sealed class MarkdownSafeHtmlTests
{
    [Fact]
    public void ToHtml_RendersCommonMarkAndTables()
    {
        var html = MarkdownSafeHtml.ToHtml("# Title\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n");

        Assert.Contains("<h1", html);
        Assert.Contains("Title", html);
        Assert.Contains("<table", html);
    }

    [Fact]
    public void ToHtml_EncodesRawHtml()
    {
        var html = MarkdownSafeHtml.ToHtml("<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>");

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void ToHtml_DoesNotEmitGenericAttributes()
    {
        var html = MarkdownSafeHtml.ToHtml("[hi](https://example.com){onclick=alert(1)}");

        Assert.DoesNotMatch(@"(?i)<[^>]+onclick\s*=", html);
        Assert.Contains("{onclick=alert(1)}", html);
    }

    [Fact]
    public void ToHtml_NeutralizesJavascriptUrls()
    {
        var html = MarkdownSafeHtml.ToHtml("[x](javascript:alert(1))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }
}
