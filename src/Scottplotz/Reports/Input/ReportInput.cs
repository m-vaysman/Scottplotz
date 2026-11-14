using System.Globalization;

namespace Scottplotz.Reports.Input;

public enum ReportRole
{
    Counterparty,
    Instrument,
    Asset,
    Side,
    TradeDate,
    SettleDate,
    Quantity,
    Price,
    Currency,
    NetCash,
    Accrued,
    Fees,
    Book,
    LifecycleAction,
    LifecycleTarget
}

public enum Alignment
{
    Matched,
    Ambiguous,
    SearchIncomplete,
    LeftOnly,
    RightOnly
}

[Flags]
public enum Composition
{
    None = 0,
    OneToOne = 1,
    ManyToOne = 2,
    OneToMany = 4,
    Package = 8,
    LifecycleCollapsed = 16
}

public enum Outcome
{
    NotCompared,
    Exact,
    WithinTolerance,
    Break
}

public enum DiffOutcome
{
    WithinTolerance,
    Break
}

public enum Disposition
{
    Matched,
    Ambiguous,
    SearchIncomplete,
    LeftOnly,
    RightOnly,
    Quarantined,
    Suppressed
}

public enum Validity
{
    Valid,
    ValidWithWarnings,
    Invalid
}

public enum DeltaKind
{
    None,
    Number,
    Days
}

public enum FieldKind
{
    Decimal,
    Date,
    Text
}

public enum SourceSide
{
    Left,
    Right
}

public sealed record FieldValue(string Text, decimal? Number, DateOnly? Date)
{
    public static FieldValue Null { get; } = new("", null, null);

    public bool IsNull => Number is null && Date is null && Text.Length == 0;

    public object? Cell => Number is { } number ? number : Date is { } date ? date : Text.Length > 0 ? Text : null;

    public static FieldValue Of(decimal number) => new(number.ToString(CultureInfo.InvariantCulture), number, null);

    public static FieldValue Of(DateOnly date) => new(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null, date);

    public static FieldValue Of(string text) => text.Length == 0 ? Null : new(text, null, null);
}

public sealed record RoleValues(IReadOnlyDictionary<ReportRole, FieldValue> Values)
{
    public static RoleValues None { get; } = new(new Dictionary<ReportRole, FieldValue>());

    public FieldValue this[ReportRole role] => Values.TryGetValue(role, out var value) ? value : FieldValue.Null;
}

public static class Roles
{
    public static FieldKind Kind(ReportRole role) =>
        role switch
        {
            ReportRole.Quantity or ReportRole.Price or ReportRole.NetCash or ReportRole.Accrued or ReportRole.Fees => FieldKind.Decimal,
            ReportRole.TradeDate or ReportRole.SettleDate => FieldKind.Date,
            _ => FieldKind.Text
        };

    public static string Names => string.Join(", ", Enum.GetNames<ReportRole>());
}

public sealed record RunInfo(
    DateOnly AsOf,
    string ProfileId,
    string Title,
    string EngineVersion,
    IReadOnlyDictionary<ReportRole, string> Roles)
{
    public bool IsMapped(ReportRole role) => Roles.ContainsKey(role);

    public string? Field(ReportRole role) => Roles.TryGetValue(role, out var field) ? field : null;
}

public sealed record ToleranceRule(string Path, string Field, string Kind, decimal? Absolute, decimal? Relative);

public sealed record RawColumns(IReadOnlyList<string> Left, IReadOnlyList<string> Right);

public sealed record ReportOptions(
    IReadOnlyList<string> CurrencyOrder,
    IReadOnlyList<string> PayingSides,
    IReadOnlyList<string>? SelectedEntries,
    int MaxCards,
    IReadOnlyDictionary<string, string> Labels,
    RawColumns? RawColumns,
    IReadOnlyList<ToleranceRule>? Tolerances,
    Scottplotz.Recon.ReconTheme Theme);

public sealed record TrailCandidate(SourceSide Source, string RowKey);

public sealed record TrailStep(string Pass, string Verdict, string? Key, string? Detail, IReadOnlyList<TrailCandidate> Candidates);

public sealed record Hint(string Code, string? Detail);

public sealed record DiffInput(
    string Field,
    ReportRole? Role,
    FieldKind Type,
    DiffOutcome Outcome,
    FieldValue Ours,
    FieldValue Theirs,
    DeltaKind DeltaKind,
    decimal? Delta,
    IReadOnlyList<string> AttributedTo);

public sealed record EntryInput(
    string Id,
    string PathId,
    Alignment Alignment,
    Composition Composition,
    Outcome Outcome,
    IReadOnlyList<string> Left,
    IReadOnlyList<string> Right,
    RoleValues? Ours,
    RoleValues? Theirs,
    string? CommittedBy,
    IReadOnlyList<TrailStep> Trail,
    IReadOnlyList<Hint> Hints,
    IReadOnlyList<DiffInput> Diffs)
{
    public bool Unrouted => Trail is [{ Detail: "Unrouted" }];
}

public sealed record TraceStep(string RuleId, string Field, string? Raw, string? Canonical);

public sealed record RowInput(
    SourceSide Source,
    string RowKey,
    Disposition Disposition,
    string? Suppression,
    Validity Validity,
    string? EntryId,
    bool ItemLead,
    RoleValues Values,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyDictionary<string, string?>? Raw,
    IReadOnlyList<TraceStep> Trace)
{
    public string SourceText => Source == SourceSide.Left ? "left" : "right";
}

public sealed record ReportInput(
    RunInfo Run,
    ReportOptions Options,
    IReadOnlyList<string> Passes,
    IReadOnlyList<EntryInput> Entries,
    IReadOnlyList<RowInput> Rows);

public sealed record ReportLimits(int MaxRows, int MaxEntries)
{
    public static ReportLimits Default { get; } = new(50_000, 50_000);

    public static ReportLimits Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection("Reports");
        return new ReportLimits(Read(section, "MaxRows", Default.MaxRows), Read(section, "MaxEntries", Default.MaxEntries));
    }

    private static int Read(IConfiguration section, string key, int fallback) =>
        string.IsNullOrWhiteSpace(section[key]) ? fallback
        : int.TryParse(section[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value
        : throw new InvalidOperationException($"Reports:{key} is '{section[key]}'. It must be a positive integer.");
}
