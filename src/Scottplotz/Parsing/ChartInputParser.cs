using Scottplotz.Charting;

namespace Scottplotz.Parsing;

public static class ChartInputParser
{
    public static Result<ChartTable> Parse(string? body, string? contentType, ParseLimits limits)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Result<ChartTable>.Error(ChartProblem.Empty("The request body is empty."));

        return IsCsv(contentType)
            ? CsvParser.Parse(body, limits)
            : JsonInputParser.Parse(body, limits);
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
