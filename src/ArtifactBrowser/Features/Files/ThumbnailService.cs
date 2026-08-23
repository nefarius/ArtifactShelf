using ArtifactBrowser.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ArtifactBrowser.Features.Files;

public sealed record ThumbnailResult(string PhysicalPath, DateTimeOffset LastModified);

/// <summary>
/// Generates and caches image thumbnails in the writable cache volume, never touching the
/// read-only artifact tree. Concurrency is bounded to protect the host from abusive traffic.
/// </summary>
public sealed class ThumbnailService : IDisposable
{
    private readonly PathGuard _pathGuard;
    private readonly ArtifactBrowserOptions _options;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly string _cacheRoot;

    public ThumbnailService(PathGuard pathGuard, IOptions<ArtifactBrowserOptions> options)
    {
        _pathGuard = pathGuard;
        _options = options.Value;
        _cacheRoot = Path.GetFullPath(_options.CacheRoot);
        _concurrencyLimiter = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentThumbnails));
        Directory.CreateDirectory(Path.Combine(_cacheRoot, "thumbnails"));
    }

    public async Task<ThumbnailResult?> GetOrCreateThumbnailAsync(string? virtualPath, CancellationToken cancellationToken)
    {
        var resolved = _pathGuard.Resolve(virtualPath);
        if (!File.Exists(resolved.PhysicalPath))
        {
            return null;
        }

        var extension = Path.GetExtension(resolved.PhysicalPath);
        if (MimeHelper.Categorize(extension) != ArtifactBrowser.Client.Models.MediaCategory.Image ||
            string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sourceInfo = new FileInfo(resolved.PhysicalPath);
        var cachePath = GetCachePath(resolved.VirtualPath, sourceInfo.LastWriteTimeUtc);

        if (File.Exists(cachePath))
        {
            var cacheInfo = new FileInfo(cachePath);
            var age = DateTimeOffset.UtcNow - cacheInfo.LastWriteTimeUtc;
            if (age.TotalDays <= _options.ThumbnailCacheMaxAgeDays)
            {
                return new ThumbnailResult(cachePath, cacheInfo.LastWriteTimeUtc);
            }
        }

        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the slot in case another request already generated it.
            if (File.Exists(cachePath))
            {
                return new ThumbnailResult(cachePath, new FileInfo(cachePath).LastWriteTimeUtc);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

            var tempPath = cachePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var image = await Image.LoadAsync(resolved.PhysicalPath, cancellationToken))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(_options.ThumbnailMaxDimension, _options.ThumbnailMaxDimension),
                    }));

                    await image.SaveAsync(tempPath, new JpegEncoder { Quality = _options.ThumbnailJpegQuality }, cancellationToken);
                }

                File.Move(tempPath, cachePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            return new ThumbnailResult(cachePath, DateTimeOffset.UtcNow);
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private string GetCachePath(string virtualPath, DateTimeOffset sourceModified)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{virtualPath}|{sourceModified.UtcTicks}|{_options.ThumbnailMaxDimension}")));
        return Path.Combine(_cacheRoot, "thumbnails", hash[..2], hash + ".jpg");
    }

    public void Dispose() => _concurrencyLimiter.Dispose();
}
