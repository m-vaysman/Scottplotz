using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Definitions;

// BO2. Every matched entry that is not exact: ours and theirs for each compared field, with the Theirs cell coloured from the engine's diff.
// Matched pairs whose two sides carry different currencies are listed apart from the field differences.
public static class SideBySideCompare
{
    private static readonly ReportRole[] Compared =
        [ReportRole.Quantity, ReportRole.Price, ReportRole.SettleDate, ReportRole.Accrued, ReportRole.Fees, ReportRole.NetCash];

    public static ReportDefinition Definition { get; } = new(
        "BO2",
        [ReportRole.Counterparty, ReportRole.Instrument, ReportRole.Side, ReportRole.Currency],
        context => Compared.Any(context.IsMapped) ? null : $"Map at least one of {string.Join(", ", Compared)} in run.roles.",
        Build);

    private static IReadOnlyList<ReportBlock> Build(ReportContext context)
    {
        var currencyField = context.Field(ReportRole.Currency)!;
        var fields = Compared.Where(context.IsMapped).ToList();
        var shown = fields.Select(role => context.Field(role)!).Append(currencyField).ToHashSet(StringComparer.Ordinal);
        var netCash = fields.Contains(ReportRole.NetCash);

        IEnumerable<DiffFact> DiffsOf(EntryFact entry) => context.Facts.DiffsByEntry[entry.Id];
        string Others(EntryFact entry, Func<string, bool> keep) => string.Join(", ", DiffsOf(entry).Select(diff => diff.Field).Where(keep));

        var entries = context.Facts.Entries
            .Where(entry => entry.IsMatched && entry.Entry.Outcome != Outcome.Exact && DiffsOf(entry).Any(diff => diff.Field != currencyField))
            .OrderBy(entry => entry.Entry.Outcome == Outcome.Break ? 0 : 1)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
        var splits = context.Facts.Entries
            .Where(entry => entry.CurrencySplit || DiffsOf(entry).Any(diff => diff.Field == currencyField))
            .ToList();

        ReportCell Theirs(EntryFact entry, ReportRole role) =>
            DiffsOf(entry).FirstOrDefault(diff => diff.Field == context.Field(role)) is { } diff
                ? DataBlocks.Cell(entry.Theirs[role], role) with { Tone = StateTone(diff.State) }
                : DataBlocks.Cell(entry.Theirs[role], role);

        ReportRow Line(EntryFact entry) =>
            new(
            [
                ReportCell.Of(entry.Short),
                ReportCell.Of(entry.Text(ReportRole.Counterparty)),
                ReportCell.Of(entry.Text(ReportRole.Instrument)),
                ReportCell.Of(entry.Text(ReportRole.Side)),
                ReportCell.Of(entry.Text(ReportRole.Currency)),
                ReportCell.Of(entry.Entry.Outcome.ToString()) with { Tone = entry.Entry.Outcome == Outcome.Break ? Tone.Break : Tone.Tolerance },
                ReportCell.Of(entry.Entry.CommittedBy),
                .. fields.SelectMany(role => new[] { DataBlocks.Cell(entry.Ours[role], role), Theirs(entry, role) }),
                .. netCash ? [ReportCell.Of(Net(entry.Theirs) - Net(entry.Ours), NumberFormat.Amount) with { Bold = true }] : Array.Empty<ReportCell>(),
                ReportCell.Of(Others(entry, field => !shown.Contains(field)))
            ]);

        ReportRow Split(EntryFact entry) =>
            ReportRow.Of(
                entry.Short,
                entry.Text(ReportRole.Counterparty),
                entry.Text(ReportRole.Instrument),
                ReportCell.Of(string.Join(", ", entry.OursCurrencies)) with { Tone = Tone.Break },
                ReportCell.Of(string.Join(", ", entry.TheirsCurrencies)) with { Tone = Tone.Break },
                entry.Entry.Outcome.ToString(),
                entry.Entry.CommittedBy,
                Others(entry, field => field != currencyField));

        return
        [
            Key(context),
            new TableBlock(
                null,
                [
                    .. Columns(context, "Entry", "Counterparty", "Instrument", "Side", "Ccy", "Outcome", "Pass"),
                    .. fields.SelectMany(_ => new[] { new ReportColumn(context.Label("Ours")), new ReportColumn(context.Label("Theirs")) }),
                    .. netCash ? [new ReportColumn(context.Label("Δ NetCash (theirs − ours)"))] : Array.Empty<ReportColumn>(),
                    new ReportColumn(context.Label("Other differences"))
                ],
                entries.Select(Line).ToList(),
                "Values are the engine's projections for each side. Colours come from the engine's field differences. Currency differences are listed below, apart from field differences.",
                Groups:
                [
                    new ColumnGroup("", 7),
                    .. fields.Select(role => new ColumnGroup(context.Label(context.Field(role)!), 2)),
                    new ColumnGroup("", netCash ? 2 : 1)
                ]),
            new TableBlock(
                context.Label("Matched pairs whose two sides carry different currencies"),
                Columns(context, "Entry", "Counterparty", "Instrument", "Ours currency", "Theirs currency", "Outcome", "Pass", "Other differences").ToList(),
                splits.Select(Split).ToList(),
                Empty: "None in this run."),
            DataBlocks.Entries(context)
        ];
    }

    private static IEnumerable<ReportColumn> Columns(ReportContext context, params string[] titles) =>
        titles.Select(title => new ReportColumn(context.Label(title)));

    private static decimal Net(RoleValues values) => values[ReportRole.NetCash].Number ?? 0m;

    private static Tone StateTone(DiffState state) =>
        state switch
        {
            DiffState.Break => Tone.Break,
            DiffState.Tolerance => Tone.Tolerance,
            _ => Tone.Attributed
        };

    private static TableBlock Key(ReportContext context) =>
        new(
            context.Label("Colour key for the Theirs cells"),
            [new ReportColumn("Colour"), new ReportColumn("Meaning")],
            [
                KeyRow(context, "Root break", Tone.Break, "The field differs and no other field explains it."),
                KeyRow(context, "Within tolerance", Tone.Tolerance, "The field differs, but inside the declared tolerance."),
                KeyRow(context, "Consequence of another field", Tone.Attributed, "The field differs because another field differs.")
            ]);

    private static ReportRow KeyRow(ReportContext context, string label, Tone tone, string meaning) =>
        ReportRow.Of(ReportCell.Of(context.Label(label)) with { Tone = tone }, meaning);
}
