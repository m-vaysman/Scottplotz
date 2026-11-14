using Scottplotz.Charting;

namespace Scottplotz.Http;

public sealed class ReconApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApiKeySource keys)
    {
        var expected = keys.Current;
        var guarded = HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/recon");
        if (string.IsNullOrEmpty(expected)
            || !guarded
            || ApiKeyCheck.Matches(context.Request.Headers["X-Api-Key"].FirstOrDefault(), expected))
        {
            await next(context);
            return;
        }

        await ProblemResults.From(ChartProblem.Unauthorized()).ExecuteAsync(context);
    }
}
