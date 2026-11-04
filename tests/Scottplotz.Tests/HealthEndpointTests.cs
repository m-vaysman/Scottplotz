using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Scottplotz.Tests;

public sealed class HealthEndpointTests : IClassFixture<Http.TestAppFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(Http.TestAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_ok_and_version()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthReport>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        var version = typeof(HealthReport).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.Equal($"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}", body.Version);
    }

    [Fact]
    public async Task Swagger_document_lists_health()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/health", out _));
        Assert.True(paths.TryGetProperty("/chart", out _));
        Assert.True(paths.TryGetProperty("/chart/explain", out _));
    }
}
