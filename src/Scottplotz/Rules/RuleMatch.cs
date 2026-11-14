using Scottplotz.Charting;

namespace Scottplotz.Rules;

public sealed record RuleMatch(string Name, bool Confident, ChartKind Kind);

public sealed record RuleDecision(RuleMatch? Match, bool Unsure);

public sealed record ChartSelection(
    string EffectiveRule,
    string? MatchedRule,
    ChartKind Kind,
    ChartPlan Plan,
    bool NeedsAdvisor,
    string TypeSource);
