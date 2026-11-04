using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Http;

public sealed record ColumnShape(string? Name, string Type, int Count);

public sealed record DataShape(string Origin, int RowCount, int PointCount, IReadOnlyList<ColumnShape> Columns);

public sealed record ChartSettings(string Type, string? Title, string? XLabel, string? YLabel, int Width, int Height, string Theme);

public sealed record ChartExplanation(
    DataShape Shape,
    string? MatchedRule,
    string EffectiveRule,
    ChartSettings Settings,
    bool AdvisorConsulted);

public sealed record ChartOutcome(ChartExplanation Explanation, ChartPlan Plan, ResolvedChartHints Hints);
