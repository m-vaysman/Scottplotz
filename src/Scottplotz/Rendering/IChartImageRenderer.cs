using Scottplotz.Charting;
using Scottplotz.Http;
using Scottplotz.Meaning;

namespace Scottplotz.Rendering;

public sealed record ChartImage(byte[] Png, ChartExplanation Explanation);

public interface IChartImageRenderer
{
    Task<Result<ChartImage>> Render(
        ChartRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        CancellationToken cancellationToken);
}

public sealed class ScottPlotChartRenderer : IChartImageRenderer
{
    public Task<Result<ChartImage>> Render(
        ChartRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        CancellationToken cancellationToken) =>
        ChartRenderer.Render(request, limits, advisor, cancellationToken);
}
