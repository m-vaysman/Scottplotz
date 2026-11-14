using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class HistogramChartType : IChartType
{
    public ChartKind Kind => ChartKind.Histogram;

    public ChartTypeInfo Describe() => new(
        "histogram",
        "Histogram",
        "How often the numbers fall into each bin.",
        "One numeric column.",
        ChartTypeRegistry.Parameters,
        """{ "data": { "reading": [1, 1, 2, 3, 5, 8, 13] }, "hints": { "type": "histogram" } }""");
}
