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

        var outcome = await ChartPipeline.RunAsync(requestResult.Value, limits, advisor, cancellationToken);
        if (outcome is not Result<ChartOutcome>.Ok rendered)
            return ProblemResults.From(((Result<ChartOutcome>.Fail)outcome).Problem);

        var chart = rendered.Value;
        logger.LogInformation(
            "Chart {Rule} from {Points} points",
            chart.Explanation.EffectiveRule,
            chart.Explanation.Shape.PointCount);

        return render
            ? new PngChartResult(renderer.Render(chart.Plan, chart.Hints), chart.Explanation.EffectiveRule)
            : Results.Ok(chart.Explanation);
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> QueryPairs(HttpRequest request) =>
        request.Query
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value)))
            .ToList();
}
