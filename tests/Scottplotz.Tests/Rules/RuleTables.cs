using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

internal static class RuleTables
{
    public static ChartTable Plain(params double[] values) =>
        new(DataOrigin.PlainArray, [new Column.Numeric(null, values)]);

    public static ChartTable Column(string name, params double[] values) =>
        new(DataOrigin.Columns, [new Column.Numeric(name, values)]);

    public static ChartTable Columns(params Column[] columns) =>
        new(DataOrigin.Columns, columns);

    public static double[] Ramp(int count) =>
        Enumerable.Range(0, count).Select(index => (double)index).ToArray();

    public static double[] Alternating(int count) =>
        Enumerable.Range(0, count).Select(index => index % 2 == 0 ? 0d : 100d).ToArray();

    public static Column.Temporal Days(params int[] days) =>
        new("date", days.Select(day => new DateTime(2026, 11, day, 0, 0, 0, DateTimeKind.Utc)).ToList());

    public static ResolvedChartHints Auto(ChartKind? type = null) =>
        new(type, null, null, null, 800, 450, ChartTheme.Light);

    public static ChartSelection Selected(ChartTable table, ChartKind? type = null)
    {
        var result = ChartSelectionBuilder.Select(table, Auto(type));
        return Assert.IsType<Result<ChartSelection>.Ok>(result).Value;
    }
}
