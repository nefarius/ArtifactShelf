using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Features.Files;
using ArtifactBrowser.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;

namespace ArtifactBrowser.Tests;

public sealed class FileSystemBrowserTests : IDisposable
{
    private readonly TempContentRoot _root = new();
    private readonly FileSystemBrowser _browser;

    public FileSystemBrowserTests()
    {
        var options = _root.CreateOptions();
        var guard = new PathGuard(options);
        _browser = new FileSystemBrowser(guard, options, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public void ListDirectory_RootPath_ExcludesHiddenEntries()
    {
        var listing = _browser.ListDirectory(string.Empty);

        Assert.Contains(listing.Entries, e => e.Name == "docs");
        Assert.Contains(listing.Entries, e => e.Name == "builds");
        Assert.Contains(listing.Entries, e => e.Name == "README.md");
        Assert.DoesNotContain(listing.Entries, e => e.Name.StartsWith('.'));
    }

    [Fact]
    public void ListDirectory_UnknownPath_ThrowsDirectoryNotFound()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _browser.ListDirectory("does-not-exist"));
    }

    [Fact]
    public void ListDirectory_MarksMarkdownFilesWithMarkdownCategory()
    {
        var listing = _browser.ListDirectory(string.Empty);

        var readme = Assert.Single(listing.Entries, e => e.Name == "README.md");
        Assert.Equal(MediaCategory.Markdown, readme.MediaCategory);
        Assert.False(readme.IsDirectory);
    }

    [Fact]
    public void GetTreeNode_OnlyIncludesDirectories()
    {
        var tree = _browser.GetTreeNode(string.Empty);

        Assert.All(tree.Children, c => Assert.False(c.Name.EndsWith(".md") || c.Name.EndsWith(".txt")));
        Assert.Contains(tree.Children, c => c.Name == "docs");
        Assert.Contains(tree.Children, c => c.Name == "builds");
    }

    [Fact]
    public void Search_Recursive_FindsNestedMatches()
    {
        var result = _browser.Search(string.Empty, "build.log", recursive: true, CancellationToken.None);

        Assert.Contains(result.Results, r => r.Path == "builds/v1/build.log");
    }

    [Fact]
    public void Search_NonRecursive_DoesNotDescendIntoSubfolders()
    {
        var result = _browser.Search(string.Empty, "build.log", recursive: false, CancellationToken.None);

        Assert.DoesNotContain(result.Results, r => r.Path == "builds/v1/build.log");
    }

    [Fact]
    public void Search_ExcludesHiddenFiles()
    {
        var result = _browser.Search(string.Empty, "hidden", recursive: true, CancellationToken.None);

        Assert.Empty(result.Results);
    }

    public void Dispose() => _root.Dispose();
}
