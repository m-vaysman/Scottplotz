using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO3. How the engine reached its results, separately for ours and theirs: rows read, lifecycle collapse, quarantine,
// items after packages, the items each pass committed in pass order, unresolved items and the residue, with a bar chart of what remains.
public static class CascadeFunnel
{
    public static ReportDefinition Definition { get; } = new("MO3", [], _ => null, Build);

    // Change is the stage's own movement; Remaining, when given, is counted directly and the change is derived from it.
    private sealed record Stage(string Name, string Note, Func<SourceSide, int>? Change, Func<SourceSide, int>? Remaining, bool Residue = false);

    private sealed record Position(int? Change, int Remaining);

    private static IReadOnlyList<Stage> Stages(ReportContext context)
    {
        var rows = context.Facts.Rows;
        int Count(SourceSide side, Func<RowFact, bool> where) => rows.Count(row => row.Row.Source == side && where(row));
        int Leads(SourceSide side, params Disposition[] dispositions) =>
            Count(side, row => row.ItemLead && (dispositions.Length == 0 || dispositions.Contains(row.Row.Disposition)));
        var anyQuarantined = rows.Any(row => row.Row.Disposition == Disposition.Quarantined);
        var anyUnresolved = rows.Any(row => row.Row.Disposition is Disposition.Ambiguous or Disposition.SearchIncomplete);

        return
        [
            new("Rows read", "Every input row", null, side => Count(side, _ => true)),
            new("After lifecycle collapse", "Superseded and cancel rows of a chain are suppressed", side => -Count(side, row => row.Row.Disposition == Disposition.Suppressed), null),
            .. anyQuarantined
                ? [new Stage("After quarantine", "Rows the engine quarantined (duplicate conflicts, broken chains or packages)", side => -Count(side, row => row.Row.Disposition == Disposition.Quarantined), null)]
                : Array.Empty<Stage>(),
            new("Items after packages", "The legs of a package become one item", null, side => Leads(side) - Leads(side, Disposition.Suppressed) - Leads(side, Disposition.Quarantined)),
            .. context.Facts.Passes.Select(pass => new Stage(
                pass,
                $"commits {context.Facts.Entries.Count(entry => entry.IsMatched && entry.Entry.CommittedBy == pass)} entries",
                side => -Count(side, row => row.ItemLead && row.CommittedBy == pass && row.Row.Disposition == Disposition.Matched),
                null)),
            .. anyUnresolved
                ? [new Stage("Unresolved", "Ambiguous or search-incomplete items", side => -Leads(side, Disposition.Ambiguous, Disposition.SearchIncomplete), null)]
                : Array.Empty<Stage>(),
            new("Residue", "Unmatched ours and unmatched theirs", null, side => Leads(side, side == SourceSide.Left ? Disposition.LeftOnly : Disposition.RightOnly), Residue: true)
        ];
    }

    private static IReadOnlyList<Position> Walk(IReadOnlyList<Stage> stages, SourceSide side) =>
        stages.Aggregate(
            (List: (IReadOnlyList<Position>)[], Previous: (int?)null),
            (state, stage) =>
            {
                var position = stage switch
                {
                    { Remaining: { } remaining, Residue: true } => new Position(null, remaining(side)),
                    { Remaining: { } remaining } => new Position(state.Previous is { } before ? remaining(side) - before : null, remaining(side)),
                    _ => new Position(stage.Change!(side), (state.Previous ?? 0) + stage.Change!(side))
                };
                return ([.. state.List, position], position.Remaining);
            }).List;

    private static ReportCell Change(int? change) => change is { } value ? ReportCell.Of(value, NumberFormat.SignedCount) : ReportCell.Empty;

    private static ReportCell Remaining(int value, bool residue) =>
        ReportCell.Of(value, NumberFormat.Count) with { Bold = true, Tone = residue ? Tone.Tolerance : Tone.None };

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var stages = Stages(context);
        var ours = Walk(stages, SourceSide.Left);
        var theirs = Walk(stages, SourceSide.Right);
        var last = stages.Count - 1;

        var rows = stages
            .Select((stage, index) => new ReportRow(
            [
                ReportCell.Of(context.Label(stage.Name)) with { Bold = true },
                Change(ours[index].Change),
                Remaining(ours[index].Remaining, stage.Residue),
                Change(theirs[index].Change),
                Remaining(theirs[index].Remaining, stage.Residue),
                ReportCell.Of(stage.Note) with { Tone = Tone.Muted }
            ]))
            .Append(new ReportRow(
            [
                ReportCell.Of(context.Label("Check: remaining after the last stage = residue")) with { Bold = true },
                ReportCell.Empty,
                ReportCell.Check(ours[last - 1].Remaining == ours[last].Remaining),
                ReportCell.Empty,
                ReportCell.Check(theirs[last - 1].Remaining == theirs[last].Remaining),
                ReportCell.Empty
            ]))
            .ToList();

        var bars = stages
            .Select((stage, index) => new BarItem(context.Label(stage.Name), ours[index].Remaining, Second: theirs[index].Remaining))
            .ToList();

        return
        [
            new TableBlock(
                null,
                [
                    new ReportColumn(context.Label("Stage")),
                    new ReportColumn(context.Label("Ours: change"), CellAlign.Right),
                    new ReportColumn(context.Label("Ours: remaining"), CellAlign.Right),
                    new ReportColumn(context.Label("Theirs: change"), CellAlign.Right),
                    new ReportColumn(context.Label("Theirs: remaining"), CellAlign.Right),
                    new ReportColumn(context.Label("Note"))
                ],
                rows),
            new BarsBlock(context.Label("Items remaining after each stage"), bars, NumberFormat.Count, context.Label("Items"), context.Label("Ours: remaining"), context.Label("Theirs: remaining")),
            DataBlocks.Rows(context)
        ];
    }
}
