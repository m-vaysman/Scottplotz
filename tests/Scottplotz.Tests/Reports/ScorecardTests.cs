using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class ScorecardTests
{
    private static string Entry(string id, string alignment, string outcome, string counterparty, string? pass, string left = "", string right = "") =>
        $$"""{ "id": "{{id}}", "alignment": "{{alignment}}", "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "ours": { "Counterparty": "{{counterparty}}" }, "committedBy": {{(pass is null ? "null" : $"\"{pass}\"")}} }""";

    private static string InputRow(string source, string key, string disposition, string counterparty, string? entry = null, bool lead = true) =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "{{disposition}}", "itemLead": {{(lead ? "true" : "false")}}, {{(entry is null ? "" : $"\"entryId\": \"{entry}\",")}} "values": { "Counterparty": "{{counterparty}}" } }""";

    private static readonly string Entries = string.Join(", ",
        Entry("e1", "Matched", "Exact", "A", "P1", "\"L-1\"", "\"R-1\""),
        Entry("e2", "Matched", "Break", "A", "P2", "\"L-2\"", "\"R-2\""),
        Entry("e3", "Matched", "WithinTolerance", "B", "P1", "\"L-3\"", "\"R-3\""),
        Entry("e4", "LeftOnly", "NotCompared", "A", "P2", "\"L-4\""),
        Entry("e5", "Ambiguous", "NotCompared", "B", "P3", "\"L-5\""));

    private static readonly string Rows = string.Join(", ",
        InputRow("left", "L-1", "Matched", "A", "e1"),
        InputRow("right", "R-1", "Matched", "A", "e1"),
        InputRow("left", "L-2", "Matched", "A", "e2"),
        InputRow("right", "R-2", "Matched", "A", "e2"),
        InputRow("left", "L-3", "Matched", "B", "e3"),
        InputRow("right", "R-3", "Matched", "B", "e3"),
        InputRow("left", "L-4", "LeftOnly", "A", "e4"),
        InputRow("left", "L-5", "Ambiguous", "B", "e5"),
        InputRow("left", "L-6", "LeftOnly", "A", lead: false),
        InputRow("right", "R-7", "RightOnly", "B"));

    private static ReportDocument Report(string entries, string rows, string passes = "[\"P1\", \"P2\"]") =>
        Compose("MO2", Input(Body(entries, rows, passes: passes)));

    private static TableBlock Counterparties(ReportDocument document) => Tables(document).Single(table => table.Title is null);

    private static IReadOnlyList<ReportTile> AllTiles(ReportDocument document) => document.Blocks.OfType<TilesBlock>().SelectMany(block => block.Tiles).ToList();

    private static IReadOnlyList<string> RowTexts(ReportDocument document, string counterparty) => ReportTesting.Texts(Row(Counterparties(document), counterparty));

    [Fact]
    public void The_tiles_count_rows_entries_and_rates_over_the_whole_run()
    {
        var tiles = AllTiles(Report(Entries, Rows));

        Assert.Equal(
            [("Internal rows", "6"), ("Counterparty rows", "4"), ("Result entries", "5"), ("Matched entries", "3"), ("Auto-match rate", "50.0%"),
                ("Clean-match rate", "66.7%"), ("Breaks", "1"), ("Unmatched ours", "1"), ("Unmatched theirs", "1"), ("Ambiguous", "1")],
            tiles.Select(tile => (tile.Label, CellText.Of(tile.Value))));
    }

    [Fact]
    public void The_ten_tiles_sit_in_two_rows_of_five_as_in_the_original()
    {
        var rows = Report(Entries, Rows).Blocks.OfType<TilesBlock>().Select(block => block.Tiles.Select(tile => tile.Label).ToList()).ToList();

        Assert.Equal(
            [["Internal rows", "Counterparty rows", "Result entries", "Matched entries", "Auto-match rate"],
                ["Clean-match rate", "Breaks", "Unmatched ours", "Unmatched theirs", "Ambiguous"]],
            rows);
    }

    [Fact]
    public void A_counterparty_with_no_matched_entries_has_a_zero_clean_match_rate_coloured_as_a_break()
    {
        var table = Counterparties(Report(Entries, Rows));

        var cell = Cell(table, Row(table, "A"), "Clean-match rate");
        Assert.Equal("50.0%", CellText.Of(cell));
        var none = Cell(Counterparties(Report("", InputRow("left", "L-1", "LeftOnly", "Q"))), Row(Counterparties(Report("", InputRow("left", "L-1", "LeftOnly", "Q"))), "Q"), "Clean-match rate");
        Assert.Equal("0.0%", CellText.Of(none));
        Assert.Equal(Tone.Break, none.Tone);
    }

    [Fact]
    public void Commits_by_pass_counts_every_entry_with_that_committed_by_open_ones_included_and_appends_unlisted_passes()
    {
        var table = Tables(Report(Entries, Rows)).Single(table => table.Title == "Commits by pass");

        Assert.Equal(["P1", "P2", "P3"], Header(table));
        Assert.Equal(["2", "2", "1"], Texts(Assert.Single(table.Rows)));
        Assert.Contains("open entries are counted", table.Note);
    }

    [Fact]
    public void Each_counterparty_row_counts_its_rows_and_entries_and_the_total_row_ties_to_the_run()
    {
        var document = Report(Entries, Rows);

        Assert.Equal(["A", "4", "2", "2", "1", "-", "1", "1", "-", "-", "50.0%", "50.0%"], RowTexts(document, "A"));
        Assert.Equal(["B", "2", "2", "1", "-", "1", "-", "-", "1", "1", "50.0%", "100.0%"], RowTexts(document, "B"));
        var table = Counterparties(document);
        Assert.True(Row(table, "A").Cells[0].Bold);
        Assert.Equal(["Total", "6", "4", "3", "1", "1", "1", "1", "1", "1", "✓", ""], RowTexts(document, "Total"));
        Assert.Equal(RowKind.Total, Row(table, "Total").Kind);
        Assert.Equal(
            ["Counterparty", "Internal rows", "Counterparty rows", "Matched entries", "Exact", "Within tolerance", "Breaks", "Unmatched ours", "Unmatched theirs", "Ambiguous", "Auto-match rate", "Clean-match rate"],
            Header(table));
    }

    [Fact]
    public void A_non_lead_leg_is_not_an_unmatched_item_but_an_unrouted_right_row_is()
    {
        var document = Report(Entries, Rows);

        Assert.Equal("1", CellText.Of(Cell(Counterparties(document), Row(Counterparties(document), "A"), "Unmatched ours")));
        Assert.Equal("1", CellText.Of(Cell(Counterparties(document), Row(Counterparties(document), "B"), "Unmatched theirs")));
    }

    [Fact]
    public void Counterparties_sort_by_auto_match_rate_descending_then_by_name()
    {
        var entries = Entry("e1", "Matched", "Exact", "Z", null, "\"L-1\"", "\"R-1\"");
        var rows = string.Join(", ", InputRow("left", "L-1", "Matched", "Z", "e1"), InputRow("right", "R-1", "Matched", "Z", "e1"), InputRow("left", "L-2", "LeftOnly", "M"), InputRow("left", "L-3", "LeftOnly", "B"));

        var names = Counterparties(Report(entries, rows)).Rows.Select(row => CellText.Of(row.Cells[0]));

        Assert.Equal(["Z", "B", "M", "Total"], names);
    }

    [Fact]
    public void The_auto_match_rate_carries_a_bar_and_the_clean_match_rate_is_coloured_by_band()
    {
        var counts = new[] { 19, 18, 17, 16 };
        var entries = string.Join(", ", counts.SelectMany(exact => Enumerable.Range(0, 20).Select(index =>
            Entry($"c{exact}-{index}", "Matched", index < exact ? "Exact" : "Break", $"C{exact}", null))));
        var rows = string.Join(", ", counts.Select(exact => InputRow("left", $"L{exact}", "LeftOnly", $"C{exact}")));
        var document = Report(entries, rows);
        var table = Counterparties(document);

        Assert.Equal(
            [Tone.Ok, Tone.Tolerance, Tone.Tolerance, Tone.Break],
            counts.Select(exact => Cell(table, Row(table, $"C{exact}"), "Clean-match rate").Tone));
        Assert.All(table.Rows.Take(4), row => Assert.Equal(0.0, Cell(table, row, "Auto-match rate").Bar));
        var auto = Cell(Counterparties(Report(Entries, Rows)), Row(Counterparties(Report(Entries, Rows)), "B"), "Auto-match rate");
        Assert.Equal(0.5, auto.Bar);
    }

    [Fact]
    public void The_total_check_fails_when_an_entry_belongs_to_a_counterparty_with_no_rows()
    {
        var entries = Entry("e1", "Matched", "Exact", "Ghost", null, "\"L-1\"", "\"R-1\"");
        var rows = string.Join(", ", InputRow("left", "L-1", "Matched", "A", "e1"), InputRow("right", "R-1", "Matched", "A", "e1"));

        var document = Report(entries, rows);

        Assert.Equal("✗", RowTexts(document, "Total")[10]);
        Assert.Contains(Checks(document), check => check.Tone == Tone.Fail);
    }

    [Fact]
    public void An_empty_run_gives_zero_tiles_a_passless_table_and_a_passing_total()
    {
        var document = Report("", "", "[]");

        Assert.All(AllTiles(document), tile => Assert.Equal(0m, Assert.IsType<CellValue.Number>(tile.Value.Value).Value));
        var passes = Tables(document).Single(table => table.Title == "Commits by pass");
        Assert.Empty(passes.Rows);
        Assert.Equal("No passes in this run.", passes.Empty);
        Assert.Equal(["Total", "-", "-", "-", "-", "-", "-", "-", "-", "-", "✓", ""], RowTexts(document, "Total"));
    }

    [Fact]
    public void The_page_ends_with_the_entry_and_row_data_sections()
    {
        var document = Report(Entries, Rows);

        Assert.Equal(["Result entries", "Result rows"], document.Blocks.OfType<DataBlock>().Select(block => block.Name));
    }

    [Fact]
    public void The_sample_figures_match_a_count_made_straight_from_the_input()
    {
        var input = Sample();
        var document = Compose("MO2", input);
        var tiles = AllTiles(document).ToDictionary(tile => tile.Label, tile => CellText.Of(tile.Value));
        var left = input.Rows.Where(row => row.Source == SourceSide.Left).ToList();
        var matchedEntries = input.Entries.Where(entry => entry.Alignment == Alignment.Matched).ToList();
        var clean = matchedEntries.Count(entry => entry.Outcome is Outcome.Exact or Outcome.WithinTolerance);

        Assert.Equal(left.Count.ToString(), tiles["Internal rows"]);
        Assert.Equal(input.Entries.Count.ToString(), tiles["Result entries"]);
        Assert.Equal(matchedEntries.Count.ToString(), tiles["Matched entries"]);
        Assert.Equal((100.0 * left.Count(row => row.Disposition == Disposition.Matched) / left.Count).ToString("0.0"), tiles["Auto-match rate"].TrimEnd('%'));
        Assert.Equal((100.0 * clean / matchedEntries.Count).ToString("0.0"), tiles["Clean-match rate"].TrimEnd('%'));
        var passes = Tables(document).Single(table => table.Title == "Commits by pass");
        Assert.Equal(
            Header(passes).Select(pass => input.Entries.Count(entry => entry.CommittedBy == pass).ToString()),
            ReportTesting.Texts(passes.Rows[0]));
        var best = Counterparties(document).Rows[0];
        var rates = left.GroupBy(row => row.Values[ReportRole.Counterparty].Text).ToDictionary(group => group.Key, group => (double)group.Count(row => row.Disposition == Disposition.Matched) / group.Count());
        Assert.Equal(rates.Values.Max(), rates[CellText.Of(best.Cells[0])]);
    }
}
