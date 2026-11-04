using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scottplotz.Http;

namespace Scottplotz.Tests.Http;

public sealed class ChartEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChartEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Chart_returns_a_png_and_the_rule_that_fired()
    {
        var response = await PostJson("/chart", """{ "data": [1, 3, 2, 5, 4] }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("plain-numbers-line", response.Headers.GetValues("X-Chart-Rule").Single());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes.Take(4));
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

public sealed class LimitedFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
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
    private readonly HttpClient _client;

    public ApiKeyEndpointTests(ApiKeyFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Chart_requires_the_configured_key()
    {
        var missing = await _client.PostAsync(
            "/chart",
            new StringContent("""{ "data": [1, 2, 3] }""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/chart")
        {
            Content = new StringContent("""{ "data": [1, 2, 3] }""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", "test-key");
        var accepted = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("plain-numbers-line", accepted.Headers.GetValues("X-Chart-Rule").Single());
    }

    [Fact]
    public async Task Health_stays_open_when_a_key_is_configured()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class ApiKeyFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IApiKeySource>(new FixedApiKey("test-key"));
        });
    }

    private sealed class FixedApiKey(string key) : IApiKeySource
    {
        public string? Current => key;
    }
}
