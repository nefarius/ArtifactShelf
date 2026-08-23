using System.Text.RegularExpressions;
using ArtifactBrowser.Options;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Thrown when a requested virtual path cannot be resolved safely within the configured content root.
/// </summary>
public sealed class PathAccessDeniedException(string message) : Exception(message);

/// <summary>
/// Resolves virtual (client-facing) paths to confined, canonical filesystem paths under the
/// configured content root. Rejects traversal attempts, symlink escapes, and hidden entries.
/// </summary>
public sealed class PathGuard
{
    private readonly string _root;
    private readonly List<Regex> _hiddenPatterns;

    public PathGuard(IOptions<ArtifactBrowserOptions> options)
    {
        var configured = options.Value.ContentRoot;
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        _hiddenPatterns = options.Value.HiddenPatterns
            .Select(GlobToRegex)
            .ToList();
    }

    public string ContentRoot => _root;

    private static Regex GlobToRegex(string glob)
    {
        var escaped = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(escaped, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public bool IsHiddenName(string name) => _hiddenPatterns.Any(p => p.IsMatch(name));

    /// <summary>
    /// Normalizes an incoming virtual path (as sent by the client, '/'-separated, no leading slash)
    /// into a list of path segments, rejecting empty segments, hidden entries, and traversal tokens.
    /// </summary>
    public static string NormalizeVirtualPath(string? virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath))
        {
            return string.Empty;
        }

        return virtualPath.Trim().Trim('/').Replace('\\', '/');
    }

    private static string[] SplitSegments(string virtualPath)
    {
        var normalized = NormalizeVirtualPath(virtualPath);
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Resolves the given virtual path to a real, confined physical path. Throws
    /// <see cref="PathAccessDeniedException"/> if the path escapes the content root, contains a
    /// hidden segment, or traverses a symlink that points outside the root.
    /// </summary>
    public ResolvedPath Resolve(string? virtualPath)
    {
        var segments = SplitSegments(virtualPath ?? string.Empty);

        var current = _root;
        foreach (var rawSegment in segments)
        {
            if (rawSegment is "." or ".." || rawSegment.Contains('\0'))
            {
                throw new PathAccessDeniedException("Path traversal is not allowed.");
            }

            if (IsHiddenName(rawSegment))
            {
                throw new PathAccessDeniedException("Path contains a hidden segment.");
            }

            current = Path.Combine(current, rawSegment);
        }

        var fullPath = Path.GetFullPath(current);
        if (!IsWithinRoot(fullPath))
        {
            throw new PathAccessDeniedException("Resolved path escapes the content root.");
        }

        EnsureNoSymlinkEscape(fullPath);

        var normalizedVirtual = string.Join('/', segments);
        return new ResolvedPath(fullPath, normalizedVirtual, segments.Length == 0 ? null : string.Join('/', segments[..^1]));
    }

    public bool IsWithinRoot(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        if (relative is "." or "")
        {
            return true;
        }

        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        return !string.Equals(relative, "..", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Walks every directory component between the root and the resolved path, following symlinks
    /// (reparse points) and rejecting any that resolve outside the content root.
    /// </summary>
    private void EnsureNoSymlinkEscape(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        if (relative == ".")
        {
            return;
        }

        var current = _root;
        var parts = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);

            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);

            if (!info.Exists)
            {
                // Non-existent entries are handled by callers (404); nothing further to validate.
                return;
            }

            if (info.LinkTarget is not null)
            {
                var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
                var resolvedFullPath = resolved is null ? current : Path.GetFullPath(resolved.FullName);
                if (!IsWithinRoot(resolvedFullPath))
                {
                    throw new PathAccessDeniedException("Path traverses a symlink that escapes the content root.");
                }
            }
        }
    }
}

public sealed record ResolvedPath(string PhysicalPath, string VirtualPath, string? ParentVirtualPath);
