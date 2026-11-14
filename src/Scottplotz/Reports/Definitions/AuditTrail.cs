using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO7. For each selected entry, its consolidation steps (lifecycle, package), the engine's decision trail (pass, verdict, key or detail,
// candidates), any hints, and the final result. The text is the engine's own trail; nothing here is inferred.
public static class AuditTrail
{
    public static ReportDefinition Definition { get; } = new(
        "MO7",
        [],
        context => Selected(context).Count == 0 ? "Nothing to explain: no entry is selected and every entry is an exact match." : null,
        Build);

    private sealed record Step(string Name, string Verdict, string Detail, string Candidates, bool Hint = false);

    private static IReadOnlyList<EntryFact> Selected(ReportContext context) =>
        context.Options.SelectedEntries is { } selected
            ? context.Facts.Entries.Where(entry => selected.Any(prefix => entry.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToList()
            : context.Facts.Entries
                .Where(entry => entry.Entry is not { Alignment: Alignment.Matched, Outcome: Outcome.Exact }
                    || entry.Entry.Composition.HasFlag(Composition.LifecycleCollapsed)
                    || entry.Entry.Composition.HasFlag(Composition.Package))
                .OrderBy(entry => Rank(entry.Entry))
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .Take(context.Options.MaxCards)
                .ToList();

    private static int Rank(EntryInput entry) =>
        entry switch
        {
            { Outcome: Outcome.Break } => 0,
            { Outcome: Outcome.WithinTolerance } => 1,
            { Alignment: Alignment.Ambiguous or Alignment.SearchIncomplete } => 2,
            { Alignment: Alignment.LeftOnly or Alignment.RightOnly } => 3,
            _ => 4
        };

    private static IReadOnlyList<Step> Steps(ReportContext context, EntryFact entry)
    {
        var suppressed = context.Facts.RowsByEntry[entry.Id].Where(row => row.Row.Suppression is not null).ToList();
        return
        [
            .. entry.Entry.Composition.HasFlag(Composition.LifecycleCollapsed)
                ? [new Step("Consolidation", "Lifecycle", $"{suppressed.Count} row(s) superseded or cancelled: {string.Join(", ", suppressed.Select(row => $"{row.Row.RowKey} ({row.Row.Suppression})"))}", "")]
                : Array.Empty<Step>(),
            .. entry.Entry.Composition.HasFlag(Composition.Package)
                ? [new Step("Consolidation", "Package", $"legs consolidated into one item: ours {entry.Keys(SourceSide.Left)} / theirs {entry.Keys(SourceSide.Right)}", "")]
                : Array.Empty<Step>(),
            .. entry.Entry.Trail.Select(attempt => new Step(
                attempt.Pass,
                attempt.Verdict,
                string.Join("  ·  ", new[] { attempt.Key is { } key ? $"key {key}" : null, attempt.Detail }.OfType<string>()),
                string.Join(", ", attempt.Candidates.Select(candidate => $"{(candidate.Source == SourceSide.Left ? "left" : "right")}:{candidate.RowKey}")))),
            .. entry.Entry.Hints.Select(hint => new Step("Hint", hint.Code, hint.Detail ?? "", "", Hint: true)),
            new Step("Result", entry.Entry.Alignment.ToString(), Result(entry), "")
        ];
    }

    private static string Result(EntryFact entry) =>
        entry.Entry switch
        {
            { Alignment: Alignment.Matched, Outcome: Outcome.Exact } => $"{entry.Entry.Composition}, exact",
            { Alignment: Alignment.Matched } => $"{entry.Entry.Composition}, {entry.Entry.Outcome}: {string.Join(", ", entry.Entry.Diffs.Select(diff => $"{diff.Field} {diff.Outcome}"))}",
            _ => $"{entry.Entry.Alignment}: no match committed"
        };

    // Green for a commit, amber for an undecided search, grey for nothing to go on, red for a veto; other steps are plain and bold.
    private static ReportCell Verdict(Step step) =>
        ReportCell.Of(step.Verdict) with
        {
            Bold = true,
            Italic = step.Hint,
            Tone = step.Verdict switch
            {
                "Committed" or "Matched" => Tone.Ok,
                "Contest" or "SearchIncomplete" or "Ambiguous" => Tone.Tolerance,
                "NoKey" or "NoCandidate" or "LeftOnly" or "RightOnly" => Tone.Attributed,
                "Vetoed" => Tone.Break,
                _ => Tone.None
            }
        };

    private static string Keys(string keys) => keys.Length == 0 ? "—" : keys;

    private static SectionBlock Card(ReportContext context, EntryFact entry) =>
        new(
            $"{entry.Short}  ·  ours {Keys(entry.Keys(SourceSide.Left))} / theirs {Keys(entry.Keys(SourceSide.Right))}  ·  {entry.Entry.Alignment}, {entry.Entry.Outcome}",
            null,
            [
                new TableBlock(
                    null,
                    [
                        new ReportColumn(context.Label("Step")),
                        new ReportColumn(context.Label("Verdict")),
                        new ReportColumn(context.Label("Key or detail"), Wrap: true),
                        new ReportColumn(context.Label("Candidates"), Wrap: true)
                    ],
                    Steps(context, entry)
                        .Select(step => new ReportRow(
                        [
                            ReportCell.Of(step.Name) with { Bold = true },
                            Verdict(step),
                            ReportCell.Of(step.Detail) with { Italic = step.Hint },
                            ReportCell.Of(step.Candidates) with { Tone = Tone.Muted }
                        ]))
                        .ToList())
            ]);

    private static IReadOnlyList<ReportBlock> Build(ReportContext context) =>
    [
        new NoteBlock("The engine's explanation records the pass that committed each match, or the pass that left an entry open, with its key, candidates and detail. Passes an entry fell through before that are not recorded by the engine."),
        .. Selected(context).Select(entry => Card(context, entry))
    ];
}
