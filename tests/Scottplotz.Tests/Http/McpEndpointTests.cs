using System.Text;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Scottplotz.Tests.Rendering;

namespace Scottplotz.Tests.Http;

public sealed class McpEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public McpEndpointTests(TestAppFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task The_tool_list_is_exactly_the_three_chart_tools()
    {
        await using var client = await Connect(_factory.CreateClient());
        var names = (await client.ListToolsAsync()).Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["list_chart_types", "render_chart", "suggest_chart_type"], names);
    }

    [Fact]
    public async Task Render_chart_matches_the_rest_png()
    {
        const string body = """{ "data": [1, 3, 2, 5, 4] }""";
        var rest = await _factory.CreateClient().PostAsync(
            "/chart",
            new StringContent(body, Encoding.UTF8, "application/json"));
        rest.EnsureSuccessStatusCode();
        var restBytes = await rest.Content.ReadAsByteArrayAsync();

        await using var client = await Connect(_factory.CreateClient());
        var result = await client.CallToolAsync("render_chart", new Dictionary<string, object?>
        {
            ["data"] = body,
            ["type"] = "line"
        });

        Assert.NotEqual(true, result.IsError);
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal(restBytes, image.DecodedData.ToArray());
        Assert.Contains("source=user", Assert.Single(result.Content.OfType<TextContentBlock>()).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Suggest_returns_a_reason_and_no_image()
    {
        await using var client = await Connect(_factory.CreateClient());
        var result = await client.CallToolAsync("suggest_chart_type", new Dictionary<string, object?>
        {
            ["data"] = """{ "data": [1, 3, 2] }"""
        });

        Assert.NotEqual(true, result.IsError);
        Assert.Empty(result.Content.OfType<ImageContentBlock>());
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        Assert.Contains("plain-numbers-line", text, StringComparison.Ordinal);
        Assert.Contains("line", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_type_lists_the_registry_ids()
    {
        await using var client = await Connect(_factory.CreateClient());
        var result = await client.CallToolAsync("render_chart", new Dictionary<string, object?>
        {
            ["data"] = """{ "data": [1, 2, 3] }""",
            ["type"] = "area"
        });

        Assert.Equal(true, result.IsError);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        Assert.Contains("multi-line", text, StringComparison.Ordinal);
        Assert.Contains("pie", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Options_set_the_png_size()
    {
        await using var client = await Connect(_factory.CreateClient());
        var result = await client.CallToolAsync("render_chart", new Dictionary<string, object?>
        {
            ["data"] = """{ "data": [1, 3, 2] }""",
            ["options"] = new Dictionary<string, object?> { ["width"] = 320, ["height"] = 180 }
        });

        Assert.NotEqual(true, result.IsError);
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>()).DecodedData.ToArray();
        Assert.Equal((320, 180), PngSize.Read(image));
    }

    private static async Task<McpClient> Connect(HttpClient http)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "mcp") },
            http,
            loggerFactory: null,
            ownsHttpClient: false);
        return await McpClient.CreateAsync(transport);
    }
}
