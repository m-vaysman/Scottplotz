using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// BO1. One line per open item (a break on its root field, or an unmatched, ambiguous or search-incomplete entry as a whole trade),
// ranked by settlement status, currency and impact, under two small counts by status and by type that tie to the number of items.
public static class ExceptionBlotter
{
    private static readonly string[] BaseTypes = ["Field break", "Unmatched ours", "Unmatched theirs"];
    private static readonly string[] OptionalTypes = ["Ambiguous", "Search incomplete"];

    public static ReportDefinition Definition { get; } = new(
        "BO1",
        [ReportRole.Counterparty, ReportRole.Instrument, ReportRole.Side, ReportRole.TradeDate, ReportRole.SettleDate, ReportRole.Currency],
        _ => null,
        Build);

    private sealed record Amounts(ReportCell Ours, ReportCell Theirs, ReportCell Delta)
    {
        public static Amounts None { get; } = new(ReportCell.Empty, ReportCell.Empty, ReportCell.Empty);
    }

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var items = context.Facts.OpenItems;
        var asOf = context.AsOf;
        var statuses = Calendar.Statuses.Where(status => status != Calendar.NoSettleDate || items.Any(item => item.SettleDate.Date is null)).ToList();
        var types = BaseTypes.Concat(OptionalTypes.Where(type => items.Any(item => item.ItemType == type))).ToList();
        var statusCounts = statuses.Select(status => items.Count(item => item.Status(asOf) == status)).ToList();
        var typeCounts = types.Select(type => items.Count(item => item.ItemType == type)).ToList();
        var ties = statusCounts.Sum() == typeCounts.Sum() && typeCounts.Sum() == items.Count;

        return
        [
            Summary(context, statuses, statusCounts, types, typeCounts, ties),
            new TableBlock(
                null,
                new[]
                    {
                        "Rank", "Entry", "Status", "Item type", "Counterparty", "Instrument", "Asset", "Side", "Trade date", "Settle date", "Age (bus. days)",
                        "Field", "Ours", "Theirs", "Delta", "Ccy", "Impact", "Suggested action", "Category"
                    }
                    .Select(title => new ReportColumn(context.Label(title)))
                    .ToList(),
                items.Select((item, index) => Line(context, item, index + 1)).ToList(),
                "Impact: full net cash for a settle-date break or an unmatched item; otherwise the absolute net-cash difference; blank when net cash is unknown. Ranked within currency only (no FX rates are applied). Rows the engine left without an entry (not routed, or excluded as invalid) are listed as unmatched. Status and age are worked out from the as-of date. Category is what the break heatmap and the ageing report group items by."),
            DataBlocks.Entries(context),
            DataBlocks.Diffs(context),
            .. items.Any(item => item.Row is not null) ? [DataBlocks.Rows(context)] : Array.Empty<ReportBlock>()
        ];
    }

    private static TableBlock Summary(ReportContext context, IReadOnlyList<string> statuses, IReadOnlyList<int> statusCounts, IReadOnlyList<string> types, IReadOnlyList<int> typeCounts, bool ties)
    {
        static ReportCell Count(int? value, bool bold = false) => new(CellValue.Of(value), NumberFormat.Count, Bold: bold);
        ReportCell Text(string? value) => ReportCell.Of(value);

        var lines = Enumerable.Range(0, Math.Max(statuses.Count, types.Count))
            .Select(index => new ReportRow(
            [
                Text(statuses.ElementAtOrDefault(index)),
                Count(index < statuses.Count ? statusCounts[index] : null),
                Text(types.ElementAtOrDefault(index)),
                Count(index < types.Count ? typeCounts[index] : null),
                ReportCell.Empty
            ]));
        var total = new ReportRow(
            [
                ReportCell.Of(context.Label("Total")),
                Count(statusCounts.Sum()),
                ReportCell.Of(context.Label("Total")),
                Count(typeCounts.Sum()),
                ReportCell.Check(ties)
            ],
            RowKind.Total);

        return new TableBlock(
            null,
            [new ReportColumn(context.Label("Status")), new ReportColumn(context.Label("Items")), new ReportColumn(context.Label("Item type")), new ReportColumn(context.Label("Items")), new ReportColumn(context.Label("Check"), CellAlign.Center)],
            [.. lines, total],
            "The check ties both totals to the number of open items.",
            Groups: [new ColumnGroup(context.Label("Open items by status"), 2), new ColumnGroup(context.Label("Open items by type"), 3)]);
    }

    private static ReportRow Line(ReportContext context, OpenItem item, int rank)
    {
        var amounts = AmountsOf(context, item);
        return new ReportRow(
        [
            ReportCell.Of(rank, NumberFormat.Integer),
            ReportCell.Of(item.Label),
            StatusCell(item.Status(context.AsOf)),
            ReportCell.Of(item.ItemType),
            ReportCell.Of(item.Text(ReportRole.Counterparty)),
            ReportCell.Of(item.Text(ReportRole.Instrument)),
            ReportCell.Of(item.Text(ReportRole.Asset)),
            ReportCell.Of(item.Text(ReportRole.Side)),
            ReportCell.Of(item.Value(ReportRole.TradeDate).Cell, NumberFormat.Date),
            ReportCell.Of(item.SettleDate.Cell, NumberFormat.Date),
            ReportCell.Of(item.Age(context.AsOf), NumberFormat.Integer),
            ReportCell.Of(item.Field),
            amounts.Ours,
            amounts.Theirs,
            amounts.Delta,
            ReportCell.Of(item.Text(ReportRole.Currency)),
            ReportCell.Of(item.Impact, NumberFormat.Amount),
            ReportCell.Of(item.Action),
            ReportCell.Of(item.Category)
        ]);
    }

    private static ReportCell StatusCell(string status) =>
        ReportCell.Of(status) with
        {
            Tone = status switch
            {
                Calendar.PastSettleDate => Tone.Break,
                Calendar.SettlesToday => Tone.Tolerance,
                Calendar.Upcoming => Tone.Attributed,
                _ => Tone.None
            }
        };

    private static Amounts AmountsOf(ReportContext context, OpenItem item)
    {
        var role = context.IsMapped(ReportRole.NetCash) ? ReportRole.NetCash : ReportRole.Quantity;
        return (item.ItemType, item.Diff) switch
        {
            ("Field break", { } diff) => new Amounts(
                DataBlocks.Cell(diff.Ours, diff.Role),
                DataBlocks.Cell(diff.Theirs, diff.Role),
                ReportCell.Of(diff.Delta, DeltaFormat(diff))),
            ("Unmatched ours", _) => Amounts.None with { Ours = DataBlocks.Cell(SideValue(item, role, SourceSide.Left), role) },
            ("Unmatched theirs", _) => Amounts.None with { Theirs = DataBlocks.Cell(SideValue(item, role, SourceSide.Right), role) },
            _ => Amounts.None
        };
    }

    // An entry's own side projection, or the orphan row's value.
    private static FieldValue SideValue(OpenItem item, ReportRole role, SourceSide side) =>
        item.Entry is { } entry ? (side == SourceSide.Left ? entry.Ours : entry.Theirs)[role] : item.Row!.Values[role];

    private static NumberFormat DeltaFormat(DiffFact diff) =>
        diff.Diff.DeltaKind == DeltaKind.Days || DataBlocks.FormatFor(diff.Role) == NumberFormat.Date ? NumberFormat.Integer : DataBlocks.FormatFor(diff.Role);
}
