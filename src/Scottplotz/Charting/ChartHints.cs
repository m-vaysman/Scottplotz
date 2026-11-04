namespace Scottplotz.Charting;

public enum ChartKind
{
    Line,
    Bar,
    Scatter,
    Histogram,
    TimeSeries,
    Pie
}

public enum ChartTheme
{
    Light,
    Dark
}

public static class HintNames
{
    public const string Type = "type";
    public const string Title = "title";
    public const string XLabel = "xLabel";
    public const string YLabel = "yLabel";
    public const string Width = "width";
    public const string Height = "height";
    public const string Theme = "theme";

    public static IReadOnlyList<string> All { get; } = [Type, Title, XLabel, YLabel, Width, Height, Theme];

    public static string List => "type, title, xLabel, yLabel, width, height, theme";

    public const int MaxTextLength = 500;
}

public sealed record ChartHints(
    ChartKind? Type,
    string? Title,
    string? XLabel,
    string? YLabel,
    int? Width,
    int? Height,
    ChartTheme? Theme)
{
    public static ChartHints None { get; } = new(null, null, null, null, null, null, null);

    public ResolvedChartHints Resolve(ChartingLimits limits) =>
        new(
            Type,
            Title,
            XLabel,
            YLabel,
            Width ?? limits.DefaultWidth,
            Height ?? limits.DefaultHeight,
            Theme ?? ChartTheme.Light);
}

public sealed record ResolvedChartHints(
    ChartKind? Type,
    string? Title,
    string? XLabel,
    string? YLabel,
    int Width,
    int Height,
    ChartTheme Theme);

public sealed record ChartRequest(ChartTable Table, ChartHints Hints);
