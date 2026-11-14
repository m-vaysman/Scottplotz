using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO4. Counterparty by break category, each cell the number of open items, shaded on a single-hue scale,
// with row and column totals and a check that the grand total equals the number of open items.
public static class BreakHeatmap
{
    public static ReportDefinition Definition { get; } = new("MO4", [ReportRole.Counterparty], _ => null, Build);

    private sealed record Line(string Counterparty, IReadOnlyList<int> Counts)
    {
        public int Total => Counts.Sum();
    }

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var items = context.Facts.OpenItems;
        var categories = Categories.Of(context.Facts);
        var lines = items
            .GroupBy(item => item.Text(ReportRole.Counterparty), StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new Line(group.Key, categories.Select(category => group.Count(item => item.Category == category)).ToList()))
            .ToList();
        var hottest = lines.SelectMany(line => line.Counts).DefaultIfEmpty(0).Max();
        var columnTotals = Enumerable.Range(0, categories.Count).Select(index => lines.Sum(line => line.Counts[index])).ToList();

        static ReportCell Count(int value, bool bold = false) => new(new CellValue.Number(value), NumberFormat.Count, Bold: bold, Align: CellAlign.Center);

        var rows = lines
            .Select(line => new ReportRow(
            [
                new ReportCell(new CellValue.Text(line.Counterparty), Bold: true),
                .. line.Counts.Select(count => Count(count) with { Heat = hottest == 0 ? 0 : (double)count / hottest, HeatColour = HeatColour.Orange }),
                Count(line.Total, bold: true),
                ReportCell.Empty
            ]))
            .Append(new ReportRow(
            [
                ReportCell.Of(context.Label("Total")),
                .. columnTotals.Select(total => Count(total)),
                Count(lines.Sum(line => line.Total)),
                ReportCell.Check(lines.Sum(line => line.Total) == items.Count)
            ],
            RowKind.Total))
            .ToList();

        return
        [
            new TableBlock(
                null,
                [
                    new ReportColumn(context.Label("Counterparty")),
                    .. categories.Select(category => new ReportColumn(context.Label(category), CellAlign.Center, Wrap: true)),
                    new ReportColumn(context.Label("Total"), CellAlign.Center),
                    new ReportColumn(context.Label("Check"), CellAlign.Center)
                ],
                rows,
                "Each cell counts the open items of one counterparty in one category; the check ties the grand total to the number of open items."),
            DataBlocks.OpenItems(context)
        ];
    }
}
