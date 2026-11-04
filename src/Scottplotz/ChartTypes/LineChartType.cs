using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class LineChartType : IChartType
{
    public ChartKind Kind => ChartKind.Line;

    public ChartTypeInfo Describe() => new(
        "line",
        "Line",
        "A line through one series of numbers.",
        "One numeric column, or a plain list of numbers.",
        ChartTypeRegistry.Parameters,
        """{ "data": [1, 3, 2, 5, 4], "hints": { "type": "line" } }""");
}
