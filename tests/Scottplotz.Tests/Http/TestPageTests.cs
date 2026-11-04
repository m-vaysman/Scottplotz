using System.Net;

namespace Scottplotz.Tests.Http;

public sealed class TestPageTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public TestPageTests(TestAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task The_test_page_is_served_at_the_root()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Render", html);
        Assert.Contains("Explain", html);
        Assert.Contains("sample-csv", html);
        Assert.Contains(">Auto<", html);
        Assert.Contains("/chart-types", html);
        Assert.Contains("https://scottplot.net", html);
        Assert.Contains("Scott Harden / Harden Technologies, LLC", html);
        Assert.Contains("thin REST API", html);
    }
}
