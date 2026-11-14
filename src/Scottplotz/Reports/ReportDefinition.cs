using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports;

public sealed record ReportContext(ReportFacts Facts)
{
    public ReportInput Input => Facts.Input;

    public RunInfo Run => Facts.Input.Run;

    public ReportOptions Options => Facts.Input.Options;

    public DateOnly AsOf => Facts.Input.Run.AsOf;

    public string Label(string text) => Options.Labels.TryGetValue(text, out var label) ? label : text;

    public string? Field(ReportRole role) => Run.Field(role);

    public bool IsMapped(ReportRole role) => Run.IsMapped(role);

    public IReadOnlyList<string> SortCurrencies(IEnumerable<string> currencies) => Ordering.SortCurrencies(currencies, Options.CurrencyOrder);
}

public sealed record ReportDefinition(
    string Id,
    IReadOnlyList<ReportRole> Required,
    Func<ReportContext, string?> Unavailable,
    Func<ReportContext, IReadOnlyList<ReportBlock>> Build,
    bool IsPending = false)
{
    public ReportInfo Info => ReportCatalogue.Of(Id);

    public static ReportDefinition Pending(string id) =>
        new(id, [], _ => "This report is not built yet.", _ => [], IsPending: true);
}

public static class ReportCatalogue
{
    private const string BackOffice = "Back office";
    private const string MiddleOffice = "Middle office";

    public static IReadOnlyList<ReportInfo> All { get; } =
    [
        new("BO1", "ExceptionBlotter", "Exception blotter", BackOffice, "Trade support, settlements",
            "What do I fix first today?", "Intraday, every run",
            "One line per open item, ranked by settlement urgency and cash at stake. The working queue of an operations team."),
        new("BO2", "SideBySideCompare", "Side-by-side field compare", BackOffice, "Reconciliations team",
            "Exactly which fields disagree on each matched pair?", "Every run",
            "Our value and theirs next to each other for every compared field, with only the disagreeing cells coloured."),
        new("BO3", "RawVsNormalized", "Raw versus normalized", BackOffice, "Operations, onboarding a new counterparty feed",
            "Why did these match when the raw values look different?", "On query, feed onboarding",
            "Each side's original columns as received, the canonical values they normalized to, the normalization steps that changed a value, and the differences."),
        new("BO4", "LifecycleChains", "Lifecycle chain", BackOffice, "Trade support",
            "Which version of the trade is live, and what changed?", "On query",
            "NEW, CANC and CORR rows drawn as a chain, with the survivor and the trade it matched."),
        new("BO5", "Allocations", "Block and allocations", BackOffice, "Allocations desk, trade support",
            "Do the allocations add up to the block?", "Every run",
            "Each block with the allocations matched to it, and a check line."),
        new("BO6", "CashLadder", "Settlement cash ladder", BackOffice, "Settlements, cash management",
            "How much cash moves each day, and how much of it is in doubt?", "Daily, start of day",
            "Expected cash by settle date and currency, ours against theirs, with the difference split into its causes."),
        new("MO1", "ControlTotals", "Control totals", MiddleOffice, "Product control, reconciliation owners",
            "Do the two books agree in total, and where does each difference come from?", "Daily, before detail review",
            "Row counts and quantity by currency, counterparty and asset, with each difference decomposed and tied back to the files."),
        new("MO2", "Scorecard", "Match-quality scorecard", MiddleOffice, "Operations control, management",
            "How good is matching, overall and by counterparty?", "Daily, weekly trend",
            "KPI tiles for the run and a table of match and clean-match rates by counterparty."),
        new("MO3", "CascadeFunnel", "Cascade funnel", MiddleOffice, "Reconciliation owners, audit",
            "How did the engine reach these results?", "Per run, on change of profile",
            "A waterfall from rows read to residue: consolidation steps, then what each pass committed, in pass order."),
        new("MO4", "BreakHeatmap", "Break heatmap", MiddleOffice, "Risk, counterparty management",
            "Where are breaks concentrated?", "Daily, weekly trend",
            "Counterparty by break category, coloured by count of open items."),
        new("MO5", "Ageing", "Ageing and settlement status", MiddleOffice, "Operational risk",
            "How old are open items, and have they reached settlement?", "Daily",
            "Open items by category, aged in business days since trade date, with their settlement status at the as-of date."),
        new("MO6", "ToleranceUsage", "Tolerance usage", MiddleOffice, "Control, tolerance governance",
            "Are tolerances hiding drift?", "Weekly, tolerance review",
            "Every within-tolerance difference, with the share of its tolerance it used."),
        new("MO7", "AuditTrail", "Match audit trail", MiddleOffice, "Audit, compliance, reconciliation owners",
            "Why was this matched, or not?", "On query",
            "For each selected entry, the decision path through consolidation and the matching passes, ending in the commit or the final disposition.")
    ];

    public static ReportInfo Of(string id) => All.Single(info => info.Id == id);
}
