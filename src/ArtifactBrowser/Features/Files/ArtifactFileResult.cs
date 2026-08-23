namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Shared <see cref="IResult"/> factory for serving an artifact file with MIME type,
/// optional attachment disposition, and HTTP range support.
/// </summary>
public static class ArtifactFileResult
{
    public static IResult Create(ResolvedPath resolved, bool download)
    {
        if (!File.Exists(resolved.PhysicalPath))
        {
            return Results.NotFound();
        }

        var fileInfo = new FileInfo(resolved.PhysicalPath);
        var extension = Path.GetExtension(resolved.PhysicalPath);
        var contentType = MimeHelper.GetContentType(extension);
        var fileName = Path.GetFileName(resolved.PhysicalPath);

        return Results.File(
            resolved.PhysicalPath,
            contentType,
            fileDownloadName: download ? fileName : null,
            lastModified: fileInfo.LastWriteTimeUtc,
            enableRangeProcessing: true);
    }

    public static IResult FromVirtualPath(string? path, bool download, PathGuard pathGuard)
    {
        var resolved = pathGuard.Resolve(path, allowHidden: true);
        return Create(resolved, download);
    }
}
