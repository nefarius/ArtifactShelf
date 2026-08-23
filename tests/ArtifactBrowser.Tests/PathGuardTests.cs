using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;

namespace ArtifactBrowser.Tests;

public sealed class PathGuardTests : IDisposable
{
    private readonly TempContentRoot _root = new();
    private readonly PathGuard _guard;

    public PathGuardTests()
    {
        _guard = new PathGuard(_root.CreateOptions());
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsContentRoot()
    {
        var resolved = _guard.Resolve(string.Empty);

        Assert.Equal(_root.ContentRoot, resolved.PhysicalPath);
        Assert.Equal(string.Empty, resolved.VirtualPath);
    }

    [Fact]
    public void Resolve_ValidNestedPath_ReturnsConfinedPhysicalPath()
    {
        var resolved = _guard.Resolve("docs/notes.txt");

        Assert.Equal(Path.Combine(_root.ContentRoot, "docs", "notes.txt"), resolved.PhysicalPath);
        Assert.Equal("docs/notes.txt", resolved.VirtualPath);
        Assert.Equal("docs", resolved.ParentVirtualPath);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("docs/../../secret")]
    [InlineData("docs/..")]
    [InlineData("..")]
    public void Resolve_TraversalAttempt_ThrowsPathAccessDenied(string maliciousPath)
    {
        Assert.Throws<PathAccessDeniedException>(() => _guard.Resolve(maliciousPath));
    }

    [Theory]
    [InlineData(".hidden-file.txt")]
    [InlineData(".hidden-dir")]
    [InlineData(".hidden-dir/secret.txt")]
    public void Resolve_HiddenSegment_ThrowsPathAccessDenied(string hiddenPath)
    {
        Assert.Throws<PathAccessDeniedException>(() => _guard.Resolve(hiddenPath));
    }

    [Theory]
    [InlineData(".hidden-file.txt")]
    [InlineData(".hidden-dir/secret.txt")]
    public void Resolve_AllowHidden_HiddenFile_Succeeds(string hiddenPath)
    {
        var resolved = _guard.Resolve(hiddenPath, allowHidden: true);

        Assert.Equal(Path.Combine(_root.ContentRoot, hiddenPath.Replace('/', Path.DirectorySeparatorChar)), resolved.PhysicalPath);
        Assert.Equal(hiddenPath, resolved.VirtualPath);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("docs/../../secret")]
    [InlineData("..")]
    public void Resolve_AllowHidden_TraversalAttempt_StillThrows(string maliciousPath)
    {
        Assert.Throws<PathAccessDeniedException>(() => _guard.Resolve(maliciousPath, allowHidden: true));
    }

    [Fact]
    public void Resolve_NullByteInSegment_ThrowsPathAccessDenied()
    {
        Assert.Throws<PathAccessDeniedException>(() => _guard.Resolve("docs/foo\0bar"));
    }

    [Fact]
    public void Resolve_SymlinkEscapingRoot_ThrowsPathAccessDenied()
    {
        var outsideDir = Directory.CreateTempSubdirectory("artifact-browser-outside-");
        try
        {
            var linkPath = Path.Combine(_root.ContentRoot, "escape-link");
            try
            {
                Directory.CreateSymbolicLink(linkPath, outsideDir.FullName);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                // Symlink creation requires elevated privileges on some CI/Windows configurations;
                // skip rather than fail the whole suite when the platform disallows it.
                return;
            }

            Assert.Throws<PathAccessDeniedException>(() => _guard.Resolve("escape-link/anything"));
        }
        finally
        {
            Directory.Delete(outsideDir.FullName, recursive: true);
        }
    }

    public void Dispose() => _root.Dispose();
}
