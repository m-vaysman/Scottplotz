using Scottplotz.Charting;
using Scottplotz.Recon;
using Scottplotz.Reports.Definitions;
using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports;

public static class ReportRegistry
{
    public static IReadOnlyList<ReportDefinition> All { get; } =
    [
        ExceptionBlotter.Definition,
        SideBySideCompare.Definition,
        RawVsNormalized.Definition,
        LifecycleChains.Definition,
        Allocations.Definition,
        CashLadder.Definition,
        ControlTotals.Definition,
        Scorecard.Definition,
        CascadeFunnel.Definition,
        BreakHeatmap.Definition,
        Ageing.Definition,
        ToleranceUsage.Definition,
        AuditTrail.Definition
    ];

    public static string IdList => string.Join(", ", All.Select(definition => $"{definition.Id} ({definition.Info.Slug})"));

    public static ReportDefinition? Find(string id) =>
        All.FirstOrDefault(definition =>
            string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.Info.Slug, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.Info.Name, id, StringComparison.OrdinalIgnoreCase));

    public static Result<ReportDocument> Compose(ReportDefinition definition, ReportInput input) =>
        Compose(definition, new ReportContext(FactsBuilder.Build(input)));

    public static Result<ReportDocument> Compose(ReportDefinition definition, ReportContext context)
    {
        var missing = definition.Required.Where(role => !context.IsMapped(role)).ToList();
        var skip = missing.Count > 0
            ? $"Unmapped report roles: {string.Join(", ", missing)}. Map them in run.roles."
            : definition.Unavailable(context);
        var info = definition.Info;
        return skip is not null
            ? Result<ReportDocument>.Error(ReportProblem.Unavailable(info.Id, skip))
            : ReconResult.Ok(new ReportDocument(
                info,
                context.Label(info.Title),
                context.AsOf,
                $"{context.Run.Title}  ·  Profile {context.Run.ProfileId}" + (context.Run.EngineVersion.Length > 0 ? $"  ·  Engine {context.Run.EngineVersion}" : ""),
                definition.Build(context)));
    }
}
