using ArtifactBrowser.Client.Models;

namespace ArtifactBrowser.Features.Files;

public static class MimeHelper
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".yml"] = "text/yaml",
        [".yaml"] = "text/yaml",
        [".csv"] = "text/csv",
        [".log"] = "text/plain",
        [".ini"] = "text/plain",
        [".cfg"] = "text/plain",
        [".conf"] = "text/plain",
        [".cs"] = "text/plain",
        [".js"] = "text/plain",
        [".ts"] = "text/plain",
        [".py"] = "text/plain",
        [".java"] = "text/plain",
        [".go"] = "text/plain",
        [".rs"] = "text/plain",
        [".c"] = "text/plain",
        [".cpp"] = "text/plain",
        [".h"] = "text/plain",
        [".sh"] = "text/plain",
        [".ps1"] = "text/plain",
        [".sql"] = "text/plain",
        [".html"] = "text/plain",
        [".css"] = "text/plain",
        [".gitignore"] = "text/plain",
        [".dockerfile"] = "text/plain",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".flac"] = "audio/flac",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".ogv"] = "video/ogg",
        [".mov"] = "video/quicktime",
        [".mkv"] = "video/x-matroska",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".tar"] = "application/x-tar",
        [".gz"] = "application/gzip",
        [".7z"] = "application/x-7z-compressed",
        [".rar"] = "application/vnd.rar",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".yml", ".yaml", ".csv", ".log", ".ini", ".cfg", ".conf",
        ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".go", ".rs", ".c", ".cpp", ".h", ".hpp",
        ".sh", ".ps1", ".sql", ".html", ".htm", ".css", ".scss", ".less", ".gitignore", ".editorconfig",
        ".dockerfile", ".razor", ".cshtml", ".toml", ".env", ".gradle", ".properties", ".bat", ".cmd",
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".ico",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".ogv", ".mov", ".mkv",
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".tar", ".gz", ".7z", ".rar", ".bz2", ".xz",
    };

    public static string GetContentType(string extension) =>
        ContentTypes.TryGetValue(extension, out var value) ? value : "application/octet-stream";

    public static MediaCategory Categorize(string extension)
    {
        if (string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return MediaCategory.Markdown;
        }

        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return MediaCategory.Pdf;
        }

        if (ImageExtensions.Contains(extension))
        {
            return MediaCategory.Image;
        }

        if (AudioExtensions.Contains(extension))
        {
            return MediaCategory.Audio;
        }

        if (VideoExtensions.Contains(extension))
        {
            return MediaCategory.Video;
        }

        if (ArchiveExtensions.Contains(extension))
        {
            return MediaCategory.Archive;
        }

        if (TextExtensions.Contains(extension))
        {
            return MediaCategory.Code;
        }

        return MediaCategory.Other;
    }

    public static bool IsPreviewableAsText(string extension) =>
        TextExtensions.Contains(extension) ||
        string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase);
}
