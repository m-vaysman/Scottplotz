using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// BO4. For each lifecycle-collapsed entry and side: the rows of the chain in link order (following each row's target), the survivor, the rows it matched
// on the other side, and which compared fields the correction changed.
public static class LifecycleChains
{
    private static readonly ReportRole[] Compared = [ReportRole.Quantity, ReportRole.Price, ReportRole.SettleDate, ReportRole.NetCash];

    public static ReportDefinition Definition { get; } = new(
        "BO4",
        [ReportRole.LifecycleAction, ReportRole.LifecycleTarget],
        context => Chains(context.Facts).Count == 0 ? "No lifecycle-collapsed entries in this run." : null,
        Build);

    private sealed record Chain(EntryFact Entry, IReadOnlyList<RowFact> Rows, IReadOnlyList<RowFact> Others, RowFact Survivor);

    private static IReadOnlyList<Chain> Chains(ReportFacts facts) =>
        facts.Entries
            .Where(entry => entry.Entry.Composition.HasFlag(Composition.LifecycleCollapsed))
            .SelectMany(entry => new[] { SourceSide.Left, SourceSide.Right }.Select(side => ChainOf(facts, entry, side)).OfType<Chain>())
            .ToList();

    // A matched entry can collapse a chain on each side: a side yields a chain only when one of its rows linked to the entry is suppressed.
    // Suppressed rows are not entry members, so they come from the rows' own entry link; the chain is those rows followed by the members that carry an action.
    private static Chain? ChainOf(ReportFacts facts, EntryFact entry, SourceSide side)
    {
        var suppressed = facts.RowsByEntry[entry.Id].Where(row => row.Row.Source == side && row.Row.Suppression is not null).ToList();
        var members = facts.Members(entry, side).ToList();
        var actionable = members.Where(row => Action(row).Length > 0).ToList();
        var survivor = actionable.FirstOrDefault() ?? members.FirstOrDefault();
        return suppressed.Count == 0 || survivor is null
            ? null
            : new Chain(entry, LinkOrder(suppressed.Concat(actionable).DistinctBy(row => row.Index).ToList()), facts.Members(entry, Other(side)).ToList(), survivor);
    }

    private static SourceSide Other(SourceSide side) => side == SourceSide.Left ? SourceSide.Right : SourceSide.Left;

    private static string Action(RowFact row) => row.Values[ReportRole.LifecycleAction].Text;

    private static string Target(RowFact row) => row.Values[ReportRole.LifecycleTarget].Text;

    // Depth is one more than the depth of the row it targets (stopping at the chain length against loops): NEW, then CANC (targets NEW), then CORR.
    private static IReadOnlyList<RowFact> LinkOrder(IReadOnlyList<RowFact> rows)
    {
        var byKey = rows.GroupBy(row => row.Row.RowKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        int Depth(RowFact row, int guard) =>
            guard < rows.Count && byKey.TryGetValue(Target(row), out var target) ? Depth(target, guard + 1) + 1 : 0;
        return rows.OrderBy(row => Depth(row, 0)).ThenBy(row => row.Row.RowKey, StringComparer.Ordinal).ToList();
    }

    private static bool Same(FieldValue first, FieldValue second) =>
        first.Number == second.Number && first.Date == second.Date && (first.Number is not null || first.Date is not null || first.Text == second.Text);

    private static string DispositionText(RowFact row) =>
        row.Row.Suppression switch
        {
            "Superseded" => "Superseded",
            "CancelEvent" => "Cancel event",
            "FullyCancelled" => "Fully cancelled",
            _ => $"Survivor ({row.Row.Disposition})"
        };

    private static IReadOnlyList<ReportBlock> Build(ReportContext context) =>
    [
        .. Chains(context.Facts).Select(chain => Card(context, chain)),
        DataBlocks.Rows(context)
    ];

    private static ReportBlock Card(ReportContext context, Chain chain)
    {
        var entry = chain.Entry;
        var first = chain.Rows.FirstOrDefault(row => Action(row) != "CANC") ?? chain.Rows[0];
        var changed = Compared.Where(role => context.IsMapped(role) && !Same(first.Values[role], chain.Survivor.Values[role])).ToList();
        var what = changed.Count == 0
            ? "No compared field changed between the first version and the survivor."
            : string.Join(", ", changed.Select(role => $"{context.Field(role)} {first.Values[role].Text} → {chain.Survivor.Values[role].Text}"));

        IEnumerable<ReportCell> Values(RowFact row, Func<ReportRole, ReportCell, ReportCell> style) =>
            Compared.Select(role => context.IsMapped(role) ? style(role, DataBlocks.Cell(row.Values[role], role)) : ReportCell.Empty);

        ReportRow ChainRow(RowFact row, int index) =>
            row.Index == chain.Survivor.Index
                ? new ReportRow(
                [
                    Text(new string('↳', index + 1), bold: true),
                    Text(row.Row.RowKey, bold: true),
                    Text(Action(row), bold: true),
                    Text(Target(row), bold: true),
                    .. Values(row, (role, cell) => cell with { Bold = true, Tone = changed.Contains(role) ? Tone.Tolerance : Tone.Link }),
                    Text(DispositionText(row), bold: true)
                ])
                : new ReportRow(
                    new[]
                    {
                        Text(new string('↳', index + 1), tone: Tone.Muted),
                        Text(row.Row.RowKey, tone: Tone.Muted),
                        Text(Action(row), tone: Tone.Muted),
                        Text(Target(row), tone: Tone.Muted)
                    }
                    .Concat(Values(row, (_, cell) => cell with { Tone = Tone.Muted }))
                    .Append(Text(DispositionText(row), tone: Tone.Muted))
                    .Select(cell => cell with { Strike = true })
                    .ToList());

        ReportRow MatchedRow(RowFact row) =>
            new(
            [
                Text(context.Label("Matched with"), bold: true, tone: Tone.Ok),
                Text(row.Row.RowKey, tone: Tone.Ok),
                Text($"pass {entry.Entry.CommittedBy}", tone: Tone.Ok),
                Text($"outcome {entry.Entry.Outcome}", tone: Tone.Ok),
                .. Values(row, (_, cell) => cell with { Tone = Tone.Ok }),
                Text(row.Row.Disposition.ToString(), tone: Tone.Ok)
            ]);

        var rows = chain.Rows.Select(ChainRow)
            .Concat(chain.Others.Select(MatchedRow))
            .Append(new ReportRow([Text(context.Label("What the correction changed"), bold: true), Text(what) with { Span = 8 }]))
            .ToList();

        return new SectionBlock(
            $"{entry.Short}  ·  {entry.Text(ReportRole.Counterparty)}  ·  {entry.Text(ReportRole.Instrument)}  ·  {chain.Rows.Count} rows in the chain",
            null,
            [
                new TableBlock(
                    null,
                    [
                        new ReportColumn(""),
                        new ReportColumn(context.Label("Report")),
                        new ReportColumn(context.Label("Action")),
                        new ReportColumn(context.Label("Target")),
                        new ReportColumn(context.Label("Quantity")),
                        new ReportColumn(context.Label("Price")),
                        new ReportColumn(context.Label("Settle date")),
                        new ReportColumn(context.Label("Net cash")),
                        new ReportColumn(context.Label("Disposition"))
                    ],
                    rows)
            ]);
    }

    private static ReportCell Text(string text, bool bold = false, Tone tone = Tone.None) =>
        new(CellValue.Of(text), Tone: tone, Bold: bold);
}
