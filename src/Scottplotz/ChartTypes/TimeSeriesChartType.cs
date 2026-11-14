using Scottplotz.Charting;

namespace Scottplotz.ChartTypes;

public sealed class TimeSeriesChartType : IChartType
{
    public ChartKind Kind => ChartKind.TimeSeries;

    public ChartTypeInfo Describe() => new(
        "timeseries",
        "Time series",
        "Numbers plotted against dates.",
        "One date column and one or more numeric columns.",
        ChartTypeRegistry.Parameters,
        """{ "data": { "date": ["2026-11-01", "2026-11-02", "2026-11-03"], "price": [100.5, 101.25, 99.75] }, "hints": { "type": "timeseries" } }""");
}
