using System.Text.Json;
using Scottplotz.Charting;

namespace Scottplotz.Recon;

public sealed record ReconFieldDraft(
    string Key,
    string Label,
    ReconFieldType Type,
    int? Decimals,
    bool Compare,
    ReconTolerance? Tolerance);

public static class ReconFields
{
    private static readonly string[] FieldNames = ["key", "label", "type", "decimals", "compare", "tolerance"];
    private static readonly string[] ToleranceNames = ["absolute", "percent"];
    private static readonly string[] Sides = ["left", "right"];
    private static readonly string[] LineNames = ["key", "name"];

    public static Result<IReadOnlyList<ReconFieldDraft>> Parse(JsonElement root, JsonElement records, ReconLimits limits) =>
        (ReconRead.Get(root, "fields") is { } declared ? Declared(declared) : ReconResult.Ok(Infer(records)))
        .Bind(drafts => Checked(drafts, limits));

    public static Result<ReconTolerance?> Tolerance(JsonElement parent, string path) =>
        ReconRead.Get(parent, "tolerance") switch
        {
            null => ReconResult.Ok<ReconTolerance?>(null),
            { ValueKind: JsonValueKind.Number } bare =>
                NonNegative(bare, path).Select(value => (ReconTolerance?)new ReconTolerance(value, 0)),
            { ValueKind: JsonValueKind.Object } both =>
                from known in ReconRead.OnlyKnown(both, ToleranceNames, path)
                from absolute in Part(both, "absolute", path)
                from percent in Part(both, "percent", path)
                select (ReconTolerance?)new ReconTolerance(absolute, percent),
            _ => ReconRead.Fail<ReconTolerance?>(ReconProblem.Invalid(path, "must be a number or an object with absolute and percent"))
        };

    public static Result<string?> Measure(JsonElement root, IReadOnlyList<ReconFieldDraft> fields)
    {
        var numeric = fields.Where(field => field.Type == ReconFieldType.Number).Select(field => field.Key).ToList();
        return ReconRead.Text(root, "measure", "measure").Bind(name =>
            name is null ? ReconResult.Ok(numeric.FirstOrDefault())
            : numeric.Contains(name, StringComparer.Ordinal) ? ReconResult.Ok<string?>(name)
            : ReconRead.Fail<string?>(ReconProblem.UnknownMeasure(name, numeric.Count == 0 ? "none" : string.Join(", ", numeric))));
    }

    public static IReadOnlyList<ReconField> Finish(IReadOnlyList<ReconFieldDraft> drafts, IReadOnlyList<ReconRow> rows) =>
        drafts
            .Select((draft, index) => new ReconField(
                draft.Key,
                draft.Label,
                draft.Type,
                draft.Decimals ?? (draft.Type == ReconFieldType.Number && !AllWhole(rows, index) ? 2 : 0),
                draft.Compare,
                draft.Tolerance))
            .ToList();

    private static bool AllWhole(IReadOnlyList<ReconRow> rows, int index) =>
        rows
            .SelectMany(row => new[] { row.Left?.Values[index], row.Right?.Values[index] })
            .OfType<ReconValue.Number>()
            .All(number => number.Value == Math.Floor(number.Value));

    private static Result<IReadOnlyList<ReconFieldDraft>> Checked(IReadOnlyList<ReconFieldDraft> drafts, ReconLimits limits)
    {
        var repeated = drafts.GroupBy(draft => draft.Key, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        return repeated is not null ? ReconRead.Fail<IReadOnlyList<ReconFieldDraft>>(ReconProblem.DuplicateField(repeated.Key))
            : drafts.Count > limits.MaxFields ? ReconRead.Fail<IReadOnlyList<ReconFieldDraft>>(ReconProblem.TooManyFields(drafts.Count, limits.MaxFields))
            : ReconResult.Ok(drafts);
    }

    private static Result<IReadOnlyList<ReconFieldDraft>> Declared(JsonElement fields) =>
        fields.ValueKind != JsonValueKind.Array
            ? ReconRead.Fail<IReadOnlyList<ReconFieldDraft>>(ReconProblem.Invalid("fields", "must be an array"))
            : fields.EnumerateArray().Select((field, index) => One(field, $"fields[{index}]")).Collect();

    private static Result<ReconFieldDraft> One(JsonElement field, string path) =>
        field.ValueKind switch
        {
            JsonValueKind.String when field.GetString() is { Length: > 0 } key =>
                ReconResult.Ok(new ReconFieldDraft(key, key, ReconFieldType.Number, null, true, null)),
            JsonValueKind.Object =>
                from known in ReconRead.OnlyKnown(field, FieldNames, path)
                from key in ReconRead.Text(field, "key", $"{path}.key").Bind(text => Required(text, $"{path}.key"))
                from label in ReconRead.Text(field, "label", $"{path}.label")
                from type in Type(field, $"{path}.type")
                from decimals in Decimals(field, $"{path}.decimals")
                from compare in ReconRead.Flag(field, "compare", $"{path}.compare")
                from tolerance in Tolerance(field, $"{path}.tolerance")
                select new ReconFieldDraft(key, label ?? key, type, decimals, compare ?? true, tolerance),
            _ => ReconRead.Fail<ReconFieldDraft>(ReconProblem.Invalid(path, "must be a field key or an object with a key"))
        };

    private static Result<string> Required(string? text, string path) =>
        string.IsNullOrWhiteSpace(text)
            ? ReconRead.Fail<string>(ReconProblem.Invalid(path, "is required and must not be empty"))
            : ReconResult.Ok(text);

    private static Result<ReconFieldType> Type(JsonElement field, string path) =>
        ReconRead.Text(field, "type", path).Bind(text => text switch
        {
            null or "number" => ReconResult.Ok(ReconFieldType.Number),
            "text" => ReconResult.Ok(ReconFieldType.Text),
            "date" => ReconResult.Ok(ReconFieldType.Date),
            _ => ReconRead.Fail<ReconFieldType>(ReconProblem.Invalid(path, "must be number, text, or date"))
        });

    private static Result<int?> Decimals(JsonElement field, string path) =>
        ReconRead.Number(field, "decimals", path).Bind(value =>
            value is null ? ReconResult.Ok<int?>(null)
            : value is >= 0 and <= 6 && value == Math.Floor(value.Value) ? ReconResult.Ok<int?>((int)value.Value)
            : ReconRead.Fail<int?>(ReconProblem.Invalid(path, "must be a whole number from 0 to 6")));

    private static Result<double> Part(JsonElement tolerance, string name, string path) =>
        ReconRead.Get(tolerance, name) is { } value ? NonNegative(value, $"{path}.{name}") : ReconResult.Ok(0d);

    private static Result<double> NonNegative(JsonElement value, string path) =>
        ReconRead.Finite(value, path).Bind(number =>
            number >= 0 ? ReconResult.Ok(number) : ReconRead.Fail<double>(ReconProblem.Invalid(path, "must be zero or more")));

    private static IReadOnlyList<ReconFieldDraft> Infer(JsonElement records)
    {
        var found = records.EnumerateArray()
            .Where(record => record.ValueKind == JsonValueKind.Object)
            .SelectMany(record => Sides.Select(side => ReconRead.Get(record, side)))
            .SelectMany(side => side switch
            {
                { ValueKind: JsonValueKind.Object } one => one.EnumerateObject(),
                { ValueKind: JsonValueKind.Array } lines => lines.EnumerateArray()
                    .Where(line => line.ValueKind == JsonValueKind.Object)
                    .SelectMany(line => line.EnumerateObject())
                    .Where(property => !LineNames.Contains(property.Name, StringComparer.Ordinal)),
                _ => Enumerable.Empty<JsonProperty>()
            })
            .Where(property => property.Value.ValueKind != JsonValueKind.Null)
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Select(group => new ReconFieldDraft(group.Key, group.Key, Guess(group.First().Value), null, true, null))
            .ToList();

        var bare = records.EnumerateArray()
            .Where(record => record.ValueKind == JsonValueKind.Object)
            .SelectMany(record => Sides.Select(side => ReconRead.Get(record, side)))
            .Any(side => side is { ValueKind: JsonValueKind.Number });
        return found.Count > 0 || !bare
            ? found
            : [new ReconFieldDraft("value", "Value", ReconFieldType.Number, null, true, null)];
    }

    private static ReconFieldType Guess(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number => ReconFieldType.Number,
            JsonValueKind.String when ReconRead.IsIsoDate(value.GetString() ?? "") => ReconFieldType.Date,
            _ => ReconFieldType.Text
        };
}
