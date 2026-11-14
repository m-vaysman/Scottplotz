using System.Text;
using Scottplotz.Recon;

namespace Scottplotz.Http;

public sealed class HtmlPageResult(ReconPage page) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var bytes = Encoding.UTF8.GetBytes(page.Html);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        httpContext.Response.ContentLength = bytes.Length;
        httpContext.Response.Headers.ContentSecurityPolicy = page.ContentSecurityPolicy;
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        await httpContext.Response.Body.WriteAsync(bytes, httpContext.RequestAborted);
    }
}
