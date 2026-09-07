namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Detects whether a request is a browser navigation that wants the HTML file browser,
/// as opposed to an automated client that expects file bytes or a real 404.
/// </summary>
public static class HtmlAccept
{
    /// <summary>
    /// True only when <c>Accept</c> includes <c>text/html</c> with a non-zero quality.
    /// <c>*/*</c> and a missing header are treated as non-HTML (curl, iwr, HttpClient).
    /// </summary>
    public static bool PrefersHtml(HttpRequest request)
    {
        var accept = request.GetTypedHeaders().Accept;
        if (accept is { Count: > 0 })
        {
            foreach (var value in accept)
            {
                if (value.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
                    && (value.Quality ?? 1) > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
