using System.ComponentModel.DataAnnotations;
using ArtifactBrowser.Client.Models;

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

    /// <summary>Glob-style patterns (matched against file/directory names) that are hidden from listings, the sidebar tree, and search. Direct file URLs still download matching files.</summary>
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
    [Range(1, long.MaxValue)]
    public long MaxTextPreviewBytes { get; set; } = 1 * 1024 * 1024;

    /// <summary>Maximum number of entries returned for a single directory listing.</summary>
    [Range(1, int.MaxValue)]
    public int MaxDirectoryEntries { get; set; } = 20_000;

    /// <summary>Maximum recursion depth allowed when building the sidebar tree.</summary>
    [Range(1, int.MaxValue)]
    public int MaxTreeDepth { get; set; } = 64;

    /// <summary>Maximum number of results returned from a recursive search.</summary>
    [Range(1, int.MaxValue)]
    public int MaxSearchResults { get; set; } = 2000;

    /// <summary>Maximum recursion depth allowed while searching.</summary>
    [Range(1, int.MaxValue)]
    public int MaxSearchDepth { get; set; } = 64;

    /// <summary>Maximum number of entries that may be included in a single ZIP download.</summary>
    [Range(1, int.MaxValue)]
    public int MaxArchiveEntries { get; set; } = 5000;

    /// <summary>Maximum total uncompressed bytes that may be included in a single ZIP download.</summary>
    [Range(1, long.MaxValue)]
    public long MaxArchiveBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>Maximum number of thumbnail generation jobs that may run concurrently.</summary>
    [Range(1, int.MaxValue)]
    public int MaxConcurrentThumbnails { get; set; } = 4;

    /// <summary>Maximum number of ZIP streaming jobs that may run concurrently.</summary>
    [Range(1, int.MaxValue)]
    public int MaxConcurrentZipJobs { get; set; } = 2;

    /// <summary>Longest edge, in pixels, of generated thumbnails.</summary>
    [Range(1, int.MaxValue)]
    public int ThumbnailMaxDimension { get; set; } = 256;

    /// <summary>JPEG quality used when encoding thumbnails.</summary>
    [Range(1, 100)]
    public int ThumbnailJpegQuality { get; set; } = 80;

    /// <summary>How long generated thumbnails remain valid in the cache before being regenerated.</summary>
    [Range(1, int.MaxValue)]
    public int ThumbnailCacheMaxAgeDays { get; set; } = 30;

    /// <summary>How long directory listings may be served from the short-lived in-memory cache.</summary>
    [Range(1, int.MaxValue)]
    public int DirectoryListingCacheSeconds { get; set; } = 5;

    /// <summary>Per-request timeout applied to file-service operations.</summary>
    [Range(1, int.MaxValue)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Default item view mode presented to new clients ("Details", "Grid", "Icons").</summary>
    public string DefaultViewMode { get; set; } = "Details";

    /// <summary>Default sort field presented to new clients ("Name", "Size", "Modified", "Type").</summary>
    public string DefaultSortField { get; set; } = "Name";

    /// <summary>Default sort direction.</summary>
    public bool DefaultSortDescending { get; set; }

    /// <summary>Default icon/grid item size ("Small", "Medium", "Large").</summary>
    public string DefaultItemSize { get; set; } = "Medium";

    /// <summary>Optional override for the top-left header label. Blank or omitted uses <see cref="UiConfigDto.DefaultBrandTitle"/>.</summary>
    [MaxLength(200)]
    public string? HeaderTitle { get; set; }

    /// <summary>Optional override for the HTML document-title base. Blank or omitted uses <see cref="UiConfigDto.DefaultBrandTitle"/>. Nested folders still use <c>{folder} — {DocumentTitle}</c>.</summary>
    [MaxLength(200)]
    public string? DocumentTitle { get; set; }

    public string ResolvedHeaderTitle => ResolveBrandTitle(HeaderTitle);

    public string ResolvedDocumentTitle => ResolveBrandTitle(DocumentTitle);

    internal static string ResolveBrandTitle(string? value) =>
        string.IsNullOrWhiteSpace(value) ? UiConfigDto.DefaultBrandTitle : value.Trim();
}
