using Microsoft.AspNetCore.Diagnostics;
using Scottplotz.Charting;
using Scottplotz.Http;
using Scottplotz.Logging;
using Scottplotz.Meaning;
using Scottplotz.Rendering;

namespace Scottplotz.Hosting;

public static class ScottplotzHost
{
    public static WebApplication Create(string[] args)
    {
        EmbeddedFont.Register();

        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration
            .AddJsonFile(UserPaths.SettingsFile, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
        builder.Logging.AddProvider(new JsonFileLoggerProvider(UserPaths.LogFile));

        builder.Services.AddSingleton(services => ChartingLimits.Bind(services.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<IColumnMeaningAdvisor, DisabledColumnMeaningAdvisor>();
        builder.Services.AddSingleton<IApiKeySource, EnvironmentApiKeySource>();
        builder.Services.AddSingleton<IChartImageRenderer, ScottPlotChartRenderer>();

        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.Limits.MaxRequestBodySize = ChartingLimits.Bind(context.Configuration).MaxRequestBodyBytes;
        });

        var configuredUrls = builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (string.IsNullOrWhiteSpace(configuredUrls))
            builder.WebHost.UseUrls($"http://0.0.0.0:{ChartingLimits.Bind(builder.Configuration).Port}");

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Scottplotz",
                Version = "v1",
                Description = "Send data, get a PNG chart."
            });
        });

        var app = builder.Build();

        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Scottplotz");
            logger.LogError(error, "Request failed");

            var activeLimits = context.RequestServices.GetRequiredService<ChartingLimits>();
            var problem = error is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }
                ? ChartProblem.BodyTooLarge(activeLimits.MaxRequestBodyBytes)
                : ChartProblem.Unexpected();
            await ProblemResults.From(problem).ExecuteAsync(context);
        }));

        app.UseMiddleware<ApiKeyMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Scottplotz");
            options.RoutePrefix = "swagger";
        });

        app.MapGet("/health", () => Results.Ok(HealthReport.Create(ThisAssemblyVersion())))
            .WithName("Health")
            .WithTags("Meta")
            .Produces<HealthReport>(StatusCodes.Status200OK);

        ChartEndpoints.Map(app);
        return app;
    }

    private static Version? ThisAssemblyVersion() =>
        typeof(HealthReport).Assembly.GetName().Version;
}
