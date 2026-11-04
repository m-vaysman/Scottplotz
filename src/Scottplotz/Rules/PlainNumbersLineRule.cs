using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class PlainNumbersLineRule
{
    public const string Name = "plain-numbers-line";

    public static RuleMatch? Match(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        if (ColumnQueries.Dates(table).Count > 0
            || ColumnQueries.Categories(table).Count > 0
            || numbers.Count != 1)
        {
            return null;
        }

        var values = numbers[0].Values;
        var correlation = Math.Abs(SeriesStats.CorrelationWithIndex(values));
        var unordered = table.Origin != DataOrigin.PlainArray
            && values.Count >= HistogramRule.MinimumCount
            && correlation < HistogramRule.MaximumOrderedCorrelation;
        if (unordered)
            return null;

        var confident = table.Origin == DataOrigin.PlainArray
            || values.Count < HistogramRule.MinimumCount
            || correlation >= 0.7;
        return new RuleMatch(Name, confident, ChartKind.Line);
    }
}
