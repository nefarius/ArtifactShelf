using System.Globalization;
using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Options;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Builds Open Graph / Twitter Card metadata from a confined artifact path.
/// Descriptions use filesystem metadata only — never file contents.
/// </summary>
public sealed class SocialEmbedService(PathGuard pathGuard, IOptions<ArtifactBrowserOptions> options)
{
    private readonly ArtifactBrowserOptions _options = options.Value;

    /// <summary>
    /// Folder (or default branding) embed for the SPA HTML shell.
    /// Missing, reserved, and file paths fall back to site branding.
    /// </summary>
    public SocialEmbed DescribeFolder(HttpRequest request)
    {
        var fallback = CreateDefault(request);
        if (IsReservedPath(request.Path))
        {
            return fallback;
        }

        try
        {
            var resolved = pathGuard.Resolve(request.Path.Value, allowHidden: true);
            if (!Directory.Exists(resolved.PhysicalPath))
            {
                return fallback;
            }

            return CreateDirectory(resolved, request);
        }
        catch (PathAccessDeniedException)
        {
            return fallback;
        }
    }

    /// <summary>
    /// File embed for a pretty file URL, or <c>null</c> when the path is not an existing file.
    /// </summary>
    public SocialEmbed? TryDescribeFile(string? artifactPath, HttpRequest request)
    {
        try
        {
            var resolved = pathGuard.Resolve(artifactPath, allowHidden: true);
            if (!File.Exists(resolved.PhysicalPath))
            {
                return null;
            }

            return CreateFile(resolved, request);
        }
        catch (PathAccessDeniedException)
        {
            return null;
        }
    }

    internal static bool IsReservedPath(PathString path)
    {
        var value = path.Value ?? "/";
        if (value.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/error", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/not-found", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase);
    }

    private SocialEmbed CreateDefault(HttpRequest request)
    {
        var siteName = _options.ResolvedDocumentTitle;
        return new SocialEmbed(
            Title: siteName,
            DocumentTitle: siteName,
            Description: siteName,
            SiteName: siteName,
            Url: GetPublicUrl(request),
            ImageUrl: null);
    }

    private SocialEmbed CreateDirectory(ResolvedPath resolved, HttpRequest request)
    {
        var siteName = _options.ResolvedDocumentTitle;
        var isRoot = resolved.VirtualPath.Length == 0;
        var title = isRoot ? siteName : Path.GetFileName(resolved.VirtualPath.Replace('/', Path.DirectorySeparatorChar));
        var documentTitle = isRoot ? siteName : $"{title} — {siteName}";
        var displayPath = isRoot ? "/" : "/" + resolved.VirtualPath;
        var description = $"{siteName} | {displayPath}";

        return new SocialEmbed(
            Title: title,
            DocumentTitle: documentTitle,
            Description: description,
            SiteName: siteName,
            Url: GetPublicUrl(request),
            ImageUrl: null);
    }

    private SocialEmbed CreateFile(ResolvedPath resolved, HttpRequest request)
    {
        var siteName = _options.ResolvedDocumentTitle;
        var name = Path.GetFileName(resolved.PhysicalPath);
        var extension = Path.GetExtension(name);
        var category = MimeHelper.Categorize(extension);

        long size = 0;
        var modified = DateTimeOffset.MinValue;
        try
        {
            var info = new FileInfo(resolved.PhysicalPath);
            size = info.Length;
            modified = info.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Keep zeros; still unfurl the name and type.
        }

        var description = $"{category} | {FormatBytes(size)} | Modified {FormatUtc(modified)}";
        var imageUrl = HasThumbnail(category, extension)
            ? $"{GetPublicOrigin(request)}/api/files/thumbnail?path={Uri.EscapeDataString(resolved.VirtualPath)}"
            : null;

        return new SocialEmbed(
            Title: name,
            DocumentTitle: $"{name} — {siteName}",
            Description: description,
            SiteName: siteName,
            Url: GetPublicUrl(request),
            ImageUrl: imageUrl);
    }

    private static bool HasThumbnail(MediaCategory category, string extension) =>
        category == MediaCategory.Image
        && !string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase);

    private static string GetPublicUrl(HttpRequest request) =>
        GetPublicOrigin(request) + request.Path.Value;

    private static string GetPublicOrigin(HttpRequest request)
    {
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
        return $"{request.Scheme}://{request.Host}{pathBase}";
    }

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{value:0} {units[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {units[unit]}");
    }

    internal static string FormatUtc(DateTimeOffset value)
    {
        if (value == DateTimeOffset.MinValue)
        {
            return string.Empty;
        }

        return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }
}
