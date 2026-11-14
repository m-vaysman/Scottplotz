using System.Globalization;
using Scottplotz.Charting;
using Scottplotz.Http;
using Scottplotz.Meaning;
using Scottplotz.Rules;

namespace Scottplotz.Rendering;

public static class ChartRenderer
{
    private static readonly ScottPlot.IPalette Palette = new ScottPlot.Palettes.Category10();

    public static async Task<Result<ChartImage>> Render(
        ChartRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        CancellationToken cancellationToken)
    {
        var outcome = await ChartPipeline.RunAsync(request, limits, advisor, cancellationToken);
        if (outcome is not Result<ChartOutcome>.Ok rendered)
            return Result<ChartImage>.Error(((Result<ChartOutcome>.Fail)outcome).Problem);

        var chart = rendered.Value;
        return Result<ChartImage>.Success(new ChartImage(Draw(chart.Plan, chart.Hints), chart.Explanation));
    }

    private static byte[] Draw(ChartPlan plan, ResolvedChartHints hints)
    {
        EmbeddedFont.Register();
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            var plot = new ScottPlot.Plot();
            ApplyTheme(plot, hints.Theme);
            Draw(plot, plan, hints);
            ApplyText(plot, hints);
            return plot.GetImageBytes(hints.Width, hints.Height, ScottPlot.ImageFormat.Png);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    private static void Draw(ScottPlot.Plot plot, ChartPlan plan, ResolvedChartHints hints)
    {
        switch (plan)
        {
            case ChartPlan.Lines lines:
                DrawLines(plot, lines);
                break;
            case ChartPlan.Points points:
                DrawPoints(plot, points);
                break;
            case ChartPlan.Bars bars:
                DrawBars(plot, bars, hints.Width);
                break;
            case ChartPlan.Distribution distribution:
                DrawHistogram(plot, distribution);
                break;
            case ChartPlan.Slices slices:
                DrawPie(plot, slices);
                break;
        }
    }

    private static void DrawLines(ScottPlot.Plot plot, ChartPlan.Lines lines)
    {
        var count = lines.Series[0].Values.Count;
        var xs = lines.Dates is not null
            ? lines.Dates.Select(date => date.ToOADate()).ToArray()
            : lines.X is not null
                ? lines.X.ToArray()
                : Enumerable.Range(0, count).Select(index => (double)index).ToArray();

        foreach (var (series, index) in lines.Series.Select((series, index) => (series, index)))
        {
            var scatter = plot.Add.Scatter(xs, series.Values.ToArray());
            scatter.LegendText = series.Name;
            scatter.Color = Palette.GetColor(index);
            scatter.LineWidth = 2;
            scatter.MarkerSize = count <= 40 ? 5 : 0;
        }

        if (lines.Dates is not null && DateTicks.WholeDays(lines.Dates))
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeFixedInterval(
                new ScottPlot.TickGenerators.TimeUnits.Day(),
                1);
        else if (lines.Dates is not null)
            plot.Axes.DateTimeTicksBottom();

        if (lines.Series.Count > 1)
            ShowLegend(plot);
    }

    private static void DrawPoints(ScottPlot.Plot plot, ChartPlan.Points points)
    {
        var scatter = plot.Add.Scatter(points.X.ToArray(), points.Y.ToArray());
        scatter.LegendText = points.YName ?? "value";
        scatter.Color = Palette.GetColor(0);
        scatter.LineWidth = 0;
        scatter.MarkerSize = 7;
    }

    private static void DrawBars(ScottPlot.Plot plot, ChartPlan.Bars bars, int plotWidth)
    {
        var seriesCount = bars.Series.Count;
        var barWidth = 0.8 / seriesCount;
        var drawn = bars.Series.SelectMany((series, seriesIndex) =>
            series.Values.Select((value, index) => new ScottPlot.Bar
            {
                Position = index + ((seriesIndex - (seriesCount - 1) / 2.0) * barWidth),
                Value = value,
                Size = barWidth * 0.9,
                FillColor = Palette.GetColor(seriesIndex)
            })).ToList();

        plot.Add.Bars(drawn);
        var ticks = CategoryTicks.Select(bars.Labels, plotWidth);
        plot.Axes.Bottom.SetTicks(ticks.Positions, ticks.Labels);
        if (seriesCount > 1)
            ShowLegend(plot);
    }

    private static void DrawHistogram(ScottPlot.Plot plot, ChartPlan.Distribution distribution)
    {
        var values = distribution.Values;
        var binCount = Math.Max(1, (int)Math.Ceiling(Math.Log2(values.Count) + 1));
        var histogram = ScottPlot.Statistics.Histogram.WithBinCount(binCount, values);
        if (histogram.Counts.All(count => count == 0))
            histogram.AddRange(values);

        var bars = plot.Add.Histogram(histogram, Palette.GetColor(0));
        bars.BarWidthFraction = 1;
    }

    private static void DrawPie(ScottPlot.Plot plot, ChartPlan.Slices slices)
    {
        var pieces = slices.Labels
            .Zip(slices.Values, (label, value) => (label, value))
            .Select((slice, index) => new ScottPlot.PieSlice(slice.value, Palette.GetColor(index), slice.label))
            .ToList();

        plot.Add.Pie(pieces);
        plot.HideAxesAndGrid();
        ShowLegend(plot);
    }

    private static void ShowLegend(ScottPlot.Plot plot)
    {
        plot.ShowLegend(LegendPlacement.OutsideData);
        plot.Legend.FontName = EmbeddedFont.FamilyName;
        plot.Legend.SetBestFontOnEachRender = false;
    }

    private static void ApplyText(ScottPlot.Plot plot, ResolvedChartHints hints)
    {
        if (!string.IsNullOrWhiteSpace(hints.Title))
            plot.Title(hints.Title, 16);
        if (!string.IsNullOrWhiteSpace(hints.XLabel))
            plot.XLabel(hints.XLabel, 12);
        if (!string.IsNullOrWhiteSpace(hints.YLabel))
            plot.YLabel(hints.YLabel, 12);

        plot.Axes.Title.Label.FontName = EmbeddedFont.FamilyName;
        plot.Axes.Title.Label.Bold = false;
        plot.Axes.Bottom.Label.FontName = EmbeddedFont.FamilyName;
        plot.Axes.Bottom.Label.Bold = false;
        plot.Axes.Left.Label.FontName = EmbeddedFont.FamilyName;
        plot.Axes.Left.Label.Bold = false;
        plot.Axes.Bottom.TickLabelStyle.FontName = EmbeddedFont.FamilyName;
        plot.Axes.Bottom.TickLabelStyle.Bold = false;
        plot.Axes.Left.TickLabelStyle.FontName = EmbeddedFont.FamilyName;
        plot.Axes.Left.TickLabelStyle.Bold = false;
        plot.Legend.FontName = EmbeddedFont.FamilyName;
    }

    private static void ApplyTheme(ScottPlot.Plot plot, ChartTheme theme)
    {
        var dark = theme == ChartTheme.Dark;
        var background = ScottPlot.Color.FromHex(dark ? "#1B1B1F" : "#FFFFFF");
        var foreground = ScottPlot.Color.FromHex(dark ? "#F2F2F2" : "#2B2B2B");
        var grid = ScottPlot.Color.FromHex(dark ? "#3A3A40" : "#E6E6E6");

        plot.FigureBackground.Color = background;
        plot.DataBackground.Color = background;
        plot.Axes.Color(foreground);
        plot.Grid.MajorLineColor = grid;
        plot.Grid.MinorLineColor = grid;
    }
}
