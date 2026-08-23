using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;

namespace ArtifactBrowser.Tests;

public sealed class PreviewServiceTests : IDisposable
{
    private readonly TempContentRoot _root = new();

    [Fact]
    public async Task GetPreviewAsync_Markdown_ReturnsMarkdownKindWithContent()
    {
        var options = _root.CreateOptions();
        var service = new PreviewService(new PathGuard(options), options);

        var preview = await service.GetPreviewAsync("README.md", CancellationToken.None);

        Assert.Equal(PreviewKind.Markdown, preview.Kind);
        Assert.Contains("# Title", preview.Content);
        Assert.False(preview.Truncated);
    }

    [Fact]
    public async Task GetPreviewAsync_PlainText_ReturnsTextKind()
    {
        var options = _root.CreateOptions();
        var service = new PreviewService(new PathGuard(options), options);

        var preview = await service.GetPreviewAsync("docs/notes.txt", CancellationToken.None);

        Assert.Equal(PreviewKind.Text, preview.Kind);
        Assert.Contains("hello world", preview.Content);
    }

    [Fact]
    public async Task GetPreviewAsync_ExceedsMaxBytes_ReturnsTooLarge()
    {
        var options = _root.CreateOptions(o => o.MaxTextPreviewBytes = 5);
        var service = new PreviewService(new PathGuard(options), options);

        var preview = await service.GetPreviewAsync("docs/notes.txt", CancellationToken.None);

        Assert.Equal(PreviewKind.TooLarge, preview.Kind);
        Assert.Null(preview.Content);
    }

    [Fact]
    public async Task GetPreviewAsync_UnknownExtension_ReturnsUnsupported()
    {
        var options = _root.CreateOptions();
        var guard = new PathGuard(options);
        var binaryPath = System.IO.Path.Combine(_root.ContentRoot, "data.bin");
        await System.IO.File.WriteAllBytesAsync(binaryPath, new byte[] { 1, 2, 3, 4 });

        var service = new PreviewService(guard, options);
        var preview = await service.GetPreviewAsync("data.bin", CancellationToken.None);

        Assert.Equal(PreviewKind.Unsupported, preview.Kind);
    }

    [Fact]
    public async Task GetPreviewAsync_MissingFile_ThrowsFileNotFound()
    {
        var options = _root.CreateOptions();
        var service = new PreviewService(new PathGuard(options), options);

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.GetPreviewAsync("does-not-exist.txt", CancellationToken.None));
    }

    public void Dispose() => _root.Dispose();
}
