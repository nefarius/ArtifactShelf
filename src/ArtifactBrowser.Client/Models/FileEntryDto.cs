namespace ArtifactBrowser.Client.Models;

public enum ArtifactKind
{
    Directory,
    File,
}

public enum MediaCategory
{
    Other,
    Directory,
    Text,
    Markdown,
    Image,
    Audio,
    Video,
    Archive,
    Pdf,
    Code,
}

/// <summary>A single file or directory entry as shown in a directory listing.</summary>
public class FileEntryDto
{
    /// <summary>Display name (final path segment).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Virtual path, relative to the content root, using '/' separators and no leading slash.</summary>
    public string Path { get; set; } = string.Empty;

    public ArtifactKind Kind { get; set; }

    /// <summary>Size in bytes. For directories, null unless recursive size was requested.</summary>
    public long? Size { get; set; }

    public DateTimeOffset Modified { get; set; }

    public string Extension { get; set; } = string.Empty;

    public MediaCategory MediaCategory { get; set; }

    public bool IsDirectory => Kind == ArtifactKind.Directory;
}

public sealed class DirectoryListingDto
{
    public string Path { get; set; } = string.Empty;

    public string? ParentPath { get; set; }

    public List<FileEntryDto> Entries { get; set; } = new();

    public bool Truncated { get; set; }
}

public sealed class TreeNodeDto
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool HasChildren { get; set; }

    public List<TreeNodeDto> Children { get; set; } = new();
}

public sealed class SearchResultDto : FileEntryDto
{
    public string ParentPath { get; set; } = string.Empty;
}

public sealed class SearchResponseDto
{
    public List<SearchResultDto> Results { get; set; } = new();

    public bool Truncated { get; set; }
}

public enum PreviewKind
{
    Text,
    Markdown,
    Image,
    Audio,
    Video,
    Pdf,
    Unsupported,
    TooLarge,
}

public sealed class PreviewDto
{
    public PreviewKind Kind { get; set; }

    /// <summary>Raw text content for Text/Markdown previews.</summary>
    public string? Content { get; set; }

    public bool Truncated { get; set; }

    public string MimeType { get; set; } = "application/octet-stream";

    public long Size { get; set; }
}

public sealed class ApiErrorDto
{
    public string Message { get; set; } = string.Empty;

    public string? Code { get; set; }
}

/// <summary>Client-safe UI configuration resolved from the server <c>ArtifactBrowser</c> section.</summary>
public sealed class UiConfigDto
{
    public const string DefaultBrandTitle = "Artifact Browser";

    public const string GitHubRepositoryUrl = "https://github.com/nefarius/ArtifactShelf";

    public string HeaderTitle { get; set; } = DefaultBrandTitle;

    public string DocumentTitle { get; set; } = DefaultBrandTitle;

    public bool ShowGitHubLink { get; set; } = true;
}
