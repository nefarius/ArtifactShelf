using ArtifactBrowser.Client.Models;

namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Translates file-service exceptions into safe HTTP responses that never leak physical paths
/// or internal details to the client.
/// </summary>
public sealed class FilesExceptionMiddleware(RequestDelegate next, ILogger<FilesExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (PathAccessDeniedException ex)
        {
            logger.LogWarning("Denied path access on {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteError(context, StatusCodes.Status400BadRequest, "The requested path is not valid.");
        }
        catch (DirectoryNotFoundException)
        {
            await WriteError(context, StatusCodes.Status404NotFound, "The requested folder was not found.");
        }
        catch (FileNotFoundException)
        {
            await WriteError(context, StatusCodes.Status404NotFound, "The requested file was not found.");
        }
        catch (ArchiveLimitExceededException ex)
        {
            logger.LogWarning("Archive limit exceeded: {Message}", ex.Message);
            if (!context.Response.HasStarted)
            {
                await WriteError(context, StatusCodes.Status413PayloadTooLarge, "The requested archive is too large.");
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to write back.
        }
        catch (OperationCanceledException)
        {
            // Request timeout (not a client abort). Let UseRequestTimeouts produce 504
            // unless archive/raw headers have already started, in which case a 200 is
            // unavoidable and aborting the connection is the safest signal.
            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            throw;
        }
    }

    private static async Task WriteError(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ApiErrorDto { Message = message });
    }
}
