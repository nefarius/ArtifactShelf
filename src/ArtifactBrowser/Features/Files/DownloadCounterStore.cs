using System.Threading.Channels;
using ArtifactBrowser.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Features.Files;

/// <summary>
/// Persists per-path download counts in SQLite under <see cref="ArtifactBrowserOptions.CacheRoot"/>.
/// Request threads only perform a non-blocking <c>TryWrite</c>; a single background worker
/// coalesces events and commits batched UPSERTs. Storage failures never escape to callers.
/// </summary>
public sealed class DownloadCounterStore : BackgroundService, IDownloadCounter, IDisposable
{
    internal const int DefaultQueueCapacity = 4096;
    internal const string DatabaseFileName = "download-counts.db";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly ILogger<DownloadCounterStore> _logger;
    private readonly string _cacheRoot;
    private readonly StringComparer _pathComparer;
    private readonly Channel<string> _channel;
    private readonly object _dbLock = new();
    private readonly object _flushLock = new();
    private readonly List<TaskCompletionSource> _flushWaiters = new();

    private SqliteConnection? _connection;
    private int _outstanding;
    private bool _persisting;
    private bool _started;
    private bool _disposed;

    public DownloadCounterStore(IOptions<ArtifactBrowserOptions> options, ILogger<DownloadCounterStore> logger)
        : this(options, logger, DefaultQueueCapacity)
    {
    }

    internal DownloadCounterStore(IOptions<ArtifactBrowserOptions> options, ILogger<DownloadCounterStore> logger, int queueCapacity)
    {
        _logger = logger;
        _cacheRoot = Path.GetFullPath(options.Value.CacheRoot);
        _pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(1, queueCapacity))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
            AllowSynchronousContinuations = false,
        });
    }

    public void RecordDownload(string virtualPath)
    {
        try
        {
            var key = ToStorageKey(virtualPath);
            if (key.Length == 0)
            {
                return;
            }

            if (_channel.Writer.TryWrite(key))
            {
                Interlocked.Increment(ref _outstanding);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dropped a download-count event for {Path}.", virtualPath);
        }
    }

    public IReadOnlyDictionary<string, long> GetCounts(IReadOnlyList<string> virtualPaths)
    {
        var result = new Dictionary<string, long>(virtualPaths.Count);
        foreach (var path in virtualPaths)
        {
            result[path] = 0;
        }

        if (virtualPaths.Count == 0)
        {
            return result;
        }

        try
        {
            if (!EnsureInitialized())
            {
                return result;
            }

            var lookup = new Dictionary<string, List<string>>(_pathComparer);
            foreach (var path in virtualPaths)
            {
                var key = ToStorageKey(path);
                if (key.Length == 0)
                {
                    continue;
                }

                if (!lookup.TryGetValue(key, out var aliases))
                {
                    aliases = new List<string>();
                    lookup[key] = aliases;
                }

                aliases.Add(path);
            }

            if (lookup.Count == 0)
            {
                return result;
            }

            foreach (var chunk in lookup.Keys.Chunk(400))
            {
                ReadChunk(chunk, lookup, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read download counts; returning zeros.");
        }

        return result;
    }

    /// <summary>
    /// Waits until the currently queued events have been processed at least once.
    /// Completes even when persistence fails so callers cannot hang on a broken store.
    /// </summary>
    internal Task FlushAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource tcs;
        lock (_flushLock)
        {
            if (_disposed || !_started || (!_persisting && Volatile.Read(ref _outstanding) == 0))
            {
                return Task.CompletedTask;
            }

            tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _flushWaiters.Add(tcs);
        }

        return tcs.Task.WaitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pending = new Dictionary<string, long>(_pathComparer);
        lock (_flushLock)
        {
            _started = true;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!EnsureInitialized())
                    {
                        CompleteFlushWaiters();
                        await Task.Delay(RetryDelay, stoppingToken);
                        continue;
                    }

                    if (pending.Count == 0)
                    {
                        if (!await _channel.Reader.WaitToReadAsync(stoppingToken))
                        {
                            break;
                        }
                    }

                    DrainInto(pending);
                    if (pending.Count == 0)
                    {
                        CompleteFlushWaiters();
                        continue;
                    }

                    if (TryPersist(pending))
                    {
                        var persisted = pending.Values.Sum();
                        pending.Clear();
                        Interlocked.Add(ref _outstanding, (int)-persisted);
                        CompleteFlushWaitersIfIdle();
                        continue;
                    }

                    CompleteFlushWaiters();
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Download-count worker encountered an unexpected error.");
                    CompleteFlushWaiters();
                    try
                    {
                        await Task.Delay(RetryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            DrainInto(pending);
            if (pending.Count > 0 && TryPersist(pending))
            {
                Interlocked.Add(ref _outstanding, (int)-pending.Values.Sum());
            }

            CompleteFlushWaiters();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
        DisposeConnection();
    }

    public override void Dispose()
    {
        _disposed = true;
        _channel.Writer.TryComplete();
        DisposeConnection();
        base.Dispose();
    }

    private void ReadChunk(
        string[] keys,
        Dictionary<string, List<string>> lookup,
        Dictionary<string, long> result)
    {
        lock (_dbLock)
        {
            if (_connection is null)
            {
                return;
            }

            using var command = _connection.CreateCommand();
            var names = new string[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                names[i] = $"$p{i}";
                command.Parameters.AddWithValue(names[i], keys[i]);
            }

            command.CommandText = $"SELECT path, count FROM download_counts WHERE path IN ({string.Join(", ", names)})";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var storedPath = reader.GetString(0);
                var count = reader.GetInt64(1);
                if (!lookup.TryGetValue(storedPath, out var aliases))
                {
                    continue;
                }

                foreach (var alias in aliases)
                {
                    result[alias] = count;
                }
            }
        }
    }

    private void DrainInto(Dictionary<string, long> batch)
    {
        while (_channel.Reader.TryRead(out var key))
        {
            batch[key] = batch.TryGetValue(key, out var current) ? current + 1 : 1;
        }
    }

    private bool TryPersist(Dictionary<string, long> batch)
    {
        lock (_flushLock)
        {
            _persisting = true;
        }

        try
        {
            if (!EnsureInitialized() || _connection is null)
            {
                return false;
            }

            lock (_dbLock)
            {
                using var transaction = _connection.BeginTransaction();
                using var command = _connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO download_counts(path, count) VALUES ($path, $delta)
                    ON CONFLICT(path) DO UPDATE SET count = count + excluded.count;
                    """;
                var pathParam = command.Parameters.Add("$path", SqliteType.Text);
                var deltaParam = command.Parameters.Add("$delta", SqliteType.Integer);

                foreach (var (path, delta) in batch)
                {
                    pathParam.Value = path;
                    deltaParam.Value = delta;
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist {Count} download-count updates; will retry.", batch.Count);
            DisposeConnection();
            return false;
        }
        finally
        {
            lock (_flushLock)
            {
                _persisting = false;
            }
        }
    }

    private bool EnsureInitialized()
    {
        lock (_dbLock)
        {
            if (_connection is { State: System.Data.ConnectionState.Open })
            {
                return true;
            }

            DisposeConnectionAlreadyLocked();

            try
            {
                Directory.CreateDirectory(_cacheRoot);
                var path = Path.Combine(_cacheRoot, DatabaseFileName);
                var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared,
                }.ToString());
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        """
                        PRAGMA journal_mode = WAL;
                        PRAGMA synchronous = NORMAL;
                        PRAGMA busy_timeout = 250;
                        CREATE TABLE IF NOT EXISTS download_counts (
                            path TEXT NOT NULL PRIMARY KEY,
                            count INTEGER NOT NULL CHECK (count >= 0)
                        );
                        """;
                    command.ExecuteNonQuery();
                }

                _connection = connection;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to initialize download-count store under {CacheRoot}.", _cacheRoot);
                return false;
            }
        }
    }

    private void CompleteFlushWaitersIfIdle()
    {
        lock (_flushLock)
        {
            if (_persisting || Volatile.Read(ref _outstanding) > 0)
            {
                return;
            }
        }

        CompleteFlushWaiters();
    }

    private void CompleteFlushWaiters()
    {
        List<TaskCompletionSource> waiters;
        lock (_flushLock)
        {
            if (_flushWaiters.Count == 0)
            {
                return;
            }

            waiters = _flushWaiters.ToList();
            _flushWaiters.Clear();
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetResult();
        }
    }

    private void DisposeConnection()
    {
        lock (_dbLock)
        {
            DisposeConnectionAlreadyLocked();
        }
    }

    private void DisposeConnectionAlreadyLocked()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            _connection.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing download-count database connection.");
        }

        _connection = null;
    }

    internal static string ToStorageKey(string? virtualPath)
    {
        var normalized = PathGuard.NormalizeVirtualPath(virtualPath);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        return OperatingSystem.IsWindows() ? normalized.ToLowerInvariant() : normalized;
    }
}
