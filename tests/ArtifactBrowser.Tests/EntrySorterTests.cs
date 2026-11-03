using ArtifactBrowser.Client.Services;
using ArtifactBrowser.Client.Utilities;

namespace ArtifactBrowser.Tests;

public sealed class EntrySorterTests
{
    private sealed record Item(string Name, bool IsDirectory, DateTimeOffset Modified, long? Size = null, string Extension = "");

    private static List<Item> Sort(IEnumerable<Item> items, SortField field, bool descending) =>
        EntrySorter.Sort(
            items,
            item => item.IsDirectory,
            item => item.Name,
            item => item.Modified,
            item => item.Size,
            item => item.Extension,
            field,
            descending);

    [Fact]
    public void Sort_Name_OrdersVersionsNaturallyWithFoldersFirst()
    {
        var items = new[]
        {
            new Item("v2.10.0", true, default),
            new Item("readme.txt", false, default),
            new Item("v2.2.0", true, default),
            new Item("v2.9.0", true, default),
        };

        var sorted = Sort(items, SortField.Name, descending: false);

        Assert.Equal(new[] { "v2.2.0", "v2.9.0", "v2.10.0", "readme.txt" }, sorted.Select(item => item.Name));
    }

    [Fact]
    public void Sort_NameDescending_ReversesWithinFoldersAndFiles()
    {
        var items = new[]
        {
            new Item("v2.10.0", true, default),
            new Item("b.txt", false, default),
            new Item("v2.2.0", true, default),
            new Item("a.txt", false, default),
        };

        var sorted = Sort(items, SortField.Name, descending: true);

        Assert.Equal(new[] { "v2.10.0", "v2.2.0", "b.txt", "a.txt" }, sorted.Select(item => item.Name));
    }

    [Fact]
    public void Sort_Modified_UsesNameAsTieBreak()
    {
        var same = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var later = same.AddDays(1);
        var items = new[]
        {
            new Item("b", true, same),
            new Item("c", true, later),
            new Item("a", true, same),
        };

        var sorted = Sort(items, SortField.Modified, descending: false);

        Assert.Equal(new[] { "a", "b", "c" }, sorted.Select(item => item.Name));
    }

    [Fact]
    public void Sort_SizeAndType_FallBackToNaturalNameForFolders()
    {
        var items = new[]
        {
            new Item("v2.10.0", true, default),
            new Item("v2.2.0", true, default),
            new Item("b.txt", false, default, Size: 10, Extension: ".txt"),
            new Item("a.zip", false, default, Size: 5, Extension: ".zip"),
        };

        var bySize = Sort(items, SortField.Size, descending: false);
        var byType = Sort(items, SortField.Type, descending: false);

        Assert.Equal(new[] { "v2.2.0", "v2.10.0", "a.zip", "b.txt" }, bySize.Select(item => item.Name));
        Assert.Equal(new[] { "v2.2.0", "v2.10.0", "b.txt", "a.zip" }, byType.Select(item => item.Name));
    }
}
