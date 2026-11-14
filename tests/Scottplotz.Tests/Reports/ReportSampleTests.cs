using System.Net;
using System.Text;
using System.Text.Json;
using Scottplotz.Charting;
using Scottplotz.Http;
using Scottplotz.Recon;
using Scottplotz.Reports;
using Scottplotz.Reports.Model;
using Scottplotz.Reports.Rendering;
using Scottplotz.Tests.Http;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class ReportSampleTests : IClassFixture<TestAppFactory>, IClassFixture<ApiKeyFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _guarded;

    public ReportSampleTests(TestAppFactory factory, ApiKeyFactory guarded)
    {
        _client = factory.CreateClient();
        _guarded = guarded.CreateClient();
    }

    public static TheoryData<string> Ids() => new(ReportRegistry.All.Where(definition => !definition.IsPending).Select(definition => definition.Id));

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public void The_registry_lists_the_thirteen_reports_in_catalogue_order()
    {
        Assert.Equal(
            ["BO1", "BO2", "BO3", "BO4", "BO5", "BO6", "MO1", "MO2", "MO3", "MO4", "MO5", "MO6", "MO7"],
            ReportRegistry.All.Select(definition => definition.Id));
        Assert.Equal(ReportCatalogue.All.Select(info => info.Id), ReportRegistry.All.Select(definition => definition.Id));
        Assert.DoesNotContain(ReportRegistry.All, definition => definition.IsPending);
        Assert.Equal("ageing", ReportRegistry.Find("MO5")!.Info.Slug);
        Assert.Same(ReportRegistry.Find("mo5"), ReportRegistry.Find("Ageing"));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_report_is_written_from_the_sample_with_every_check_passing(string id)
    {
        var document = Compose(id, Sample());

        Assert.NotEmpty(document.Blocks);
        Assert.All(Checks(document), check => Assert.Equal("✓", CellText.Of(check)));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_report_page_is_the_same_bytes_each_time(string id)
    {
        var first = ReportHtml.Render(Compose(id, Sample()), ReconTheme.Auto);
        var second = ReportHtml.Render(Compose(id, Sample()), ReconTheme.Auto);

        Assert.Equal(first.Html, second.Html);
        Assert.DoesNotContain("<script", first.Html);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_report_survives_an_input_with_no_entries_and_no_rows(string id)
    {
        var input = Input(Body("", ""));

        var result = ReportRegistry.Compose(ReportRegistry.Find(id)!, input);

        Assert.True(result is Result<ReportDocument>.Ok or Result<ReportDocument>.Fail { Problem.Code: "report-unavailable" });
    }

    [Fact]
    public async Task The_listing_and_the_sample_are_served()
    {
        var listing = JsonSerializer.Deserialize<JsonElement>(await _client.GetStringAsync("/reports"));
        var sample = await _client.GetAsync("/reports/sample");

        Assert.Equal(13, listing.GetArrayLength());
        Assert.Equal("BO1", listing[0].GetProperty("id").GetString());
        Assert.Equal("exception-blotter", listing[0].GetProperty("slug").GetString());
        Assert.Equal("application/json", sample.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(Sample().Entries);
    }

    [Fact]
    public async Task A_report_is_returned_as_a_page_and_as_data()
    {
        var page = await _client.PostAsync("/reports/MO5", Json(ReportEndpoints.Sample));
        var data = await _client.PostAsync("/reports/ageing/data", Json(ReportEndpoints.Sample));

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ReportHtml.Policy, Assert.Single(page.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("MO5 · Ageing and settlement status", await page.Content.ReadAsStringAsync());
        var json = JsonSerializer.Deserialize<JsonElement>(await data.Content.ReadAsStringAsync());
        Assert.Equal("MO5", json.GetProperty("id").GetString());
        Assert.Equal("table", json.GetProperty("blocks")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task An_unknown_report_lists_the_reports_and_a_bad_body_says_where()
    {
        var unknown = await _client.PostAsync("/reports/ZZ9", Json("{}"));
        var bad = await _client.PostAsync("/reports/MO5", Json("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [], "rows": [], "colour": 1 }"""));

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("MO5 (ageing)", JsonSerializer.Deserialize<JsonElement>(await unknown.Content.ReadAsStringAsync()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("colour", JsonSerializer.Deserialize<JsonElement>(await bad.Content.ReadAsStringAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Posting_needs_the_api_key_when_one_is_set_but_the_listing_stays_open()
    {
        var denied = await _guarded.PostAsync("/reports/MO5", Json(ReportEndpoints.Sample));
        var listing = await _guarded.GetAsync("/reports");
        var request = new HttpRequestMessage(HttpMethod.Post, "/reports/MO5") { Content = Json(ReportEndpoints.Sample) };
        request.Headers.Add("X-Api-Key", "test-key");
        var allowed = await _guarded.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
