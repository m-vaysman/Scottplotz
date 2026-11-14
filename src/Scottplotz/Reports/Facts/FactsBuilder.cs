using Scottplotz.Reports.Input;

namespace Scottplotz.Reports.Facts;

public static class FactsBuilder
{
    private const string NotRouted = "Not routed to any matching path: check the profile routes";
    private static readonly string[] UnresolvedCodes = ["UnknownParty", "UnknownSymbol", "MissingKey"];

    public static ReportFacts Build(ReportInput input)
    {
        var rowByKey = input.Rows.ToDictionary(row => (row.Source, row.RowKey));
        var rowsByEntry = input.Rows.Where(row => row.EntryId is not null).ToLookup(row => row.EntryId!, StringComparer.Ordinal);
        var currencyMapped = input.Run.IsMapped(ReportRole.Currency);
        var entries = input.Entries
            .Select((entry, index) => new EntryFact(
                index,
                entry,
                Currencies(entry.Left, SourceSide.Left, rowByKey, currencyMapped),
                Currencies(entry.Right, SourceSide.Right, rowByKey, currencyMapped),
                rowsByEntry[entry.Id].Count(row => row.Disposition == Disposition.Suppressed),
                RootFields(entry),
                rowsByEntry[entry.Id].SelectMany(row => row.Diagnostics).Distinct(StringComparer.Ordinal).ToList()))
            .ToList();
        var entryById = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var rows = input.Rows
            .Select((row, index) => new RowFact(index, row, row.EntryId is { } id && entryById.TryGetValue(id, out var entry) ? entry : null))
            .ToList();
        var diffs = entries
            .Where(entry => entry.IsMatched)
            .SelectMany(entry => entry.Entry.Diffs.Select(diff => (entry, diff)))
            .Select((pair, index) => new DiffFact(index, pair.entry, pair.diff, ToleranceAmount(input.Options.Tolerances, pair.entry.Entry.PathId, pair.diff)))
            .ToList();
        return new ReportFacts(input, entries, rows, diffs, OpenItems(input, entries, rows, diffs), Passes(input));
    }

    private static IReadOnlyList<string> Currencies(
        IReadOnlyList<string> members,
        SourceSide side,
        Dictionary<(SourceSide, string), RowInput> rows,
        bool mapped) =>
        mapped
            ? members
                .Select(key => rows.TryGetValue((side, key), out var row) ? row.Values[ReportRole.Currency].Text : "")
                .Where(text => text.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    // A root break is a Break that no other field explains; when every break is attributed, the first break stands in.
    private static IReadOnlyList<string> RootFields(EntryInput entry)
    {
        var breaks = entry.Diffs.Where(diff => diff.Outcome == DiffOutcome.Break).ToList();
        var roots = breaks.Where(diff => diff.AttributedTo.Count == 0).Select(diff => diff.Field).ToList();
        return roots.Count == 0 && breaks.Count > 0 ? [breaks[0].Field] : roots;
    }

    public static decimal? ToleranceAmount(IReadOnlyList<ToleranceRule>? tolerances, string pathId, DiffInput diff)
    {
        var rule = tolerances?.FirstOrDefault(item => item.Path == pathId && item.Field == diff.Field);
        var scale = Math.Max(Math.Abs(diff.Ours.Number ?? 0m), Math.Abs(diff.Theirs.Number ?? 0m));
        return rule switch
        {
            { Kind: "Absolute", Absolute: { } absolute } => absolute,
            { Kind: "Relative", Relative: { } relative } => relative * scale,
            { Kind: "AbsoluteOrRelative", Absolute: { } absolute, Relative: { } relative } => Math.Max(absolute, relative * scale),
            _ => null
        };
    }

    private static IReadOnlyList<string> Passes(ReportInput input) =>
        input.Passes.Concat(input.Entries.Select(entry => entry.CommittedBy).OfType<string>()).Distinct(StringComparer.Ordinal).ToList();

    private static IReadOnlyList<OpenItem> OpenItems(ReportInput input, IReadOnlyList<EntryFact> entries, IReadOnlyList<RowFact> rows, IReadOnlyList<DiffFact> diffs)
    {
        var diffsByEntry = diffs.ToLookup(diff => diff.Entry.Id, StringComparer.Ordinal);
        var netCash = input.Run.IsMapped(ReportRole.NetCash);
        var fromEntries = entries.Select(entry => OpenItem(entry, diffsByEntry[entry.Id].ToList(), netCash)).OfType<OpenItem>();
        var orphans = rows
            .Where(row => row is { Entry: null, ItemLead: true, Row.Disposition: Disposition.LeftOnly or Disposition.RightOnly or Disposition.Quarantined })
            .Select(row => Orphan(row, netCash));
        var asOf = input.Run.AsOf;
        var order = input.Options.CurrencyOrder;
        return fromEntries.Concat(orphans)
            .OrderBy(item => Calendar.StatusRank(item.SettleDate.Date, asOf))
            .ThenBy(item => Ordering.CurrencyRank(item.Text(ReportRole.Currency), order))
            .ThenBy(item => item.Text(ReportRole.Currency), StringComparer.Ordinal)
            .ThenByDescending(item => item.Impact ?? decimal.MinValue)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static OpenItem Orphan(RowFact row, bool netCash)
    {
        var type = row.Row.Disposition == Disposition.RightOnly || row.Row is { Disposition: Disposition.Quarantined, Source: SourceSide.Right }
            ? "Unmatched theirs"
            : "Unmatched ours";
        var codes = string.Join(", ", row.Row.Diagnostics.Distinct(StringComparer.Ordinal));
        var action = row.Row.Validity == Validity.Invalid
            ? $"Fix the input row: the engine excluded it as invalid ({codes})"
            : NotRouted;
        return new OpenItem(null, row, type, type, null, netCash ? Abs(row.Values[ReportRole.NetCash].Number) : null, action, row.Values[ReportRole.SettleDate]);
    }

    private static decimal? Abs(decimal? value) => value is { } number ? Math.Abs(number) : null;

    private static OpenItem? OpenItem(EntryFact entry, IReadOnlyList<DiffFact> diffs, bool netCash) =>
        entry.Entry switch
        {
            { Outcome: Outcome.Break } => Break(entry, diffs, netCash),
            { Alignment: Alignment.LeftOnly, Unrouted: true } => Whole(entry, "Unmatched ours", entry.Ours, netCash, NotRouted),
            { Alignment: Alignment.RightOnly, Unrouted: true } => Whole(entry, "Unmatched theirs", entry.Theirs, netCash, NotRouted),
            { Alignment: Alignment.LeftOnly } => Whole(entry, "Unmatched ours", entry.Ours, netCash, "Chase confirmation: not in their file"),
            { Alignment: Alignment.RightOnly } => Whole(entry, "Unmatched theirs", entry.Theirs, netCash, "DK: investigate alleged trade"),
            { Alignment: Alignment.Ambiguous } => Whole(entry, "Ambiguous", entry.Entry.Ours is not null ? entry.Ours : entry.Theirs, false, "Resolve ambiguity: choose the rows that belong together"),
            { Alignment: Alignment.SearchIncomplete } => Whole(entry, "Search incomplete", entry.Entry.Ours is not null ? entry.Ours : entry.Theirs, false, "Review: the match search hit a profile limit"),
            _ => null
        };

    private static OpenItem Break(EntryFact entry, IReadOnlyList<DiffFact> diffs, bool netCash)
    {
        var root = diffs.FirstOrDefault(diff => diff.Field == entry.RootFields.FirstOrDefault());
        var cashDiff = diffs.FirstOrDefault(diff => diff.Role == ReportRole.NetCash);
        var bothCash = entry.Ours[ReportRole.NetCash].Number is not null && entry.Theirs[ReportRole.NetCash].Number is not null;
        decimal? impact = (netCash, root?.Role == ReportRole.SettleDate, cashDiff) switch
        {
            (false, _, _) => null,
            (true, true, _) => Abs(entry.Ours[ReportRole.NetCash].Number),
            (true, false, { Ours.Number: not null, Theirs.Number: not null }) => Abs(cashDiff.Delta),
            (true, false, null) when bothCash => 0m,
            _ => null
        };
        var field = root?.Field ?? "(whole trade)";
        return new OpenItem(entry, null, "Field break", field, root, impact, Action(entry, BreakAction(root?.Role, field)), entry.Ours[ReportRole.SettleDate]);
    }

    private static OpenItem Whole(EntryFact entry, string type, RoleValues side, bool netCash, string action) =>
        new(entry, null, type, type, null, netCash ? Abs(side[ReportRole.NetCash].Number) : null, Action(entry, action), side[ReportRole.SettleDate]);

    private static string BreakAction(ReportRole? role, string field) =>
        role switch
        {
            ReportRole.Price => "Confirm execution price with counterparty",
            ReportRole.Quantity => "Confirm fill quantity (partial fill?)",
            ReportRole.SettleDate => "Align settlement date / SSI convention",
            ReportRole.Fees => "Check commission schedule",
            ReportRole.Accrued => "Check accrual convention and day count",
            ReportRole.NetCash => "Reconcile net cash components",
            ReportRole.Currency => "Confirm settlement currency",
            ReportRole.TradeDate => "Confirm trade date",
            _ => $"Investigate {field} difference"
        };

    private static string Action(EntryFact entry, string action) =>
        entry.Diagnostics.Where(code => UnresolvedCodes.Contains(code, StringComparer.Ordinal)).ToList() is { Count: > 0 } codes
            ? $"Set up reference data ({string.Join(", ", codes)}), then: {action}"
            : action;
}
