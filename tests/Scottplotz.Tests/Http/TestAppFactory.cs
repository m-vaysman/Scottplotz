using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scottplotz.Charting;
using Scottplotz.Http;

namespace Scottplotz.Tests.Http;

public class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Charting:MaxPoints"] = ChartingLimits.Default.MaxPoints.ToString(),
                ["Charting:MinDimension"] = ChartingLimits.Default.MinDimension.ToString(),
                ["Charting:MaxDimension"] = ChartingLimits.Default.MaxDimension.ToString(),
                ["Charting:DefaultWidth"] = ChartingLimits.Default.DefaultWidth.ToString(),
                ["Charting:DefaultHeight"] = ChartingLimits.Default.DefaultHeight.ToString(),
                ["Charting:MaxRequestBodyBytes"] = ChartingLimits.Default.MaxRequestBodyBytes.ToString(),
                ["Charting:InvalidNumberPolicy"] = "Reject",
                ["Charting:Port"] = ChartingLimits.Default.Port.ToString()
            });
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IApiKeySource>(new FixedApiKey(null));
        });
    }

    protected sealed class FixedApiKey(string? key) : IApiKeySource
    {
        public string? Current => key;
    }
}

public sealed class ApiKeyFactory : TestAppFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IApiKeySource>(new FixedApiKey("test-key"));
        });
    }
}
