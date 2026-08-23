using System.IO.Compression;
using ArtifactBrowser.Options;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Features.Files;

public sealed class ArchiveLimitExceededException(string message) : Exception(message);

/// <summary>Mutable running totals shared across the recursive archive-building calls.</summary>
internal sealed class ArchiveTally
{
    public int EntryCount { get; set; }

    public long TotalBytes { get; set; }
}

/// <summary>
/// Streams a ZIP archive of one or more selected files/folders directly to the response body
/// without buffering to disk or writing into the read-only artifact tree. Enforces entry-count
/// and total-byte limits so a single request cannot exhaust disk, memory, or CPU.
/// </summary>
public sealed class ZipStreamer(PathGuard pathGuard, IOptions<ArtifactBrowserOptions> options)
{
    private readonly ArtifactBrowserOptions _options = options.Value;
    private readonly SemaphoreSlim _concurrencyLimiter = new(Math.Max(1, options.Value.MaxConcurrentZipJobs));

    public async Task WriteZipAsync(Stream destination, IReadOnlyList<string> virtualPaths, CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
            var tally = new ArchiveTally();
            var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var virtualPath in virtualPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var resolved = pathGuard.Resolve(virtualPath);

                if (!seenRoots.Add(resolved.VirtualPath))
                {
                    continue;
                }

                if (Directory.Exists(resolved.PhysicalPath))
                {
                    await AddDirectoryAsync(archive, resolved.PhysicalPath, tally, cancellationToken);
                }
                else if (File.Exists(resolved.PhysicalPath))
                {
                    await AddFileAsync(archive, resolved.PhysicalPath, Path.GetFileName(resolved.PhysicalPath), tally, cancellationToken);
                }
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task AddDirectoryAsync(ZipArchive archive, string physicalDir, ArchiveTally tally, CancellationToken cancellationToken)
    {
        var baseName = Path.GetFileName(physicalDir.TrimEnd(Path.DirectorySeparatorChar));
        var pending = new Stack<string>();
        pending.Push(physicalDir);

        while (pending.Count > 0)
        {
            var currentDir = pending.Pop();
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(currentDir).EnumerateFileSystemInfos("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                });
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            try
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry is DirectoryInfo directory)
                    {
                        if (directory.LinkTarget is not null
                            || directory.Attributes.HasFlag(FileAttributes.ReparsePoint)
                            || pathGuard.IsHiddenName(directory.Name))
                        {
                            continue;
                        }

                        pending.Push(directory.FullName);
                        continue;
                    }

                    if (entry is not FileInfo fileInfo || fileInfo.LinkTarget is not null)
                    {
                        continue; // Do not follow symlinked files into the archive.
                    }

                    var relative = Path.GetRelativePath(physicalDir, fileInfo.FullName).Replace(Path.DirectorySeparatorChar, '/');
                    var segments = relative.Split('/');
                    if (segments.Any(pathGuard.IsHiddenName))
                    {
                        continue;
                    }

                    await AddFileAsync(archive, fileInfo.FullName, $"{baseName}/{relative}", tally, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Enumeration can throw as the lazy enumerator advances; skip this directory.
            }
        }
    }

    private async Task AddFileAsync(ZipArchive archive, string physicalPath, string entryName, ArchiveTally tally, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(physicalPath);
        if (!fileInfo.Exists)
        {
            return;
        }

        tally.EntryCount++;
        if (tally.EntryCount > _options.MaxArchiveEntries)
        {
            throw new ArchiveLimitExceededException($"Archive entry limit of {_options.MaxArchiveEntries} exceeded.");
        }

        tally.TotalBytes += fileInfo.Length;
        if (tally.TotalBytes > _options.MaxArchiveBytes)
        {
            throw new ArchiveLimitExceededException($"Archive size limit of {_options.MaxArchiveBytes} bytes exceeded.");
        }

        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        entry.LastWriteTime = fileInfo.LastWriteTime;

        await using var entryStream = entry.Open();
        await using var fileStream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        await fileStream.CopyToAsync(entryStream, cancellationToken);
    }
}
