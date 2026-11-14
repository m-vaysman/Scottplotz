using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scottplotz.Charting;
using Scottplotz.Reports;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using Scottplotz.Reports.Rendering;

namespace Scottplotz.Http;

public sealed record ReportListing(string Id, string Slug, string Title, string Office, string Users, string Question, string Cadence, string About, IReadOnlyList<string> RequiredRoles);

public static class ReportEndpoints
{
    private static readonly JsonSerializerOptions DataJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<string> SampleText = new(() =>
    {
        using var stream = typeof(ReportEndpoints).Assembly.GetManifestResourceStream("reports/sample.json")
            ?? throw new InvalidOperationException("The report sample is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    });

    public static string Sample => SampleText.Value;

    public static IReadOnlyList<ReportListing> Listing() =>
        ReportRegistry.All
            .Select(definition => definition.Info)
            .Select(info => new ReportListing(
                info.Id, info.Slug, info.Title, info.Office, info.Users, info.Question, info.Cadence, info.About,
                ReportRegistry.Find(info.Id)!.Required.Select(role => role.ToString()).ToList()))
            .ToList();

    public static void Map(WebApplication app)
    {
        app.MapGet("/reports", () => Results.Ok(Listing()))
            .WithName("Reports")
            .WithTags("Reports")
            .Produces<IReadOnlyList<ReportListing>>(StatusCodes.Status200OK);

        app.MapGet("/reports/sample", () => Results.Content(Sample, "application/json; charset=utf-8"))
            .WithName("ReportSample")
            .WithTags("Reports")
            .Produces(StatusCodes.Status200OK, contentType: "application/json");

        app.MapPost("/reports/{id}", (string id, HttpRequest request, ChartingLimits limits, ReportLimits reportLimits, ILoggerFactory loggers, CancellationToken cancellationToken) =>
                Execute(id, request, limits, reportLimits, loggers, cancellationToken, (document, input) => new ReportPageResult(ReportHtml.Render(document, input.Options.Theme))))
            .WithName("Report")
            .WithTags("Reports")
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/reports/{id}/data", (string id, HttpRequest request, ChartingLimits limits, ReportLimits reportLimits, ILoggerFactory loggers, CancellationToken cancellationToken) =>
                Execute(id, request, limits, reportLimits, loggers, cancellationToken, (document, _) => Results.Content(Data(document), "application/json; charset=utf-8")))
            .WithName("ReportData")
            .WithTags("Reports")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    public static string Data(ReportDocument document) =>
        JsonSerializer.Serialize(
            new
            {
                id = document.Info.Id,
                title = document.Title,
                asOf = document.AsOf.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                run = document.RunLine,
                blocks = document.Blocks.Select(Plain).ToList()
            },
            DataJson);

    private static object Plain(ReportBlock block) =>
        block switch
        {
            TableBlock table => new { kind = "table", title = table.Title, columns = table.Columns.Select(column => column.Title).ToList(), rows = table.Rows.Select(Plain).ToList(), note = table.Note },
            TilesBlock tiles => new { kind = "tiles", tiles = tiles.Tiles.Select(tile => new { label = tile.Label, value = CellText.Of(tile.Value), tone = Tone(tile.Value.Tone), sub = tile.Sub }).ToList() },
            BarsBlock bars => new { kind = "bars", title = bars.Title, bars = bars.Bars.Select(bar => new { label = bar.Label, value = bar.Value, tone = Tone(bar.Tone), note = bar.Note }).ToList() },
            NoteBlock note => new { kind = "note", text = note.Text },
            SectionBlock section => new { kind = "section", title = section.Title, sub = section.Sub, blocks = section.Blocks.Select(Plain).ToList() },
            DataBlock data => new { kind = "data", name = data.Name, columns = data.Table.Columns.Select(column => column.Title).ToList(), rows = data.Table.Rows.Select(Plain).ToList() },
            _ => new { kind = "unknown" }
        };

    private static object Plain(ReportRow row) =>
        new
        {
            kind = row.Kind == RowKind.Normal ? null : row.Kind.ToString(),
            cells = row.Cells.Select(cell => new
            {
                text = CellText.Of(cell),
                value = cell.Value is CellValue.Number number ? number.Value : (decimal?)null,
                tone = Tone(cell.Tone),
                span = cell.Span > 1 ? cell.Span : (int?)null
            }).ToList()
        };

    private static string? Tone(Tone tone) => tone == Reports.Model.Tone.None ? null : tone.ToString();

    private static async Task<IResult> Execute(
        string id,
        HttpRequest request,
        ChartingLimits limits,
        ReportLimits reportLimits,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        Func<ReportDocument, ReportInput, IResult> respond)
    {
        if (ReportRegistry.Find(id) is not { } definition)
            return ProblemResults.From(ReportProblem.UnknownReport(id, ReportRegistry.IdList));

        var body = await RequestBodyReader.ReadAsync(request, limits.MaxRequestBodyBytes, cancellationToken);
        if (body is not Result<string>.Ok text)
            return ProblemResults.From(((Result<string>.Fail)body).Problem);

        var parsed = ReportInputParser.Parse(text.Value, reportLimits);
        if (parsed is not Result<ReportInput>.Ok input)
            return ProblemResults.From(((Result<ReportInput>.Fail)parsed).Problem);

        var composed = ReportRegistry.Compose(definition, input.Value);
        if (composed is not Result<ReportDocument>.Ok document)
            return ProblemResults.From(((Result<ReportDocument>.Fail)composed).Problem);

        loggers.CreateLogger("Scottplotz.Reports").LogInformation(
            "Report {Report} from {Entries} entries and {Rows} rows",
            definition.Id,
            input.Value.Entries.Count,
            input.Value.Rows.Count);

        return respond(document.Value, input.Value);
    }
}

public sealed class ReportPageResult(ReportPage page) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var bytes = Encoding.UTF8.GetBytes(page.Html);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        httpContext.Response.ContentLength = bytes.Length;
        httpContext.Response.Headers.ContentSecurityPolicy = page.ContentSecurityPolicy;
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        await httpContext.Response.Body.WriteAsync(bytes, httpContext.RequestAborted);
    }
}

public sealed class ReportsApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApiKeySource keys)
    {
        var expected = keys.Current;
        var guarded = HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/reports");
        if (string.IsNullOrEmpty(expected)
            || !guarded
            || ApiKeyCheck.Matches(context.Request.Headers["X-Api-Key"].FirstOrDefault(), expected))
        {
            await next(context);
            return;
        }

        await ProblemResults.From(ChartProblem.Unauthorized()).ExecuteAsync(context);
    }
}
