using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class ScatterRule
{
    public const string Name = "scatter";

    public static RuleMatch? Match(ChartTable table) =>
        ColumnQueries.Dates(table).Count == 0
        && ColumnQueries.Categories(table).Count == 0
        && ColumnQueries.Numbers(table).Count == 2
            ? new RuleMatch(Name, Confident: true, ChartKind.Scatter)
            : null;
}
