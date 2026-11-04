using Scottplotz.Charting;
using Scottplotz.Meaning;
using Scottplotz.Rules;

namespace Scottplotz.Http;

public static class ChartPipeline
{
    public static async Task<Result<ChartOutcome>> RunAsync(
        ChartRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        CancellationToken cancellationToken)
    {
        var hints = request.Hints.Resolve(limits);
        var selected = ChartSelectionBuilder.Select(request.Table, hints);
        if (selected is not Result<ChartSelection>.Ok ok)
            return Result<ChartOutcome>.Error(((Result<ChartSelection>.Fail)selected).Problem);

        var decision = ok.Value;
        var advice = decision.NeedsAdvisor
            ? await advisor.AdviseAsync(request.Table, cancellationToken)
            : null;
        var labeled = MeaningAdvice.Apply(hints, advice, decision.NeedsAdvisor);

        var explanation = new ChartExplanation(
            Shape(request.Table),
            decision.MatchedRule,
            decision.EffectiveRule,
            new ChartSettings(
                ChartKinds.Token(decision.Kind),
                labeled.Title,
                labeled.XLabel,
                labeled.YLabel,
                labeled.Width,
                labeled.Height,
                labeled.Theme == ChartTheme.Dark ? "dark" : "light"),
            decision.NeedsAdvisor);

        return Result<ChartOutcome>.Success(new ChartOutcome(explanation, decision.Plan, labeled));
    }

    private static DataShape Shape(ChartTable table) =>
        new(
            Origin(table.Origin),
            table.RowCount,
            table.PointCount,
            table.Columns.Select(column => new ColumnShape(column.Name, ColumnType(column), column.Count)).ToList());

    private static string Origin(DataOrigin origin) =>
        origin switch
        {
            DataOrigin.PlainArray => "plainArray",
            DataOrigin.Columns => "columns",
            DataOrigin.Rows => "rows",
            DataOrigin.Csv => "csv",
            _ => origin.ToString()
        };

    private static string ColumnType(Column column) =>
        column switch
        {
            Column.Numeric => "number",
            Column.Temporal => "date",
            Column.Categorical => "text",
            _ => "unknown"
        };
}
