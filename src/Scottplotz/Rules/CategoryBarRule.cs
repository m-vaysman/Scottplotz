using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class CategoryBarRule
{
    public const string Name = "category-bar";

    public static RuleMatch? Match(ChartTable table) =>
        ColumnQueries.Dates(table).Count == 0
        && ColumnQueries.Categories(table).Count > 0
        && ColumnQueries.Numbers(table).Count > 0
            ? new RuleMatch(Name, Confident: true, ChartKind.Bar)
            : null;
}
