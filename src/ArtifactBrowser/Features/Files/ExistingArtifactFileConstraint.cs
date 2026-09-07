namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Matches a catch-all route when the path is an existing file under the content root
/// (including hidden names), or a missing path requested by a non-HTML client.
/// Directories and browser navigations to missing paths fall through to the SPA.
/// Traversal/symlink-escape attempts match so the endpoint can return 400.
/// </summary>
public sealed class ExistingArtifactFileConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (routeDirection == RouteDirection.UrlGeneration)
        {
            return true;
        }

        if (httpContext is null)
        {
            return false;
        }

        values.TryGetValue(routeKey, out var raw);
        var path = raw?.ToString();
        var pathGuard = httpContext.RequestServices.GetRequiredService<PathGuard>();

        try
        {
            var resolved = pathGuard.Resolve(path, allowHidden: true);
            if (File.Exists(resolved.PhysicalPath))
            {
                return true;
            }

            if (Directory.Exists(resolved.PhysicalPath))
            {
                return false;
            }

            // Missing file or folder: browsers still get the SPA; automation gets 404.
            return !HtmlAccept.PrefersHtml(httpContext.Request);
        }
        catch (PathAccessDeniedException)
        {
            return true;
        }
    }
}
