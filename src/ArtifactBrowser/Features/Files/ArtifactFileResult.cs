namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Shared <see cref="IResult"/> factory for serving an artifact file with MIME type,
/// optional attachment disposition, and HTTP range support.
/// </summary>
public static class ArtifactFileResult
{
    public static IResult Create(ResolvedPath resolved, bool download, IDownloadCounter? downloadCounter = null, bool recordDownload = false)
    {
        if (!File.Exists(resolved.PhysicalPath))
        {
            return Results.NotFound();
        }

        var fileInfo = new FileInfo(resolved.PhysicalPath);
        var extension = Path.GetExtension(resolved.PhysicalPath);
        var contentType = MimeHelper.GetContentType(extension);
        var fileName = Path.GetFileName(resolved.PhysicalPath);

        IResult fileResult = Results.File(
            resolved.PhysicalPath,
            contentType,
            fileDownloadName: download ? fileName : null,
            lastModified: fileInfo.LastWriteTimeUtc,
            enableRangeProcessing: true);

        if (!recordDownload || downloadCounter is null)
        {
            return fileResult;
        }

        return new CountingFileResult(fileResult, downloadCounter, resolved.VirtualPath);
    }

    public static IResult FromVirtualPath(
        string? path,
        bool download,
        PathGuard pathGuard,
        IDownloadCounter? downloadCounter = null,
        bool recordDownload = false)
    {
        var resolved = pathGuard.Resolve(path, allowHidden: true);
        return Create(resolved, download, downloadCounter, recordDownload);
    }

    /// <summary>
    /// Executes the inner file result first, then enqueues a count without touching the response.
        /// Bookkeeping exceptions are swallowed so a failed store cannot fail a completed download.
    /// </summary>
    private sealed class CountingFileResult(IResult inner, IDownloadCounter downloadCounter, string virtualPath) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await inner.ExecuteAsync(httpContext);

            if (httpContext.Response.StatusCode is not (StatusCodes.Status200OK or StatusCodes.Status206PartialContent))
            {
                return;
            }

            try
            {
                downloadCounter.RecordDownload(virtualPath);
            }
            catch
            {
                // Statistics are best-effort; the file has already been served.
            }
        }
    }
}
