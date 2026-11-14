using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO5. Open items by category, aged in business days from trade date to the as-of date, with their settlement status.
// Two checks tie each category's age buckets and its status columns back to its item count.
public static class Ageing
{
    private static readonly (string Label, Func<int, bool> Holds)[] Ages =
    [
        ("Age 0 or less", age => age <= 0),
        ("Age 1 bus. day", age => age == 1),
        ("Age 2 bus. days", age => age == 2),
        ("Age 3 bus. days", age => age == 3),
        ("Age 4+ bus. days", age => age >= 4)
    ];

    public static ReportDefinition Definition { get; } = new("MO5", [ReportRole.TradeDate, ReportRole.SettleDate], _ => null, Build);

    private sealed record Line(string Category, IReadOnlyList<int> Buckets, int Unaged, IReadOnlyList<int> Statuses, int Items)
    {
        public bool Ties => Buckets.Sum() + Unaged == Items && Statuses.Sum() == Items;
    }

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var asOf = context.AsOf;
        var lines = Categories.Of(context.Facts)
            .Select(category =>
            {
                var items = context.Facts.OpenItems.Where(item => item.Category == category).ToList();
                var ages = items.Select(item => item.Age(asOf)).ToList();
                return new Line(
                    category,
                    Ages.Select(bucket => ages.Count(age => age is { } days && bucket.Holds(days))).ToList(),
                    ages.Count(age => age is null),
                    Calendar.Statuses.Select(status => items.Count(item => item.Status(asOf) == status)).ToList(),
                    items.Count);
            })
            .ToList();
        var hottest = lines.SelectMany(line => line.Buckets).DefaultIfEmpty(0).Max();
        var pastSettle = Calendar.Statuses.ToList().IndexOf(Calendar.PastSettleDate);

        ReportCell Count(int value, bool bold = false) => new(new CellValue.Number(value), NumberFormat.Count, Bold: bold, Align: CellAlign.Center);

        var rows = lines
            .Select(line => new ReportRow(
            [
                new ReportCell(new CellValue.Text(line.Category), Bold: true),
                .. line.Buckets.Select(count => Count(count) with { Heat = hottest == 0 ? 0 : (double)count / hottest }),
                .. line.Statuses.Select((count, index) => Count(count) with { Tone = index == pastSettle && count > 0 ? Tone.Break : Tone.None }),
                Count(line.Items, bold: true),
                ReportCell.Check(line.Ties)
            ]))
            .Append(new ReportRow(
            [
                ReportCell.Of(context.Label("Total")),
                .. Enumerable.Range(0, Ages.Length).Select(index => Count(lines.Sum(line => line.Buckets[index]))),
                .. Enumerable.Range(0, Calendar.Statuses.Count).Select(index => Count(lines.Sum(line => line.Statuses[index]))),
                Count(lines.Sum(line => line.Items)),
                ReportCell.Check(lines.Sum(line => line.Items) == context.Facts.OpenItems.Count)
            ],
            RowKind.Total))
            .ToList();

        return
        [
            new TableBlock(
                null,
                [
                    new ReportColumn(context.Label("Category")),
                    .. Ages.Select(bucket => new ReportColumn(context.Label(bucket.Label), CellAlign.Center, Wrap: true)),
                    .. Calendar.Statuses.Select(status => new ReportColumn(context.Label(status), CellAlign.Center, Wrap: true)),
                    new ReportColumn(context.Label("Items"), CellAlign.Center),
                    new ReportColumn(context.Label("Check"), CellAlign.Center)
                ],
                rows,
                "Age = business days from trade date to the as-of date (Monday to Friday, no holiday calendar). The check ties each row's age buckets and its status columns to its item count."),
            DataBlocks.OpenItems(context)
        ];
    }
}
