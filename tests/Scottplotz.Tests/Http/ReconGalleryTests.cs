using System.Net;
using Scottplotz.Recon;

namespace Scottplotz.Tests.Http;

public sealed class ReconGalleryTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public ReconGalleryTests(TestAppFactory factory) =>
        _client = factory.CreateClient();

    private static string RepoFile(string relative)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Scottplotz.sln")))
            folder = folder.Parent;

        return File.ReadAllText(Path.Combine(folder!.FullName, relative));
    }

    [Fact]
    public async Task The_gallery_page_is_served()
    {
        var response = await _client.GetAsync("/recon.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("/recon/views", html);
        Assert.Contains("/sample", html);
        Assert.Contains("/explain", html);
        Assert.Contains(">Render<", html);
        Assert.Contains("X-Api-Key", html);
        Assert.Contains("sandbox=\"allow-scripts\"", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public void The_view_list_in_the_docs_matches_the_registry()
    {
        var doc = RepoFile(Path.Combine("docs", "recon-views.md"));

        Assert.Contains($"There are {ReconViews.All.Count} views.", doc);
        Assert.All(ReconViews.All, view =>
        {
            Assert.Contains($"\n## {view.Name}\n", doc.Replace("\r\n", "\n"));
            Assert.All(view.Options, option => Assert.Contains($"| `{option.Name}` |", doc));
        });
    }
}
