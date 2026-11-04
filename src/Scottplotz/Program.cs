using Scottplotz;
using Scottplotz.Charting;
using Scottplotz.Meaning;
using Scottplotz.Rendering;

EmbeddedFont.Register();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(ChartingLimits.Bind(builder.Configuration));
builder.Services.AddSingleton<IColumnMeaningAdvisor, DisabledColumnMeaningAdvisor>();

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

app.Run();

static Version? ThisAssemblyVersion() =>
    typeof(HealthReport).Assembly.GetName().Version;

public partial class Program;
