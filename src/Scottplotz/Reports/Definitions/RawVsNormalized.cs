using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// BO3. One card per one-to-one matched pair: each side's original columns as received, the canonical values on both sides,
// the normalization steps that changed a value, and the engine's differences. The only report that shows original columns.
public static class RawVsNormalized
{
    private static readonly ReportRole[] Canonical =
    [
        ReportRole.Counterparty, ReportRole.Instrument, ReportRole.Asset, ReportRole.Side, ReportRole.TradeDate, ReportRole.SettleDate,
        ReportRole.Currency, ReportRole.Quantity, ReportRole.Price, ReportRole.Accrued, ReportRole.Fees, ReportRole.NetCash
    ];

    public static ReportDefinition Definition { get; } = new("BO3", [], Unavailable, Build);

    private sealed record Step(string Side, TraceStep Trace);

    private static string? Unavailable(ReportContext context) =>
        context.Options.RawColumns is null
            ? "Needs options.rawColumns: the report cannot list each side's original columns without them."
            : Cards(context).Count == 0 ? "No one-to-one matched entries to show." : null;

    private static IReadOnlyList<EntryFact> Cards(ReportContext context)
    {
        var oneToOne = context.Facts.Entries.Where(entry => entry is { IsMatched: true, Entry: { Left.Count: 1, Right.Count: 1 } });
        return context.Options.SelectedEntries is { } selected
            ? oneToOne.Where(entry => selected.Any(prefix => entry.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToList()
            : oneToOne
                .Where(entry => entry.Entry.Outcome != Outcome.Exact || Changed(context, entry).Count > 0)
                .OrderBy(entry => entry.Entry.Outcome switch { Outcome.Break => 0, Outcome.WithinTolerance => 1, _ => 2 })
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .Take(context.Options.MaxCards)
                .ToList();
    }

    private static RowFact? Member(ReportContext context, EntryFact entry, SourceSide side) =>
        context.Facts.Members(entry, side).FirstOrDefault();

    // A step counts only when it has both values and the normalized one differs from the raw one; ours first, then theirs.
    private static IReadOnlyList<Step> Changed(ReportContext context, EntryFact entry) =>
        new[] { ("Ours", SourceSide.Left), ("Theirs", SourceSide.Right) }
            .SelectMany(pair => (Member(context, entry, pair.Item2)?.Row.Trace ?? [])
                .Where(step => step is { Raw: { } raw, Canonical: { } canonical } && raw != canonical)
                .Select(step => new Step(pair.Item1, step)))
            .ToList();

    private static IReadOnlyList<string> RawColumns(ReportContext context, SourceSide side, IReadOnlyList<EntryFact> cards)
    {
        var declared = side == SourceSide.Left ? context.Options.RawColumns!.Left : context.Options.RawColumns!.Right;
        var rows = cards.Select(entry => Member(context, entry, side)).OfType<RowFact>().ToList();
        return declared.Distinct(StringComparer.Ordinal).Where(column => rows.Any(row => RawValue(row, column) is not null)).ToList();
    }

    private static string? RawValue(RowFact row, string column) =>
        row.Row.Raw is { } raw && raw.TryGetValue(column, out var value) ? value : null;

    private static DiffState? StateOf(IEnumerable<DiffFact> diffs, string field) =>
        diffs.Where(diff => diff.Field == field).Select(diff => (DiffState?)diff.State).FirstOrDefault();

    private static Tone ToneOf(DiffState? state) =>
        state switch { DiffState.Break => Tone.Break, DiffState.Tolerance => Tone.Tolerance, DiffState.Attributed => Tone.Attributed, _ => Tone.None };

    private static NumberFormat DeltaFormat(DiffFact diff) =>
        diff.Diff.DeltaKind == DeltaKind.Days || DataBlocks.FormatFor(diff.Role) == NumberFormat.Date ? NumberFormat.Integer : DataBlocks.FormatFor(diff.Role);

    private static string Why(EntryFact entry, IReadOnlyList<DiffFact> diffs, int steps) =>
        entry.Entry.Outcome switch
        {
            Outcome.Break => $"break on {string.Join(", ", entry.RootFields)}",
            Outcome.WithinTolerance => $"within tolerance on {string.Join(", ", diffs.Where(diff => diff.State == DiffState.Tolerance).Select(diff => diff.Field))}",
            _ => $"exact after {steps} normalization step{(steps == 1 ? "" : "s")}"
        };

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var cards = Cards(context);
        var left = RawColumns(context, SourceSide.Left, cards);
        var right = RawColumns(context, SourceSide.Right, cards);
        var roles = Canonical.Where(context.IsMapped).ToList();
        return
        [
            .. cards.Select(entry => Card(context, entry, left, right, roles)),
            DataBlocks.Entries(context),
            DataBlocks.Diffs(context)
        ];
    }

    private static TableBlock AsReceived(ReportContext context, string title, IReadOnlyList<string> columns, RowFact? row) =>
        new(
            null,
            [new ReportColumn(context.Label(title)), .. columns.Select(column => new ReportColumn(column))],
            [
                new ReportRow(
                [
                    new ReportCell(new CellValue.Text(context.Label("raw")), Bold: true),
                    .. columns.Select(column => ReportCell.Of(row is null ? null : RawValue(row, column)) with { Tone = Tone.Muted })
                ],
                RowKind.SubHeader)
            ]);

    private static SectionBlock Card(ReportContext context, EntryFact entry, IReadOnlyList<string> left, IReadOnlyList<string> right, IReadOnlyList<ReportRole> roles)
    {
        var diffs = context.Facts.DiffsByEntry[entry.Id].ToList();
        var steps = Changed(context, entry);
        var banner = $"{entry.Short}  ·  {Why(entry, diffs, steps.Count)}  ·  matched by {entry.Entry.CommittedBy}  ·  outcome {entry.Entry.Outcome}  ·  ours {entry.Entry.Left[0]} / theirs {entry.Entry.Right[0]}";

        var canonical = new TableBlock(
            null,
            [new ReportColumn(context.Label("Canonical")), .. roles.Select(role => new ReportColumn(context.Label(context.Field(role)!), Roles.Kind(role) == FieldKind.Decimal ? CellAlign.Right : CellAlign.Auto))],
            [
                new ReportRow([new ReportCell(new CellValue.Text(context.Label("Ours, normalized")), Bold: true), .. roles.Select(role => DataBlocks.Cell(entry.Ours[role], role))]),
                new ReportRow(
                [
                    new ReportCell(new CellValue.Text(context.Label("Theirs, normalized")), Bold: true),
                    .. roles.Select(role => DataBlocks.Cell(entry.Theirs[role], role) with { Tone = ToneOf(StateOf(diffs, context.Field(role)!)) })
                ])
            ]);

        var stepsTable = new TableBlock(
            context.Label("Normalization steps that changed a value"),
            [new ReportColumn("Side"), new ReportColumn("Rule"), new ReportColumn("Field"), new ReportColumn("Raw to canonical")],
            steps.Select(step => ReportRow.Of(step.Side, step.Trace.RuleId, step.Trace.Field, $"{step.Trace.Raw}  →  {step.Trace.Canonical}")).ToList(),
            Empty: "None: normalization changed no value.");

        var diffTable = new TableBlock(
            context.Label("Differences"),
            new[] { "Field", "Ours", "Theirs", "Delta", "Outcome", "Attributed to" }
                .Select(title => new ReportColumn(context.Label(title), title is "Ours" or "Theirs" or "Delta" ? CellAlign.Right : CellAlign.Auto))
                .ToList(),
            diffs.Count == 0
                ? [ReportRow.Of(ReportCell.Of(context.Label("None: exact after normalization")) with { Tone = Tone.Ok, Span = 6 })]
                : diffs.Select(diff => ReportRow.Of(
                    diff.Field,
                    DataBlocks.Cell(diff.Ours, diff.Role),
                    DataBlocks.Cell(diff.Theirs, diff.Role),
                    ReportCell.Of(diff.Delta, DeltaFormat(diff)),
                    ReportCell.Of(diff.Diff.Outcome.ToString()) with { Tone = ToneOf(diff.State) },
                    string.Join(", ", diff.Diff.AttributedTo))).ToList());

        return new SectionBlock(
            banner,
            null,
            [
                AsReceived(context, "Ours, as received", left, Member(context, entry, SourceSide.Left)),
                AsReceived(context, "Theirs, as received", right, Member(context, entry, SourceSide.Right)),
                canonical,
                stepsTable,
                diffTable
            ]);
    }
}
