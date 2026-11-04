using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class BarChartType : IChartType
{
    public ChartKind Kind => ChartKind.Bar;

    public ChartTypeInfo Describe() => new(
        "bar",
        "Bar",
        "Bars for categories and their numbers.",
        "One text column and one or more numeric columns.",
        ChartTypeRegistry.Parameters,
        """{ "data": [{ "fruit": "apples", "count": 4 }, { "fruit": "pears", "count": 7 }], "hints": { "type": "bar" } }""");
}
