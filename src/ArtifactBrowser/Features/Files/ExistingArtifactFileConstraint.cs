namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Matches a catch-all route only when the path is an existing file under the content root
/// (including hidden names). Directories and missing paths fall through to the SPA.
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
            return File.Exists(resolved.PhysicalPath);
        }
        catch (PathAccessDeniedException)
        {
            return true;
        }
    }
}
