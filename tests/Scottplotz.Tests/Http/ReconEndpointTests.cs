using System.Net;
using System.Text;
using System.Text.Json;
using Scottplotz.Recon;

namespace Scottplotz.Tests.Http;

public sealed class ReconEndpointTests : IClassFixture<TestAppFactory>, IClassFixture<ApiKeyFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _guarded;

    public ReconEndpointTests(TestAppFactory factory, ApiKeyFactory guarded)
    {
        _client = factory.CreateClient();
        _guarded = guarded.CreateClient();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string Sample(string view) => ReconViews.Find(view)!.Assets.Sample;

    [Fact]
    public async Task Views_match_the_registry()
    {
        var response = await _client.GetAsync("/recon/views");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ReconViews.All.Select(view => view.Name),
            body.EnumerateArray().Select(view => view.GetProperty("name").GetString()));
        var dumbbell = body.EnumerateArray().First(view => view.GetProperty("name").GetString() == "dumbbell");
        Assert.Equal(["sortBy", "maxRows"], dumbbell.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("name").GetString()));
        Assert.Equal("input", dumbbell.GetProperty("options")[0].GetProperty("default").GetString());
        Assert.Equal(400, dumbbell.GetProperty("options")[1].GetProperty("default").GetInt32());
    }

    [Fact]
    public async Task The_sample_request_renders_a_page_with_a_matching_policy()
    {
        var sample = await _client.GetAsync("/recon/dumbbell/sample");
        Assert.Equal("application/json", sample.Content.Headers.ContentType?.MediaType);

        var response = await _client.PostAsync("/recon/dumbbell", Json(await sample.Content.ReadAsStringAsync()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        var html = await response.Content.ReadAsStringAsync();
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains($"content=\"{policy}\"", html);
        Assert.Contains("<title>Purchase order lines against supplier invoices</title>", html);
        Assert.Contains("id=\"rd-model\"", html);
    }

    [Fact]
    public async Task The_same_request_returns_the_same_bytes()
    {
        var first = await (await _client.PostAsync("/recon/dumbbell", Json(Sample("dumbbell")))).Content.ReadAsByteArrayAsync();
        var second = await (await _client.PostAsync("/recon/dumbbell", Json(Sample("dumbbell")))).Content.ReadAsByteArrayAsync();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Explain_returns_the_resolved_model()
    {
        var response = await _client.PostAsync("/recon/dumbbell/explain", Json(Sample("dumbbell")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var model = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("dumbbell", model.GetProperty("view").GetProperty("name").GetString());
        Assert.Equal(5, model.GetProperty("summary").GetProperty("counts").GetProperty("break").GetInt32());
        Assert.Equal("Ordered only", model.GetProperty("labels").GetProperty("leftOnly").GetString());
        Assert.Equal("break", model.GetProperty("records")[1].GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("POST", "/recon/nope")]
    [InlineData("POST", "/recon/nope/explain")]
    [InlineData("GET", "/recon/nope/sample")]
    public async Task An_unknown_view_lists_the_available_ones(string method, string path)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = Json("{}") });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("unknown-view", problem.GetProperty("code").GetString());
        Assert.Contains("dumbbell", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_bad_request_returns_problem_details()
    {
        var response = await _client.PostAsync("/recon/dumbbell", Json("""{ "records": [ { "key": "A" } ] }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("missing-sides", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Posting_needs_the_api_key_when_one_is_set_but_discovery_stays_open()
    {
        var denied = await _guarded.PostAsync("/recon/dumbbell", Json(Sample("dumbbell")));
        var explainDenied = await _guarded.PostAsync("/recon/dumbbell/explain", Json(Sample("dumbbell")));
        var views = await _guarded.GetAsync("/recon/views");
        var sample = await _guarded.GetAsync("/recon/dumbbell/sample");
        var request = new HttpRequestMessage(HttpMethod.Post, "/recon/dumbbell") { Content = Json(Sample("dumbbell")) };
        request.Headers.Add("X-Api-Key", "test-key");
        var allowed = await _guarded.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, explainDenied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, views.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sample.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
