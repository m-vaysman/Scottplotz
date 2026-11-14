using Scottplotz.Charting;
using Scottplotz.Recon;

namespace Scottplotz.Http;

public static class ReconEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/recon/views", () => Results.Ok(ReconViews.Describe()))
            .WithName("ReconViews")
            .WithTags("Reconciliation")
            .Produces<IReadOnlyList<ReconViewInfo>>(StatusCodes.Status200OK);

        app.MapGet("/recon/{view}/sample", (string view) =>
                ReconViews.Find(view) is { } found
                    ? Results.Content(found.Assets.Sample, "application/json; charset=utf-8")
                    : ProblemResults.From(ReconProblem.UnknownView(view, ReconViews.NameList)))
            .WithName("ReconSample")
            .WithTags("Reconciliation")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/recon/{view}", (string view, HttpRequest request, ChartingLimits limits, ReconLimits reconLimits, ILoggerFactory loggers, CancellationToken cancellationToken) =>
                Execute(view, request, limits, reconLimits, loggers, cancellationToken, page => new HtmlPageResult(page)))
            .WithName("Recon")
            .WithTags("Reconciliation")
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/recon/{view}/explain", (string view, HttpRequest request, ChartingLimits limits, ReconLimits reconLimits, ILoggerFactory loggers, CancellationToken cancellationToken) =>
                Execute(view, request, limits, reconLimits, loggers, cancellationToken, page => Results.Content(page.ModelJson, "application/json; charset=utf-8")))
            .WithName("ExplainRecon")
            .WithTags("Reconciliation")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Execute(
        string name,
        HttpRequest request,
        ChartingLimits limits,
        ReconLimits reconLimits,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        Func<ReconPage, IResult> respond)
    {
        if (ReconViews.Find(name) is not { } view)
            return ProblemResults.From(ReconProblem.UnknownView(name, ReconViews.NameList));

        var body = await RequestBodyReader.ReadAsync(request, limits.MaxRequestBodyBytes, cancellationToken);
        if (body is not Result<string>.Ok text)
            return ProblemResults.From(((Result<string>.Fail)body).Problem);

        var parsed = ReconParser.Parse(text.Value, view, reconLimits);
        if (parsed is not Result<ReconModel>.Ok model)
            return ProblemResults.From(((Result<ReconModel>.Fail)parsed).Problem);

        loggers.CreateLogger("Scottplotz.Recon").LogInformation(
            "Recon {View} from {Records} records",
            view.Name,
            model.Value.Records.Count);

        return respond(PageShell.Compose(model.Value, view, ReconViews.Kit));
    }
}
