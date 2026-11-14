using System.Globalization;
using System.Text.Json;
using Scottplotz.Charting;

namespace Scottplotz.Recon;

public static class ReconOptions
{
    public static Result<IReadOnlyList<KeyValuePair<string, ReconValue?>>> Resolve(JsonElement root, ReconView view) =>
        ReconRead.Get(root, "options") switch
        {
            null => ReconResult.Ok(Defaults(view, [])),
            { ValueKind: JsonValueKind.Object } options =>
                options.EnumerateObject()
                    .Select(given => view.Options.FirstOrDefault(option => option.Name == given.Name) is { } option
                        ? Value(option, given.Value).Select(value => new KeyValuePair<string, ReconValue?>(option.Name, value))
                        : ReconRead.Fail<KeyValuePair<string, ReconValue?>>(ReconProblem.UnknownOption(view.Name, given.Name, Names(view))))
                    .Collect()
                    .Select(given => Defaults(view, given)),
            _ => ReconRead.Fail<IReadOnlyList<KeyValuePair<string, ReconValue?>>>(ReconProblem.Invalid("options", "must be an object"))
        };

    private static string Names(ReconView view) =>
        view.Options.Count == 0 ? "none" : string.Join(", ", view.Options.Select(option => option.Name));

    private static IReadOnlyList<KeyValuePair<string, ReconValue?>> Defaults(
        ReconView view,
        IReadOnlyList<KeyValuePair<string, ReconValue?>> given) =>
        view.Options
            .Select(option => given
                .Where(item => item.Key == option.Name)
                .DefaultIfEmpty(new KeyValuePair<string, ReconValue?>(option.Name, option.Default))
                .Last())
            .ToList();

    private static Result<ReconValue?> Value(ReconOption option, JsonElement value) =>
        (option.Type, value.ValueKind) switch
        {
            (_, JsonValueKind.Null) => ReconResult.Ok(option.Default),
            (ReconOptionType.Number or ReconOptionType.Integer, JsonValueKind.Number) => Number(option, value.GetDouble()),
            (ReconOptionType.Number, _) => Bad(option, "must be a number"),
            (ReconOptionType.Integer, _) => Bad(option, "must be a whole number"),
            (ReconOptionType.Choice, JsonValueKind.String) when option.Allowed.Contains(value.GetString() ?? "", StringComparer.Ordinal) =>
                ReconResult.Ok<ReconValue?>(new ReconValue.Text(value.GetString() ?? "")),
            (ReconOptionType.Choice, _) => Bad(option, $"must be one of {string.Join(", ", option.Allowed)}"),
            (ReconOptionType.Text, JsonValueKind.String) => ReconResult.Ok<ReconValue?>(new ReconValue.Text(value.GetString() ?? "")),
            (ReconOptionType.Text, _) => Bad(option, "must be a string"),
            (ReconOptionType.Bool, JsonValueKind.True) => ReconResult.Ok<ReconValue?>(new ReconValue.Flag(true)),
            (ReconOptionType.Bool, JsonValueKind.False) => ReconResult.Ok<ReconValue?>(new ReconValue.Flag(false)),
            (ReconOptionType.Bool, _) => Bad(option, "must be true or false"),
            (ReconOptionType.Numbers, JsonValueKind.Array) when value.EnumerateArray().All(item =>
                item.ValueKind == JsonValueKind.Number && double.IsFinite(item.GetDouble())) =>
                ReconResult.Ok<ReconValue?>(new ReconValue.Json(value.GetRawText())),
            (ReconOptionType.Numbers, _) => Bad(option, "must be an array of numbers"),
            (ReconOptionType.Json, JsonValueKind.Object or JsonValueKind.Array) =>
                ReconResult.Ok<ReconValue?>(new ReconValue.Json(value.GetRawText())),
            (ReconOptionType.Json, _) => Bad(option, "must be an object or an array"),
            _ => Bad(option, "has an unsupported value")
        };

    private static Result<ReconValue?> Number(ReconOption option, double value) =>
        !double.IsFinite(value) ? Bad(option, "must be a finite number")
        : option.Type == ReconOptionType.Integer && value != Math.Floor(value) ? Bad(option, "must be a whole number")
        : option.Min is double min && value < min ? Bad(option, $"must be at least {min.ToString("R", CultureInfo.InvariantCulture)}")
        : option.Max is double max && value > max ? Bad(option, $"must be at most {max.ToString("R", CultureInfo.InvariantCulture)}")
        : ReconResult.Ok<ReconValue?>(new ReconValue.Number(value));

    private static Result<ReconValue?> Bad(ReconOption option, string expected) =>
        ReconRead.Fail<ReconValue?>(ReconProblem.InvalidOption(option.Name, expected));
}
