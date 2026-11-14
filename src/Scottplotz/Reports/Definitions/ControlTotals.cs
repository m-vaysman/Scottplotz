using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO1. Row counts by counterparty and asset, the engine's quantity side-by-side by currency with each difference
// decomposed and tied back to the file totals, and the matched pairs whose two sides carry different currencies.
public static class ControlTotals
{
    private static readonly Disposition[] NotInTotals = [Disposition.Suppressed, Disposition.Quarantined, Disposition.SearchIncomplete];
    private static readonly Disposition[] Counted = [Disposition.Matched, Disposition.LeftOnly, Disposition.RightOnly];

    public static ReportDefinition Definition { get; } = new(
        "MO1",
        [ReportRole.Counterparty, ReportRole.Asset, ReportRole.Currency, ReportRole.Quantity],
        _ => null,
        Build);

    private sealed record Side(int Rows, int Matched, int Unmatched, int Ambiguous, int Other)
    {
        public static Side Of(IEnumerable<RowFact> rows, SourceSide side, Disposition unmatched)
        {
            var mine = rows.Where(row => row.Row.Source == side).ToList();
            int Count(Disposition disposition) => mine.Count(row => row.Row.Disposition == disposition);
            return new Side(mine.Count, Count(Disposition.Matched), Count(unmatched), Count(Disposition.Ambiguous),
                mine.Count - Count(Disposition.Matched) - Count(unmatched) - Count(Disposition.Ambiguous));
        }

        public static Side Sum(IEnumerable<Side> sides) =>
            sides.Aggregate(new Side(0, 0, 0, 0, 0), (total, side) =>
                new Side(total.Rows + side.Rows, total.Matched + side.Matched, total.Unmatched + side.Unmatched, total.Ambiguous + side.Ambiguous, total.Other + side.Other));

        public bool Ties(bool others) => Rows == Matched + Unmatched + Ambiguous + (others ? Other : 0);
    }

    private sealed record PairCounts(string Counterparty, string Asset, Side Ours, Side Theirs);

    private sealed record Figures(
        decimal Ours,
        decimal Theirs,
        decimal UnmatchedOurs,
        decimal UnmatchedTheirs,
        decimal InsideMatched,
        decimal CurrencySplit,
        decimal AmbiguousOurs,
        decimal AmbiguousTheirs,
        IReadOnlyList<decimal> AddBacks,
        decimal FileOurs,
        decimal FileTheirs)
    {
        public decimal Delta => Theirs - Ours;

        public decimal FieldDifferences => InsideMatched - CurrencySplit;

        public bool Balanced => Delta == UnmatchedOurs + UnmatchedTheirs + InsideMatched;

        // AddBacks alternate ours, theirs.
        public bool TiesToFiles =>
            FileOurs == Ours + AmbiguousOurs + AddBacks.Where((_, index) => index % 2 == 0).Sum()
            && FileTheirs == Theirs + AmbiguousTheirs + AddBacks.Where((_, index) => index % 2 == 1).Sum();

        public static Figures Sum(IReadOnlyList<Figures> lines, int addBackCount) =>
            new(
                lines.Sum(line => line.Ours),
                lines.Sum(line => line.Theirs),
                lines.Sum(line => line.UnmatchedOurs),
                lines.Sum(line => line.UnmatchedTheirs),
                lines.Sum(line => line.InsideMatched),
                lines.Sum(line => line.CurrencySplit),
                lines.Sum(line => line.AmbiguousOurs),
                lines.Sum(line => line.AmbiguousTheirs),
                Enumerable.Range(0, addBackCount).Select(index => lines.Sum(line => line.AddBacks[index])).ToList(),
                lines.Sum(line => line.FileOurs),
                lines.Sum(line => line.FileTheirs));
    }

    private sealed record AddBack(string Ours, string Theirs, Func<RowFact, bool> Holds);

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var facts = context.Facts;
        var others = facts.Rows.Any(row => NotInTotals.Contains(row.Row.Disposition));
        var packages = facts.Rows.Any(row => row is { ItemLead: false, Row.Disposition: Disposition.Matched or Disposition.LeftOnly or Disposition.RightOnly });
        var addBacks = AddBacks(others, packages);
        return
        [
            CountsTable(context, others),
            QuantityTable(context, addBacks),
            CurrencySplitTable(context),
            .. Notes(others, packages),
            DataBlocks.Rows(context),
            SideBySideData(context)
        ];
    }

    private static IReadOnlyList<AddBack> AddBacks(bool others, bool packages) =>
    [
        .. others
            ? [new AddBack(
                "Suppressed, quarantined, search incomplete ours (not in totals)",
                "Suppressed, quarantined, search incomplete theirs (not in totals)",
                row => NotInTotals.Contains(row.Row.Disposition))]
            : Array.Empty<AddBack>(),
        .. packages
            ? [new AddBack(
                "Package legs merged into one item ours (not in totals)",
                "Package legs merged into one item theirs (not in totals)",
                row => !row.ItemLead && Counted.Contains(row.Row.Disposition))]
            : Array.Empty<AddBack>()
    ];

    private static ReportCell Count(int value, bool bold = false) => ReportCell.Of(value, NumberFormat.Count) with { Bold = bold };

    private static ReportCell Quantity(decimal value, Tone tone = Tone.None, bool bold = false) => ReportCell.Of(value, NumberFormat.Quantity) with { Tone = tone, Bold = bold };

    private static ReportColumn Heading(ReportContext context, string title) => new(context.Label(title), CellAlign.Right, Wrap: true);

    private static TableBlock CountsTable(ReportContext context, bool others)
    {
        var pairs = context.Facts.Rows
            .GroupBy(row => (Counterparty: row.Values[ReportRole.Counterparty].Text, Asset: row.Values[ReportRole.Asset].Text))
            .Select(group => new PairCounts(
                group.Key.Counterparty,
                group.Key.Asset,
                Side.Of(group, SourceSide.Left, Disposition.LeftOnly),
                Side.Of(group, SourceSide.Right, Disposition.RightOnly)))
            .OrderBy(pair => pair.Counterparty, StringComparer.Ordinal)
            .ThenBy(pair => pair.Asset, StringComparer.Ordinal)
            .ToList();
        var ours = Side.Sum(pairs.Select(pair => pair.Ours));
        var theirs = Side.Sum(pairs.Select(pair => pair.Theirs));
        var leftRows = context.Facts.Rows.Count(row => row.Row.Source == SourceSide.Left);
        var rightRows = context.Facts.Rows.Count(row => row.Row.Source == SourceSide.Right);

        IEnumerable<ReportCell> Figures(Side left, Side right) =>
        [
            Count(left.Rows), Count(right.Rows),
            Count(left.Matched), Count(right.Matched),
            Count(left.Unmatched), Count(right.Unmatched),
            Count(left.Ambiguous), Count(right.Ambiguous),
            .. others ? [Count(left.Other), Count(right.Other)] : Array.Empty<ReportCell>()
        ];

        var rows = pairs
            .Select(pair => new ReportRow(
            [
                new ReportCell(new CellValue.Text(pair.Counterparty), Bold: true),
                ReportCell.Of(pair.Asset),
                .. Figures(pair.Ours, pair.Theirs),
                ReportCell.Check(pair.Ours.Ties(others) && pair.Theirs.Ties(others))
            ]))
            .Append(new ReportRow(
            [
                ReportCell.Of(context.Label("Total")),
                ReportCell.Empty,
                .. Figures(ours, theirs),
                ReportCell.Check(ours.Rows == leftRows && theirs.Rows == rightRows && ours.Ties(others) && theirs.Ties(others))
            ],
            RowKind.Total))
            .ToList();

        return new TableBlock(
            context.Label("Part 1 · Row counts by counterparty and asset"),
            [
                new ReportColumn(context.Label("Counterparty")),
                new ReportColumn(context.Label("Asset")),
                .. new[] { "Rows ours", "Rows theirs", "Matched ours", "Matched theirs", "Unmatched ours", "Unmatched theirs", "Ambiguous ours", "Ambiguous theirs" }.Select(title => Heading(context, title)),
                .. others
                    ? new[] { "Other ours (suppressed, quarantined, search incomplete)", "Other theirs" }.Select(title => Heading(context, title))
                    : [],
                Heading(context, "Check")
            ],
            rows,
            "Counts are input rows, not consolidated items. The check ties each row's matched, unmatched and ambiguous counts (and the other count, when shown) to its row count; the total row also ties to the number of rows in each file.");
    }

    private static IReadOnlyList<string> Currencies(ReportContext context) =>
        context.SortCurrencies(
            context.Facts.Rows.Where(row => row.Row.Disposition != Disposition.Suppressed).Select(row => row.Values[ReportRole.Currency].Text).Where(currency => currency.Length > 0));

    private static decimal QuantityOf(IEnumerable<RowFact> rows) => rows.Sum(row => row.Values[ReportRole.Quantity].Number ?? 0m);

    private static IEnumerable<RowFact> Keyed(ReportContext context, SideBySideLine line, string currency) =>
        context.Facts.Rows.Where(row =>
            row.Values[ReportRole.Counterparty].Text == line.Row
            && row.Values[ReportRole.Asset].Text == line.Column
            && row.Values[ReportRole.Currency].Text == currency);

    private static Figures Measure(ReportContext context, IReadOnlyList<AddBack> addBacks, string currency, SideBySideLine line)
    {
        var keyed = Keyed(context, line, currency).ToList();
        decimal Of(SourceSide side, Func<RowFact, bool> holds) => QuantityOf(keyed.Where(row => row.Row.Source == side && holds(row)));
        decimal Split(SourceSide side) => Of(side, row => row.Row.Disposition == Disposition.Matched && row.PairCurrencySplit);
        return new Figures(
            line.Left,
            line.Right,
            line.LeftOnly,
            line.RightOnly,
            line.MatchedDelta,
            Split(SourceSide.Right) - Split(SourceSide.Left),
            Of(SourceSide.Left, row => row.Row.Disposition == Disposition.Ambiguous),
            Of(SourceSide.Right, row => row.Row.Disposition == Disposition.Ambiguous),
            addBacks.SelectMany(addBack => new[] { Of(SourceSide.Left, addBack.Holds), Of(SourceSide.Right, addBack.Holds) }).ToList(),
            Of(SourceSide.Left, _ => true),
            Of(SourceSide.Right, _ => true));
    }

    private static IReadOnlyList<ReportCell> QuantityCells(Figures figures, bool balanced, bool ties) =>
    [
        Quantity(figures.Ours),
        Quantity(figures.Theirs),
        Quantity(figures.Delta, figures.Delta != 0 ? Tone.Break : Tone.None, bold: true),
        Quantity(figures.UnmatchedOurs),
        Quantity(figures.UnmatchedTheirs),
        Quantity(figures.InsideMatched),
        ReportCell.Check(balanced),
        Quantity(figures.CurrencySplit, figures.CurrencySplit != 0 ? Tone.Tolerance : Tone.None),
        Quantity(figures.FieldDifferences),
        Quantity(figures.AmbiguousOurs),
        Quantity(figures.AmbiguousTheirs),
        .. figures.AddBacks.Select(value => Quantity(value)),
        Quantity(figures.FileOurs),
        Quantity(figures.FileTheirs),
        ReportCell.Check(ties)
    ];

    private static TableBlock QuantityTable(ReportContext context, IReadOnlyList<AddBack> addBacks)
    {
        var facts = context.Facts;
        var currencies = Currencies(context);
        var columns = 3 + 8 + 3 + 2 * addBacks.Count + 3;

        IEnumerable<ReportRow> Block(string currency)
        {
            var lines = SideBySide.Lines(facts, currency)
                .Select(line => (Line: line, Figures: Measure(context, addBacks, currency, line)))
                .ToList();
            var subtotal = Figures.Sum(lines.Select(item => item.Figures).ToList(), 2 * addBacks.Count);
            return
            [
                new ReportRow([new ReportCell(new CellValue.Text($"{currency}  ·  totals in {currency} only"), Span: columns)], RowKind.Band),
                .. lines.Select(item => new ReportRow(
                [
                    ReportCell.Of(currency),
                    new ReportCell(new CellValue.Text(item.Line.Row), Bold: true),
                    ReportCell.Of(item.Line.Column),
                    .. QuantityCells(item.Figures, item.Figures.Balanced && item.Figures.Delta == item.Line.Delta, item.Figures.TiesToFiles)
                ])),
                new ReportRow(
                [
                    ReportCell.Of(currency),
                    ReportCell.Of($"{context.Label("Subtotal")} {currency}"),
                    ReportCell.Empty,
                    .. QuantityCells(subtotal, subtotal.Balanced, subtotal.TiesToFiles)
                ],
                RowKind.Total)
            ];
        }

        return new TableBlock(
            context.Label("Part 2 · Quantity side-by-side by currency (engine side-by-side view), with the difference decomposed and tied back to the files"),
            [
                .. new[] { "Currency", "Counterparty", "Asset" }.Select(title => new ReportColumn(context.Label(title))),
                .. new[] { "Ours", "Theirs", "Δ (theirs − ours)", "Unmatched ours", "Unmatched theirs", "Inside matched", "Check",
                    "Inside matched: currency split", "Inside matched: field differences", "Ambiguous ours (not in totals)", "Ambiguous theirs (not in totals)" }
                    .Select(title => Heading(context, title)),
                .. addBacks.SelectMany(addBack => new[] { addBack.Ours, addBack.Theirs }).Select(title => Heading(context, title)),
                .. new[] { "File total ours", "File total theirs", "Tie-out" }.Select(title => Heading(context, title))
            ],
            currencies.SelectMany(Block).ToList(),
            "Quantities are summed within a currency only. Units differ by asset (face for bonds, shares for equity, notional for FX and swaps), so compare a line across its own row, not down the column.");
    }

    private static TableBlock CurrencySplitTable(ReportContext context)
    {
        var facts = context.Facts;
        var rows = facts.Entries
            .Where(entry => entry.CurrencySplit)
            .Select(entry =>
            {
                var ours = facts.Members(entry, SourceSide.Left).FirstOrDefault();
                var theirs = facts.Members(entry, SourceSide.Right).FirstOrDefault();
                return new ReportRow(
                [
                    ReportCell.Of(entry.Short),
                    ReportCell.Of(entry.Keys(SourceSide.Left)),
                    ReportCell.Of(entry.Keys(SourceSide.Right)),
                    ReportCell.Of(ours?.Values[ReportRole.Counterparty].Text),
                    ReportCell.Of(ours?.Values[ReportRole.Asset].Text),
                    ReportCell.Of(string.Join(", ", entry.OursCurrencies)) with { Tone = Tone.Break },
                    ReportCell.Of(string.Join(", ", entry.TheirsCurrencies)) with { Tone = Tone.Break },
                    ReportCell.Of(ours?.Values[ReportRole.Quantity].Number, NumberFormat.Quantity),
                    ReportCell.Of(theirs?.Values[ReportRole.Quantity].Number, NumberFormat.Quantity),
                    ReportCell.Of(entry.Entry.CommittedBy),
                    ReportCell.Of(entry.Entry.Outcome.ToString()),
                    ReportCell.Of(string.Join(", ", entry.Entry.Diffs.Select(diff => diff.Field)))
                ]);
            })
            .ToList();
        return new TableBlock(
            context.Label("Part 3 · Matched pairs whose two sides carry different currencies (their halves sit in different currency blocks above)"),
            Headers(context),
            rows);
    }

    private static IReadOnlyList<ReportColumn> Headers(ReportContext context) =>
        new[] { "Entry", "Ours rows", "Theirs rows", "Counterparty", "Asset", "Ours currency", "Theirs currency", "Quantity ours", "Quantity theirs", "Matched by", "Outcome", "Break fields" }
            .Select(title => new ReportColumn(context.Label(title)))
            .ToList();

    private static IReadOnlyList<ReportBlock> Notes(bool others, bool packages) =>
    [
        new NoteBlock("Reading the engine view:"),
        new NoteBlock("• Each row is placed in the currency block of its own currency. A matched pair booked in one currency but reported in another puts our quantity in one block and theirs in the other, so both blocks show an \"inside matched\" amount that is really one currency break. The \"Inside matched: currency split\" column separates that from genuine field differences."),
        new NoteBlock((others, packages) switch
        {
            (false, false) => "• The engine leaves Ambiguous rows out of its side-by-side view, so its Ours and Theirs do not equal the file totals. The columns from \"Ambiguous ours (not in totals)\" to \"File total theirs\" add them back and tie each line to the files.",
            _ => "• The engine leaves Ambiguous, SearchIncomplete, Quarantined and Suppressed rows out of its side-by-side view and counts a package once, not once per leg, so its Ours and Theirs do not equal the file totals. The columns from \"Ambiguous ours (not in totals)\" to \"File total theirs\" add them back and tie each line to the files."
        })
    ];

    private static DataBlock SideBySideData(ReportContext context)
    {
        var currencies = Currencies(context);
        return new DataBlock(
            "Engine side-by-side",
            "Quantity by counterparty and asset, one block per currency. Left only is negated; Delta = Right − Left = Left only + Right only + Matched delta. Ambiguous, search incomplete, quarantined and suppressed rows are not counted.",
            new TableBlock(
                null,
                new[] { "Currency (filter)", "Row: counterparty", "Column: asset", "Left", "Right", "Delta", "LeftOnly", "RightOnly", "MatchedDelta" }.Select(title => new ReportColumn(title)).ToList(),
                currencies
                    .SelectMany(currency => SideBySide.Lines(context.Facts, currency).Select(line => ReportRow.Of(
                        currency,
                        line.Row,
                        line.Column,
                        ReportCell.Of(line.Left, NumberFormat.Quantity),
                        ReportCell.Of(line.Right, NumberFormat.Quantity),
                        ReportCell.Of(line.Delta, NumberFormat.Quantity),
                        ReportCell.Of(line.LeftOnly, NumberFormat.Quantity),
                        ReportCell.Of(line.RightOnly, NumberFormat.Quantity),
                        ReportCell.Of(line.MatchedDelta, NumberFormat.Quantity))))
                    .ToList()));
    }
}
