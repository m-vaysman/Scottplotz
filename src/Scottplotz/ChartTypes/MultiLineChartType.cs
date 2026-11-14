using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class MultiLineChartType : IChartType
{
    public ChartKind Kind => ChartKind.MultiLine;

    public ChartTypeInfo Describe() => new(
        "multi-line",
        "Multi-line",
        "Several numeric series on one chart.",
        "Three or more numeric columns.",
        ChartTypeRegistry.Parameters,
        """{ "data": { "x": [1, 2, 3], "a": [2, 3, 4], "b": [4, 3, 2] }, "hints": { "type": "multi-line" } }""");
}
