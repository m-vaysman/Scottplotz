namespace Scottplotz.Http;

public sealed class PngChartResult(byte[] png, string rule) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "image/png";
        httpContext.Response.ContentLength = png.Length;
        httpContext.Response.Headers["X-Chart-Rule"] = rule;
        await httpContext.Response.Body.WriteAsync(png);
    }
}
