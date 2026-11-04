using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class ColumnQueries
{
    public static IReadOnlyList<Column.Numeric> Numbers(ChartTable table) =>
        table.Columns.OfType<Column.Numeric>().ToList();

    public static IReadOnlyList<Column.Temporal> Dates(ChartTable table) =>
        table.Columns.OfType<Column.Temporal>().ToList();

    public static IReadOnlyList<Column.Categorical> Categories(ChartTable table) =>
        table.Columns.OfType<Column.Categorical>().ToList();
}
