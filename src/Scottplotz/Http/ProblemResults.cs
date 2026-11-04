using Scottplotz.Charting;

namespace Scottplotz.Http;

public static class ProblemResults
{
    public static IResult From(ChartProblem problem) =>
        Results.Problem(
            title: problem.Title,
            detail: problem.Detail,
            statusCode: problem.Status,
            extensions: new Dictionary<string, object?> { ["code"] = problem.Code });
}
