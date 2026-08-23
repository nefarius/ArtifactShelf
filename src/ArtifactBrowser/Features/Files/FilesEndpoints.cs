using ArtifactBrowser.Client.Models;

namespace ArtifactBrowser.Features.Files;

public sealed record ArchiveRequest(List<string> Paths);

public static class FilesEndpoints
{
    public static RouteGroupBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/files").WithTags("Files");

        group.MapGet("/list", (string? path, FileSystemBrowser browser) =>
        {
            return Results.Ok(browser.ListDirectory(path));
        });

        group.MapGet("/tree", (string? path, FileSystemBrowser browser) =>
        {
            return Results.Ok(browser.GetTreeNode(path));
        });

        group.MapGet("/search", (string? path, string q, bool recursive, FileSystemBrowser browser, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.Ok(new SearchResponseDto());
            }

            return Results.Ok(browser.Search(path, q, recursive, ct));
        });

        group.MapGet("/preview", async (string? path, PreviewService previewService, CancellationToken ct) =>
        {
            var preview = await previewService.GetPreviewAsync(path, ct);
            return Results.Ok(preview);
        });

        group.MapGet("/raw", (string? path, bool? download, PathGuard pathGuard) =>
        {
            var resolved = pathGuard.Resolve(path);
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
                fileDownloadName: download == true ? fileName : null,
                lastModified: fileInfo.LastWriteTimeUtc,
                enableRangeProcessing: true);
        });

        group.MapGet("/thumbnail", async (string? path, ThumbnailService thumbnailService, CancellationToken ct) =>
        {
            var result = await thumbnailService.GetOrCreateThumbnailAsync(path, ct);
            if (result is null)
            {
                return Results.NotFound();
            }

            return Results.File(result.PhysicalPath, "image/jpeg", enableRangeProcessing: true, lastModified: result.LastModified);
        }).RequireRateLimiting("heavy");

        group.MapPost("/archive", async (ArchiveRequest request, HttpContext context, ZipStreamer zipStreamer) =>
        {
            if (request.Paths.Count == 0)
            {
                return Results.BadRequest(new ApiErrorDto { Message = "No paths were provided." });
            }

            context.Response.ContentType = "application/zip";
            context.Response.Headers.ContentDisposition = "attachment; filename=\"artifacts.zip\"";

            // ZipArchive writes a trailing data descriptor synchronously when an entry stream is
            // disposed, even though the rest of the archive is written asynchronously. Kestrel (and
            // the test server) disallow synchronous writes by default, so this must be relaxed for
            // this response only.
            var syncIoFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
            if (syncIoFeature is not null)
            {
                syncIoFeature.AllowSynchronousIO = true;
            }

            try
            {
                await zipStreamer.WriteZipAsync(context.Response.Body, request.Paths, context.RequestAborted);
            }
            catch (ArchiveLimitExceededException)
            {
                // Headers are already sent by the time the limit is hit on a large archive;
                // aborting the connection is the safest way to signal failure to the client.
                context.Abort();
            }

            return Results.Empty;
        }).RequireRateLimiting("heavy");

        return group;
    }
}
