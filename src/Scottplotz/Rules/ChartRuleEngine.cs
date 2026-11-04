using Scottplotz.Charting;

namespace Scottplotz.Rules;

public static class ChartRuleEngine
{
    private static readonly IReadOnlyList<Func<ChartTable, RuleMatch?>> Rules =
    [
        TimeSeriesRule.Match,
        CategoryBarRule.Match,
        ScatterRule.Match,
        MultiLineRule.Match,
        HistogramRule.Match,
        PlainNumbersLineRule.Match
    ];

    public static RuleDecision Decide(ChartTable table)
    {
        var matches = Rules
            .Select(rule => rule(table))
            .OfType<RuleMatch>()
            .ToList();

        var confident = matches.FirstOrDefault(match => match.Confident);
        return confident is not null
            ? new RuleDecision(confident, Unsure: false)
            : new RuleDecision(matches.FirstOrDefault(), Unsure: true);
    }
}
