using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scottplotz.Charting;

namespace Scottplotz.Recon;

public static partial class ReconRead
{
    public static Result<T> Fail<T>(ChartProblem problem) => Result<T>.Error(problem);

    public static JsonElement? Get(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    public static Result<bool> OnlyKnown(JsonElement parent, IReadOnlyList<string> allowed, string where)
    {
        var unknown = parent.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => !allowed.Contains(name, StringComparer.Ordinal));
        return unknown is null
            ? ReconResult.Ok(true)
            : Fail<bool>(ReconProblem.UnknownProperty(where, unknown, string.Join(", ", allowed)));
    }

    public static Result<string?> Text(JsonElement parent, string name, string path) =>
        Get(parent, name) switch
        {
            null => ReconResult.Ok<string?>(null),
            { ValueKind: JsonValueKind.String } value => ReconResult.Ok(value.GetString()),
            _ => Fail<string?>(ReconProblem.Invalid(path, "must be a string"))
        };

    public static Result<bool?> Flag(JsonElement parent, string name, string path) =>
        Get(parent, name) switch
        {
            null => ReconResult.Ok<bool?>(null),
            { ValueKind: JsonValueKind.True } => ReconResult.Ok<bool?>(true),
            { ValueKind: JsonValueKind.False } => ReconResult.Ok<bool?>(false),
            _ => Fail<bool?>(ReconProblem.Invalid(path, "must be true or false"))
        };

    public static Result<double?> Number(JsonElement parent, string name, string path) =>
        Get(parent, name) switch
        {
            null => ReconResult.Ok<double?>(null),
            { } value => Finite(value, path).Select(number => (double?)number)
        };

    public static Result<double> Finite(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? ReconResult.Ok(number)
            : Fail<double>(ReconProblem.Invalid(path, "must be a finite number"));

    public static Result<string?> Date(JsonElement parent, string name, string path) =>
        Text(parent, name, path).Bind(text =>
            text is null || IsIsoDate(text)
                ? ReconResult.Ok(text)
                : Fail<string?>(ReconProblem.Invalid(path, "must be an ISO 8601 date such as 2026-03-31")));

    public static bool IsIsoDate(string text) =>
        IsoDate().IsMatch(text) && ParseDate(text) is not null;

    public static DateTime? ParseDate(string text) =>
        DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var value)
            ? value
            : null;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();
}
