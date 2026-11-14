using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class PieChartType : IChartType
{
    public ChartKind Kind => ChartKind.Pie;

    public ChartTypeInfo Describe() => new(
        "pie",
        "Pie",
        "Slices for one numeric series. Drawn only when you ask for it.",
        "One numeric column. An optional text column supplies the slice labels.",
        ChartTypeRegistry.Parameters,
        """{ "data": [{ "fruit": "apples", "count": 4 }, { "fruit": "pears", "count": 7 }], "hints": { "type": "pie" } }""");
}
