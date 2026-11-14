using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// BO6. Expected cash by settle date and currency, signed by side (receive +, pay -), ours against theirs,
// with the difference split into unmatched ours, unmatched theirs, inside matched entries and unresolved rows. Never added across currencies.
public static class CashLadder
{
    private static readonly string[] LadderHeaders =
    [
        "Settle date", "Ccy", "Items (ours)", "Ours: expected cash", "Theirs: expected cash", "Difference",
        "Unmatched ours", "Unmatched theirs", "Inside matched entries", "Unresolved (ambiguous, search incomplete)", "Check"
    ];

    public static ReportDefinition Definition { get; } =
        new("BO6", [ReportRole.NetCash, ReportRole.Side, ReportRole.SettleDate, ReportRole.Currency], _ => null, Build);

    private sealed record Figures(int Items, decimal Ours, decimal Theirs, decimal UnmatchedOurs, decimal UnmatchedTheirs, decimal Inside, decimal Unresolved)
    {
        public static Figures Zero { get; } = new(0, 0, 0, 0, 0, 0, 0);

        public decimal Difference => Theirs - Ours;

        public bool Ties => Math.Round(Difference - (UnmatchedOurs + UnmatchedTheirs + Inside + Unresolved), 2, MidpointRounding.AwayFromZero) == 0;

        public bool InBreak => Math.Round(Difference, 2, MidpointRounding.AwayFromZero) != 0;

        public static Figures operator +(Figures a, Figures b) =>
            new(a.Items + b.Items, a.Ours + b.Ours, a.Theirs + b.Theirs, a.UnmatchedOurs + b.UnmatchedOurs, a.UnmatchedTheirs + b.UnmatchedTheirs, a.Inside + b.Inside, a.Unresolved + b.Unresolved);
    }

    private sealed record Line(DateOnly Settle, string Currency, Figures Figures);

    private static bool InBase(RowFact row) =>
        row is { ItemLead: true, Row.Disposition: not (Disposition.Suppressed or Disposition.Quarantined) };

    private static Figures Measure(IReadOnlyCollection<RowFact> rows, IReadOnlyList<string> payingSides)
    {
        decimal Cash(SourceSide side, params Disposition[] dispositions) =>
            rows.Where(row => row.Row.Source == side && (dispositions.Length == 0 || dispositions.Contains(row.Row.Disposition))).Sum(row => row.SignedCash(payingSides));

        return new Figures(
            rows.Count(row => row.Row.Source == SourceSide.Left),
            Cash(SourceSide.Left),
            Cash(SourceSide.Right),
            -Cash(SourceSide.Left, Disposition.LeftOnly),
            Cash(SourceSide.Right, Disposition.RightOnly),
            Cash(SourceSide.Right, Disposition.Matched) - Cash(SourceSide.Left, Disposition.Matched),
            Cash(SourceSide.Right, Disposition.Ambiguous, Disposition.SearchIncomplete) - Cash(SourceSide.Left, Disposition.Ambiguous, Disposition.SearchIncomplete));
    }

    private static IReadOnlyList<Line> Lines(ReportContext context)
    {
        var order = context.Options.CurrencyOrder;
        return context.Facts.Rows
            .Where(InBase)
            .Select(row => (Settle: row.Values[ReportRole.SettleDate].Date, Currency: row.Values[ReportRole.Currency].Text))
            .Where(key => key.Settle is not null && key.Currency.Length > 0)
            .Select(key => (Settle: key.Settle!.Value, key.Currency))
            .Distinct()
            .OrderBy(key => key.Settle)
            .ThenBy(key => Ordering.CurrencyRank(key.Currency, order))
            .ThenBy(key => key.Currency, StringComparer.Ordinal)
            .Select(key => new Line(
                key.Settle,
                key.Currency,
                Measure(
                    context.Facts.Rows.Where(row => InBase(row) && row.Values[ReportRole.SettleDate].Date == key.Settle && row.Values[ReportRole.Currency].Text == key.Currency).ToList(),
                    context.Options.PayingSides)))
            .ToList();
    }

    private static ReportCell Amount(decimal value, bool bold = false) => ReportCell.Of(value, NumberFormat.Amount) with { Bold = bold };

    private static IReadOnlyList<ReportCell> FigureCells(Figures figures, bool flagDifference) =>
    [
        ReportCell.Of(figures.Items, NumberFormat.Count),
        Amount(figures.Ours),
        Amount(figures.Theirs),
        Amount(figures.Difference, bold: true) with { Tone = flagDifference && figures.InBreak ? Tone.Alert : Tone.None },
        Amount(figures.UnmatchedOurs),
        Amount(figures.UnmatchedTheirs),
        Amount(figures.Inside),
        Amount(figures.Unresolved),
        ReportCell.Check(figures.Ties)
    ];

    private static IReadOnlyList<ReportColumn> Columns(ReportContext context, IEnumerable<string> titles) =>
        titles.Select((title, index) => new ReportColumn(context.Label(title), index < 2 ? CellAlign.Auto : title == "Check" ? CellAlign.Center : CellAlign.Right, Wrap: index >= 2)).ToList();

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var lines = Lines(context);
        var asOf = context.AsOf;
        var currencies = context.SortCurrencies(lines.Select(line => line.Currency));

        var ladder = lines.Select(line => new ReportRow(
        [
            ReportCell.Of(line.Settle, NumberFormat.Date) with { Tone = line.Settle < asOf ? Tone.Shade : Tone.None },
            ReportCell.Of(line.Currency),
            .. FigureCells(line.Figures, flagDifference: true)
        ])).ToList();

        var totals = currencies.Select(currency => new ReportRow(
        [
            ReportCell.Of(currency) with { Bold = true },
            ReportCell.Empty,
            .. FigureCells(lines.Where(line => line.Currency == currency).Aggregate(Figures.Zero, (sum, line) => sum + line.Figures), flagDifference: false)
        ])).ToList();

        var splits = context.Facts.Entries
            .Where(entry => entry.CurrencySplit)
            .Select(entry => new ReportRow(
            [
                ReportCell.Of(entry.Short),
                ReportCell.Of(entry.Keys(SourceSide.Left)),
                ReportCell.Of(entry.Keys(SourceSide.Right)),
                ReportCell.Of(string.Join(", ", entry.OursCurrencies)) with { Tone = Tone.Break },
                ReportCell.Of(string.Join(", ", entry.TheirsCurrencies)) with { Tone = Tone.Break },
                ReportCell.Of(entry.Entry.Outcome.ToString())
            ])).ToList();

        return
        [
            new NoteBlock("Sign convention: receive +, pay −. Difference = theirs − ours = unmatched ours + unmatched theirs + differences inside matched entries + unresolved rows."),
            new TableBlock(null, Columns(context, LadderHeaders), ladder, Empty: "No settle dates in this run."),
            new TableBlock(
                context.Label("Totals by currency (never added across currencies)"),
                Columns(context, ["Ccy", "", .. LadderHeaders.Skip(2)]),
                totals,
                Empty: "No settle dates in this run."),
            new TableBlock(
                context.Label("Matched pairs whose two sides carry different currencies (each side's cash sits in its own currency above)"),
                new[] { "Entry", "Ours rows", "Theirs rows", "Ours currency", "Theirs currency", "Outcome" }.Select(title => new ReportColumn(context.Label(title))).ToList(),
                splits,
                Empty: "None in this run."),
            DataBlocks.Rows(context)
        ];
    }
}
