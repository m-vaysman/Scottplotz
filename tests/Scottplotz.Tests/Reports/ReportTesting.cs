using Scottplotz.Charting;
using Scottplotz.Http;
using Scottplotz.Reports;
using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;

namespace Scottplotz.Tests.Reports;

// Helpers shared by every report test: parse a JSON input, build its facts, compose a report, and read cells back.
public static class ReportTesting
{
    public const string Roles =
        """{ "Counterparty": "Counterparty", "Instrument": "Instrument", "Asset": "AssetClass", "Side": "Side", "TradeDate": "TradeDate", "SettleDate": "SettleDate", "Quantity": "Quantity", "Price": "Price", "Currency": "Currency", "NetCash": "NetCash", "Accrued": "Accrued", "Fees": "Fees", "Book": "Book", "LifecycleAction": "EventAction", "LifecycleTarget": "EventTarget" }""";

    public static string Body(string entries, string rows, string options = "{}", string asOf = "2026-03-31", string passes = "[]", string roles = Roles) =>
        $$"""{ "run": { "asOf": "{{asOf}}", "profileId": "test", "engineVersion": "1.0", "roles": {{roles}} }, "options": {{options}}, "passes": {{passes}}, "entries": [ {{entries}} ], "rows": [ {{rows}} ] }""";

    public static ReportInput Input(string body) =>
        ReportInputParser.Parse(body, ReportLimits.Default) switch
        {
            Result<ReportInput>.Ok ok => ok.Value,
            Result<ReportInput>.Fail fail => throw new Xunit.Sdk.XunitException($"{fail.Problem.Code}: {fail.Problem.Detail}"),
            _ => throw new InvalidOperationException()
        };

    public static ChartProblem Problem(string body) =>
        ReportInputParser.Parse(body, ReportLimits.Default) switch
        {
            Result<ReportInput>.Fail fail => fail.Problem,
            _ => throw new Xunit.Sdk.XunitException("Expected a problem, but the input parsed.")
        };

    public static ReportInput Sample() => Input(ReportEndpoints.Sample);

    public static ReportFacts Facts(string body) => FactsBuilder.Build(Input(body));

    public static ReportDocument Compose(string id, ReportInput input) =>
        ReportRegistry.Compose(ReportRegistry.Find(id)!, input) switch
        {
            Result<ReportDocument>.Ok ok => ok.Value,
            Result<ReportDocument>.Fail fail => throw new Xunit.Sdk.XunitException($"{fail.Problem.Code}: {fail.Problem.Detail}"),
            _ => throw new InvalidOperationException()
        };

    public static string? Unavailable(string id, ReportInput input) =>
        ReportRegistry.Compose(ReportRegistry.Find(id)!, input) is Result<ReportDocument>.Fail fail ? fail.Problem.Detail : null;

    // Every table in a document, including those inside sections; data sections are left out.
    public static IReadOnlyList<TableBlock> Tables(ReportDocument document) => Tables(document.Blocks).ToList();

    private static IEnumerable<TableBlock> Tables(IEnumerable<ReportBlock> blocks) =>
        blocks.SelectMany(block => block switch
        {
            TableBlock table => [table],
            SectionBlock section => Tables(section.Blocks),
            _ => Enumerable.Empty<TableBlock>()
        });

    public static IReadOnlyList<string> Texts(ReportRow row) => row.Cells.Select(CellText.Of).ToList();

    public static IReadOnlyList<string> Header(TableBlock table) => table.Columns.Select(column => column.Title).ToList();

    // The row whose first cell reads as given.
    public static ReportRow Row(TableBlock table, string first) =>
        table.Rows.FirstOrDefault(row => row.Cells.Count > 0 && CellText.Of(row.Cells[0]) == first)
        ?? throw new Xunit.Sdk.XunitException($"No row starts with '{first}'. Rows start with: {string.Join(" | ", table.Rows.Select(row => row.Cells.Count > 0 ? CellText.Of(row.Cells[0]) : ""))}");

    // The cell of a row under the column with the given title.
    public static ReportCell Cell(TableBlock table, ReportRow row, string column)
    {
        var index = Header(table).ToList().IndexOf(column);
        return index >= 0 && index < row.Cells.Count
            ? row.Cells[index]
            : throw new Xunit.Sdk.XunitException($"No column '{column}'. Columns: {string.Join(" | ", Header(table))}");
    }

    public static IEnumerable<ReportCell> Checks(ReportDocument document) =>
        Tables(document).SelectMany(table => table.Rows).SelectMany(row => row.Cells).Where(cell => cell.Tone is Tone.Ok or Tone.Fail && CellText.Of(cell) is "✓" or "✗");
}
