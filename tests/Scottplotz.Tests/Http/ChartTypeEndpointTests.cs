using System.Text.Json;
using Scottplotz.ChartTypes;

namespace Scottplotz.Tests.Http;

public sealed class ChartTypeEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public ChartTypeEndpointTests(TestAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Chart_types_match_the_registry()
    {
        var response = await _client.GetAsync("/chart-types");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        var expected = JsonSerializer.SerializeToElement(
            ChartTypeRegistry.Describe(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Equal(expected.GetArrayLength(), body.GetArrayLength());
        foreach (var (want, got) in expected.EnumerateArray().Zip(body.EnumerateArray()))
        {
            Assert.Equal(want.GetProperty("id").GetString(), got.GetProperty("id").GetString());
            Assert.Equal(want.GetProperty("displayName").GetString(), got.GetProperty("displayName").GetString());
            Assert.Equal(want.GetProperty("description").GetString(), got.GetProperty("description").GetString());
            Assert.Equal(want.GetProperty("dataShape").GetString(), got.GetProperty("dataShape").GetString());
            Assert.Equal(want.GetProperty("example").GetString(), got.GetProperty("example").GetString());
            var wantParameters = want.GetProperty("parameters").EnumerateArray()
                .Select(item => $"{item.GetProperty("name").GetString()}={item.GetProperty("defaultValue").GetString()}")
                .ToArray();
            var gotParameters = got.GetProperty("parameters").EnumerateArray()
                .Select(item => $"{item.GetProperty("name").GetString()}={item.GetProperty("defaultValue").GetString()}")
                .ToArray();
            Assert.Equal(wantParameters, gotParameters);
        }
    }
}
