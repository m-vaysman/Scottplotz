using System.Globalization;
using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// BO5. Each matched many-to-one or one-to-many entry outside a package: the block, its allocations, and a totals line
// that says whether the allocations add up to the block in quantity and in net cash.
public static class Allocations
{
    public static ReportDefinition Definition { get; } = new(
        "BO5",
        [ReportRole.Quantity],
        context => Groups(context.Facts).Count == 0 ? "No many-to-one or one-to-many entries (outside packages) in this run." : null,
        Build);

    private sealed record Group(EntryFact Entry, RowFact Block, string BlockSide, IReadOnlyList<RowFact> Members);

    // The block side is the composition, not the member counts: duplicate copies collapsed into the block can inflate them.
    private static IReadOnlyList<Group> Groups(ReportFacts facts) =>
        facts.Entries
            .Where(entry => entry.IsMatched && !entry.Entry.Composition.HasFlag(Composition.Package))
            .Where(entry => entry.Entry.Composition.HasFlag(Composition.ManyToOne) || entry.Entry.Composition.HasFlag(Composition.OneToMany))
            .Select(entry => entry.Entry.Composition.HasFlag(Composition.ManyToOne)
                ? Of(facts, entry, SourceSide.Right, "theirs", SourceSide.Left)
                : Of(facts, entry, SourceSide.Left, "ours", SourceSide.Right))
            .OfType<Group>()
            .ToList();

    private static Group? Of(ReportFacts facts, EntryFact entry, SourceSide blockSide, string label, SourceSide memberSide) =>
        facts.Members(entry, blockSide).FirstOrDefault() is { } block ? new Group(entry, block, label, facts.Members(entry, memberSide).ToList()) : null;

    private static decimal Quantity(RowFact row) => row.Values[ReportRole.Quantity].Number ?? 0m;

    private static decimal NetCash(RowFact row) => row.Values[ReportRole.NetCash].Number ?? 0m;

    private static string Signed(decimal difference) => difference.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static ReportCell Check(bool equal, decimal difference) =>
        new(CellValue.Of(equal ? "✓ equal" : "✗ " + Signed(difference)), Tone: equal ? Tone.Ok : Tone.Fail, Bold: true);

    private static IReadOnlyList<ReportBlock> Build(ReportContext context) =>
    [
        .. Groups(context.Facts).Select(group => Card(context, group)),
        DataBlocks.Rows(context)
    ];

    private static ReportBlock Card(ReportContext context, Group group)
    {
        var entry = group.Entry;
        var price = context.IsMapped(ReportRole.Price);
        var netCash = context.IsMapped(ReportRole.NetCash);

        IEnumerable<ReportCell> Line(RowFact row) =>
        [
            ReportCell.Of(row.Row.RowKey),
            ReportCell.Of(row.Values[ReportRole.Book].Text),
            DataBlocks.Cell(row.Values[ReportRole.Quantity], ReportRole.Quantity),
            price ? DataBlocks.Cell(row.Values[ReportRole.Price], ReportRole.Price) : ReportCell.Empty,
            netCash ? DataBlocks.Cell(row.Values[ReportRole.NetCash], ReportRole.NetCash) : ReportCell.Empty
        ];

        var blockRow = new ReportRow(
            [
                new ReportCell(CellValue.Of($"{context.Label("Block")} ({group.BlockSide})"), Bold: true),
                .. Line(group.Block).Select(cell => cell with { Bold = true, Tone = Tone.Link }),
                ReportCell.Empty,
                ReportCell.Empty
            ],
            RowKind.Total);
        var allocationRows = group.Members.Select(member => new ReportRow(
            [
                new ReportCell(CellValue.Of(context.Label("allocation")), Indent: 1),
                .. Line(member).Select(cell => cell with { Tone = Tone.Link }),
                ReportCell.Empty,
                ReportCell.Empty
            ]));

        var quantityGap = group.Members.Sum(Quantity) - Quantity(group.Block);
        var cashTotal = group.Members.Sum(NetCash);
        var cashGap = cashTotal - NetCash(group.Block);
        var totalRow = new ReportRow(
        [
            new ReportCell(CellValue.Of(context.Label("Allocations total")), Bold: true),
            ReportCell.Empty,
            ReportCell.Empty,
            ReportCell.Of(group.Members.Sum(Quantity), NumberFormat.Quantity) with { Bold = true },
            ReportCell.Empty,
            netCash ? ReportCell.Of(cashTotal, NumberFormat.Amount) with { Bold = true } : ReportCell.Empty,
            Check(quantityGap == 0, quantityGap),
            netCash ? Check(Math.Round(cashGap, 2, MidpointRounding.AwayFromZero) == 0, cashGap) : ReportCell.Empty
        ]);

        return new SectionBlock(
            $"{entry.Short}  ·  {entry.Text(ReportRole.Counterparty)}  ·  {entry.Text(ReportRole.Instrument)}  ·  {entry.Text(ReportRole.Side)}  ·  matched by {entry.Entry.CommittedBy}  ·  outcome {entry.Entry.Outcome}",
            null,
            [
                new TableBlock(
                    null,
                    [
                        new ReportColumn(context.Label("Line")),
                        new ReportColumn(context.Label("Row")),
                        new ReportColumn(context.Label("Book")),
                        new ReportColumn(context.Label("Quantity")),
                        new ReportColumn(context.Label("Price")),
                        new ReportColumn(context.Label("Net cash")),
                        new ReportColumn(context.Label("Quantity check")),
                        new ReportColumn(context.Label("Net cash check"))
                    ],
                    [blockRow, .. allocationRows, totalRow])
            ]);
    }
}
