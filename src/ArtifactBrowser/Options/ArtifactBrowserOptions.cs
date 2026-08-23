namespace ArtifactBrowser.Options;

/// <summary>
/// Server-side configuration for the artifact browser. Bound from the "ArtifactBrowser"
/// configuration section (appsettings.json, environment variables, etc.).
/// </summary>
public sealed class ArtifactBrowserOptions
{
    public const string SectionName = "ArtifactBrowser";

    /// <summary>Absolute path to the read-only artifact tree that is browsed.</summary>
    public string ContentRoot { get; set; } = "/data";

    /// <summary>Absolute path to the writable cache used for generated thumbnails.</summary>
    public string CacheRoot { get; set; } = "/cache";

    /// <summary>Glob-style patterns (matched against file/directory names) that are hidden from listings, search, and direct access.</summary>
    public List<string> HiddenPatterns { get; set; } = new()
    {
        ".*",
        "Thumbs.db",
        "desktop.ini",
        "@eaDir",
        "$RECYCLE.BIN",
        "System Volume Information",
    };

    /// <summary>Maximum number of bytes read when generating a raw text/markdown preview.</summary>
    public long MaxTextPreviewBytes { get; set; } = 1 * 1024 * 1024;

    /// <summary>Maximum number of entries returned for a single directory listing.</summary>
    public int MaxDirectoryEntries { get; set; } = 20_000;

    /// <summary>Maximum recursion depth allowed when building the sidebar tree.</summary>
    public int MaxTreeDepth { get; set; } = 64;

    /// <summary>Maximum number of results returned from a recursive search.</summary>
    public int MaxSearchResults { get; set; } = 2000;

    /// <summary>Maximum recursion depth allowed while searching.</summary>
    public int MaxSearchDepth { get; set; } = 64;

    /// <summary>Maximum number of entries that may be included in a single ZIP download.</summary>
    public int MaxArchiveEntries { get; set; } = 5000;

    /// <summary>Maximum total uncompressed bytes that may be included in a single ZIP download.</summary>
    public long MaxArchiveBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>Maximum number of thumbnail generation jobs that may run concurrently.</summary>
    public int MaxConcurrentThumbnails { get; set; } = 4;

    /// <summary>Maximum number of ZIP streaming jobs that may run concurrently.</summary>
    public int MaxConcurrentZipJobs { get; set; } = 2;

    /// <summary>Longest edge, in pixels, of generated thumbnails.</summary>
    public int ThumbnailMaxDimension { get; set; } = 256;

    /// <summary>JPEG quality used when encoding thumbnails.</summary>
    public int ThumbnailJpegQuality { get; set; } = 80;

    /// <summary>How long generated thumbnails remain valid in the cache before being regenerated.</summary>
    public int ThumbnailCacheMaxAgeDays { get; set; } = 30;

    /// <summary>How long directory listings may be served from the short-lived in-memory cache.</summary>
    public int DirectoryListingCacheSeconds { get; set; } = 5;

    /// <summary>Per-request timeout applied to file-service operations.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Default item view mode presented to new clients ("Details", "Grid", "Icons").</summary>
    public string DefaultViewMode { get; set; } = "Details";

    /// <summary>Default sort field presented to new clients ("Name", "Size", "Modified", "Type").</summary>
    public string DefaultSortField { get; set; } = "Name";

    /// <summary>Default sort direction.</summary>
    public bool DefaultSortDescending { get; set; }

    /// <summary>Default icon/grid item size ("Small", "Medium", "Large").</summary>
    public string DefaultItemSize { get; set; } = "Medium";
}
