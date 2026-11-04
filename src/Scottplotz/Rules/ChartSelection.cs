using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class ChartSelectionBuilder
{
    public static Result<ChartSelection> Select(ChartTable table, ResolvedChartHints hints)
    {
        var decision = ChartRuleEngine.Decide(table);
        var kind = hints.Type ?? decision.Match?.Kind;
        if (kind is null)
            return Result<ChartSelection>.Error(ChartProblem.NoRule());

        return ChartPlanFactory.Create(table, kind.Value).Bind(plan =>
            Result<ChartSelection>.Success(new ChartSelection(
                hints.Type is { } forced ? ChartKinds.HintRule(forced) : decision.Match!.Name,
                decision.Match?.Name,
                kind.Value,
                plan,
                NeedsAdvisor: hints.Type is null && decision.Match is { Confident: false })));
    }
}
