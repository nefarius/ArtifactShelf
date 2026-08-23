using System.IO.Compression;
using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;

namespace ArtifactBrowser.Tests;

public sealed class ZipStreamerTests : IDisposable
{
    private readonly TempContentRoot _root = new();

    [Fact]
    public async Task WriteZipAsync_IncludesSelectedFilesAndFolders()
    {
        var options = _root.CreateOptions();
        var streamer = new ZipStreamer(new PathGuard(options), options);

        using var destination = new MemoryStream();
        await streamer.WriteZipAsync(destination, new[] { "README.md", "docs" }, CancellationToken.None);

        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read);

        Assert.Contains(archive.Entries, e => e.Name == "README.md");
        Assert.Contains(archive.Entries, e => e.FullName.Replace('\\', '/') == "docs/notes.txt");
    }

    [Fact]
    public async Task WriteZipAsync_EntryLimitExceeded_ThrowsArchiveLimitExceeded()
    {
        var options = _root.CreateOptions(o => o.MaxArchiveEntries = 1);
        var streamer = new ZipStreamer(new PathGuard(options), options);

        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArchiveLimitExceededException>(() =>
            streamer.WriteZipAsync(destination, new[] { "docs" }, CancellationToken.None));
    }

    [Fact]
    public async Task WriteZipAsync_SizeLimitExceeded_ThrowsArchiveLimitExceeded()
    {
        var options = _root.CreateOptions(o => o.MaxArchiveBytes = 1);
        var streamer = new ZipStreamer(new PathGuard(options), options);

        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArchiveLimitExceededException>(() =>
            streamer.WriteZipAsync(destination, new[] { "README.md" }, CancellationToken.None));
    }

    public void Dispose() => _root.Dispose();
}
