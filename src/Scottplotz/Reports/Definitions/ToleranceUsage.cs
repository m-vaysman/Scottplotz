using System.Globalization;
using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// MO6. Every within-tolerance difference that no other field explains, with the share of its declared tolerance it used.
// A check ties the largest share to 100%.
public static class ToleranceUsage
{
    private static readonly string[] Kinds = ["Absolute", "Relative", "AbsoluteOrRelative"];

    public static ReportDefinition Definition { get; } = new("MO6", [], Unavailable, Build);

    private static string? Unavailable(ReportContext context) =>
        context.Options.Tolerances is null
            ? "This report needs options.tolerances: a difference carries only the fact that it is within tolerance, so the tolerance amount comes from the declared rules."
            : Tolerated(context).Count == 0 ? "No within-tolerance differences in this run." : null;

    private static IReadOnlyList<DiffFact> Tolerated(ReportContext context) =>
        context.Facts.Diffs
            .Where(diff => diff.Diff.Outcome == DiffOutcome.WithinTolerance && diff.Diff.AttributedTo.Count == 0 && diff.ToleranceAmount is > 0m)
            .OrderByDescending(Used)
            .ThenBy(diff => diff.Entry.Id, StringComparer.Ordinal)
            .ThenBy(diff => diff.Field, StringComparer.Ordinal)
            .ToList();

    private static decimal Used(DiffFact diff) =>
        diff.ToleranceAmount is { } tolerance and not 0m ? Math.Abs(diff.Delta ?? 0m) / tolerance : 0m;

    private static string Declared(IEnumerable<ToleranceRule> rules) =>
        string.Join(
            "  ·  ",
            rules
                .Where(rule => Kinds.Contains(rule.Kind))
                .Select(rule => $"{rule.Field} ({rule.Path}): " + rule.Kind switch
                {
                    "Absolute" => $"absolute {Text(rule.Absolute)}",
                    "Relative" => $"relative {Text(rule.Relative)}",
                    _ => $"absolute {Text(rule.Absolute)} or relative {Text(rule.Relative)}"
                }));

    private static string Text(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static NumberFormat DeltaFormat(DiffFact diff) =>
        diff.Diff.DeltaKind == DeltaKind.Days || DataBlocks.FormatFor(diff.Role) == NumberFormat.Date ? NumberFormat.Integer : DataBlocks.FormatFor(diff.Role);

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var rows = Tolerated(context);
        var within = rows.All(diff => Used(diff) <= 1m);

        ReportRow Line(DiffFact diff) =>
            new(
            [
                ReportCell.Of(diff.Entry.Short),
                ReportCell.Of(diff.Entry.Text(ReportRole.Counterparty)),
                ReportCell.Of(diff.Entry.Text(ReportRole.Instrument)),
                ReportCell.Of(diff.Field),
                DataBlocks.Cell(diff.Ours, diff.Role),
                DataBlocks.Cell(diff.Theirs, diff.Role),
                ReportCell.Of(diff.Delta, DeltaFormat(diff)),
                ReportCell.Of(diff.ToleranceAmount, NumberFormat.Price),
                ReportCell.Of(Used(diff), NumberFormat.Percent) with { Bold = true, Bar = (double)Math.Min(Used(diff), 1m), HeatColour = HeatColour.Amber },
                ReportCell.Of(Used(diff) >= 1m ? "On the boundary" : "") with { Tone = Used(diff) >= 1m ? Tone.Tolerance : Tone.None }
            ]);

        var check = ReportRow.Of(
            ReportCell.Of(context.Label("Check: no tolerated difference exceeds its tolerance")) with { Bold = true, Span = 8 },
            ReportCell.Check(within),
            ReportCell.Empty);

        return
        [
            new NoteBlock($"Tolerances ({context.Run.ProfileId}): {Declared(context.Options.Tolerances!)}. Boundaries are inclusive."),
            new TableBlock(
                null,
                new[] { "Entry", "Counterparty", "Instrument", "Field", "Ours", "Theirs", "Delta", "Tolerance", "% of tolerance used", "Flag" }.Select(title => new ReportColumn(context.Label(title))).ToList(),
                [.. rows.Select(Line), check]),
            DataBlocks.Diffs(context)
        ];
    }
}
