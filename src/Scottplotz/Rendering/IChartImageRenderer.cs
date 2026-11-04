using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Rendering;

public interface IChartImageRenderer
{
    byte[] Render(ChartPlan plan, ResolvedChartHints hints);
}

public sealed class ScottPlotChartRenderer : IChartImageRenderer
{
    public byte[] Render(ChartPlan plan, ResolvedChartHints hints) =>
        ChartRenderer.Render(plan, hints);
}
