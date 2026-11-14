using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO2. KPI tiles for the run, the entries committed by each pass, and a table of match and clean-match rates by counterparty.
public static class Scorecard
{
    public static ReportDefinition Definition { get; } = new("MO2", [ReportRole.Counterparty], _ => null, Build);

    private sealed record Line(
        string Counterparty,
        int InternalRows,
        int CounterpartyRows,
        int Matched,
        int Exact,
        int WithinTolerance,
        int Breaks,
        int UnmatchedOurs,
        int UnmatchedTheirs,
        int Ambiguous,
        int MatchedInternalRows)
    {
        public decimal AutoMatchRate => Rate(MatchedInternalRows, InternalRows);

        public decimal CleanMatchRate => Rate(Exact + WithinTolerance, Matched);
    }

    private static decimal Rate(int part, int whole) => whole == 0 ? 0m : (decimal)part / whole;

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var facts = context.Facts;
        var left = facts.Rows.Where(row => row.Row.Source == SourceSide.Left).ToList();
        var right = facts.Rows.Where(row => row.Row.Source == SourceSide.Right).ToList();
        var lines = facts.Rows
            .Select(row => row.Values[ReportRole.Counterparty].Text)
            .Distinct(StringComparer.Ordinal)
            .Select(name => LineOf(facts, name))
            .OrderByDescending(line => line.AutoMatchRate)
            .ThenBy(line => line.Counterparty, StringComparer.Ordinal)
            .ToList();
        return
        [
            .. Tiles(context, left.Count, right.Count),
            CommitsByPass(context),
            CounterpartyTable(context, lines, left.Count, right.Count),
            DataBlocks.Entries(context),
            DataBlocks.Rows(context)
        ];
    }

    private static Line LineOf(ReportFacts facts, string name)
    {
        var rows = facts.Rows.Where(row => row.Values[ReportRole.Counterparty].Text == name).ToList();
        var entries = facts.Entries.Where(entry => entry.Text(ReportRole.Counterparty) == name).ToList();
        int Rows(SourceSide side, Disposition? only = null, bool leadOnly = false) =>
            rows.Count(row => row.Row.Source == side && (only is null || row.Row.Disposition == only) && (!leadOnly || row.ItemLead));
        int Outcomes(Outcome outcome) => entries.Count(entry => entry.Entry.Outcome == outcome);
        return new Line(
            name,
            Rows(SourceSide.Left),
            Rows(SourceSide.Right),
            entries.Count(entry => entry.IsMatched),
            Outcomes(Outcome.Exact),
            Outcomes(Outcome.WithinTolerance),
            Outcomes(Outcome.Break),
            Rows(SourceSide.Left, Disposition.LeftOnly, leadOnly: true),
            Rows(SourceSide.Right, Disposition.RightOnly, leadOnly: true),
            entries.Count(entry => entry.Entry.Alignment == Alignment.Ambiguous),
            Rows(SourceSide.Left, Disposition.Matched));
    }

    private static IReadOnlyList<ReportBlock> Tiles(ReportContext context, int leftRows, int rightRows)
    {
        var facts = context.Facts;
        var matched = facts.Entries.Count(entry => entry.IsMatched);
        var clean = facts.Entries.Count(entry => entry.Entry.Outcome is Outcome.Exact or Outcome.WithinTolerance);
        ReportTile Count(string label, int value) => new(context.Label(label), ReportCell.Of(value, NumberFormat.Count));
        ReportTile Percent(string label, decimal value) => new(context.Label(label), ReportCell.Of(value, NumberFormat.Percent));
        ReportTile[] tiles =
        [
            Count("Internal rows", leftRows),
            Count("Counterparty rows", rightRows),
            Count("Result entries", facts.Entries.Count),
            Count("Matched entries", matched),
            Percent("Auto-match rate", Rate(facts.Rows.Count(row => row.Row.Source == SourceSide.Left && row.Row.Disposition == Disposition.Matched), leftRows)),
            Percent("Clean-match rate", Rate(clean, matched)),
            Count("Breaks", facts.Entries.Count(entry => entry.Entry.Outcome == Outcome.Break)),
            Count("Unmatched ours", facts.Rows.Count(row => row is { ItemLead: true, Row: { Source: SourceSide.Left, Disposition: Disposition.LeftOnly } })),
            Count("Unmatched theirs", facts.Rows.Count(row => row is { ItemLead: true, Row: { Source: SourceSide.Right, Disposition: Disposition.RightOnly } })),
            Count("Ambiguous", facts.Entries.Count(entry => entry.Entry.Alignment == Alignment.Ambiguous))
        ];
        return [.. tiles.Chunk(5).Select(row => new TilesBlock(row))];
    }

    // Follows the Excel original: every entry carrying the pass as committedBy is counted, open entries included.
    private static TableBlock CommitsByPass(ReportContext context) =>
        new(
            context.Label("Commits by pass"),
            context.Facts.Passes.Select(pass => new ReportColumn(pass, CellAlign.Center)).ToList(),
            context.Facts.Passes.Count == 0
                ? []
                : [ReportRow.Of(context.Facts.Passes.Select(pass =>
                    ReportCell.Of(context.Facts.Entries.Count(entry => entry.Entry.CommittedBy == pass), NumberFormat.Count) with { Align = CellAlign.Center }).ToArray<object?>())],
            "Counts every entry that carries the pass as its committed-by pass. An open entry carries the pass that left it open, so open entries are counted as well as matched ones.",
            "No passes in this run.");

    // Excel colour rules, kept as written: 0.95 and above is green, 0.85 to 0.9499999 is amber, below 0.85 is red; nothing between 0.9499999 and 0.95.
    private static Tone CleanTone(decimal rate) =>
        rate >= 0.95m ? Tone.Ok
        : rate is >= 0.85m and <= 0.9499999m ? Tone.Tolerance
        : rate < 0.85m ? Tone.Break
        : Tone.None;

    private static TableBlock CounterpartyTable(ReportContext context, IReadOnlyList<Line> lines, int leftRows, int rightRows)
    {
        var facts = context.Facts;
        var matched = facts.Entries.Count(entry => entry.IsMatched);
        ReportCell Count(int value) => ReportCell.Of(value, NumberFormat.Count);
        int Sum(Func<Line, int> pick) => lines.Sum(pick);

        var rows = lines
            .Select(line => new ReportRow(
            [
                new ReportCell(new CellValue.Text(line.Counterparty), Bold: true),
                Count(line.InternalRows),
                Count(line.CounterpartyRows),
                Count(line.Matched),
                Count(line.Exact),
                Count(line.WithinTolerance),
                Count(line.Breaks),
                Count(line.UnmatchedOurs),
                Count(line.UnmatchedTheirs),
                Count(line.Ambiguous),
                ReportCell.Of(line.AutoMatchRate, NumberFormat.Percent) with { Bar = (double)line.AutoMatchRate },
                ReportCell.Of(line.CleanMatchRate, NumberFormat.Percent) with { Tone = CleanTone(line.CleanMatchRate) }
            ]))
            .Append(new ReportRow(
            [
                ReportCell.Of(context.Label("Total")),
                Count(Sum(line => line.InternalRows)),
                Count(Sum(line => line.CounterpartyRows)),
                Count(Sum(line => line.Matched)),
                Count(Sum(line => line.Exact)),
                Count(Sum(line => line.WithinTolerance)),
                Count(Sum(line => line.Breaks)),
                Count(Sum(line => line.UnmatchedOurs)),
                Count(Sum(line => line.UnmatchedTheirs)),
                Count(Sum(line => line.Ambiguous)),
                ReportCell.Check(Sum(line => line.InternalRows) == leftRows && Sum(line => line.CounterpartyRows) == rightRows && Sum(line => line.Matched) == matched),
                ReportCell.Empty
            ],
            RowKind.Total))
            .ToList();

        return new TableBlock(
            null,
            [
                new ReportColumn(context.Label("Counterparty")),
                .. new[] { "Internal rows", "Counterparty rows", "Matched entries", "Exact", "Within tolerance", "Breaks", "Unmatched ours", "Unmatched theirs", "Ambiguous", "Auto-match rate", "Clean-match rate" }
                    .Select(title => new ReportColumn(context.Label(title), CellAlign.Right, Wrap: true))
            ],
            rows,
            "Auto-match rate = internal rows in matched entries ÷ internal rows. Clean-match rate = (exact + within tolerance) ÷ matched entries. Unmatched counts are items from the input rows, so rows the engine left without an entry (not routed, or excluded as invalid) are included; a package counts once. Entry counts use the counterparty of the entry's projection. The check in the total row ties the internal rows, counterparty rows and matched entries to the whole run.");
    }
}
