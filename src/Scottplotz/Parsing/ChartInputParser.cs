using Scottplotz.Charting;

namespace Scottplotz.Parsing;

public static class ChartInputParser
{
    public static Result<ChartTable> Parse(string? body, string? contentType, ParseLimits limits) =>
        ParseRequest(body, contentType, [], ChartingLimits.ForParser(limits))
            .Bind(request => Result<ChartTable>.Success(request.Table));

    public static Result<ChartRequest> ParseRequest(
        string? body,
        string? contentType,
        IReadOnlyList<KeyValuePair<string, string?>> query,
        ChartingLimits limits)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Result<ChartRequest>.Error(ChartProblem.Empty("The request body is empty."));

        if (!IsCsv(contentType))
        {
            var hinted = query.FirstOrDefault(pair => HintParser.IsKnown(pair.Key));
            return hinted.Key is not null
                ? Result<ChartRequest>.Error(ChartProblem.HintInQuery(hinted.Key))
                : JsonInputParser.ParseRequest(body, limits);
        }

        return CsvParser.Parse(body, limits.ToParseLimits())
            .Bind(table => HintParser.FromPairs(query, limits)
                .Bind(hints => Result<ChartRequest>.Success(new ChartRequest(table, hints))));
    }

    public static bool IsCsv(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var mediaType = contentType.Split(';')[0].Trim();
        return mediaType.Equals("text/csv", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/csv", StringComparison.OrdinalIgnoreCase);
    }
}
