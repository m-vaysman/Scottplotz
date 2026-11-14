using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Facts;

public static class Categories
{
    private static readonly ReportRole[] FieldOrder =
    [
        ReportRole.Price, ReportRole.Quantity, ReportRole.SettleDate, ReportRole.Fees,
        ReportRole.Accrued, ReportRole.NetCash, ReportRole.Currency, ReportRole.TradeDate
    ];

    private static readonly string[] WholeTrade = ["Unmatched ours", "Unmatched theirs", "Ambiguous", "Search incomplete"];

    // Open-item categories in report order: mapped field names that occur, other field categories, then the whole-trade types.
    public static IReadOnlyList<string> Of(ReportFacts facts)
    {
        var present = facts.OpenItems.Select(item => item.Category).ToHashSet(StringComparer.Ordinal);
        var known = FieldOrder.Select(role => facts.Input.Run.Field(role)).OfType<string>().Where(present.Contains).ToList();
        var others = present.Except(known, StringComparer.Ordinal).Except(WholeTrade, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return
        [
            .. known,
            .. others,
            "Unmatched ours",
            "Unmatched theirs",
            .. WholeTrade.Skip(2).Where(present.Contains)
        ];
    }
}

public sealed record SideBySideLine(string Row, string Column, decimal Left, decimal Right, decimal LeftOnly, decimal RightOnly, decimal MatchedDelta)
{
    public decimal Delta => Right - Left;
}

public static class SideBySide
{
    // The engine's side-by-side view for one currency: quantity by counterparty and asset, each consolidated item counted once.
    // LeftOnly is negative, so Delta = LeftOnly + RightOnly + MatchedDelta. Suppressed, Quarantined, Ambiguous and SearchIncomplete rows are left out.
    public static IReadOnlyList<SideBySideLine> Lines(ReportFacts facts, string currency) =>
        facts.Rows
            .Where(row => row.ItemLead
                && row.Row.Disposition is Disposition.Matched or Disposition.LeftOnly or Disposition.RightOnly
                && row.Values[ReportRole.Currency].Text == currency
                && Measured(facts, row))
            .GroupBy(row => (Row: row.Values[ReportRole.Counterparty].Text, Column: row.Values[ReportRole.Asset].Text))
            .Select(group =>
            {
                decimal Sum(SourceSide side, Disposition disposition) =>
                    group.Where(row => row.Row.Source == side && row.Row.Disposition == disposition).Sum(row => row.Values[ReportRole.Quantity].Number ?? 0m);
                var matchedLeft = Sum(SourceSide.Left, Disposition.Matched);
                var matchedRight = Sum(SourceSide.Right, Disposition.Matched);
                var leftOnly = Sum(SourceSide.Left, Disposition.LeftOnly);
                var rightOnly = Sum(SourceSide.Right, Disposition.RightOnly);
                return new SideBySideLine(group.Key.Row, group.Key.Column, matchedLeft + leftOnly, matchedRight + rightOnly, -leftOnly, rightOnly, matchedRight - matchedLeft);
            })
            .OrderBy(line => line.Row, StringComparer.Ordinal)
            .ThenBy(line => line.Column, StringComparer.Ordinal)
            .ToList();

    private static bool Measured(ReportFacts facts, RowFact row) =>
        row.Values[ReportRole.Quantity].Number is not null
        && (row.Row.Disposition != Disposition.Matched
            || row.Entry is null
            || facts.Members(row.Entry, SourceSide.Left).Concat(facts.Members(row.Entry, SourceSide.Right)).All(member => member.Values[ReportRole.Quantity].Number is not null));
}

public static class DataBlocks
{
    public static NumberFormat FormatFor(ReportRole? role) =>
        role switch
        {
            ReportRole.Quantity => NumberFormat.Quantity,
            ReportRole.NetCash or ReportRole.Accrued or ReportRole.Fees => NumberFormat.Amount,
            ReportRole.TradeDate or ReportRole.SettleDate => NumberFormat.Date,
            _ => NumberFormat.Price
        };

    public static ReportCell Cell(FieldValue value, ReportRole? role) =>
        ReportCell.Of(value.Cell, value.Date is not null ? NumberFormat.Date : value.Number is not null ? FormatFor(role) : NumberFormat.General);

    public static DataBlock OpenItems(ReportContext context) =>
        new(
            "Open items",
            "Every open item in rank order. Age and status are worked out from the as-of date.",
            new TableBlock(
                null,
                Columns("entryId", "itemType", "category", "counterparty", "currency", "tradeDate", "settleDate", "status", "ageBD"),
                context.Facts.OpenItems.Select(item => ReportRow.Of(
                    item.Key,
                    item.ItemType,
                    item.Category,
                    item.Text(ReportRole.Counterparty),
                    item.Text(ReportRole.Currency),
                    item.Value(ReportRole.TradeDate).Cell,
                    item.SettleDate.Cell,
                    item.Status(context.AsOf),
                    ReportCell.Of(item.Age(context.AsOf), NumberFormat.Integer))).ToList()));

    public static DataBlock Diffs(ReportContext context) =>
        new(
            "Differences",
            "Every field difference on a matched entry.",
            new TableBlock(
                null,
                Columns("entryId", "path", "field", "outcome", "state", "ours", "theirs", "delta", "deltaKind", "toleranceAmount", "attributedTo"),
                context.Facts.Diffs.Select(diff => ReportRow.Of(
                    diff.Entry.Id,
                    diff.Entry.Entry.PathId,
                    diff.Field,
                    diff.Diff.Outcome.ToString(),
                    diff.State.ToString(),
                    Cell(diff.Ours, diff.Role),
                    Cell(diff.Theirs, diff.Role),
                    ReportCell.Of(diff.Delta, diff.Diff.DeltaKind == DeltaKind.Days || FormatFor(diff.Role) == NumberFormat.Date ? NumberFormat.Integer : FormatFor(diff.Role)),
                    diff.Diff.DeltaKind.ToString(),
                    ReportCell.Of(diff.ToleranceAmount, NumberFormat.Price),
                    string.Join(", ", diff.Diff.AttributedTo))).ToList()));

    public static DataBlock Entries(ReportContext context) =>
        new(
            "Result entries",
            "One line per engine entry.",
            new TableBlock(
                null,
                Columns("entryId", "path", "alignment", "composition", "outcome", "committedBy", "counterparty", "instrument", "asset", "side", "tradeDate", "settleDate",
                    "currency", "theirsCurrency", "ccySplit", "leftRows", "rightRows", "suppressedRows", "leftKeys", "rightKeys",
                    "oursQuantity", "theirsQuantity", "oursPrice", "theirsPrice", "oursNetCash", "theirsNetCash", "rootFields", "diffFields", "diagnostics"),
                context.Facts.Entries.Select(entry => ReportRow.Of(
                    entry.Id,
                    entry.Entry.PathId,
                    entry.Entry.Alignment.ToString(),
                    entry.Entry.Composition.ToString(),
                    entry.Entry.Outcome.ToString(),
                    entry.Entry.CommittedBy,
                    entry.Text(ReportRole.Counterparty),
                    entry.Text(ReportRole.Instrument),
                    entry.Text(ReportRole.Asset),
                    entry.Text(ReportRole.Side),
                    entry.Value(ReportRole.TradeDate).Cell,
                    entry.Value(ReportRole.SettleDate).Cell,
                    entry.Text(ReportRole.Currency),
                    entry.Theirs[ReportRole.Currency].Text,
                    entry.CurrencySplit ? "Y" : "N",
                    ReportCell.Of(entry.Entry.Left.Count, NumberFormat.Count),
                    ReportCell.Of(entry.Entry.Right.Count, NumberFormat.Count),
                    ReportCell.Of(entry.SuppressedRows, NumberFormat.Count),
                    entry.Keys(SourceSide.Left),
                    entry.Keys(SourceSide.Right),
                    Cell(entry.Ours[ReportRole.Quantity], ReportRole.Quantity),
                    Cell(entry.Theirs[ReportRole.Quantity], ReportRole.Quantity),
                    Cell(entry.Ours[ReportRole.Price], ReportRole.Price),
                    Cell(entry.Theirs[ReportRole.Price], ReportRole.Price),
                    Cell(entry.Ours[ReportRole.NetCash], ReportRole.NetCash),
                    Cell(entry.Theirs[ReportRole.NetCash], ReportRole.NetCash),
                    string.Join(", ", entry.RootFields),
                    string.Join(", ", entry.Entry.Diffs.Select(diff => diff.Field)),
                    string.Join(", ", entry.Diagnostics))).ToList()));

    public static DataBlock Rows(ReportContext context) =>
        new(
            "Result rows",
            "One line per input row, with the engine's disposition.",
            new TableBlock(
                null,
                Columns("source", "rowKey", "disposition", "suppression", "validity", "entryId", "committedBy", "itemLead", "counterparty", "instrument", "asset", "side",
                    "currency", "tradeDate", "settleDate", "quantity", "price", "accrued", "fees", "netCash", "book", "action", "target", "pairCcySplit", "cashSign", "signedCash", "diagnostics"),
                context.Facts.Rows.Select(row => ReportRow.Of(
                    row.Source,
                    row.Row.RowKey,
                    row.Row.Disposition.ToString(),
                    row.Row.Suppression,
                    row.Row.Validity.ToString(),
                    row.Entry?.Id,
                    row.CommittedBy,
                    ReportCell.Of(row.ItemLead ? 1 : 0, NumberFormat.Integer),
                    row.Values[ReportRole.Counterparty].Text,
                    row.Values[ReportRole.Instrument].Text,
                    row.Values[ReportRole.Asset].Text,
                    row.Values[ReportRole.Side].Text,
                    row.Values[ReportRole.Currency].Text,
                    row.Values[ReportRole.TradeDate].Cell,
                    row.Values[ReportRole.SettleDate].Cell,
                    Cell(row.Values[ReportRole.Quantity], ReportRole.Quantity),
                    Cell(row.Values[ReportRole.Price], ReportRole.Price),
                    Cell(row.Values[ReportRole.Accrued], ReportRole.Accrued),
                    Cell(row.Values[ReportRole.Fees], ReportRole.Fees),
                    Cell(row.Values[ReportRole.NetCash], ReportRole.NetCash),
                    row.Values[ReportRole.Book].Text,
                    row.Values[ReportRole.LifecycleAction].Text,
                    row.Values[ReportRole.LifecycleTarget].Text,
                    row.PairCurrencySplit ? "Y" : "N",
                    ReportCell.Of(row.CashSign(context.Options.PayingSides), NumberFormat.Integer),
                    ReportCell.Of(row.SignedCash(context.Options.PayingSides), NumberFormat.Amount),
                    string.Join("; ", row.Row.Diagnostics))).ToList()));

    private static IReadOnlyList<ReportColumn> Columns(params string[] titles) =>
        titles.Select(title => new ReportColumn(title)).ToList();
}
