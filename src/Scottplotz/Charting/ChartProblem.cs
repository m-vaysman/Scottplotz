namespace Scottplotz.Charting;

public sealed record ChartProblem(string Code, string Title, string Detail, int Status = 400)
{
    public static ChartProblem MalformedJson(string reason) =>
        new("malformed-json", "Malformed JSON", $"The request body is not valid JSON. {reason}");

    public static ChartProblem MalformedCsv(string reason) =>
        new("malformed-csv", "Malformed CSV", $"The CSV could not be parsed. {reason}");

    public static ChartProblem MissingData() =>
        new("missing-data", "Missing data", "JSON body must be an object with a data property.");

    public static ChartProblem UnknownProperty(string name) =>
        new("unknown-property", "Unknown property", $"Unknown JSON property '{name}'. Allowed properties: data, hints.");

    public static ChartProblem Empty(string? detail = null) =>
        new("empty-data", "Empty data", detail ?? "Data is empty. Send at least one row of values.");

    public static ChartProblem DuplicateColumn(string name) =>
        new("duplicate-column", "Duplicate column", $"Column '{name}' appears more than once.");

    public static ChartProblem MismatchedLengths(string column, int count, string other, int otherCount) =>
        new(
            "mismatched-columns",
            "Mismatched column lengths",
            $"Column '{column}' has {count} values but column '{other}' has {otherCount}. Every column must be the same length.");

    public static ChartProblem CsvRowWidth(int row, int count, int headerCount) =>
        new(
            "mismatched-columns",
            "Mismatched column lengths",
            $"CSV row {row} has {count} values but the header has {headerCount} columns.");

    public static ChartProblem NullValue(string column, int row) =>
        new(
            "null-value",
            "Null value",
            $"Column '{column}' row {row} is null. Nulls are rejected. Set Charting:InvalidNumberPolicy to Drop to skip those rows.");

    public static ChartProblem NonFinite(string column, int row, string kind) =>
        new(
            "non-finite",
            "Non-finite number",
            $"Column '{column}' row {row} is {kind}. Non-finite numbers are rejected. Set Charting:InvalidNumberPolicy to Drop to skip those rows.");

    public static ChartProblem NonNumeric(string column, int row, string value) =>
        new("non-numeric", "Non-numeric value", $"Column '{column}' row {row} has a non-numeric value '{value}'.");

    public static ChartProblem UnsupportedValue(string column, int row, string kind) =>
        new("unsupported-value", "Unsupported value", $"Column '{column}' row {row} has an unsupported {kind} value.");

    public static ChartProblem TooManyPoints(int count, int max) =>
        new("too-many-points", "Too many points", $"This request has {count} points, which is above the limit of {max}.");

    public static ChartProblem PlainArrayShape() =>
        new("plain-array", "Invalid plain array", "A plain array must contain only numbers. Use rows of objects for records, or an object of arrays for columns.");

    public static ChartProblem UnknownHint(string name) =>
        new("unknown-hint", "Unknown hint", $"Unknown hint '{name}'. Allowed hints: {HintNames.List}.");

    public static ChartProblem DuplicateHint(string name) =>
        new("duplicate-hint", "Duplicate hint", $"Hint '{name}' was specified more than once.");

    public static ChartProblem InvalidHint(string name, string value, string allowed) =>
        new("invalid-hint", "Invalid hint", $"Hint '{name}' is '{value}'. {allowed}");

    public static ChartProblem HintType(string name, string expected) =>
        new("invalid-hint", "Invalid hint", $"Hint '{name}' must be {expected}.");

    public static ChartProblem HintInQuery(string name) =>
        new("hint-in-query", "Hint in query string", $"Remove hint '{name}' from the query string. JSON requests put hints in the body.");

    public static ChartProblem HintsShape() =>
        new("invalid-hint", "Invalid hint", "The hints property must be an object. Example: { \"type\": \"line\", \"title\": \"Prices\" }.");
}
