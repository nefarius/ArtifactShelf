using ArtifactBrowser.Client.Services;

namespace ArtifactBrowser.Client.Utilities;

/// <summary>
/// Orders browser entries the same way in the main panel and the folder tree.
/// Folders stay ahead of files. Name uses natural order. Size and type fall back
/// to natural name for folders, which have neither a size nor an extension.
/// </summary>
public static class EntrySorter
{
    public static List<T> Sort<T>(
        IEnumerable<T> entries,
        Func<T, bool> isDirectory,
        Func<T, string> name,
        Func<T, DateTimeOffset> modified,
        Func<T, long?> size,
        Func<T, string> extension,
        SortField field,
        bool descending)
    {
        var directories = new List<T>();
        var files = new List<T>();
        foreach (var entry in entries)
        {
            if (isDirectory(entry))
            {
                directories.Add(entry);
            }
            else
            {
                files.Add(entry);
            }
        }

        var orderedDirectories = OrderGroup(directories, name, modified, size, extension, field, folders: true, descending);
        var orderedFiles = OrderGroup(files, name, modified, size, extension, field, folders: false, descending);
        orderedDirectories.AddRange(orderedFiles);
        return orderedDirectories;
    }

    private static List<T> OrderGroup<T>(
        List<T> group,
        Func<T, string> name,
        Func<T, DateTimeOffset> modified,
        Func<T, long?> size,
        Func<T, string> extension,
        SortField field,
        bool folders,
        bool descending)
    {
        IOrderedEnumerable<T> ordered = field switch
        {
            SortField.Modified => group.OrderBy(modified).ThenBy(name, NaturalSortComparer.Instance),
            SortField.Size when !folders => group.OrderBy(entry => size(entry) ?? -1),
            SortField.Type when !folders => group.OrderBy(extension, StringComparer.OrdinalIgnoreCase),
            _ => group.OrderBy(name, NaturalSortComparer.Instance),
        };

        var result = ordered.ToList();
        if (descending)
        {
            result.Reverse();
        }

        return result;
    }
}
