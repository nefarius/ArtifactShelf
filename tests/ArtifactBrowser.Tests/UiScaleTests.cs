using ArtifactBrowser.Client.Utilities;

namespace ArtifactBrowser.Tests;

public sealed class UiScaleTests
{
    [Theory]
    [InlineData(80, 80)]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    [InlineData(79, 80)]
    [InlineData(201, 200)]
    [InlineData(int.MinValue, 80)]
    [InlineData(int.MaxValue, 200)]
    [InlineData(85, 90)]
    [InlineData(84, 80)]
    public void Clamp_SnapsToStepAndBounds(int input, int expected)
    {
        Assert.Equal(expected, UiScale.Clamp(input));
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData("", 100)]
    [InlineData("   ", 100)]
    [InlineData("not-a-number", 100)]
    [InlineData("100.5", 100)]
    [InlineData("110", 110)]
    [InlineData("75", 80)]
    [InlineData("250", 200)]
    [InlineData("110foo", 100)]
    [InlineData("2147483648", 100)]
    [InlineData("-2147483649", 100)]
    [InlineData("+110", 110)]
    public void Parse_RejectsGarbageAndClamps(string? raw, int expected)
    {
        Assert.Equal(expected, UiScale.Parse(raw));
    }

    [Fact]
    public void Increase_StepsByTenUntilMax()
    {
        Assert.Equal(110, UiScale.Increase(100));
        Assert.Equal(200, UiScale.Increase(200));
        Assert.Equal(200, UiScale.Increase(195));
        Assert.Equal(200, UiScale.Increase(int.MaxValue));
        Assert.InRange(UiScale.Increase(int.MaxValue), UiScale.MinPercent, UiScale.MaxPercent);
    }

    [Fact]
    public void Decrease_StepsByTenUntilMin()
    {
        Assert.Equal(90, UiScale.Decrease(100));
        Assert.Equal(80, UiScale.Decrease(80));
        Assert.Equal(80, UiScale.Decrease(85));
        Assert.Equal(80, UiScale.Decrease(int.MinValue));
        Assert.InRange(UiScale.Decrease(int.MinValue), UiScale.MinPercent, UiScale.MaxPercent);
    }

    [Fact]
    public void Reset_ReturnsDefault()
    {
        Assert.Equal(100, UiScale.Reset());
        Assert.Equal(UiScale.DefaultPercent, UiScale.Reset());
    }

    [Fact]
    public void CanIncreaseAndDecrease_RespectBounds()
    {
        Assert.True(UiScale.CanIncrease(100));
        Assert.False(UiScale.CanIncrease(200));
        Assert.True(UiScale.CanDecrease(100));
        Assert.False(UiScale.CanDecrease(80));
    }

    [Theory]
    [InlineData(80, "0.8")]
    [InlineData(100, "1")]
    [InlineData(110, "1.1")]
    [InlineData(200, "2")]
    public void ToCssMultiplier_UsesInvariantDecimal(int percent, string expected)
    {
        Assert.Equal(expected, UiScale.ToCssMultiplier(percent));
    }
}
