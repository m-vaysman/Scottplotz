using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class ScatterChartType : IChartType
{
    public ChartKind Kind => ChartKind.Scatter;

    public ChartTypeInfo Describe() => new(
        "scatter",
        "Scatter",
        "Points from two numeric columns.",
        "Exactly two numeric columns, or one date column and one numeric column.",
        ChartTypeRegistry.Parameters,
        """{ "data": { "x": [1, 2, 3, 4], "y": [2, 3, 5, 4] }, "hints": { "type": "scatter" } }""");
}
