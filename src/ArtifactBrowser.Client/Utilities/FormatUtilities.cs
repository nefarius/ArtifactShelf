namespace ArtifactBrowser.Client.Utilities;

public static class FormatUtilities
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string FormatBytes(long? bytes)
    {
        if (bytes is null)
        {
            return string.Empty;
        }

        double value = bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {Units[unit]}" : $"{value:0.#} {Units[unit]}";
    }

    public static string FormatDate(DateTimeOffset value)
    {
        if (value == DateTimeOffset.MinValue)
        {
            return string.Empty;
        }

        return value.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }
}
