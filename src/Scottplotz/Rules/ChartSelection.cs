using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class ChartSelectionBuilder
{
    public static Result<ChartSelection> Select(ChartTable table, ResolvedChartHints hints)
    {
        var decision = ChartRuleEngine.Decide(table);
        if (hints.Type is { } forced)
            return Plan(table, forced, decision, TypeSources.User, needsAdvisor: false);

        if (decision.Match is null)
            return Result<ChartSelection>.Error(ChartProblem.NoRule());

        var kind = decision.Match.Kind;
        var source = TypeSources.Rules;
        if (decision.Unsure && SmallTypeModel.Suggest(table) is { } suggested)
        {
            kind = suggested;
            source = TypeSources.Model;
        }

        return Plan(table, kind, decision, source, needsAdvisor: decision.Unsure);
    }

    private static Result<ChartSelection> Plan(
        ChartTable table,
        ChartKind kind,
        RuleDecision decision,
        string source,
        bool needsAdvisor) =>
        ChartPlanFactory.Create(table, kind).Bind(plan =>
            Result<ChartSelection>.Success(new ChartSelection(
                source == TypeSources.User
                    ? ChartKinds.HintRule(kind)
                    : source == TypeSources.Model
                        ? ModelRule(kind)
                        : decision.Match!.Name,
                decision.Match?.Name,
                kind,
                plan,
                needsAdvisor,
                source)));

    private static string ModelRule(ChartKind kind) =>
        kind == ChartKind.Histogram ? HistogramRule.Name : PlainNumbersLineRule.Name;
}
