using System.Globalization;
using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class ChartPlanFactory
{
    public static Result<ChartPlan> Create(ChartTable table, ChartKind kind) =>
        kind switch
        {
            ChartKind.Line => Lines(table, requireDates: false),
            ChartKind.MultiLine => Lines(table, requireDates: false),
            ChartKind.TimeSeries => Lines(table, requireDates: true),
            ChartKind.Bar => Bars(table),
            ChartKind.Scatter => Scatter(table),
            ChartKind.Histogram => Histogram(table),
            ChartKind.Pie => Pie(table),
            _ => Result<ChartPlan>.Error(ChartProblem.IncompatibleHint(ChartKinds.Token(kind), "is not a supported chart"))
        };

    private static Result<ChartPlan> Lines(ChartTable table, bool requireDates)
    {
        var numbers = ColumnQueries.Numbers(table);
        var dates = ColumnQueries.Dates(table);
        if (numbers.Count == 0)
            return Result<ChartPlan>.Error(ChartProblem.IncompatibleHint(
                requireDates ? "timeseries" : "line",
                "needs at least one numeric column"));

        if (requireDates && dates.Count == 0)
            return Result<ChartPlan>.Error(ChartProblem.IncompatibleHint(
                "timeseries",
                "needs a date column and a numeric column"));

        if (dates.Count > 0)
            return Result<ChartPlan>.Success(new ChartPlan.Lines(null, dates[0].Values, Series(numbers)));

        if (numbers.Count >= 3 && SeriesStats.IsStrictlyMonotonic(numbers[0].Values))
            return Result<ChartPlan>.Success(new ChartPlan.Lines(numbers[0].Values, null, Series(numbers.Skip(1).ToList())));

        return Result<ChartPlan>.Success(new ChartPlan.Lines(null, null, Series(numbers)));
    }

    private static Result<ChartPlan> Scatter(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        var dates = ColumnQueries.Dates(table);
        if (numbers.Count >= 2)
            return Result<ChartPlan>.Success(new ChartPlan.Points(
                numbers[0].Values,
                numbers[1].Values,
                numbers[0].Name,
                numbers[1].Name));

        if (dates.Count >= 1 && numbers.Count >= 1)
            return Result<ChartPlan>.Success(new ChartPlan.Points(
                dates[0].Values.Select(date => date.ToOADate()).ToList(),
                numbers[0].Values,
                dates[0].Name,
                numbers[0].Name));

        return Result<ChartPlan>.Error(ChartProblem.IncompatibleHint(
            "scatter",
            "needs two numeric columns, or a date column and a numeric column"));
    }

    private static Result<ChartPlan> Histogram(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        return numbers.Count == 0
            ? Result<ChartPlan>.Error(ChartProblem.IncompatibleHint("histogram", "needs a numeric column"))
            : Result<ChartPlan>.Success(new ChartPlan.Distribution(numbers[0].Values, numbers[0].Name));
    }

    private static Result<ChartPlan> Bars(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        if (numbers.Count == 0)
            return Result<ChartPlan>.Error(ChartProblem.IncompatibleHint("bar", "needs a numeric column"));

        var categories = ColumnQueries.Categories(table);
        if (categories.Count == 0)
        {
            var labels = IndexLabels(numbers[0].Values.Count);
            return Result<ChartPlan>.Success(new ChartPlan.Bars(labels, Series(numbers)));
        }

        var order = categories[0].Values.Distinct(StringComparer.Ordinal).ToList();
        var series = numbers
            .Select((column, index) => new NamedSeries(SeriesName(column, index, numbers.Count), SumByLabel(categories[0].Values, column.Values, order)))
            .ToList();
        return Result<ChartPlan>.Success(new ChartPlan.Bars(order, series));
    }

    private static Result<ChartPlan> Pie(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        if (numbers.Count != 1)
            return Result<ChartPlan>.Error(ChartProblem.IncompatibleHint("pie", "needs one numeric series"));

        if (numbers[0].Values.Any(value => value < 0 || !double.IsFinite(value)))
            return Result<ChartPlan>.Error(ChartProblem.IncompatibleHint("pie", "needs finite numbers that are zero or greater"));

        var categories = ColumnQueries.Categories(table);
        if (categories.Count == 0)
            return Result<ChartPlan>.Success(new ChartPlan.Slices(IndexLabels(numbers[0].Values.Count), numbers[0].Values));

        var order = categories[0].Values.Distinct(StringComparer.Ordinal).ToList();
        var totals = SumByLabel(categories[0].Values, numbers[0].Values, order);
        return Result<ChartPlan>.Success(new ChartPlan.Slices(order, totals));
    }

    private static IReadOnlyList<NamedSeries> Series(IReadOnlyList<Column.Numeric> columns) =>
        columns.Select((column, index) => new NamedSeries(SeriesName(column, index, columns.Count), column.Values)).ToList();

    private static string SeriesName(Column column, int index, int count) =>
        string.IsNullOrEmpty(column.Name)
            ? count == 1 ? "value" : $"series-{index + 1}"
            : column.Name;

    private static IReadOnlyList<string> IndexLabels(int count) =>
        Enumerable.Range(1, count).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToList();

    private static IReadOnlyList<double> SumByLabel(
        IReadOnlyList<string> labels,
        IReadOnlyList<double> values,
        IReadOnlyList<string> order)
    {
        var sums = labels
            .Zip(values, (label, value) => (label, value))
            .GroupBy(item => item.label, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.value), StringComparer.Ordinal);

        return order.Select(label => sums[label]).ToList();
    }
}
