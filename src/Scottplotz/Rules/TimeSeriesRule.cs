using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class TimeSeriesRule
{
    public const string Name = "timeseries";

    public static RuleMatch? Match(ChartTable table) =>
        ColumnQueries.Dates(table).Count > 0 && ColumnQueries.Numbers(table).Count > 0
            ? new RuleMatch(Name, Confident: true, ChartKind.TimeSeries)
            : null;
}
