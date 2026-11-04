using System.Text;
using Scottplotz.Charting;

namespace Scottplotz.Http;

public static class RequestBodyReader
{
    public static async Task<Result<string>> ReadAsync(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.ContentLength is long declared && declared > maxBytes)
            return Result<string>.Error(ChartProblem.BodyTooLarge((int)Math.Min(declared, int.MaxValue), maxBytes));

        var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await request.Body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                return Result<string>.Error(ChartProblem.BodyTooLarge(total, maxBytes));

            buffer.Write(chunk, 0, read);
        }

        return Result<string>.Success(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
