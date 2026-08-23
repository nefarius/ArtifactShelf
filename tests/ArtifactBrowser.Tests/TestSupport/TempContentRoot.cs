using ArtifactBrowser.Options;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Tests.TestSupport;

/// <summary>
/// Creates a temporary content root + cache root populated with a representative artifact
/// tree, and cleans both up on disposal. Used by unit and integration tests.
/// </summary>
public sealed class TempContentRoot : IDisposable
{
    public string ContentRoot { get; }

    public string CacheRoot { get; }

    public TempContentRoot()
    {
        ContentRoot = Directory.CreateTempSubdirectory("artifact-browser-content-").FullName;
        CacheRoot = Directory.CreateTempSubdirectory("artifact-browser-cache-").FullName;

        Directory.CreateDirectory(Path.Combine(ContentRoot, "docs"));
        Directory.CreateDirectory(Path.Combine(ContentRoot, "builds", "v1"));
        Directory.CreateDirectory(Path.Combine(ContentRoot, ".hidden-dir"));

        File.WriteAllText(Path.Combine(ContentRoot, "README.md"), "# Title\n\nSome **text**.\n");
        File.WriteAllText(Path.Combine(ContentRoot, "docs", "notes.txt"), "hello world\n");
        File.WriteAllText(Path.Combine(ContentRoot, "docs", "file2.txt"), "second\n");
        File.WriteAllText(Path.Combine(ContentRoot, "docs", "file10.txt"), "tenth\n");
        File.WriteAllText(Path.Combine(ContentRoot, "builds", "v1", "build.log"), "build ok\n");
        File.WriteAllText(Path.Combine(ContentRoot, ".hidden-file.txt"), "should not be listed\n");
        File.WriteAllText(Path.Combine(ContentRoot, ".hidden-dir", "secret.txt"), "hidden\n");

        // Colliding names that must not steal reserved application routes.
        Directory.CreateDirectory(Path.Combine(ContentRoot, "api", "files"));
        File.WriteAllText(Path.Combine(ContentRoot, "health"), "collision-health\n");
        File.WriteAllText(Path.Combine(ContentRoot, "api", "files", "raw"), "collision-raw\n");
    }

    public IOptions<ArtifactBrowserOptions> CreateOptions(Action<ArtifactBrowserOptions>? configure = null)
    {
        var options = new ArtifactBrowserOptions
        {
            ContentRoot = ContentRoot,
            CacheRoot = CacheRoot,
        };

        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    public void Dispose()
    {
        TryDelete(ContentRoot);
        TryDelete(CacheRoot);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; ignore files still locked by the OS/AV on Windows CI.
        }
    }
}
