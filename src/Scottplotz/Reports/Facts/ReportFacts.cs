using Scottplotz.Reports.Input;

namespace Scottplotz.Reports.Facts;

public sealed record EntryFact(
    int Index,
    EntryInput Entry,
    IReadOnlyList<string> OursCurrencies,
    IReadOnlyList<string> TheirsCurrencies,
    int SuppressedRows,
    IReadOnlyList<string> RootFields,
    IReadOnlyList<string> Diagnostics)
{
    public string Id => Entry.Id;

    public string Short => Id.Length > 12 ? Id[..12] : Id;

    public bool IsMatched => Entry.Alignment == Alignment.Matched;

    public RoleValues Ours => Entry.Ours ?? RoleValues.None;

    public RoleValues Theirs => Entry.Theirs ?? RoleValues.None;

    public bool CurrencySplit =>
        IsMatched && OursCurrencies.Count > 0 && TheirsCurrencies.Count > 0 && !OursCurrencies.SequenceEqual(TheirsCurrencies, StringComparer.Ordinal);

    public FieldValue Value(ReportRole role) => Ours[role].IsNull ? Theirs[role] : Ours[role];

    public string Text(ReportRole role) => Value(role).Text;

    public string Keys(SourceSide side) => string.Join("; ", side == SourceSide.Left ? Entry.Left : Entry.Right);
}

public sealed record RowFact(int Index, RowInput Row, EntryFact? Entry)
{
    public RoleValues Values => Row.Values;

    public bool ItemLead => Row.ItemLead;

    public string Source => Row.SourceText;

    public bool PairCurrencySplit => Entry is { CurrencySplit: true } && Row.Disposition == Disposition.Matched;

    public string? CommittedBy => Row.Disposition == Disposition.Matched ? Entry?.Entry.CommittedBy : null;

    public int? CashSign(IReadOnlyList<string> payingSides) =>
        Values[ReportRole.Side].Text is { Length: > 0 } side ? payingSides.Contains(side, StringComparer.Ordinal) ? -1 : 1 : null;

    public decimal SignedCash(IReadOnlyList<string> payingSides) =>
        Values[ReportRole.NetCash].Number is { } cash && CashSign(payingSides) is { } sign ? cash * sign : 0m;
}

public enum DiffState
{
    Break,
    Tolerance,
    Attributed
}

public sealed record DiffFact(int Index, EntryFact Entry, DiffInput Diff, decimal? ToleranceAmount)
{
    public string Field => Diff.Field;

    public ReportRole? Role => Diff.Role;

    public FieldValue Ours => Diff.Ours;

    public FieldValue Theirs => Diff.Theirs;

    public decimal? Delta => Diff.DeltaKind == DeltaKind.None ? null : Diff.Delta;

    public DiffState State =>
        Diff.AttributedTo.Count > 0 ? DiffState.Attributed
        : Diff.Outcome == DiffOutcome.WithinTolerance ? DiffState.Tolerance
        : DiffState.Break;
}

public sealed record OpenItem(
    EntryFact? Entry,
    RowFact? Row,
    string ItemType,
    string Category,
    DiffFact? Diff,
    decimal? Impact,
    string Action,
    FieldValue SettleDate)
{
    public string Field =>
        Diff is { } diff ? diff.Field
        : Row is { Row.Validity: Validity.Invalid } ? "(excluded: invalid)"
        : Row is not null ? "(not routed)"
        : Entry is { Entry.Unrouted: true } ? "(not routed)"
        : "(whole trade)";

    public string Key => Entry?.Id ?? $"(no entry) {Row!.Source}:{Row.Row.RowKey}";

    public string Label => Entry?.Short ?? Row!.Row.RowKey;

    public FieldValue Value(ReportRole role) => Entry?.Value(role) ?? Row!.Values[role];

    public string Text(ReportRole role) => Value(role).Text;

    public string Status(DateOnly asOf) => Calendar.Status(SettleDate.Date, asOf);

    public int? Age(DateOnly asOf) => Value(ReportRole.TradeDate).Date is { } trade ? Calendar.BusinessAge(trade, asOf) : null;
}

public sealed record ReportFacts(
    ReportInput Input,
    IReadOnlyList<EntryFact> Entries,
    IReadOnlyList<RowFact> Rows,
    IReadOnlyList<DiffFact> Diffs,
    IReadOnlyList<OpenItem> OpenItems,
    IReadOnlyList<string> Passes)
{
    public ILookup<string, RowFact> RowsByEntry { get; } = Rows.Where(row => row.Entry is not null).ToLookup(row => row.Entry!.Id, StringComparer.Ordinal);

    public ILookup<string, DiffFact> DiffsByEntry { get; } = Diffs.ToLookup(diff => diff.Entry.Id, StringComparer.Ordinal);

    public IReadOnlyDictionary<(SourceSide Source, string RowKey), RowFact> RowByKey { get; } = Rows.ToDictionary(row => (row.Row.Source, row.Row.RowKey));

    public IEnumerable<RowFact> Members(EntryFact entry, SourceSide side) =>
        (side == SourceSide.Left ? entry.Entry.Left : entry.Entry.Right)
            .Select(key => RowByKey.TryGetValue((side, key), out var row) ? row : null)
            .OfType<RowFact>();
}

public static class Calendar
{
    public const string NoSettleDate = "No settle date";
    public const string PastSettleDate = "Past settle date";
    public const string SettlesToday = "Settles today";
    public const string Upcoming = "Upcoming";

    public static IReadOnlyList<string> Statuses { get; } = [PastSettleDate, SettlesToday, Upcoming, NoSettleDate];

    public static string Status(DateOnly? settle, DateOnly asOf) =>
        settle is not { } date ? NoSettleDate
        : date < asOf ? PastSettleDate
        : date == asOf ? SettlesToday
        : Upcoming;

    public static int StatusRank(DateOnly? settle, DateOnly asOf) =>
        settle is not { } date ? 3 : date < asOf ? 0 : date == asOf ? 1 : 2;

    // Excel NETWORKDAYS(trade, asOf) - 1: weekdays in the closed interval, negative when the trade date is after the as-of date.
    public static int BusinessAge(DateOnly trade, DateOnly asOf) => NetworkDays(trade, asOf) - 1;

    public static int NetworkDays(DateOnly start, DateOnly end) =>
        start <= end ? Weekdays(start, end) : -Weekdays(end, start);

    private static int Weekdays(DateOnly first, DateOnly last) =>
        Enumerable.Range(0, last.DayNumber - first.DayNumber + 1)
            .Select(first.AddDays)
            .Count(day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
}

public static class Ordering
{
    public static int CurrencyRank(string currency, IReadOnlyList<string> order) =>
        order.Select((code, index) => (code, index)).Where(pair => pair.code == currency).Select(pair => pair.index).DefaultIfEmpty(order.Count).First();

    public static IReadOnlyList<string> SortCurrencies(IEnumerable<string> currencies, IReadOnlyList<string> order) =>
        currencies.Distinct(StringComparer.Ordinal)
            .OrderBy(currency => CurrencyRank(currency, order))
            .ThenBy(currency => currency, StringComparer.Ordinal)
            .ToList();
}
