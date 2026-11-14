using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed record ChartTypeParameter(string Name, string Description, string DefaultValue);

public sealed record ChartTypeInfo(
    string Id,
    string DisplayName,
    string Description,
    string DataShape,
    IReadOnlyList<ChartTypeParameter> Parameters,
    string Example);

public interface IChartType
{
    ChartKind Kind { get; }

    ChartTypeInfo Describe();
}

public static class ChartTypeRegistry
{
    public static IReadOnlyList<ChartTypeParameter> Parameters { get; } =
    [
        new("title", "Text drawn above the chart.", ""),
        new("xLabel", "Label for the horizontal axis.", ""),
        new("yLabel", "Label for the vertical axis.", ""),
        new("width", "Image width in pixels.", "800"),
        new("height", "Image height in pixels.", "450"),
        new("theme", "light or dark.", "light")
    ];

    public static IReadOnlyList<IChartType> All { get; } =
    [
        new LineChartType(),
        new BarChartType(),
        new ScatterChartType(),
        new HistogramChartType(),
        new TimeSeriesChartType(),
        new MultiLineChartType(),
        new PieChartType()
    ];

    public static IReadOnlyList<ChartTypeInfo> Describe() =>
        All.Select(type => type.Describe()).ToList();

    public static IChartType? Find(string id) =>
        All.FirstOrDefault(type => type.Describe().Id == id);

    public static string IdList => string.Join(", ", All.Select(type => type.Describe().Id));
}
