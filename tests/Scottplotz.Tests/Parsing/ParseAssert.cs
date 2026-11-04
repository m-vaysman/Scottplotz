using Scottplotz.Charting;
using Scottplotz.Parsing;

namespace Scottplotz.Tests.Parsing;

internal static class ParseAssert
{
    public static ChartTable Ok(
        string body,
        string? contentType = "application/json",
        InvalidNumberPolicy policy = InvalidNumberPolicy.Reject,
        int maxPoints = 100_000)
    {
        var result = ChartInputParser.Parse(body, contentType, new ParseLimits(maxPoints, policy));
        return Assert.IsType<Result<ChartTable>.Ok>(result).Value;
    }

    public static ChartProblem Fail(
        string body,
        string? contentType = "application/json",
        InvalidNumberPolicy policy = InvalidNumberPolicy.Reject,
        int maxPoints = 100_000)
    {
        var result = ChartInputParser.Parse(body, contentType, new ParseLimits(maxPoints, policy));
        return Assert.IsType<Result<ChartTable>.Fail>(result).Problem;
    }
}
