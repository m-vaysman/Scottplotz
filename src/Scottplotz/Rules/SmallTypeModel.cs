using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class SmallTypeModel
{
    public const double Split = 0.5;

    public static ChartKind? Suggest(ChartTable table)
    {
        var numbers = ColumnQueries.Numbers(table);
        if (table.Origin == DataOrigin.PlainArray
            || ColumnQueries.Dates(table).Count > 0
            || ColumnQueries.Categories(table).Count > 0
            || numbers.Count != 1
            || numbers[0].Values.Count < HistogramRule.MinimumCount)
        {
            return null;
        }

        var correlation = Math.Abs(SeriesStats.CorrelationWithIndex(numbers[0].Values));
        if (correlation < HistogramRule.MaximumOrderedCorrelation || correlation >= 0.7)
            return null;

        return correlation < Split ? ChartKind.Histogram : ChartKind.Line;
    }
}
