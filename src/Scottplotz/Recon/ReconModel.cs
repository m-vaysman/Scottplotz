namespace Scottplotz.Recon;

public abstract record ReconValue
{
    public sealed record Number(double Value) : ReconValue;

    public sealed record Text(string Value) : ReconValue;

    public sealed record Flag(bool Value) : ReconValue;

    public sealed record Json(string Raw) : ReconValue;
}

public sealed record ReconSide(string Name, string Short);

public sealed record ReconTolerance(double Absolute, double Percent)
{
    public static ReconTolerance None { get; } = new(0, 0);
}

public enum ReconFieldType
{
    Number,
    Text,
    Date
}

public sealed record ReconField(
    string Key,
    string Label,
    ReconFieldType Type,
    int Decimals,
    bool Compare,
    ReconTolerance? Tolerance);

public enum ReconStatus
{
    LeftOnly,
    Exact,
    Tolerance,
    Break,
    RightOnly
}

public enum ReconDuplicate
{
    None,
    Left,
    Right,
    Either
}

public sealed record ReconLine(string? Key, string? Name, IReadOnlyList<ReconValue?> Values);

public sealed record ReconSideValues(IReadOnlyList<ReconValue?> Values, IReadOnlyList<ReconLine> Lines);

public sealed record ReconRecord(
    string Key,
    string Name,
    ReconStatus Status,
    int Count,
    ReconSideValues? Left,
    ReconSideValues? Right,
    double? Delta,
    double? Percent,
    IReadOnlyList<string> Breaks,
    IReadOnlyList<string> Group,
    string? Date,
    string? Reason,
    ReconDuplicate Duplicate,
    IReadOnlyList<KeyValuePair<string, ReconValue?>> Attributes);

public sealed record ReconGroup(
    IReadOnlyList<string> Path,
    string Name,
    string? Note,
    ReconSideValues? Left,
    ReconSideValues? Right,
    IReadOnlyList<KeyValuePair<string, ReconValue?>> Attributes);

public sealed record ReconSummary(
    int LeftOnly,
    int Exact,
    int Tolerance,
    int Break,
    int RightOnly,
    int Duplicates,
    int LeftCount,
    int RightCount,
    double? LeftTotal,
    double? RightTotal,
    double? Net);

public enum ReconTheme
{
    Auto,
    Light,
    Dark
}

public sealed record ReconModel(
    string ViewName,
    string ViewTitle,
    ReconSide Left,
    ReconSide Right,
    string Unit,
    string UnitOne,
    string? AsOf,
    ReconTolerance Tolerance,
    IReadOnlyList<ReconField> Fields,
    string? Measure,
    IReadOnlyList<ReconRecord> Records,
    IReadOnlyList<ReconGroup> Groups,
    ReconSummary Summary,
    IReadOnlyList<KeyValuePair<string, string>> Text,
    IReadOnlyList<KeyValuePair<string, string>> Labels,
    IReadOnlyList<KeyValuePair<string, ReconValue?>> Options,
    ReconTheme Theme,
    IReadOnlyList<KeyValuePair<string, string>> Palette);
