using Scottplotz.Charting;
using Scottplotz.Meaning;
using Scottplotz.Parsing;
using Scottplotz.Rendering;

namespace Scottplotz.Http;

public static class ChartEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/chart", PostChart)
            .WithName("Chart")
            .WithTags("Charts")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/chart/explain", PostExplain)
            .WithName("ExplainChart")
            .WithTags("Charts")
            .Produces<ChartExplanation>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static Task<IResult> PostChart(
        HttpRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        ILoggerFactory loggerFactory,
        IChartImageRenderer renderer,
        CancellationToken cancellationToken) =>
        Execute(request, limits, advisor, loggerFactory, renderer, cancellationToken, render: true);

    private static Task<IResult> PostExplain(
        HttpRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        ILoggerFactory loggerFactory,
        IChartImageRenderer renderer,
        CancellationToken cancellationToken) =>
        Execute(request, limits, advisor, loggerFactory, renderer, cancellationToken, render: false);

    private static async Task<IResult> Execute(
        HttpRequest request,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        ILoggerFactory loggerFactory,
        IChartImageRenderer renderer,
        CancellationToken cancellationToken,
        bool render)
    {
        var logger = loggerFactory.CreateLogger("Scottplotz.Chart");
        var body = await RequestBodyReader.ReadAsync(request, limits.MaxRequestBodyBytes, cancellationToken);
        if (body is not Result<string>.Ok text)
            return ProblemResults.From(((Result<string>.Fail)body).Problem);

        var parsed = ChartInputParser.ParseRequest(text.Value, request.ContentType, QueryPairs(request), limits);
        if (parsed is not Result<ChartRequest>.Ok requestResult)
            return ProblemResults.From(((Result<ChartRequest>.Fail)parsed).Problem);

        if (!render)
        {
            var explained = await ChartPipeline.RunAsync(requestResult.Value, limits, advisor, cancellationToken);
            return explained is Result<ChartOutcome>.Ok explanation
                ? Results.Ok(explanation.Value.Explanation)
                : ProblemResults.From(((Result<ChartOutcome>.Fail)explained).Problem);
        }

        var outcome = await renderer.Render(requestResult.Value, limits, advisor, cancellationToken);
        if (outcome is not Result<ChartImage>.Ok rendered)
            return ProblemResults.From(((Result<ChartImage>.Fail)outcome).Problem);

        var chart = rendered.Value;
        logger.LogInformation(
            "Chart {Rule} from {Points} points via {TypeSource}",
            chart.Explanation.EffectiveRule,
            chart.Explanation.Shape.PointCount,
            chart.Explanation.TypeSource);

        return new PngChartResult(chart.Png, chart.Explanation.EffectiveRule, chart.Explanation.TypeSource);
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> QueryPairs(HttpRequest request) =>
        request.Query
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value)))
            .ToList();
}
