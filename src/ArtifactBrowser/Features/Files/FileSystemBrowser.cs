using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Read-only, hardened access to directory listings, the sidebar tree, and recursive search.
/// All results are derived from the confined path resolved by <see cref="PathGuard"/>.
/// </summary>
public sealed class FileSystemBrowser(PathGuard pathGuard, IOptions<ArtifactBrowserOptions> options, IMemoryCache cache)
{
    private readonly ArtifactBrowserOptions _options = options.Value;

    public DirectoryListingDto ListDirectory(string? virtualPath)
    {
        var resolved = pathGuard.Resolve(virtualPath);

        var cacheKey = $"list:{resolved.VirtualPath}";
        if (cache.TryGetValue<DirectoryListingDto>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!Directory.Exists(resolved.PhysicalPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {resolved.VirtualPath}");
        }

        var entries = new List<FileEntryDto>();
        var truncated = false;

        var dirInfo = new DirectoryInfo(resolved.PhysicalPath);
        foreach (var entry in EnumerateSafely(dirInfo))
        {
            if (pathGuard.IsHiddenName(entry.Name))
            {
                continue;
            }

            if (entries.Count >= _options.MaxDirectoryEntries)
            {
                truncated = true;
                break;
            }

            entries.Add(ToDto(entry, resolved.VirtualPath));
        }

        var dto = new DirectoryListingDto
        {
            Path = resolved.VirtualPath,
            ParentPath = resolved.ParentVirtualPath,
            Entries = entries,
            Truncated = truncated,
        };

        cache.Set(cacheKey, dto, TimeSpan.FromSeconds(Math.Max(0, _options.DirectoryListingCacheSeconds)));
        return dto;
    }

    public TreeNodeDto GetTreeNode(string? virtualPath)
    {
        var resolved = pathGuard.Resolve(virtualPath);
        if (!Directory.Exists(resolved.PhysicalPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {resolved.VirtualPath}");
        }

        var children = new List<TreeNodeDto>();
        var dirInfo = new DirectoryInfo(resolved.PhysicalPath);
        foreach (var entry in EnumerateSafely(dirInfo))
        {
            if (entry is not DirectoryInfo di || pathGuard.IsHiddenName(entry.Name))
            {
                continue;
            }

            var childVirtual = resolved.VirtualPath.Length == 0 ? entry.Name : $"{resolved.VirtualPath}/{entry.Name}";
            children.Add(new TreeNodeDto
            {
                Name = entry.Name,
                Path = childVirtual,
                HasChildren = HasAnySubdirectory(di),
            });
        }

        return new TreeNodeDto
        {
            Name = resolved.VirtualPath.Length == 0 ? "/" : Path.GetFileName(resolved.VirtualPath),
            Path = resolved.VirtualPath,
            HasChildren = children.Count > 0,
            Children = children.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private bool HasAnySubdirectory(DirectoryInfo dir)
    {
        try
        {
            foreach (var sub in dir.EnumerateDirectories())
            {
                if (!pathGuard.IsHiddenName(sub.Name) && !IsSymlink(sub))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }

        return false;
    }

    public SearchResponseDto Search(string? virtualPath, string query, bool recursive, CancellationToken cancellationToken)
    {
        var resolved = pathGuard.Resolve(virtualPath);
        if (!Directory.Exists(resolved.PhysicalPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {resolved.VirtualPath}");
        }

        var results = new List<SearchResultDto>();
        var truncated = false;

        void Walk(DirectoryInfo dir, string virtualDir, int depth)
        {
            if (truncated || depth > _options.MaxSearchDepth)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var entry in EnumerateSafely(dir))
            {
                if (pathGuard.IsHiddenName(entry.Name))
                {
                    continue;
                }

                if (results.Count >= _options.MaxSearchResults)
                {
                    truncated = true;
                    return;
                }

                var childVirtual = virtualDir.Length == 0 ? entry.Name : $"{virtualDir}/{entry.Name}";

                if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var dto = ToDto(entry, virtualDir);
                    results.Add(new SearchResultDto
                    {
                        Name = dto.Name,
                        Path = childVirtual,
                        Kind = dto.Kind,
                        Size = dto.Size,
                        Modified = dto.Modified,
                        Extension = dto.Extension,
                        MediaCategory = dto.MediaCategory,
                        ParentPath = virtualDir,
                    });
                }

                if (recursive && entry is DirectoryInfo subDir)
                {
                    Walk(subDir, childVirtual, depth + 1);
                }
            }
        }

        Walk(new DirectoryInfo(resolved.PhysicalPath), resolved.VirtualPath, 0);

        return new SearchResponseDto { Results = results, Truncated = truncated };
    }

    private static bool IsSymlink(FileSystemInfo info) => info.LinkTarget is not null;

    private IEnumerable<FileSystemInfo> EnumerateSafely(DirectoryInfo dir)
    {
        IEnumerable<FileSystemInfo> items;
        try
        {
            items = dir.EnumerateFileSystemInfos();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Enumerable.Empty<FileSystemInfo>();
        }

        // Materialize while swallowing per-entry errors (e.g. broken symlinks) and skip symlinks
        // whose target cannot be safely validated.
        var safe = new List<FileSystemInfo>();
        foreach (var item in items)
        {
            try
            {
                if (item.LinkTarget is not null)
                {
                    var resolvedTarget = item.ResolveLinkTarget(returnFinalTarget: true);
                    if (resolvedTarget is null)
                    {
                        continue;
                    }

                    var fullTarget = Path.GetFullPath(resolvedTarget.FullName);
                    var rootWithSep = pathGuard.ContentRoot + Path.DirectorySeparatorChar;
                    if (!fullTarget.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(fullTarget, pathGuard.ContentRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                safe.Add(item);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Skip unreadable entries.
            }
        }

        return safe;
    }

    private static FileEntryDto ToDto(FileSystemInfo entry, string parentVirtualPath)
    {
        var isDirectory = entry is DirectoryInfo;
        var virtualPath = parentVirtualPath.Length == 0 ? entry.Name : $"{parentVirtualPath}/{entry.Name}";
        var extension = isDirectory ? string.Empty : Path.GetExtension(entry.Name);

        long? size = null;
        DateTimeOffset modified;
        try
        {
            modified = entry.LastWriteTimeUtc;
            if (!isDirectory && entry is FileInfo fi)
            {
                size = fi.Length;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            modified = DateTimeOffset.MinValue;
        }

        return new FileEntryDto
        {
            Name = entry.Name,
            Path = virtualPath,
            Kind = isDirectory ? ArtifactKind.Directory : ArtifactKind.File,
            Size = size,
            Modified = modified,
            Extension = extension,
            MediaCategory = isDirectory ? MediaCategory.Directory : MimeHelper.Categorize(extension),
        };
    }
}
