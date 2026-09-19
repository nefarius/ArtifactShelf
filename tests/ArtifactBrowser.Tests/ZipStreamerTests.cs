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
        var streamer = new ZipStreamer(new PathGuard(options), options, NullDownloadCounter.Instance);

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
        var streamer = new ZipStreamer(new PathGuard(options), options, NullDownloadCounter.Instance);

        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArchiveLimitExceededException>(() =>
            streamer.WriteZipAsync(destination, new[] { "docs" }, CancellationToken.None));
    }

    [Fact]
    public async Task WriteZipAsync_SizeLimitExceeded_ThrowsArchiveLimitExceeded()
    {
        var options = _root.CreateOptions(o => o.MaxArchiveBytes = 1);
        var streamer = new ZipStreamer(new PathGuard(options), options, NullDownloadCounter.Instance);

        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<ArchiveLimitExceededException>(() =>
            streamer.WriteZipAsync(destination, new[] { "README.md" }, CancellationToken.None));
    }

    [Fact]
    public async Task WriteZipAsync_RecordsEachIncludedFileWithoutAwaitingCounterIo()
    {
        var options = _root.CreateOptions();
        var counter = new InMemoryDownloadCounter();
        var streamer = new ZipStreamer(new PathGuard(options), options, counter);

        using var destination = new MemoryStream();
        await streamer.WriteZipAsync(destination, new[] { "README.md", "docs" }, CancellationToken.None);

        Assert.Contains("README.md", counter.Recorded);
        Assert.Contains("docs/notes.txt", counter.Recorded);
        Assert.Contains("docs/file2.txt", counter.Recorded);
        Assert.Contains("docs/file10.txt", counter.Recorded);
        Assert.Equal(1, counter.GetCounts(new[] { "README.md" })["README.md"]);
    }

    [Fact]
    public async Task WriteZipAsync_WhenCounterThrows_StillWritesArchive()
    {
        var options = _root.CreateOptions();
        var streamer = new ZipStreamer(new PathGuard(options), options, new ThrowingDownloadCounter());

        using var destination = new MemoryStream();
        await streamer.WriteZipAsync(destination, new[] { "README.md" }, CancellationToken.None);

        destination.Position = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, e => e.Name == "README.md");
    }

    public void Dispose() => _root.Dispose();
}
