using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scottplotz.ChartTypes;
using Scottplotz.Tests.Rendering;

namespace Scottplotz.Tests.Http;

public sealed class ChartEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public ChartEndpointTests(TestAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Chart_returns_a_png_and_the_rule_that_fired()
    {
        var response = await PostJson("/chart", """{ "data": [1, 3, 2, 5, 4] }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("plain-numbers-line", response.Headers.GetValues("X-Chart-Rule").Single());
        Assert.Equal("rules", response.Headers.GetValues("X-Chart-Type-Source").Single());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes.Take(4));
        Assert.Equal((800, 450), PngSize.Read(bytes));

        var again = await PostJson("/chart", """{ "data": [1, 3, 2, 5, 4] }""");
        Assert.Equal(bytes, await again.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task An_unknown_type_lists_the_registry_ids()
    {
        var response = await PostJson("/chart", """{ "data": [1, 2, 3], "hints": { "type": "area" } }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("invalid-hint", json.RootElement.GetProperty("code").GetString());
        var detail = json.RootElement.GetProperty("detail").GetString();
        Assert.NotNull(detail);
        foreach (var id in ChartTypeRegistry.Describe().Select(type => type.Id))
            Assert.Contains(id, detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_explicit_type_matches_the_automatic_png()
    {
        var automatic = await PostJson("/chart", """{ "data": [1, 3, 2, 5, 4] }""");
        var chosen = await PostJson("/chart", """{ "data": [1, 3, 2, 5, 4], "hints": { "type": "line" } }""");

        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Equal("user", chosen.Headers.GetValues("X-Chart-Type-Source").Single());
        Assert.Equal(
            await automatic.Content.ReadAsByteArrayAsync(),
            await chosen.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Chart_uses_the_requested_image_size()
    {
        var response = await PostJson(
            "/chart",
            """{ "data": [1, 3, 2, 5, 4], "hints": { "width": 321, "height": 222 } }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((321, 222), PngSize.Read(await response.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task Explain_returns_the_shape_rule_and_settings()
    {
        var response = await PostJson("/chart/explain", """{ "data": [1, 3, 2] }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("plainArray", json.RootElement.GetProperty("shape").GetProperty("origin").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("shape").GetProperty("pointCount").GetInt32());
        Assert.Equal("plain-numbers-line", json.RootElement.GetProperty("effectiveRule").GetString());
        Assert.Equal("line", json.RootElement.GetProperty("settings").GetProperty("type").GetString());
        Assert.Equal(800, json.RootElement.GetProperty("settings").GetProperty("width").GetInt32());
        Assert.Equal("light", json.RootElement.GetProperty("settings").GetProperty("theme").GetString());
        Assert.False(json.RootElement.GetProperty("advisorConsulted").GetBoolean());
    }

    [Fact]
    public async Task Csv_returns_a_time_series()
    {
        var content = new StringContent("date,price\n2026-11-03,101.2\n2026-11-04,102\n", Encoding.UTF8, "text/csv");
        var response = await _client.PostAsync("/chart", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("timeseries", response.Headers.GetValues("X-Chart-Rule").Single());
    }

    [Fact]
    public async Task A_pie_hint_overrides_the_rule()
    {
        var response = await PostJson(
            "/chart",
            """
            {
              "data": [{ "fruit": "apples", "count": 4 }, { "fruit": "pears", "count": 7 }],
              "hints": { "type": "pie", "title": "Fruit" }
            }
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("hint-pie", response.Headers.GetValues("X-Chart-Rule").Single());
    }

    [Theory]
    [InlineData("""{ "data": [1], "hints": { "colour": "red" } }""", "unknown-hint")]
    [InlineData("{}", "missing-data")]
    [InlineData("""{ "hints": {} }""", "missing-data")]
    [InlineData("{", "malformed-json")]
    [InlineData("""{ "data": [] }""", "empty-data")]
    [InlineData("""{ "data": { "a": [1, 2], "b": [3] } }""", "mismatched-columns")]
    [InlineData("""{ "data": [1, null] }""", "null-value")]
    public async Task Bad_input_is_a_problem_detail(string body, string code)
    {
        var response = await PostJson("/chart", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    private Task<HttpResponseMessage> PostJson(string path, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return _client.PostAsync(path, content);
    }
}

public sealed class ChartLimitTests : IClassFixture<LimitedFactory>
{
    private readonly HttpClient _client;

    public ChartLimitTests(LimitedFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Too_many_points_is_rejected()
    {
        var response = await _client.PostAsync(
            "/chart",
            new StringContent("""{ "data": [1, 2, 3, 4, 5, 6] }""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("too-many-points", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_body_over_the_limit_is_rejected()
    {
        var payload = new string('x', 200);
        var response = await _client.PostAsync("/chart", new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("body-too-large", json.RootElement.GetProperty("code").GetString());
    }
}

public sealed class LimitedFactory : TestAppFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Charting:MaxPoints"] = "5",
                ["Charting:MaxRequestBodyBytes"] = "80"
            });
        });
    }
}

public sealed class ApiKeyEndpointTests : IClassFixture<ApiKeyFactory>
{
    private const string Key = "test-key";
    private readonly HttpClient _client;

    public ApiKeyEndpointTests(ApiKeyFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Chart_requires_the_configured_key()
    {
        var missing = await Post("/chart", null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var accepted = await Post("/chart", Key);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("plain-numbers-line", accepted.Headers.GetValues("X-Chart-Rule").Single());
    }

    [Theory]
    [InlineData("test-kez")]
    [InlineData("no")]
    [InlineData("")]
    [InlineData("TEST-KEY")]
    public async Task A_wrong_key_is_unauthorized(string key)
    {
        var response = await Post("/chart", key);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("unauthorized", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Explain_requires_the_same_key()
    {
        var missing = await Post("/chart/explain", null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);

        var accepted = await Post("/chart/explain", Key);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var json = await JsonDocument.ParseAsync(await accepted.Content.ReadAsStreamAsync());
        Assert.Equal("plain-numbers-line", json.RootElement.GetProperty("effectiveRule").GetString());
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/")]
    public async Task Routes_outside_chart_stay_open(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_key_is_not_written_to_the_log()
    {
        await Post("/chart", "test-kez");
        await Post("/chart", Key);

        var log = await File.ReadAllTextAsync(global::Scottplotz.Hosting.UserPaths.LogFile);
        Assert.DoesNotContain(Key, log, StringComparison.Ordinal);
        Assert.DoesNotContain("test-kez", log, StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> Post(string path, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("""{ "data": [1, 2, 3] }""", Encoding.UTF8, "application/json")
        };
        if (key is not null)
            request.Headers.TryAddWithoutValidation("X-Api-Key", key);
        return _client.SendAsync(request);
    }
}
