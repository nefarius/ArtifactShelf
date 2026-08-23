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

    [Fact]
    public void ToHtml_NeutralizesImageAndObfuscatedScriptUrls()
    {
        var html = MarkdownSafeHtml.ToHtml(
            "![](javascript:alert(1))\n\n[x]( data:text/html,hi) [y](vbscript:msgbox(1))");

        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToHtml_PreservesLiteralScriptUrlAndEventHandlerText()
    {
        var html = MarkdownSafeHtml.ToHtml(
            "Do not write href=\"javascript:alert(1)\" or onclick=\"alert(1)\" in docs.\n");

        Assert.Contains("javascript:alert(1)", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("onclick=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"(?i)<[^>]+(?:href|src)\s*=\s*['""]?\s*(?:javascript|data|vbscript)\s*:", html);
        Assert.DoesNotMatch(@"(?i)<[^>]+\son[a-z]+\s*=", html);
    }

    [Fact]
    public void ToHtml_PreservesEventHandlerLookalikesInLinkTextAndImageAlt()
    {
        var html = MarkdownSafeHtml.ToHtml(
            "[hello onclick=x](https://example.com)\n\n![ onclick=foo ](https://example.com/x.png)");

        Assert.Contains("hello onclick=x", html);
        Assert.Contains("onclick=foo", html);
        Assert.Contains("https://example.com", html);
    }

    [Fact]
    public void ToHtml_PreservesJavascriptInCodeSpan()
    {
        var html = MarkdownSafeHtml.ToHtml("`href=\"javascript:alert(1)\"`");

        Assert.Contains("javascript:alert(1)", html, StringComparison.OrdinalIgnoreCase);
    }
}
