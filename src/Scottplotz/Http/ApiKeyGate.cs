using System.Security.Cryptography;
using System.Text;
using Scottplotz.Charting;

namespace Scottplotz.Http;

public interface IApiKeySource
{
    string? Current { get; }
}

public sealed class EnvironmentApiKeySource : IApiKeySource
{
    public const string VariableName = "SCOTTPLOTZ_API_KEY";

    public string? Current => Environment.GetEnvironmentVariable(VariableName);
}

public static class ApiKeyCheck
{
    public static bool Matches(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
            return false;

        var actual = Encoding.UTF8.GetBytes(provided);
        var required = Encoding.UTF8.GetBytes(expected);
        return actual.Length == required.Length && CryptographicOperations.FixedTimeEquals(actual, required);
    }
}

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApiKeySource keys)
    {
        var expected = keys.Current;
        var path = context.Request.Path;
        var guarded = path.StartsWithSegments("/mcp")
            || (path.StartsWithSegments("/chart") && !path.StartsWithSegments("/chart-types"));
        if (string.IsNullOrEmpty(expected) || !guarded)
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (ApiKeyCheck.Matches(provided, expected))
        {
            await next(context);
            return;
        }

        await ProblemResults.From(ChartProblem.Unauthorized()).ExecuteAsync(context);
    }
}
