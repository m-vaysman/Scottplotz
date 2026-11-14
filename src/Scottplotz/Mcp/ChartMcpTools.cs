using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Scottplotz.Charting;
using Scottplotz.ChartTypes;
using Scottplotz.Meaning;
using Scottplotz.Parsing;
using Scottplotz.Rendering;
using Scottplotz.Rules;

namespace Scottplotz.Mcp;

[McpServerToolType]
public static class ChartMcpTools
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "list_chart_types"), Description("List every chart type this service can draw. Same data as GET /chart-types.")]
    public static string ListChartTypes() =>
        JsonSerializer.Serialize(ChartTypeRegistry.Describe(), Json);

    [McpServerTool(Name = "suggest_chart_type"), Description("Rank chart types for this data. Does not draw a picture.")]
    public static string SuggestChartType(
        [Description("JSON body in the same shape as POST /chart, for example {\"data\":[1,2,3]}.")] string data,
        ChartingLimits limits)
    {
        var parsed = ChartInputParser.Parse(data, "application/json", limits.ToParseLimits());
        if (parsed is not Result<ChartTable>.Ok table)
            throw new ModelContextProtocol.McpException(((Result<ChartTable>.Fail)parsed).Problem.Detail);

        return JsonSerializer.Serialize(ChartSuggestions.Rank(table.Value), Json);
    }

    [McpServerTool(Name = "render_chart"), Description("Draw a PNG with the same render call as POST /chart.")]
    public static async Task<CallToolResult> RenderChart(
        [Description("JSON body in the same shape as POST /chart.")] string data,
        IChartImageRenderer renderer,
        ChartingLimits limits,
        IColumnMeaningAdvisor advisor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        [Description("Chart type id from list_chart_types. Omit it to let the service choose.")] string? type = null,
        [Description("Title drawn above the chart.")] string? title = null,
        [Description("Optional width, height, theme, xLabel, and yLabel.")] JsonElement? options = null)
    {
        if (data.Length > limits.MaxRequestBodyBytes)
            return Error(ChartProblem.BodyTooLarge(data.Length, limits.MaxRequestBodyBytes).Detail);

        var parsed = ChartInputParser.ParseRequest(data, "application/json", [], limits);
        if (parsed is not Result<ChartRequest>.Ok request)
            return Error(((Result<ChartRequest>.Fail)parsed).Problem.Detail);

        var hints = Merge(request.Value.Hints, type, title, options, limits);
        if (hints is not Result<ChartHints>.Ok valid)
            return Error(((Result<ChartHints>.Fail)hints).Problem.Detail);

        var rendered = await renderer.Render(
            new ChartRequest(request.Value.Table, valid.Value),
            limits,
            advisor,
            cancellationToken);
        if (rendered is not Result<ChartImage>.Ok image)
            return Error(((Result<ChartImage>.Fail)rendered).Problem.Detail);

        var chart = image.Value;
        loggerFactory.CreateLogger("Scottplotz.Chart").LogInformation(
            "Chart {Rule} from {Points} points via {TypeSource}",
            chart.Explanation.EffectiveRule,
            chart.Explanation.Shape.PointCount,
            chart.Explanation.TypeSource);

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = $"type={chart.Explanation.Settings.Type} source={chart.Explanation.TypeSource}"
                },
                ImageContentBlock.FromBytes(chart.Png, "image/png")
            ]
        };
    }

    private static CallToolResult Error(string detail) =>
        new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = detail }]
        };

    private static Result<ChartHints> Merge(
        ChartHints body,
        string? type,
        string? title,
        JsonElement? options,
        ChartingLimits limits)
    {
        var payload = new Dictionary<string, object?>();
        Add(payload, "type", type ?? (body.Type is { } kind ? ChartKinds.Token(kind) : null));
        Add(payload, "title", title ?? body.Title);
        Add(payload, "xLabel", ReadOption(options, "xLabel") ?? body.XLabel);
        Add(payload, "yLabel", ReadOption(options, "yLabel") ?? body.YLabel);
        Add(payload, "width", ReadOption(options, "width") ?? body.Width?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(payload, "height", ReadOption(options, "height") ?? body.Height?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(payload, "theme", ReadOption(options, "theme") ?? Theme(body.Theme));
        return HintParser.FromElement(JsonSerializer.SerializeToElement(payload), limits);
    }

    private static void Add(IDictionary<string, object?> payload, string name, string? value)
    {
        if (value is not null)
            payload[name] = value;
    }

    private static string? ReadOption(JsonElement? options, string name) =>
        options is { ValueKind: JsonValueKind.Object } bag && bag.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            }
            : null;

    private static string? Theme(ChartTheme? theme) =>
        theme switch
        {
            ChartTheme.Dark => "dark",
            ChartTheme.Light => "light",
            _ => null
        };
}
