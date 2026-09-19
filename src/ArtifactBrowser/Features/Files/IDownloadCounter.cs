namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Best-effort per-file download statistics. Implementations must never throw from
/// <see cref="RecordDownload"/> and must never perform blocking I/O on that path.
/// </summary>
public interface IDownloadCounter
{
    /// <summary>
    /// Records a completed download without blocking the caller. Failures are dropped.
    /// </summary>
    void RecordDownload(string virtualPath);

    /// <summary>
    /// Batched lookup of persisted counts. Missing or unread paths return 0. Never throws.
    /// </summary>
    IReadOnlyDictionary<string, long> GetCounts(IReadOnlyList<string> virtualPaths);
}

/// <summary>No-op counter used when statistics are disabled or unavailable in tests.</summary>
internal sealed class NullDownloadCounter : IDownloadCounter
{
    public static readonly NullDownloadCounter Instance = new();

    public void RecordDownload(string virtualPath)
    {
    }

    public IReadOnlyDictionary<string, long> GetCounts(IReadOnlyList<string> virtualPaths)
    {
        var result = new Dictionary<string, long>(virtualPaths.Count);
        foreach (var path in virtualPaths)
        {
            result[path] = 0;
        }

        return result;
    }
}
