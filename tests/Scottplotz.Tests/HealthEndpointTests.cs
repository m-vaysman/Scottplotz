using System.Net;
using System.Net.Http.Json;

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
        Assert.Equal(HealthReport.Create(typeof(HealthReport).Assembly.GetName().Version).Version, body.Version);
    }

    [Fact]
    public async Task Swagger_document_lists_health()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/health", json, StringComparison.Ordinal);
    }
}
