using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class MultiLineRule
{
    public const string Name = "multi-line";

    public static RuleMatch? Match(ChartTable table) =>
        ColumnQueries.Dates(table).Count == 0
        && ColumnQueries.Categories(table).Count == 0
        && ColumnQueries.Numbers(table).Count >= 3
            ? new RuleMatch(Name, Confident: true, ChartKind.MultiLine)
            : null;
}
