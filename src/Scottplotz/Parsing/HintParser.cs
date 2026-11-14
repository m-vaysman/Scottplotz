using System.Globalization;
using System.Text.Json;
using Scottplotz.Charting;
using Scottplotz.ChartTypes;

namespace Scottplotz.Parsing;

public static class HintParser
{
    private static readonly IReadOnlyDictionary<string, ChartTheme> Themes =
        new Dictionary<string, ChartTheme>(StringComparer.Ordinal)
        {
            ["light"] = ChartTheme.Light,
            ["dark"] = ChartTheme.Dark
        };

    public static bool IsKnown(string name) => HintNames.All.Contains(name, StringComparer.Ordinal);

    public static Result<ChartHints> FromElement(JsonElement? hints, ChartingLimits limits)
    {
        if (hints is null || hints.Value.ValueKind == JsonValueKind.Null)
            return Result<ChartHints>.Success(ChartHints.None);

        if (hints.Value.ValueKind != JsonValueKind.Object)
            return Result<ChartHints>.Error(ChartProblem.HintsShape());

        var values = hints.Value.EnumerateObject()
            .Select(property => (property.Name, Value: ReadJson(property.Value)))
            .ToList();

        return Assemble(values, limits);
    }

    public static Result<ChartHints> FromPairs(
        IReadOnlyList<KeyValuePair<string, string?>> pairs,
        ChartingLimits limits) =>
        Assemble(
            pairs.Select(pair => (pair.Key, Value: (HintValue)new HintValue.Text(pair.Value ?? ""))).ToList(),
            limits);

    private static Result<ChartHints> Assemble(
        IReadOnlyList<(string Name, HintValue Value)> values,
        ChartingLimits limits)
    {
        var duplicate = values
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            return Result<ChartHints>.Error(ChartProblem.DuplicateHint(duplicate.Key));

        return values.Aggregate(
            Result<ChartHints>.Success(ChartHints.None),
            (current, item) => current.Bind(hints => Apply(hints, item.Name, item.Value, limits)));
    }

    private static Result<ChartHints> Apply(ChartHints hints, string name, HintValue value, ChartingLimits limits) =>
        name switch
        {
            HintNames.Type => ReadKind(value).Bind(kind => Result<ChartHints>.Success(hints with { Type = kind })),
            HintNames.Title => ReadText(HintNames.Title, value).Bind(text => Result<ChartHints>.Success(hints with { Title = text })),
            HintNames.XLabel => ReadText(HintNames.XLabel, value).Bind(text => Result<ChartHints>.Success(hints with { XLabel = text })),
            HintNames.YLabel => ReadText(HintNames.YLabel, value).Bind(text => Result<ChartHints>.Success(hints with { YLabel = text })),
            HintNames.Width => ReadDimension(HintNames.Width, value, limits).Bind(width => Result<ChartHints>.Success(hints with { Width = width })),
            HintNames.Height => ReadDimension(HintNames.Height, value, limits).Bind(height => Result<ChartHints>.Success(hints with { Height = height })),
            HintNames.Theme => ReadTheme(value).Bind(theme => Result<ChartHints>.Success(hints with { Theme = theme })),
            _ => Result<ChartHints>.Error(ChartProblem.UnknownHint(name))
        };

    private static Result<ChartKind> ReadKind(HintValue value) =>
        value switch
        {
            HintValue.Text text when ChartTypeRegistry.Find(text.Value.Trim()) is { } chartType => Result<ChartKind>.Success(chartType.Kind),
            HintValue.Text text => Result<ChartKind>.Error(ChartProblem.InvalidHint(
                HintNames.Type,
                text.Value,
                $"Allowed values: {ChartTypeRegistry.IdList}.")),
            _ => Result<ChartKind>.Error(ChartProblem.HintType(HintNames.Type, $"one of: {ChartTypeRegistry.IdList}"))
        };

    private static Result<ChartTheme> ReadTheme(HintValue value) =>
        value switch
        {
            HintValue.Text text when Themes.TryGetValue(text.Value.Trim(), out var theme) => Result<ChartTheme>.Success(theme),
            HintValue.Text text => Result<ChartTheme>.Error(ChartProblem.InvalidHint(
                HintNames.Theme,
                text.Value,
                "Allowed values: light, dark.")),
            _ => Result<ChartTheme>.Error(ChartProblem.HintType(HintNames.Theme, "light or dark"))
        };

    private static Result<string?> ReadText(string name, HintValue value)
    {
        if (value is not HintValue.Text text)
            return Result<string?>.Error(ChartProblem.HintType(name, "a string"));

        var trimmed = text.Value.Trim();
        if (trimmed.Length == 0)
            return Result<string?>.Success(null);

        return trimmed.Length > HintNames.MaxTextLength
            ? Result<string?>.Error(ChartProblem.InvalidHint(name, trimmed, $"Allowed length is 1–{HintNames.MaxTextLength} characters."))
            : Result<string?>.Success(trimmed);
    }

    private static Result<int> ReadDimension(string name, HintValue value, ChartingLimits limits)
    {
        var range = $"Allowed range is {limits.MinDimension}–{limits.MaxDimension}.";
        if (value is HintValue.Invalid invalid)
            return Result<int>.Error(ChartProblem.InvalidHint(name, invalid.Raw, range));

        int? number = value switch
        {
            HintValue.WholeNumber whole => whole.Value,
            HintValue.Text text when int.TryParse(text.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };

        if (number is null)
            return Result<int>.Error(ChartProblem.HintType(name, $"an integer from {limits.MinDimension} to {limits.MaxDimension}"));

        return number.Value < limits.MinDimension || number.Value > limits.MaxDimension
            ? Result<int>.Error(ChartProblem.InvalidHint(name, number.Value.ToString(CultureInfo.InvariantCulture), range))
            : Result<int>.Success(number.Value);
    }

    private static HintValue ReadJson(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => new HintValue.Text(element.GetString() ?? ""),
            JsonValueKind.Number when element.TryGetInt32(out var number) => new HintValue.WholeNumber(number),
            JsonValueKind.Number => new HintValue.Invalid(element.GetRawText()),
            JsonValueKind.Null => new HintValue.Invalid("null"),
            JsonValueKind.True => new HintValue.Invalid("true"),
            JsonValueKind.False => new HintValue.Invalid("false"),
            _ => new HintValue.Invalid(element.ValueKind.ToString().ToLowerInvariant())
        };

    private abstract record HintValue
    {
        public sealed record Text(string Value) : HintValue;

        public sealed record WholeNumber(int Value) : HintValue;

        public sealed record Invalid(string Raw) : HintValue;
    }
}
