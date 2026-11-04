using Microsoft.Extensions.Configuration;
using Scottplotz.Charting;
using Scottplotz.Parsing;

namespace Scottplotz.Tests.Parsing;

public sealed class HintParsingTests
{
    private static readonly ChartingLimits Limits = ChartingLimits.Default;

    [Fact]
    public void Reads_every_hint_and_resolves_defaults_for_the_ones_left_out()
    {
        var request = Request(
            """
            {
              "data": [1, 2, 3],
              "hints": {
                "type": "bar",
                "title": " Fruit ",
                "xLabel": "Kind",
                "yLabel": "Count",
                "width": 640,
                "height": 480,
                "theme": "dark"
              }
            }
            """);

        Assert.Equal(ChartKind.Bar, request.Hints.Type);
        Assert.Equal("Fruit", request.Hints.Title);
        Assert.Equal("Kind", request.Hints.XLabel);
        Assert.Equal("Count", request.Hints.YLabel);
        Assert.Equal(640, request.Hints.Width);
        Assert.Equal(480, request.Hints.Height);
        Assert.Equal(ChartTheme.Dark, request.Hints.Theme);
        Assert.Equal([1d, 2d, 3d], Assert.IsType<Column.Numeric>(Assert.Single(request.Table.Columns)).Values);

        var resolved = ChartHints.None.Resolve(Limits);
        Assert.Null(resolved.Type);
        Assert.Equal(800, resolved.Width);
        Assert.Equal(450, resolved.Height);
        Assert.Equal(ChartTheme.Light, resolved.Theme);
    }

    [Fact]
    public void Rejects_an_unknown_hint_and_names_the_allowed_ones()
    {
        var problem = Fail("""{ "data": [1], "hints": { "colour": "red" } }""");

        Assert.Equal("unknown-hint", problem.Code);
        Assert.Contains("colour", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("xLabel", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("theme", problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("area", "line, bar, scatter, histogram, timeseries, multi-line, pie")]
    [InlineData("Line", "line, bar, scatter, histogram, timeseries, multi-line, pie")]
    [InlineData("pie chart", "line, bar, scatter, histogram, timeseries, multi-line, pie")]
    public void Rejects_an_unknown_chart_type(string type, string allowed)
    {
        var problem = Fail($$"""{ "data": [1], "hints": { "type": "{{type}}" } }""");

        Assert.Equal("invalid-hint", problem.Code);
        Assert.Contains("type", problem.Detail, StringComparison.Ordinal);
        Assert.Contains(allowed, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_theme_outside_light_and_dark()
    {
        var problem = Fail("""{ "data": [1], "hints": { "theme": "blue" } }""");

        Assert.Equal("invalid-hint", problem.Code);
        Assert.Contains("light, dark", problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(4001)]
    public void Rejects_a_dimension_outside_the_configured_range(int width)
    {
        var problem = Fail($$"""{ "data": [1], "hints": { "width": {{width}} } }""");

        Assert.Equal("invalid-hint", problem.Code);
        Assert.Contains("width", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("100", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("4000", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_the_dimension_boundaries()
    {
        var request = Request("""{ "data": [1], "hints": { "width": 100, "height": 4000 } }""");

        Assert.Equal(100, request.Hints.Width);
        Assert.Equal(4000, request.Hints.Height);
    }

    [Fact]
    public void Rejects_a_fractional_dimension()
    {
        var problem = Fail("""{ "data": [1], "hints": { "height": 450.5 } }""");

        Assert.Equal("invalid-hint", problem.Code);
        Assert.Contains("450.5", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_hint_with_the_wrong_json_type()
    {
        var problem = Fail("""{ "data": [1], "hints": { "title": 12 } }""");

        Assert.Equal("invalid-hint", problem.Code);
        Assert.Contains("title", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("string", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_hints_that_are_not_an_object()
    {
        var problem = Fail("""{ "data": [1], "hints": ["pie"] }""");

        Assert.Equal("invalid-hint", problem.Code);
        Assert.Contains("object", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_csv_hints_from_the_query_string()
    {
        var result = ChartInputParser.ParseRequest(
            "price\n1\n2\n",
            "text/csv",
            [new KeyValuePair<string, string?>("type", "histogram"), new KeyValuePair<string, string?>("width", "320")],
            Limits);

        var request = Assert.IsType<Result<ChartRequest>.Ok>(result).Value;
        Assert.Equal(ChartKind.Histogram, request.Hints.Type);
        Assert.Equal(320, request.Hints.Width);
        Assert.Equal(DataOrigin.Csv, request.Table.Origin);
    }

    [Fact]
    public void Rejects_an_unknown_csv_hint()
    {
        var result = ChartInputParser.ParseRequest(
            "price\n1\n",
            "text/csv",
            [new KeyValuePair<string, string?>("colour", "red")],
            Limits);

        var problem = Assert.IsType<Result<ChartRequest>.Fail>(result).Problem;
        Assert.Equal("unknown-hint", problem.Code);
    }

    [Fact]
    public void Rejects_json_hints_placed_in_the_query_string()
    {
        var result = ChartInputParser.ParseRequest(
            """{ "data": [1, 2] }""",
            "application/json",
            [new KeyValuePair<string, string?>("type", "pie")],
            Limits);

        var problem = Assert.IsType<Result<ChartRequest>.Fail>(result).Problem;
        Assert.Equal("hint-in-query", problem.Code);
        Assert.Contains("type", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Loads_limits_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Charting:MaxPoints"] = "25",
                ["Charting:MinDimension"] = "200",
                ["Charting:InvalidNumberPolicy"] = "Drop",
                ["Charting:Port"] = "6000"
            })
            .Build();

        var limits = ChartingLimits.Bind(configuration);

        Assert.Equal(25, limits.MaxPoints);
        Assert.Equal(200, limits.MinDimension);
        Assert.Equal(InvalidNumberPolicy.Drop, limits.InvalidNumbers);
        Assert.Equal(6000, limits.Port);
        Assert.Equal(800, limits.DefaultWidth);
    }

    [Fact]
    public void Rejects_an_unknown_invalid_number_policy()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ChartingLimits.ParsePolicy("skip"));

        Assert.Contains("Reject", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Drop", exception.Message, StringComparison.Ordinal);
    }

    private static ChartRequest Request(string json)
    {
        var result = ChartInputParser.ParseRequest(json, "application/json", [], Limits);
        return Assert.IsType<Result<ChartRequest>.Ok>(result).Value;
    }

    private static ChartProblem Fail(string json)
    {
        var result = ChartInputParser.ParseRequest(json, "application/json", [], Limits);
        return Assert.IsType<Result<ChartRequest>.Fail>(result).Problem;
    }
}
