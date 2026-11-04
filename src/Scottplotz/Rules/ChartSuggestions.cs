using Scottplotz.Charting;

namespace Scottplotz.Rules;

public sealed record ChartSuggestion(string Type, string Reason, int Rank);

public static class ChartSuggestions
{
    public static IReadOnlyList<ChartSuggestion> Rank(ChartTable table)
    {
        var decision = ChartRuleEngine.Decide(table);
        if (decision.Match is null)
            return [];

        var model = decision.Unsure ? SmallTypeModel.Suggest(table) : null;
        return model is { } chosen
            ?
            [
                new(ChartKinds.Token(chosen), "The rules were unsure, so the small model chose this type.", 1),
                new(ChartKinds.Token(decision.Match.Kind), $"The {decision.Match.Name} rule is the fallback.", 2)
            ]
            :
            [
                new(ChartKinds.Token(decision.Match.Kind), $"The {decision.Match.Name} rule matched.", 1)
            ];
    }
}
