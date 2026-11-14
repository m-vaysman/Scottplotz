namespace Scottplotz.Recon;

public enum FieldMatch
{
    Equal,
    Within,
    Beyond
}

public static class ReconComparison
{
    public static FieldMatch Compare(ReconField field, ReconValue? left, ReconValue? right, ReconTolerance fallback) =>
        (left, right) switch
        {
            (null, null) => FieldMatch.Equal,
            (null, _) or (_, null) => FieldMatch.Beyond,
            (ReconValue.Number l, ReconValue.Number r) => Numbers(l.Value, r.Value, field.Tolerance ?? fallback),
            (ReconValue.Text l, ReconValue.Text r) when field.Type == ReconFieldType.Date =>
                Dates(l.Value, r.Value, field.Tolerance?.Absolute ?? 0),
            (ReconValue.Text l, ReconValue.Text r) =>
                string.Equals(l.Value, r.Value, StringComparison.Ordinal) ? FieldMatch.Equal : FieldMatch.Beyond,
            _ => FieldMatch.Beyond
        };

    public static FieldMatch Numbers(double left, double right, ReconTolerance tolerance)
    {
        var gap = Math.Abs(right - left);
        if (gap == 0)
            return FieldMatch.Equal;

        var slack = 1e-9 * Math.Max(1, Math.Abs(left));
        var withinAbsolute = gap <= tolerance.Absolute + slack;
        var withinPercent = tolerance.Percent > 0 && left != 0 && gap / Math.Abs(left) * 100 <= tolerance.Percent + 1e-9;
        return withinAbsolute || withinPercent ? FieldMatch.Within : FieldMatch.Beyond;
    }

    public static FieldMatch Dates(string left, string right, double toleranceDays)
    {
        var days = ReconRead.ParseDate(left) is DateTime l && ReconRead.ParseDate(right) is DateTime r
            ? Math.Abs((r - l).TotalDays)
            : double.PositiveInfinity;
        return days == 0 ? FieldMatch.Equal : days <= toleranceDays ? FieldMatch.Within : FieldMatch.Beyond;
    }

    public static IReadOnlyList<FieldMatch> Matches(
        IReadOnlyList<ReconField> fields,
        IReadOnlyList<ReconValue?> left,
        IReadOnlyList<ReconValue?> right,
        ReconTolerance fallback) =>
        fields
            .Select((field, index) => field.Compare
                ? Compare(field, left[index], right[index], fallback)
                : FieldMatch.Equal)
            .ToList();

    public static ReconStatus Status(IReadOnlyList<FieldMatch> matches) =>
        matches.Contains(FieldMatch.Beyond) ? ReconStatus.Break
        : matches.Contains(FieldMatch.Within) ? ReconStatus.Tolerance
        : ReconStatus.Exact;

    public static string Token(ReconStatus status) =>
        status switch
        {
            ReconStatus.LeftOnly => "leftOnly",
            ReconStatus.Exact => "exact",
            ReconStatus.Tolerance => "tolerance",
            ReconStatus.Break => "break",
            ReconStatus.RightOnly => "rightOnly",
            _ => throw new InvalidOperationException("Unknown status.")
        };

    public static IReadOnlyList<string> Tokens { get; } =
        Enum.GetValues<ReconStatus>().Select(Token).ToList();

    public static ReconStatus? FromToken(string token) =>
        Enum.GetValues<ReconStatus>()
            .Select(status => (ReconStatus?)status)
            .FirstOrDefault(status => Token(status!.Value) == token);
}
