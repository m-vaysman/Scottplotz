namespace Scottplotz.Http;

public sealed class PngChartResult(byte[] png, string rule, string typeSource) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "image/png";
        httpContext.Response.ContentLength = png.Length;
        httpContext.Response.Headers["X-Chart-Rule"] = rule;
        httpContext.Response.Headers["X-Chart-Type-Source"] = typeSource;
        await httpContext.Response.Body.WriteAsync(png);
    }
}
