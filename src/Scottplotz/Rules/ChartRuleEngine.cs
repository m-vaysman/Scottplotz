using Scottplotz.Charting;

namespace Scottplotz.Rules;

public sealed record OrderedRule(string Name, Func<ChartTable, RuleMatch?> Match);

public static class ChartRuleEngine
{
    public static IReadOnlyList<OrderedRule> Ordered { get; } =
    [
        new(TimeSeriesRule.Name, TimeSeriesRule.Match),
        new(CategoryBarRule.Name, CategoryBarRule.Match),
        new(ScatterRule.Name, ScatterRule.Match),
        new(MultiLineRule.Name, MultiLineRule.Match),
        new(HistogramRule.Name, HistogramRule.Match),
        new(PlainNumbersLineRule.Name, PlainNumbersLineRule.Match)
    ];

    public static RuleDecision Decide(ChartTable table) =>
        Decide(table, Ordered.Select(rule => rule.Match).ToArray());

    public static RuleDecision Decide(ChartTable table, IReadOnlyList<Func<ChartTable, RuleMatch?>> rules)
    {
        var matches = rules
            .Select(rule => rule(table))
            .OfType<RuleMatch>()
            .ToList();

        var confident = matches.FirstOrDefault(match => match.Confident);
        return confident is not null
            ? new RuleDecision(confident, Unsure: false)
            : new RuleDecision(matches.FirstOrDefault(), Unsure: true);
    }
}
