using System.Net;

namespace Scottplotz.Tests.Http;

public sealed class ReportsGalleryTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public ReportsGalleryTests(TestAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task The_reports_gallery_is_served()
    {
        var response = await _client.GetAsync("/reports.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("fetch('/reports')", html);
        Assert.Contains("/reports/sample", html);
        Assert.Contains("/data", html);
        Assert.Contains(">Render<", html);
        Assert.Contains("X-Api-Key", html);
        Assert.Contains("id=\"page\" title=\"Rendered report\" sandbox", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public async Task The_main_page_links_to_the_reports_gallery()
    {
        Assert.Contains("href=\"/reports.html\"", await _client.GetStringAsync("/"));
    }
}
