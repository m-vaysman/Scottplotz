using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scottplotz.Charting;
using Scottplotz.Meaning;
using Scottplotz.Rendering;

namespace Scottplotz.Tests.Http;

public sealed class ErrorCodeTests :
    IClassFixture<TestAppFactory>,
    IClassFixture<LimitedFactory>,
    IClassFixture<ApiKeyFactory>,
    IClassFixture<BrokenRendererFactory>
{
    private readonly IReadOnlyDictionary<string, HttpClient> _clients;

    public ErrorCodeTests(
        TestAppFactory app,
        LimitedFactory limited,
        ApiKeyFactory keyed,
        BrokenRendererFactory broken)
    {
        _clients = new Dictionary<string, HttpClient>
        {
            ["app"] = app.CreateClient(),
            ["limited"] = limited.CreateClient(),
            ["keyed"] = keyed.CreateClient(),
            ["broken"] = broken.CreateClient()
        };
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return Case("app", "malformed-json", "/chart", "{");
        yield return Case("app", "malformed-csv", "/chart", "\"unterminated", "text/csv");
        yield return Case("app", "missing-data", "/chart", "{}");
        yield return Case("app", "empty-data", "/chart", """{ "data": [] }""");
        yield return Case("app", "mismatched-columns", "/chart", """{ "data": { "a": [1, 2], "b": [3] } }""");
        yield return Case("app", "non-numeric", "/chart", """{ "data": { "price": [1, "n/a"] } }""");
        yield return Case("app", "null-value", "/chart", """{ "data": [1, null] }""");
        yield return Case("app", "non-finite", "/chart", """{ "data": [1, NaN] }""");
        yield return Case("app", "unknown-hint", "/chart", """{ "data": [1], "hints": { "colour": "red" } }""");
        yield return Case("app", "invalid-hint", "/chart", """{ "data": [1], "hints": { "type": "waffle" } }""");
        yield return Case("app", "hint-in-query", "/chart?type=pie", """{ "data": [1, 2, 3] }""");
        yield return Case("app", "unknown-property", "/chart", """{ "values": [1] }""");
        yield return Case("app", "duplicate-hint", "/chart?type=line&type=bar", "v\n1\n2\n", "text/csv");
        yield return Case("app", "unsupported-value", "/chart", """{ "data": { "price": [1, { "n": 1 }] } }""");
        yield return Case("app", "incompatible-hint", "/chart", """{ "data": { "x": [1, 2], "y": [3, 4] }, "hints": { "type": "pie" } }""");
        yield return Case("app", "no-rule", "/chart", """{ "data": { "label": ["a", "b"] } }""");
        yield return Case("limited", "too-many-points", "/chart", """{ "data": [1, 2, 3, 4, 5, 6] }""");
        yield return Case("limited", "body-too-large", "/chart", new string('x', 200), chunked: true);
        yield return Case("keyed", "unauthorized", "/chart", """{ "data": [1, 2, 3] }""", status: 401);
        yield return Case("broken", "server-error", "/chart", """{ "data": [1, 2, 3] }""", status: 500);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_documented_error_code(ErrorCase error)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, error.Path)
        {
            Content = error.Chunked
                ? new UnmeasuredContent(Encoding.UTF8.GetBytes(error.Body), error.ContentType)
                : new StringContent(error.Body, Encoding.UTF8, error.ContentType)
        };

        var response = await _clients[error.Host].SendAsync(request);

        Assert.Equal(error.Status, (int)response.StatusCode);
        Assert.NotEqual(200, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(error.Code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
    }

    private static object[] Case(
        string host,
        string code,
        string path,
        string body,
        string contentType = "application/json",
        int status = 400,
        bool chunked = false) =>
        [new ErrorCase(host, code, status, path, contentType, body, chunked)];

    private sealed class UnmeasuredContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnmeasuredContent(byte[] bytes, string mediaType)
        {
            _bytes = bytes;
            Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

public sealed record ErrorCase(
    string Host,
    string Code,
    int Status,
    string Path,
    string ContentType,
    string Body,
    bool Chunked);

public sealed class BrokenRendererFactory : TestAppFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IChartImageRenderer, BrokenRenderer>();
        });
    }

    private sealed class BrokenRenderer : IChartImageRenderer
    {
        public Task<Result<ChartImage>> Render(
            ChartRequest request,
            ChartingLimits limits,
            IColumnMeaningAdvisor advisor,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("forced");
    }
}
