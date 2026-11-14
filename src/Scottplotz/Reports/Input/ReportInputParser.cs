using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scottplotz.Charting;
using Scottplotz.Recon;

namespace Scottplotz.Reports.Input;

public static class ReportInputParser
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static Result<ReportInput> Parse(string body, ReportLimits limits)
    {
        try
        {
            return JsonSerializer.Deserialize<Wire.Root>(body, Json) is { } root
                ? Map(root, limits)
                : Fail<ReportInput>(ReportProblem.Unreadable("", "The body must be a JSON object."));
        }
        catch (JsonException error)
        {
            return Fail<ReportInput>(error.InnerException?.GetType().Name == "JsonReaderException" || error.GetType().Name == "JsonReaderException"
                ? ChartProblem.MalformedJson(error.Message)
                : error.Path is { Length: > 1 } path && error.Message.Contains("could not be mapped", StringComparison.Ordinal)
                ? ReportProblem.Unreadable(path, "Unknown property. See docs/reports-input.md for the properties allowed here.")
                : error.Path is { Length: > 1 } at
                    ? ReportProblem.Unreadable(at, "The value has the wrong type or shape.")
                    : ChartProblem.MalformedJson(error.Message));
        }
    }

    private static Result<T> Fail<T>(ChartProblem problem) => Result<T>.Error(problem);

    private static Result<ReportInput> Map(Wire.Root root, ReportLimits limits) =>
        from run in Run(root.Run)
        from options in Options(root.Options)
        from rowsIn in Required(root.Rows, "rows")
        from entriesIn in Required(root.Entries, "entries")
        from sized in Sized(rowsIn.Count, entriesIn.Count, limits)
        from rows in rowsIn.Select((row, index) => Row(row, $"rows[{index}]")).Collect()
        from entries in entriesIn.Select((entry, index) => Entry(entry, $"entries[{index}]")).Collect()
        from linked in Linked(entries, rows)
        select new ReportInput(run, options, root.Passes ?? [], entries, rows);

    private static Result<T> Required<T>(T? value, string path) where T : class =>
        value is null ? Fail<T>(ReportProblem.Missing(path)) : ReconResult.Ok(value);

    private static Result<bool> Sized(int rows, int entries, ReportLimits limits) =>
        rows > limits.MaxRows ? Fail<bool>(ReportProblem.TooMany("rows", rows, limits.MaxRows))
        : entries > limits.MaxEntries ? Fail<bool>(ReportProblem.TooMany("entries", entries, limits.MaxEntries))
        : ReconResult.Ok(true);

    private static Result<RunInfo> Run(Wire.Run? run) =>
        run is null ? Fail<RunInfo>(ReportProblem.Missing("run"))
        : string.IsNullOrWhiteSpace(run.ProfileId) ? Fail<RunInfo>(ReportProblem.Missing("run.profileId"))
        : run.Roles is null ? Fail<RunInfo>(ReportProblem.Missing("run.roles"))
        : from asOf in Date(run.AsOf, "run.asOf")
          from roles in run.Roles.Select(pair => Role(pair.Key, "run.roles").Select(role => new KeyValuePair<ReportRole, string>(role, pair.Value))).Collect()
          select new RunInfo(asOf, run.ProfileId, run.Title ?? "RecDiffy reconciliation", run.EngineVersion ?? "", roles.ToDictionary(pair => pair.Key, pair => pair.Value));

    private static Result<DateOnly> Date(string? text, string path) =>
        text is null ? Fail<DateOnly>(ReportProblem.Missing(path))
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? ReconResult.Ok(date)
        : Fail<DateOnly>(ReportProblem.Invalid(path, "must be an ISO date such as 2026-03-31"));

    private static Result<ReportRole> Role(string name, string path) =>
        Enum.TryParse<ReportRole>(name, ignoreCase: false, out var role) && Enum.IsDefined(role) && !int.TryParse(name, out _)
            ? ReconResult.Ok(role)
            : Fail<ReportRole>(ReportProblem.Invalid($"{path}.{name}", $"is not a role. Roles: {Roles.Names}"));

    private static Result<T> Token<T>(string? text, string path, T? fallback = null) where T : struct, Enum =>
        text is null && fallback is { } value ? ReconResult.Ok(value)
        : text is null ? Fail<T>(ReportProblem.Missing(path))
        : Enum.TryParse<T>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(text, out _) ? ReconResult.Ok(parsed)
        : Fail<T>(ReportProblem.Invalid(path, $"is '{text}'. Allowed values: {string.Join(", ", Enum.GetNames<T>())}"));

    private static Result<ReportOptions> Options(Wire.Options? options) =>
        from theme in Token(options?.Theme, "options.theme", (ReconTheme?)ReconTheme.Auto)
        from tolerances in (options?.Tolerances ?? []).Select((rule, index) => Tolerance(rule, $"options.tolerances[{index}]")).Collect()
        select new ReportOptions(
            options?.CurrencyOrder ?? ["USD", "EUR", "GBP", "CHF", "CAD", "JPY"],
            options?.PayingSides ?? ["BUY", "PAY_FIXED", "PAY"],
            options?.SelectedEntries,
            options?.MaxCards is > 0 and var cards ? cards : 50,
            options?.Labels ?? new Dictionary<string, string>(),
            options?.RawColumns is { } raw ? new RawColumns(raw.Left ?? [], raw.Right ?? []) : null,
            options?.Tolerances is null ? null : tolerances,
            theme);

    private static Result<ToleranceRule> Tolerance(Wire.Tolerance rule, string path) =>
        from absolute in Value(rule.Absolute, FieldKind.Decimal, $"{path}.absolute")
        from relative in Value(rule.Relative, FieldKind.Decimal, $"{path}.relative")
        select new ToleranceRule(rule.Path ?? "", rule.Field ?? "", rule.Kind ?? "", absolute.Number, relative.Number);

    private static Result<RowInput> Row(Wire.Row row, string path) =>
        string.IsNullOrEmpty(row.RowKey) ? Fail<RowInput>(ReportProblem.Missing($"{path}.rowKey"))
        : from source in Token<SourceSide>(row.Source, $"{path}.source")
          from disposition in Token<Disposition>(row.Disposition, $"{path}.disposition")
          from validity in Token(row.Validity, $"{path}.validity", (Validity?)Validity.Valid)
          from values in Values(row.Values, $"{path}.values")
          select new RowInput(
              source,
              row.RowKey,
              disposition,
              row.Suppression,
              validity,
              string.IsNullOrEmpty(row.EntryId) ? null : row.EntryId,
              row.ItemLead ?? true,
              values ?? RoleValues.None,
              row.Diagnostics ?? [],
              row.Raw,
              (row.Trace ?? []).Select(step => new TraceStep(step.RuleId ?? "", step.Field ?? "", step.Raw, step.Canonical)).ToList());

    private static Result<EntryInput> Entry(Wire.Entry entry, string path) =>
        string.IsNullOrEmpty(entry.Id) ? Fail<EntryInput>(ReportProblem.Missing($"{path}.id"))
        : from alignment in Token<Alignment>(entry.Alignment, $"{path}.alignment")
          from outcome in Token<Outcome>(entry.Outcome, $"{path}.outcome")
          from flags in (entry.Composition ?? []).Select((flag, index) => Token<Composition>(flag, $"{path}.composition[{index}]")).Collect()
          from ours in Values(entry.Ours, $"{path}.ours")
          from theirs in Values(entry.Theirs, $"{path}.theirs")
          from trail in (entry.Trail ?? []).Select((step, index) => Step(step, $"{path}.trail[{index}]")).Collect()
          from diffs in (entry.Diffs ?? []).Select((diff, index) => Diff(diff, $"{path}.diffs[{index}]")).Collect()
          select new EntryInput(
              entry.Id,
              entry.PathId ?? "",
              alignment,
              flags.Aggregate(Composition.None, (all, flag) => all | flag),
              outcome,
              entry.Left ?? [],
              entry.Right ?? [],
              ours,
              theirs,
              string.IsNullOrEmpty(entry.CommittedBy) ? null : entry.CommittedBy,
              trail,
              (entry.Hints ?? []).Select(hint => new Hint(hint.Code ?? "", hint.Detail)).ToList(),
              diffs);

    private static Result<TrailStep> Step(Wire.Step step, string path) =>
        (step.Candidates ?? [])
            .Select((candidate, index) => Token<SourceSide>(candidate.Source, $"{path}.candidates[{index}].source")
                .Select(source => new TrailCandidate(source, candidate.RowKey ?? "")))
            .Collect()
            .Select(candidates => new TrailStep(step.Pass ?? "", step.Verdict ?? "", step.Key, step.Detail, candidates));

    private static Result<DiffInput> Diff(Wire.Diff diff, string path) =>
        string.IsNullOrEmpty(diff.Field) ? Fail<DiffInput>(ReportProblem.Missing($"{path}.field"))
        : from kind in Token(diff.Type, $"{path}.type", (FieldKind?)FieldKind.Text)
          from role in diff.Role is null ? ReconResult.Ok<ReportRole?>(null) : Role(diff.Role, $"{path}.role").Select(found => (ReportRole?)found)
          from outcome in Token<DiffOutcome>(diff.Outcome, $"{path}.outcome")
          from ours in Value(diff.Ours, kind, $"{path}.ours")
          from theirs in Value(diff.Theirs, kind, $"{path}.theirs")
          from deltaKind in Token(diff.DeltaKind, $"{path}.deltaKind", (DeltaKind?)DeltaKind.None)
          from delta in Value(diff.Delta, FieldKind.Decimal, $"{path}.delta")
          select new DiffInput(diff.Field, role, kind, outcome, ours, theirs, deltaKind, deltaKind == DeltaKind.None ? null : delta.Number, diff.AttributedTo ?? []);

    private static Result<RoleValues?> Values(Dictionary<string, JsonElement>? values, string path) =>
        values is null
            ? ReconResult.Ok<RoleValues?>(null)
            : values
                .Select(pair => Role(pair.Key, path).Bind(role => Value(pair.Value, Roles.Kind(role), $"{path}.{pair.Key}")
                    .Select(value => new KeyValuePair<ReportRole, FieldValue>(role, value))))
                .Collect()
                .Select(pairs => (RoleValues?)new RoleValues(pairs.Where(pair => !pair.Value.IsNull).ToDictionary(pair => pair.Key, pair => pair.Value)));

    public static Result<FieldValue> Value(JsonElement? element, FieldKind kind, string path) =>
        element is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? ReconResult.Ok(FieldValue.Null)
        : value.ValueKind == JsonValueKind.String && value.GetString() is { Length: 0 } ? ReconResult.Ok(FieldValue.Null)
        : (kind, value.ValueKind) switch
        {
            (FieldKind.Decimal, JsonValueKind.Number) when value.TryGetDecimal(out var number) => ReconResult.Ok(FieldValue.Of(number)),
            (FieldKind.Decimal, JsonValueKind.String) when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) =>
                ReconResult.Ok(FieldValue.Of(number)),
            (FieldKind.Decimal, _) => Fail<FieldValue>(ReportProblem.Invalid(path, "must be a number")),
            (FieldKind.Date, JsonValueKind.String) when DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) =>
                ReconResult.Ok(FieldValue.Of(date)),
            (FieldKind.Date, _) => Fail<FieldValue>(ReportProblem.Invalid(path, "must be an ISO date such as 2026-03-31")),
            (FieldKind.Text, JsonValueKind.String) => ReconResult.Ok(FieldValue.Of(value.GetString() ?? "")),
            (FieldKind.Text, JsonValueKind.Number) => ReconResult.Ok(FieldValue.Of(value.GetRawText())),
            _ => Fail<FieldValue>(ReportProblem.Invalid(path, "must be text"))
        };

    private static Result<bool> Linked(IReadOnlyList<EntryInput> entries, IReadOnlyList<RowInput> rows)
    {
        var repeatedEntry = entries.GroupBy(entry => entry.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (repeatedEntry is not null)
            return Fail<bool>(ReportProblem.Duplicate("Entry id", repeatedEntry.Key));

        var repeatedRow = rows.GroupBy(row => (row.Source, row.RowKey)).FirstOrDefault(group => group.Count() > 1);
        if (repeatedRow is not null)
            return Fail<bool>(ReportProblem.Duplicate($"The {repeatedRow.First().SourceText} row", repeatedRow.Key.RowKey));

        var ids = entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var orphan = rows.Select((row, index) => (row, index)).FirstOrDefault(pair => pair.row.EntryId is { } id && !ids.Contains(id));
        if (orphan.row is not null)
            return Fail<bool>(ReportProblem.UnknownEntry($"rows[{orphan.index}].entryId", orphan.row.EntryId!));

        var keys = rows.Select(row => (row.Source, row.RowKey)).ToHashSet();
        var missing = entries
            .SelectMany((entry, index) => entry.Left.Select(key => (index, Side: SourceSide.Left, key)).Concat(entry.Right.Select(key => (index, Side: SourceSide.Right, key))))
            .Where(member => !keys.Contains((member.Side, member.key)))
            .Select(member => ReportProblem.UnknownRow($"entries[{member.index}].{(member.Side == SourceSide.Left ? "left" : "right")}", member.Side == SourceSide.Left ? "left" : "right", member.key))
            .FirstOrDefault();
        return missing is not null ? Fail<bool>(missing) : ReconResult.Ok(true);
    }

    private static class Wire
    {
        public sealed record Root(Run? Run, Options? Options, List<string>? Passes, List<Entry>? Entries, List<Row>? Rows);

        public sealed record Run(string? AsOf, string? ProfileId, string? Title, string? EngineVersion, Dictionary<string, string>? Roles);

        public sealed record Options(
            List<string>? CurrencyOrder,
            List<string>? PayingSides,
            List<string>? SelectedEntries,
            int? MaxCards,
            Dictionary<string, string>? Labels,
            Raw? RawColumns,
            List<Tolerance>? Tolerances,
            string? Theme);

        public sealed record Raw(List<string>? Left, List<string>? Right);

        public sealed record Tolerance(string? Path, string? Field, string? Kind, JsonElement? Absolute, JsonElement? Relative);

        public sealed record Entry(
            string? Id,
            string? PathId,
            string? Alignment,
            List<string>? Composition,
            string? Outcome,
            List<string>? Left,
            List<string>? Right,
            Dictionary<string, JsonElement>? Ours,
            Dictionary<string, JsonElement>? Theirs,
            string? CommittedBy,
            List<Step>? Trail,
            List<HintWire>? Hints,
            List<Diff>? Diffs);

        public sealed record Step(string? Pass, string? Verdict, string? Key, string? Detail, List<Candidate>? Candidates);

        public sealed record Candidate(string? Source, string? RowKey);

        public sealed record HintWire(string? Code, string? Detail);

        public sealed record Diff(
            string? Field,
            string? Role,
            string? Type,
            string? Outcome,
            JsonElement? Ours,
            JsonElement? Theirs,
            string? DeltaKind,
            JsonElement? Delta,
            List<string>? AttributedTo);

        public sealed record Row(
            string? Source,
            string? RowKey,
            string? Disposition,
            string? Suppression,
            string? Validity,
            string? EntryId,
            bool? ItemLead,
            Dictionary<string, JsonElement>? Values,
            List<string>? Diagnostics,
            Dictionary<string, string?>? Raw,
            List<TraceWire>? Trace);

        public sealed record TraceWire(string? RuleId, string? Field, string? Raw, string? Canonical);
    }
}
