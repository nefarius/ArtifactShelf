using System.Net.Mime;
using System.Text;
using System.Text.Encodings.Web;

namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Open Graph / Twitter Card values for a folder or file pretty URL.
/// Strings are unencoded; Razor encodes on render, and <see cref="ToHtml"/> encodes for raw HTML.
/// </summary>
public sealed record SocialEmbed(
    string Title,
    string DocumentTitle,
    string Description,
    string SiteName,
    string Url,
    string? ImageUrl)
{
    public string TwitterCard => ImageUrl is null ? "summary" : "summary_large_image";

    public IResult ToHtmlResult() => new HtmlResult(this);

    private sealed class HtmlResult(SocialEmbed embed) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var html = embed.ToHtml();
            var bytes = Encoding.UTF8.GetBytes(html);
            httpContext.Response.ContentType = MediaTypeNames.Text.Html + "; charset=utf-8";
            httpContext.Response.ContentLength = bytes.Length;
            if (!HttpMethods.IsHead(httpContext.Request.Method))
            {
                await httpContext.Response.Body.WriteAsync(bytes);
            }
        }
    }

    public string ToHtml()
    {
        var enc = HtmlEncoder.Default;
        var imageTag = ImageUrl is null
            ? string.Empty
            : $"""<meta property="og:image" content="{enc.Encode(ImageUrl)}" />""" + "\n";

        return
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8" />
            """
            + $"<title>{enc.Encode(DocumentTitle)}</title>\n"
            + $"""<meta property="og:title" content="{enc.Encode(Title)}" />""" + "\n"
            + $"""<meta property="og:description" content="{enc.Encode(Description)}" />""" + "\n"
            + $"""<meta property="og:url" content="{enc.Encode(Url)}" />""" + "\n"
            + """<meta property="og:type" content="website" />""" + "\n"
            + $"""<meta property="og:site_name" content="{enc.Encode(SiteName)}" />""" + "\n"
            + imageTag
            + $"""<meta name="twitter:card" content="{TwitterCard}" />""" + "\n"
            + $"""<meta name="twitter:title" content="{enc.Encode(Title)}" />""" + "\n"
            + $"""<meta name="twitter:description" content="{enc.Encode(Description)}" />""" + "\n"
            + """
            </head>
            <body>
            """
            + $"<p>{enc.Encode(Title)}</p>\n"
            + """
            </body>
            </html>
            """;
    }
}
