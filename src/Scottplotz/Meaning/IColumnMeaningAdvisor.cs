using Scottplotz.Charting;

namespace Scottplotz.Meaning;

public sealed record ColumnMeaning(string? Title, string? XLabel, string? YLabel);

public interface IColumnMeaningAdvisor
{
    ValueTask<ColumnMeaning?> AdviseAsync(ChartTable table, CancellationToken cancellationToken);
}

public sealed class DisabledColumnMeaningAdvisor : IColumnMeaningAdvisor
{
    public ValueTask<ColumnMeaning?> AdviseAsync(ChartTable table, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ColumnMeaning?>(null);
}

public static class MeaningAdvice
{
    public static ResolvedChartHints Apply(ResolvedChartHints hints, ColumnMeaning? advice, bool needsAdvisor) =>
        advice is null || !needsAdvisor
            ? hints
            : hints with
            {
                Title = hints.Title ?? advice.Title,
                XLabel = hints.XLabel ?? advice.XLabel,
                YLabel = hints.YLabel ?? advice.YLabel
            };
}
