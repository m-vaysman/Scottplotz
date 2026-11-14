using System.Globalization;
using System.Text.Json;
using Scottplotz.Charting;

namespace Scottplotz.Recon;

public sealed record ReconRow(
    string Path,
    string Key,
    string? Name,
    int Count,
    ReconSideValues? Left,
    ReconSideValues? Right,
    ReconStatus? Status,
    IReadOnlyList<string> Group,
    string? Date,
    string? Reason,
    ReconDuplicate Duplicate,
    IReadOnlyList<KeyValuePair<string, ReconValue?>> Attributes);

public static class ReconRows
{
    private static readonly string[] RecordNames =
        ["key", "name", "count", "left", "right", "status", "group", "date", "reason", "duplicate", "attrs"];
    private static readonly string[] GroupNames = ["key", "name", "note", "left", "right", "attrs"];
    private static readonly string[] LineNames = ["key", "name"];

    public static Result<IReadOnlyList<ReconRow>> Parse(
        JsonElement records,
        IReadOnlyList<ReconFieldDraft> fields,
        string? measure) =>
        records.EnumerateArray()
            .Select((record, index) => One(record, $"records[{index}]", fields, MeasureIndex(fields, measure)))
            .Collect();

    public static Result<IReadOnlyList<ReconGroup>> Groups(
        JsonElement root,
        IReadOnlyList<ReconFieldDraft> fields,
        string? measure) =>
        ReconRead.Get(root, "groups") switch
        {
            null => ReconResult.Ok<IReadOnlyList<ReconGroup>>([]),
            { ValueKind: JsonValueKind.Array } groups =>
                groups.EnumerateArray()
                    .Select((group, index) => OneGroup(group, $"groups[{index}]", fields, MeasureIndex(fields, measure)))
                    .Collect(),
            _ => ReconRead.Fail<IReadOnlyList<ReconGroup>>(ReconProblem.Invalid("groups", "must be an array"))
        };

    public static int? MeasureIndex(IReadOnlyList<ReconFieldDraft> fields, string? measure) =>
        fields
            .Select((field, index) => (field.Key, Index: (int?)index))
            .FirstOrDefault(item => item.Key == measure)
            .Index;

    private static Result<ReconRow> One(JsonElement record, string path, IReadOnlyList<ReconFieldDraft> fields, int? measure) =>
        record.ValueKind != JsonValueKind.Object
            ? ReconRead.Fail<ReconRow>(ReconProblem.Invalid(path, "must be an object"))
            : from known in ReconRead.OnlyKnown(record, RecordNames, path)
              from key in Key(record, $"{path}.key")
              from name in ReconRead.Text(record, "name", $"{path}.name")
              from count in Count(record, $"{path}.count")
              from left in Side(record, "left", $"{path}.left", fields, measure)
              from right in Side(record, "right", $"{path}.right", fields, measure)
              from status in Status(record, $"{path}.status")
              from grouping in Path(record, "group", $"{path}.group")
              from date in ReconRead.Date(record, "date", $"{path}.date")
              from reason in ReconRead.Text(record, "reason", $"{path}.reason")
              from duplicate in Duplicate(record, $"{path}.duplicate")
              from attributes in Attributes(record, $"{path}.attrs")
              select new ReconRow(path, key, name, count, left, right, status, grouping, date, reason, duplicate, attributes);

    private static Result<ReconGroup> OneGroup(JsonElement group, string path, IReadOnlyList<ReconFieldDraft> fields, int? measure) =>
        group.ValueKind != JsonValueKind.Object
            ? ReconRead.Fail<ReconGroup>(ReconProblem.Invalid(path, "must be an object"))
            : from known in ReconRead.OnlyKnown(@group, GroupNames, path)
              from key in Path(@group, "key", $"{path}.key")
              from named in key.Count == 0
                  ? ReconRead.Fail<IReadOnlyList<string>>(ReconProblem.Invalid($"{path}.key", "is required"))
                  : ReconResult.Ok(key)
              from name in ReconRead.Text(@group, "name", $"{path}.name")
              from note in ReconRead.Text(@group, "note", $"{path}.note")
              from left in Side(@group, "left", $"{path}.left", fields, measure)
              from right in Side(@group, "right", $"{path}.right", fields, measure)
              from attributes in Attributes(@group, $"{path}.attrs")
              select new ReconGroup(named, name ?? named[^1], note, left, right, attributes);

    private static Result<string> Key(JsonElement record, string path) =>
        ReconRead.Get(record, "key") switch
        {
            { ValueKind: JsonValueKind.String } text when text.GetString() is { Length: > 0 } key => ReconResult.Ok(key),
            { ValueKind: JsonValueKind.Number } number => ReconResult.Ok(number.GetRawText()),
            _ => ReconRead.Fail<string>(ReconProblem.Invalid(path, "is required and must be a string or a number"))
        };

    private static Result<int> Count(JsonElement record, string path) =>
        ReconRead.Number(record, "count", path).Bind(value =>
            value is null ? ReconResult.Ok(1)
            : value is >= 1 and <= 1_000_000_000 && value == Math.Floor(value.Value) ? ReconResult.Ok((int)value.Value)
            : ReconRead.Fail<int>(ReconProblem.Invalid(path, "must be a whole number of one or more")));

    private static Result<ReconStatus?> Status(JsonElement record, string path) =>
        ReconRead.Text(record, "status", path).Bind(text =>
            text is null ? ReconResult.Ok<ReconStatus?>(null)
            : ReconComparison.FromToken(text) is { } status ? ReconResult.Ok<ReconStatus?>(status)
            : ReconRead.Fail<ReconStatus?>(ReconProblem.Invalid(path, $"must be one of {string.Join(", ", ReconComparison.Tokens)}")));

    private static Result<ReconDuplicate> Duplicate(JsonElement record, string path) =>
        ReconRead.Get(record, "duplicate") switch
        {
            null or { ValueKind: JsonValueKind.False } => ReconResult.Ok(ReconDuplicate.None),
            { ValueKind: JsonValueKind.True } => ReconResult.Ok(ReconDuplicate.Either),
            { ValueKind: JsonValueKind.String } side when side.GetString() == "left" => ReconResult.Ok(ReconDuplicate.Left),
            { ValueKind: JsonValueKind.String } side when side.GetString() == "right" => ReconResult.Ok(ReconDuplicate.Right),
            _ => ReconRead.Fail<ReconDuplicate>(ReconProblem.Invalid(path, "must be true, false, \"left\", or \"right\""))
        };

    private static Result<IReadOnlyList<string>> Path(JsonElement record, string name, string path) =>
        ReconRead.Get(record, name) switch
        {
            null => ReconResult.Ok<IReadOnlyList<string>>([]),
            { ValueKind: JsonValueKind.String } one => ReconResult.Ok<IReadOnlyList<string>>([one.GetString() ?? ""]),
            { ValueKind: JsonValueKind.Array } many when many.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String) =>
                ReconResult.Ok<IReadOnlyList<string>>(many.EnumerateArray().Select(item => item.GetString() ?? "").ToList()),
            _ => ReconRead.Fail<IReadOnlyList<string>>(ReconProblem.Invalid(path, "must be a string or an array of strings"))
        };

    private static Result<IReadOnlyList<KeyValuePair<string, ReconValue?>>> Attributes(JsonElement record, string path) =>
        ReconRead.Get(record, "attrs") switch
        {
            null => ReconResult.Ok<IReadOnlyList<KeyValuePair<string, ReconValue?>>>([]),
            { ValueKind: JsonValueKind.Object } attributes =>
                attributes.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => Loose(property.Value, $"{path}.{property.Name}")
                        .Select(value => new KeyValuePair<string, ReconValue?>(property.Name, value)))
                    .Collect(),
            _ => ReconRead.Fail<IReadOnlyList<KeyValuePair<string, ReconValue?>>>(ReconProblem.Invalid(path, "must be an object"))
        };

    public static Result<ReconValue?> Loose(JsonElement value, string path) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => ReconResult.Ok<ReconValue?>(null),
            JsonValueKind.String => ReconResult.Ok<ReconValue?>(new ReconValue.Text(value.GetString() ?? "")),
            JsonValueKind.True => ReconResult.Ok<ReconValue?>(new ReconValue.Flag(true)),
            JsonValueKind.False => ReconResult.Ok<ReconValue?>(new ReconValue.Flag(false)),
            JsonValueKind.Number => ReconRead.Finite(value, path).Select(number => (ReconValue?)new ReconValue.Number(number)),
            _ => ReconRead.Fail<ReconValue?>(ReconProblem.Invalid(path, "must be a string, a number, true, false, or null"))
        };

    private static Result<ReconSideValues?> Side(
        JsonElement record,
        string side,
        string path,
        IReadOnlyList<ReconFieldDraft> fields,
        int? measure) =>
        ReconRead.Get(record, side) switch
        {
            null => ReconResult.Ok<ReconSideValues?>(null),
            { ValueKind: JsonValueKind.Number } bare when measure is int index =>
                ReconRead.Finite(bare, path)
                    .Select(number => (ReconSideValues?)new ReconSideValues(Spread(fields.Count, [(index, new ReconValue.Number(number))]), [])),
            { ValueKind: JsonValueKind.Number } =>
                ReconRead.Fail<ReconSideValues?>(ReconProblem.Invalid(path, "is a bare number, but no numeric field is declared")),
            { ValueKind: JsonValueKind.Object } values =>
                Cells(values, path, fields, []).Select(cells => (ReconSideValues?)new ReconSideValues(cells, [])),
            { ValueKind: JsonValueKind.Array } lines =>
                lines.EnumerateArray()
                    .Select((line, index) => Line(line, $"{path}[{index}]", fields))
                    .Collect()
                    .Select(parsed => (ReconSideValues?)new ReconSideValues(Combine(fields, parsed), parsed)),
            _ => ReconRead.Fail<ReconSideValues?>(ReconProblem.Invalid(path, "must be a number, an object of field values, an array of lines, or null"))
        };

    private static Result<ReconLine> Line(JsonElement line, string path, IReadOnlyList<ReconFieldDraft> fields) =>
        line.ValueKind != JsonValueKind.Object
            ? ReconRead.Fail<ReconLine>(ReconProblem.Invalid(path, "must be an object of field values"))
            : from key in LineKey(line, $"{path}.key")
              from name in ReconRead.Text(line, "name", $"{path}.name")
              from cells in Cells(line, path, fields, LineNames)
              select new ReconLine(key, name, cells);

    private static Result<string?> LineKey(JsonElement line, string path) =>
        ReconRead.Get(line, "key") switch
        {
            null => ReconResult.Ok<string?>(null),
            { ValueKind: JsonValueKind.String } text => ReconResult.Ok(text.GetString()),
            { ValueKind: JsonValueKind.Number } number => ReconResult.Ok<string?>(number.GetRawText()),
            _ => ReconRead.Fail<string?>(ReconProblem.Invalid(path, "must be a string or a number"))
        };

    private static IReadOnlyList<ReconValue?> Combine(IReadOnlyList<ReconFieldDraft> fields, IReadOnlyList<ReconLine> lines) =>
        fields
            .Select((field, index) =>
            {
                var values = lines.Select(line => line.Values[index]).Where(value => value is not null).ToList();
                return values.Count == 0 ? null
                    : field.Type == ReconFieldType.Number
                        ? new ReconValue.Number(values.OfType<ReconValue.Number>().Sum(number => number.Value))
                        : values.Distinct().Count() == 1 ? values[0] : null;
            })
            .ToList();

    private static Result<IReadOnlyList<ReconValue?>> Cells(
        JsonElement values,
        string path,
        IReadOnlyList<ReconFieldDraft> fields,
        IReadOnlyList<string> reserved) =>
        values.EnumerateObject()
            .Where(property => !reserved.Contains(property.Name, StringComparer.Ordinal))
            .Select(property => Cell(property, path, fields))
            .Collect()
            .Select(cells => Spread(fields.Count, cells));

    private static IReadOnlyList<ReconValue?> Spread(int count, IReadOnlyList<(int Index, ReconValue? Value)> cells)
    {
        var byIndex = cells.ToLookup(cell => cell.Index, cell => cell.Value);
        return Enumerable.Range(0, count).Select(index => byIndex[index].LastOrDefault()).ToList();
    }

    private static Result<(int Index, ReconValue? Value)> Cell(JsonProperty property, string path, IReadOnlyList<ReconFieldDraft> fields)
    {
        var index = fields.Select((field, position) => (field.Key, position))
            .Where(item => item.Key == property.Name)
            .Select(item => (int?)item.position)
            .FirstOrDefault();
        return index is int found
            ? Value(property.Value, fields[found], $"{path}.{property.Name}").Select(value => (found, value))
            : ReconRead.Fail<(int, ReconValue?)>(ReconProblem.UnknownField(path, property.Name, string.Join(", ", fields.Select(field => field.Key))));
    }

    private static Result<ReconValue?> Value(JsonElement value, ReconFieldDraft field, string path) =>
        (value.ValueKind, field.Type) switch
        {
            (JsonValueKind.Null, _) => ReconResult.Ok<ReconValue?>(null),
            (_, ReconFieldType.Number) => ReconRead.Finite(value, path).Select(number => (ReconValue?)new ReconValue.Number(number)),
            (JsonValueKind.String, ReconFieldType.Date) when ReconRead.IsIsoDate(value.GetString() ?? "") =>
                ReconResult.Ok<ReconValue?>(new ReconValue.Text(value.GetString() ?? "")),
            (_, ReconFieldType.Date) => ReconRead.Fail<ReconValue?>(ReconProblem.Invalid(path, "must be an ISO 8601 date such as 2026-03-31")),
            (JsonValueKind.String, ReconFieldType.Text) => ReconResult.Ok<ReconValue?>(new ReconValue.Text(value.GetString() ?? "")),
            (JsonValueKind.Number, ReconFieldType.Text) =>
                ReconResult.Ok<ReconValue?>(new ReconValue.Text(value.GetDouble().ToString("R", CultureInfo.InvariantCulture))),
            _ => ReconRead.Fail<ReconValue?>(ReconProblem.Invalid(path, "must be a string"))
        };

    public static Result<ReconRecord> Build(ReconRow row, IReadOnlyList<ReconField> fields, int? measure, ReconTolerance tolerance)
    {
        if (row.Left is null && row.Right is null && row.Status is null)
            return ReconRead.Fail<ReconRecord>(ReconProblem.MissingSides(row.Path));

        var mismatch = row.Status is { } claimed ? Mismatch(claimed, row) : null;
        if (mismatch is not null)
            return ReconRead.Fail<ReconRecord>(ReconProblem.StatusMismatch(row.Path, ReconComparison.Token(row.Status!.Value), mismatch));

        var matches = row.Left is not null && row.Right is not null
            ? ReconComparison.Matches(fields, row.Left.Values, row.Right.Values, tolerance)
            : [];
        var derived = row.Left is null ? ReconStatus.RightOnly
            : row.Right is null ? ReconStatus.LeftOnly
            : ReconComparison.Status(matches);
        var left = measure is int at ? (row.Left?.Values[at] as ReconValue.Number)?.Value : null;
        var right = measure is int there ? (row.Right?.Values[there] as ReconValue.Number)?.Value : null;
        var delta = right - left;
        return ReconResult.Ok(new ReconRecord(
            row.Key,
            row.Name ?? row.Key,
            row.Status ?? derived,
            row.Count,
            row.Left,
            row.Right,
            delta,
            delta is double gap && left is double start && start != 0 ? gap / Math.Abs(start) * 100 : null,
            fields.Zip(matches).Where(pair => pair.Second == FieldMatch.Beyond).Select(pair => pair.First.Key).ToList(),
            row.Group,
            row.Date,
            row.Reason,
            row.Duplicate,
            row.Attributes));
    }

    private static string? Mismatch(ReconStatus claimed, ReconRow row) =>
        (row.Left, row.Right) switch
        {
            (null, null) => null,
            (not null, not null) when claimed is ReconStatus.LeftOnly or ReconStatus.RightOnly => "the record has both sides",
            (not null, null) when claimed != ReconStatus.LeftOnly => "the record has only a left side",
            (null, not null) when claimed != ReconStatus.RightOnly => "the record has only a right side",
            _ => null
        };

    public static ReconSummary Summarize(
        IReadOnlyList<ReconRecord> records,
        int? measure,
        double? leftOverride,
        double? rightOverride)
    {
        double? Total(Func<ReconRecord, ReconSideValues?> side) =>
            measure is int index
                ? records.Select(record => (side(record)?.Values[index] as ReconValue.Number)?.Value ?? 0).Sum()
                : null;

        var leftTotal = leftOverride ?? Total(record => record.Left);
        var rightTotal = rightOverride ?? Total(record => record.Right);
        int Count(ReconStatus status) => records.Where(record => record.Status == status).Sum(record => record.Count);
        return new ReconSummary(
            Count(ReconStatus.LeftOnly),
            Count(ReconStatus.Exact),
            Count(ReconStatus.Tolerance),
            Count(ReconStatus.Break),
            Count(ReconStatus.RightOnly),
            records.Where(record => record.Duplicate != ReconDuplicate.None).Sum(record => record.Count),
            records.Where(record => record.Status != ReconStatus.RightOnly).Sum(record => record.Count),
            records.Where(record => record.Status != ReconStatus.LeftOnly).Sum(record => record.Count),
            leftTotal,
            rightTotal,
            rightTotal - leftTotal);
    }
}
