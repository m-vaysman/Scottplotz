using Scottplotz.Charting;
using Scottplotz.Meaning;
using Scottplotz.Rendering;

namespace Scottplotz.Tests.Rendering;

public sealed class LayoutTests
{
    [Fact]
    public void Fifty_category_labels_keep_a_gap()
    {
        var labels = Enumerable.Range(0, 50).Select(index => $"item-{index:00}").ToArray();
        var (positions, _) = CategoryTicks.Select(labels, 800);
        var pixelsPerCategory = 800d / labels.Length;
        var gaps = positions.Zip(positions.Skip(1), (left, right) => (right - left) * pixelsPerCategory);

        Assert.NotEmpty(gaps);
        Assert.All(gaps, gap => Assert.True(gap >= CategoryTicks.MinimumLabelGap));
    }

    [Fact]
    public void Midnight_dates_use_whole_day_ticks()
    {
        var days = Enumerable.Range(1, 3)
            .Select(day => new DateTime(2026, 11, day, 0, 0, 0, DateTimeKind.Utc))
            .ToArray();
        var hours = days.Select(day => day.AddHours(6)).ToArray();

        Assert.True(DateTicks.WholeDays(days));
        Assert.False(DateTicks.WholeDays(hours));
    }

    [Fact]
    public void The_legend_sits_outside_the_data_area()
    {
        Assert.Equal(ScottPlot.Edge.Right, LegendPlacement.OutsideData);
    }

    [Theory]
    [InlineData(321, 222)]
    [InlineData(800, 450)]
    public void The_png_header_records_the_requested_size(int width, int height)
    {
        var png = Render(width, height);

        Assert.Equal((width, height), PngSize.Read(png));
    }

    private static byte[] Render(int width, int height)
    {
        var table = new ChartTable(DataOrigin.PlainArray, [new Column.Numeric(null, [1, 3, 2, 5, 4])]);
        var request = new ChartRequest(table, new ChartHints(null, "Numbers", "X", "Y", width, height, ChartTheme.Light));
        var rendered = ChartRenderer.Render(
            request,
            ChartingLimits.Default,
            new DisabledColumnMeaningAdvisor(),
            CancellationToken.None).GetAwaiter().GetResult();
        return Assert.IsType<Result<ChartImage>.Ok>(rendered).Value.Png;
    }
}
