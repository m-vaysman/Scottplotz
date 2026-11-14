using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class ToleranceUsageTests
{
    private const string Rules =
        """
        { "tolerances": [
            { "path": "t", "field": "Price", "kind": "Absolute", "absolute": 0.10, "relative": null },
            { "path": "t", "field": "Fees", "kind": "Relative", "absolute": null, "relative": 0.02 },
            { "path": "t", "field": "SettleDate", "kind": "Absolute", "absolute": 2, "relative": null },
            { "path": "t", "field": "NetCash", "kind": "AbsoluteOrRelative", "absolute": 5, "relative": 0.001 },
            { "path": "t", "field": "Quantity", "kind": "Banded", "absolute": 1, "relative": null } ] }
        """;

    private static string Entry(string id, string diffs, string outcome = "WithinTolerance") =>
        $$"""{ "id": "{{id}}", "pathId": "t", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "{{outcome}}", "left": [], "right": [], "ours": { "Counterparty": "BAML", "Instrument": "BUND" }, "theirs": { "Counterparty": "BAML", "Instrument": "BUND" }, "committedBy": "P1", "diffs": [ {{diffs}} ] }""";

    private static string Diff(string field, string ours, string theirs, string delta, string outcome = "WithinTolerance", string attributed = "", string type = "decimal", string kind = "Number") =>
        $$"""{ "field": "{{field}}", "role": "{{field}}", "type": "{{type}}", "outcome": "{{outcome}}", "ours": {{ours}}, "theirs": {{theirs}}, "deltaKind": "{{kind}}", "delta": {{delta}}, "attributedTo": [{{attributed}}] }""";

    private static string Entries(params string[] entries) => string.Join(",", entries);

    private static readonly string Standard = Entries(
        Entry("e2", Diff("Price", "99.5", "99.6", "0.1")),
        Entry("e1", Diff("Price", "99.5", "99.55", "0.05")),
        Entry("e3", Diff("Fees", "100", "102", "2") + "," + Diff("NetCash", "5000", "5002", "2", attributed: "\"Fees\"")),
        Entry("e4", Diff("Price", "10", "11", "1", "Break"), "Break"),
        Entry("e5", Diff("Accrued", "10", "10.2", "0.2")),
        Entry("e6", Diff("Quantity", "1000", "1001", "1")));

    private static ReportDocument Build(string? entries = null, string options = Rules) =>
        Compose("MO6", Input(Body(entries ?? Standard, "", options)));

    private static TableBlock Table(ReportDocument document) => Tables(document)[0];

    private static IEnumerable<ReportRow> Data(TableBlock table) => table.Rows.Where(row => row.Kind == RowKind.Normal);

    private static string Text(TableBlock table, string entry, string column) => CellText.Of(Cell(table, Row(table, entry), column));

    [Fact]
    public void Only_unexplained_within_tolerance_differences_with_a_declared_amount_are_listed()
    {
        var table = Table(Build());

        Assert.Equal(["e2", "e3", "e1"], Data(table).Select(row => CellText.Of(row.Cells[0])));
    }

    [Fact]
    public void Rows_are_sorted_by_share_used_then_entry_id_then_field()
    {
        var entries = Entries(
            Entry("b", Diff("Price", "99.5", "99.55", "0.05")),
            Entry("a", Diff("Price", "99.5", "99.55", "0.05") + "," + Diff("Fees", "100", "100.5", "0.5")),
            Entry("c", Diff("Price", "99.5", "99.58", "0.08")));

        var table = Table(Build(entries));

        Assert.Equal(["c|Price", "a|Price", "b|Price", "a|Fees"], Data(table).Select(row => $"{CellText.Of(row.Cells[0])}|{CellText.Of(row.Cells[3])}"));
    }

    [Fact]
    public void Each_row_shows_ours_theirs_delta_tolerance_and_the_share_used()
    {
        var table = Table(Build());

        Assert.Equal(["e1", "BAML", "BUND", "Price", "99.5", "99.55", "0.05", "0.1", "50.0%", ""], Texts(Row(table, "e1")));
        Assert.True(Cell(table, Row(table, "e1"), "% of tolerance used").Bold);
    }

    [Fact]
    public void A_relative_tolerance_is_a_share_of_the_larger_side()
    {
        var table = Table(Build());

        Assert.Equal("2.04", Text(table, "e3", "Tolerance"));
        Assert.Equal("98.0%", Text(table, "e3", "% of tolerance used"));
    }

    [Fact]
    public void The_percentage_cell_carries_a_data_bar_capped_at_one()
    {
        var table = Table(Build());

        Assert.Equal(0.5, Cell(table, Row(table, "e1"), "% of tolerance used").Bar);
        Assert.Equal(1.0, Cell(table, Row(table, "e2"), "% of tolerance used").Bar);
        var over = Table(Build(Entry("o", Diff("Price", "99.5", "99.62", "0.12"))));
        Assert.Equal(1.0, Cell(over, Row(over, "o"), "% of tolerance used").Bar);
    }

    [Fact]
    public void A_difference_on_the_boundary_is_flagged_and_the_check_still_passes()
    {
        var table = Table(Build());

        Assert.Equal("On the boundary", Text(table, "e2", "Flag"));
        Assert.Equal(Tone.Tolerance, Cell(table, Row(table, "e2"), "Flag").Tone);
        Assert.Equal("", Text(table, "e1", "Flag"));
        Assert.Equal(Tone.None, Cell(table, Row(table, "e1"), "Flag").Tone);
        var check = table.Rows[^1];
        Assert.Equal(RowKind.Total, check.Kind);
        Assert.Equal("Check: no tolerated difference exceeds its tolerance", CellText.Of(check.Cells[0]));
        Assert.Equal(8, check.Cells[0].Span);
        Assert.Equal(("✓", Tone.Ok), (CellText.Of(check.Cells[1]), check.Cells[1].Tone));
    }

    [Fact]
    public void The_check_fails_when_a_tolerated_difference_exceeds_its_tolerance()
    {
        var table = Table(Build(Entry("o", Diff("Price", "99.5", "99.62", "0.12"))));

        Assert.Equal("120.0%", Text(table, "o", "% of tolerance used"));
        Assert.Equal("On the boundary", Text(table, "o", "Flag"));
        Assert.Equal(("✗", Tone.Fail), (CellText.Of(table.Rows[^1].Cells[1]), table.Rows[^1].Cells[1].Tone));
    }

    [Fact]
    public void A_date_difference_shows_dates_and_its_delta_in_whole_days()
    {
        var entries = Entry("d", Diff("SettleDate", "\"2026-04-01\"", "\"2026-04-02\"", "1", type: "date", kind: "Days"));

        var table = Table(Build(entries));

        Assert.Equal(["d", "BAML", "BUND", "SettleDate", "2026-04-01", "2026-04-02", "1", "2", "50.0%", ""], Texts(Row(table, "d")));
    }

    [Fact]
    public void The_note_lists_the_declared_tolerances_and_leaves_out_unknown_kinds()
    {
        var note = Assert.IsType<NoteBlock>(Build().Blocks[0]);

        Assert.Equal(
            "Tolerances (test): Price (t): absolute 0.10  ·  Fees (t): relative 0.02  ·  SettleDate (t): absolute 2  ·  NetCash (t): absolute 5 or relative 0.001. Boundaries are inclusive.",
            note.Text);
    }

    [Fact]
    public void The_report_is_unavailable_without_declared_tolerances_or_without_tolerated_differences()
    {
        Assert.Contains("needs options.tolerances", Unavailable("MO6", Input(Body(Standard, "", "{}"))));
        Assert.Contains("No within-tolerance differences in this run.", Unavailable("MO6", Input(Body(Entry("x", Diff("Price", "1", "2", "1", "Break"), "Break"), "", Rules))));
        Assert.Contains("No within-tolerance differences in this run.", Unavailable("MO6", Input(Body(Entry("x", Diff("Price", "1", "1.01", "0.01", attributed: "\"Fees\"")), "", Rules))));
    }

    [Fact]
    public void The_page_ends_with_the_differences_data_block()
    {
        Assert.Equal("Differences", Assert.IsType<DataBlock>(Build().Blocks[^1]).Name);
    }

    [Fact]
    public void Column_titles_follow_the_label_options()
    {
        var options = Rules.Replace("{ \"tolerances\"", "{ \"labels\": { \"Flag\": \"Warning\", \"Check: no tolerated difference exceeds its tolerance\": \"All within\" }, \"tolerances\"");

        var table = Table(Build(options: options));

        Assert.Equal("Warning", Header(table)[^1]);
        Assert.Equal("All within", CellText.Of(table.Rows[^1].Cells[0]));
    }

    [Fact]
    public void On_the_sample_the_rows_are_the_unexplained_within_tolerance_differences_with_their_shares()
    {
        var input = Sample();
        var rules = input.Options.Tolerances!;
        var expected = input.Entries
            .Where(entry => entry.Alignment == Alignment.Matched)
            .SelectMany(entry => entry.Diffs.Select(diff => (Entry: entry, Diff: diff)))
            .Where(pair => pair.Diff.Outcome == DiffOutcome.WithinTolerance && pair.Diff.AttributedTo.Count == 0)
            .Select(pair =>
            {
                var rule = rules.FirstOrDefault(r => r.Path == pair.Entry.PathId && r.Field == pair.Diff.Field);
                var scale = Math.Max(Math.Abs(pair.Diff.Ours.Number ?? 0m), Math.Abs(pair.Diff.Theirs.Number ?? 0m));
                var amount = rule?.Kind switch
                {
                    "Absolute" => rule.Absolute,
                    "Relative" => rule.Relative * scale,
                    "AbsoluteOrRelative" => Math.Max(rule.Absolute ?? 0m, (rule.Relative ?? 0m) * scale),
                    _ => null
                };
                return (pair.Entry, pair.Diff, Amount: amount);
            })
            .Where(item => item.Amount > 0m)
            .Select(item => (Id: item.Entry.Id[..12], item.Diff.Field, Used: Math.Abs(item.Diff.Delta ?? 0m) / item.Amount!.Value))
            .OrderByDescending(item => item.Used)
            .ToList();

        var document = Compose("MO6", input);
        var table = Table(document);
        var rows = Data(table).ToList();

        Assert.Equal(expected.Count, rows.Count);
        Assert.Equal(expected.Select(item => CellText.Number(item.Used, NumberFormat.Percent)), rows.Select(row => CellText.Of(row.Cells[8])));
        Assert.Equal(expected.Count(item => item.Used >= 1m), rows.Count(row => CellText.Of(row.Cells[9]) == "On the boundary"));
        Assert.Equal(expected.All(item => item.Used <= 1m) ? "✓" : "✗", CellText.Of(table.Rows[^1].Cells[1]));
    }
}
