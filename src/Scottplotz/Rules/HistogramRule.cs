using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class HistogramRule
{
    public const string Name = "histogram";
    public const int MinimumCount = 32;

    // Below this absolute correlation with row order, the sequence is not a line.
    public const double MaximumOrderedCorrelation = 0.35;

    public static RuleMatch? Match(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        if (table.Origin == DataOrigin.PlainArray
            || ColumnQueries.Dates(table).Count > 0
            || ColumnQueries.Categories(table).Count > 0
            || numbers.Count != 1
            || numbers[0].Values.Count < MinimumCount)
        {
            return null;
        }

        var correlation = Math.Abs(SeriesStats.CorrelationWithIndex(numbers[0].Values));
        return correlation < MaximumOrderedCorrelation
            ? new RuleMatch(Name, Confident: true, ChartKind.Histogram)
            : null;
    }
}
