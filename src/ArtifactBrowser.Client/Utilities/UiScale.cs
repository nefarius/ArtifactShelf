namespace ArtifactBrowser.Client.Utilities;

/// <summary>Clamps and steps the page UI scale percent stored in localStorage.</summary>
public static class UiScale
{
    public const int DefaultPercent = 100;

    public const int MinPercent = 80;

    public const int MaxPercent = 200;

    public const int StepPercent = 10;

    public static int Clamp(int percent)
    {
        if (percent <= MinPercent)
        {
            return MinPercent;
        }

        if (percent >= MaxPercent)
        {
            return MaxPercent;
        }

        return (int)Math.Round(percent / (double)StepPercent, MidpointRounding.AwayFromZero) * StepPercent;
    }

    public static int Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out var value))
        {
            return DefaultPercent;
        }

        return Clamp(value);
    }

    public static int Increase(int percent) => Clamp(percent + StepPercent);

    public static int Decrease(int percent) => Clamp(percent - StepPercent);

    public static int Reset() => DefaultPercent;

    public static bool CanIncrease(int percent) => Clamp(percent) < MaxPercent;

    public static bool CanDecrease(int percent) => Clamp(percent) > MinPercent;

    public static string ToCssMultiplier(int percent) =>
        (Clamp(percent) / 100.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
