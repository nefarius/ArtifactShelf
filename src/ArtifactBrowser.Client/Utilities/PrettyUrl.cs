namespace ArtifactBrowser.Client.Utilities;

/// <summary>Builds the public pretty URL for an artifact virtual path.</summary>
public static class PrettyUrl
{
    public static string FromVirtualPath(string baseUri, string? virtualPath)
    {
        var origin = (baseUri ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(virtualPath))
        {
            return origin + "/";
        }

        var encoded = string.Join('/',
            virtualPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return origin + "/" + encoded;
    }
}
