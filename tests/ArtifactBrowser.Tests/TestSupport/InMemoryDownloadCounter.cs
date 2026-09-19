using ArtifactBrowser.Features.Files;

namespace ArtifactBrowser.Tests.TestSupport;

/// <summary>Synchronous in-memory counter for unit tests that need deterministic counts.</summary>
internal sealed class InMemoryDownloadCounter : IDownloadCounter
{
    private readonly Dictionary<string, long> _counts = new(OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal);

    public IReadOnlyList<string> Recorded => _recorded;
    private readonly List<string> _recorded = new();

    public void Set(string virtualPath, long count) => _counts[virtualPath] = count;

    public void RecordDownload(string virtualPath)
    {
        _recorded.Add(virtualPath);
        _counts[virtualPath] = _counts.TryGetValue(virtualPath, out var current) ? current + 1 : 1;
    }

    public IReadOnlyDictionary<string, long> GetCounts(IReadOnlyList<string> virtualPaths)
    {
        var result = new Dictionary<string, long>(virtualPaths.Count);
        foreach (var path in virtualPaths)
        {
            result[path] = _counts.TryGetValue(path, out var count) ? count : 0;
        }

        return result;
    }
}

/// <summary>Throws from every member so callers can prove bookkeeping failures stay isolated.</summary>
internal sealed class ThrowingDownloadCounter : IDownloadCounter
{
    public void RecordDownload(string virtualPath) =>
        throw new IOException("Download statistics are unavailable.");

    public IReadOnlyDictionary<string, long> GetCounts(IReadOnlyList<string> virtualPaths) =>
        throw new IOException("Download statistics are unavailable.");
}
