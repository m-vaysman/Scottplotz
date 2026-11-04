namespace Scottplotz.Rules;

public sealed record NamedSeries(string Name, IReadOnlyList<double> Values);

public abstract record ChartPlan
{
    public sealed record Lines(
        IReadOnlyList<double>? X,
        IReadOnlyList<DateTime>? Dates,
        IReadOnlyList<NamedSeries> Series) : ChartPlan;

    public sealed record Points(
        IReadOnlyList<double> X,
        IReadOnlyList<double> Y,
        string? XName,
        string? YName) : ChartPlan;

    public sealed record Bars(IReadOnlyList<string> Labels, IReadOnlyList<NamedSeries> Series) : ChartPlan;

    public sealed record Distribution(IReadOnlyList<double> Values, string? Name) : ChartPlan;

    public sealed record Slices(IReadOnlyList<string> Labels, IReadOnlyList<double> Values) : ChartPlan;
}
