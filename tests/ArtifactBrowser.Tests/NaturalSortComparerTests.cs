using ArtifactBrowser.Client.Utilities;

namespace ArtifactBrowser.Tests;

public sealed class NaturalSortComparerTests
{
    [Fact]
    public void Sort_OrdersNumbersNaturally()
    {
        var input = new[] { "file10.txt", "file2.txt", "file1.txt" };

        Array.Sort(input, NaturalSortComparer.Instance);

        Assert.Equal(new[] { "file1.txt", "file2.txt", "file10.txt" }, input);
    }

    [Fact]
    public void Sort_IsCaseInsensitiveForNonNumericTokens()
    {
        var input = new[] { "Banana", "apple", "Cherry" };

        Array.Sort(input, NaturalSortComparer.Instance);

        Assert.Equal(new[] { "apple", "Banana", "Cherry" }, input);
    }

    [Fact]
    public void Sort_TreatsEqualStringsAsEqual()
    {
        Assert.Equal(0, NaturalSortComparer.Instance.Compare("same.txt", "same.txt"));
    }
}
