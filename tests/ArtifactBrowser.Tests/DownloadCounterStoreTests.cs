using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtifactBrowser.Tests;

public sealed class DownloadCounterStoreTests : IDisposable
{
    private readonly TempContentRoot _root = new();

    [Fact]
    public async Task RecordDownload_PersistsAndCoalescesIncrements()
    {
        await using var store = CreateStartedStore();

        store.RecordDownload("docs/notes.txt");
        store.RecordDownload("/docs/notes.txt");
        store.RecordDownload("docs\\notes.txt");
        await store.FlushAsync();

        var counts = store.GetCounts(new[] { "docs/notes.txt" });
        Assert.Equal(3, counts["docs/notes.txt"]);
    }

    [Fact]
    public async Task GetCounts_ReturnsZeroForUnknownAndBatchesKnownPaths()
    {
        await using var store = CreateStartedStore();

        store.RecordDownload("README.md");
        store.RecordDownload("docs/notes.txt");
        await store.FlushAsync();

        var counts = store.GetCounts(new[] { "README.md", "docs/notes.txt", "missing.bin" });
        Assert.Equal(1, counts["README.md"]);
        Assert.Equal(1, counts["docs/notes.txt"]);
        Assert.Equal(0, counts["missing.bin"]);
    }

    [Fact]
    public async Task Counts_SurviveStoreRestart()
    {
        var first = CreateStore();
        await first.StartAsync(CancellationToken.None);
        first.RecordDownload("README.md");
        await first.FlushAsync();
        await first.StopAsync(CancellationToken.None);
        first.Dispose();

        var second = CreateStore();
        await second.StartAsync(CancellationToken.None);
        try
        {
            var counts = second.GetCounts(new[] { "README.md" });
            Assert.Equal(1, counts["README.md"]);
        }
        finally
        {
            await second.StopAsync(CancellationToken.None);
            second.Dispose();
        }
    }

    [Fact]
    public void RecordDownload_WhenQueueIsFull_DoesNotThrowOrBlock()
    {
        using var store = CreateStore(queueCapacity: 1);

        store.RecordDownload("first.txt");
        store.RecordDownload("dropped.txt");
        store.RecordDownload("also-dropped.txt");

        var counts = store.GetCounts(new[] { "first.txt", "dropped.txt" });
        Assert.Equal(0, counts["first.txt"]);
        Assert.Equal(0, counts["dropped.txt"]);
    }

    [Fact]
    public async Task RecordDownload_WhenPersistenceUnavailable_DoesNotThrow()
    {
        var blocked = Path.Combine(_root.CacheRoot, "not-a-directory");
        File.WriteAllText(blocked, "cannot-be-a-cache-root");
        var options = _root.CreateOptions(o => o.CacheRoot = blocked);
        using var store = new DownloadCounterStore(options, NullLogger<DownloadCounterStore>.Instance);

        store.RecordDownload("README.md");
        await store.StartAsync(CancellationToken.None);
        await store.FlushAsync();

        var counts = store.GetCounts(new[] { "README.md" });
        Assert.Equal(0, counts["README.md"]);

        await store.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void GetCounts_WhenPersistenceUnavailable_ReturnsZeros()
    {
        var blocked = Path.Combine(_root.CacheRoot, "blocked-file");
        File.WriteAllText(blocked, "nope");
        var options = _root.CreateOptions(o => o.CacheRoot = blocked);
        using var store = new DownloadCounterStore(options, NullLogger<DownloadCounterStore>.Instance);

        var counts = store.GetCounts(new[] { "README.md", "docs/notes.txt" });
        Assert.Equal(0, counts["README.md"]);
        Assert.Equal(0, counts["docs/notes.txt"]);
    }

    [Fact]
    public void ToStorageKey_NormalizesSeparatorsAndTrimsSlashes()
    {
        Assert.Equal(
            DownloadCounterStore.ToStorageKey("docs/notes.txt"),
            DownloadCounterStore.ToStorageKey("/docs\\notes.txt/"));
    }

    [Fact]
    public void ToStorageKey_UsesPlatformCaseRules()
    {
        var lower = DownloadCounterStore.ToStorageKey("README.md");
        var upper = DownloadCounterStore.ToStorageKey("readme.md");

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(lower, upper);
        }
        else
        {
            Assert.NotEqual(lower, upper);
        }
    }

    public void Dispose() => _root.Dispose();

    private DownloadCounterStore CreateStore(int queueCapacity = DownloadCounterStore.DefaultQueueCapacity) =>
        new(_root.CreateOptions(), NullLogger<DownloadCounterStore>.Instance, queueCapacity);

    private StartedStore CreateStartedStore() => new(CreateStore());

    private sealed class StartedStore : IAsyncDisposable
    {
        public StartedStore(DownloadCounterStore store)
        {
            Store = store;
            store.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public DownloadCounterStore Store { get; }

        public void RecordDownload(string path) => Store.RecordDownload(path);

        public Task FlushAsync() => Store.FlushAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        public IReadOnlyDictionary<string, long> GetCounts(IReadOnlyList<string> paths) => Store.GetCounts(paths);

        public async ValueTask DisposeAsync()
        {
            await Store.StopAsync(CancellationToken.None);
            Store.Dispose();
        }
    }
}
