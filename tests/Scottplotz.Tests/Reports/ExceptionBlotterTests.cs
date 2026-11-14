using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class ExceptionBlotterTests
{
    private const string PriceBreak =
        """{ "field": "Price", "role": "Price", "type": "decimal", "outcome": "Break", "ours": 99.5, "theirs": 99.6, "deltaKind": "Number", "delta": 0.1 }""";

    private const string CashConsequence =
        """{ "field": "NetCash", "role": "NetCash", "type": "decimal", "outcome": "Break", "ours": 5000, "theirs": 5010, "deltaKind": "Number", "delta": 10, "attributedTo": ["Price"] }""";

    private const string SettleBreak =
        """{ "field": "SettleDate", "role": "SettleDate", "type": "date", "outcome": "Break", "ours": "2026-03-30", "theirs": "2026-04-01", "deltaKind": "Days", "delta": 2 }""";

    private static string Break(string id, string diffs, string settle = "2026-03-30", string currency = "USD") =>
        $$"""{ "id": "{{id}}", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "Break", "ours": { "Counterparty": "BAML", "Instrument": "IBM", "Side": "BUY", "Currency": "{{currency}}", "NetCash": 5000, "SettleDate": "{{settle}}", "TradeDate": "2026-03-27" }, "theirs": { "Currency": "{{currency}}", "NetCash": 5010 }, "committedBy": "P1", "diffs": [ {{diffs}} ] }""";

    private static string Open(string id, string alignment, string ours, string theirs, string trail = "") =>
        $$"""{ "id": "{{id}}", "alignment": "{{alignment}}", "outcome": "NotCompared", "ours": {{ours}}, "theirs": {{theirs}}, "committedBy": "P1"{{trail}} }""";

    private static readonly string Mixed = string.Join(
        ", ",
        Break("brk1", $"{PriceBreak}, {CashConsequence}"),
        Open("left1", "LeftOnly", """{ "Counterparty": "JPM", "Currency": "USD", "NetCash": 700, "Quantity": 70, "SettleDate": "2026-03-31", "TradeDate": "2026-03-30" }""", "null"),
        Open("right1", "RightOnly", "null", """{ "Counterparty": "GS", "Currency": "EUR", "NetCash": -300, "Quantity": 30, "SettleDate": "2026-04-02" }"""),
        Open("amb1", "Ambiguous", """{ "Counterparty": "MS", "Currency": "USD", "NetCash": 99, "Quantity": 9 }""", "null"));

    private static IReadOnlyList<TableBlock> Tables(string entries, string rows = "", string options = "{}", string roles = ReportTesting.Roles) =>
        ReportTesting.Tables(Compose("BO1", Input(Body(entries, rows, options, roles: roles))));

    private static IReadOnlyList<string> Column(TableBlock table, string title) =>
        table.Rows.Select(row => CellText.Of(Cell(table, row, title))).ToList();

    [Fact]
    public void The_summary_counts_open_items_by_status_and_by_type_and_ties_both_totals_to_the_item_count()
    {
        var summary = Tables(Mixed)[0];

        Assert.Equal(
            [
                ["Past settle date", "1", "Field break", "1", ""],
                ["Settles today", "1", "Unmatched ours", "1", ""],
                ["Upcoming", "1", "Unmatched theirs", "1", ""],
                ["No settle date", "1", "Ambiguous", "1", ""],
                ["Total", "4", "Total", "4", "✓"]
            ],
            summary.Rows.Select(row => Texts(row)));
        Assert.Equal(RowKind.Total, summary.Rows[^1].Kind);
        Assert.Equal(Tone.Ok, summary.Rows[^1].Cells[4].Tone);
        Assert.Equal(["Open items by status", "Open items by type"], summary.Groups!.Select(group => group.Title));
    }

    [Fact]
    public void The_optional_status_and_type_rows_appear_only_when_an_item_needs_them()
    {
        var summary = Tables(Break("brk1", PriceBreak))[0];

        Assert.Equal(
            [
                ["Past settle date", "1", "Field break", "1", ""],
                ["Settles today", "-", "Unmatched ours", "-", ""],
                ["Upcoming", "-", "Unmatched theirs", "-", ""],
                ["Total", "1", "Total", "1", "✓"]
            ],
            summary.Rows.Select(row => Texts(row)));
    }

    [Fact]
    public void A_search_incomplete_entry_adds_its_type_row_and_the_shorter_list_is_padded_with_blanks()
    {
        var entries = Open("s1", "SearchIncomplete", """{ "Counterparty": "MS", "Currency": "USD", "SettleDate": "2026-04-03" }""", "null");

        var summary = Tables(entries)[0];

        Assert.Equal(["Field break", "Unmatched ours", "Unmatched theirs", "Search incomplete", "Total"], summary.Rows.Select(row => Texts(row)[2]));
        Assert.Equal(["Past settle date", "Settles today", "Upcoming", "", "Total"], summary.Rows.Select(row => Texts(row)[0]));
    }

    [Fact]
    public void A_break_line_carries_the_root_field_both_values_the_delta_the_cash_impact_and_the_action()
    {
        var blotter = Tables(Break("brk1abcdefgh1234", $"{PriceBreak}, {CashConsequence}"))[1];

        var row = Assert.Single(blotter.Rows);
        Assert.Equal(
            ["1", "brk1abcdefgh", "Past settle date", "Field break", "BAML", "IBM", "", "BUY", "2026-03-27", "2026-03-30", "2", "Price", "99.5", "99.6", "0.1", "USD", "10.00", "Confirm execution price with counterparty", "Price"],
            Texts(row));
        Assert.Equal(Header(blotter).Count, row.Cells.Count);
    }

    [Fact]
    public void The_table_header_reads_as_the_original_and_labels_replace_headings()
    {
        var blotter = Tables(Mixed, options: """{ "labels": { "Rank": "No.", "Open items by type": "By type" } }""");

        Assert.Equal(
            ["No.", "Entry", "Status", "Item type", "Counterparty", "Instrument", "Asset", "Side", "Trade date", "Settle date", "Age (bus. days)", "Field", "Ours", "Theirs", "Delta", "Ccy", "Impact", "Suggested action", "Category"],
            Header(blotter[1]));
        Assert.Equal("By type", blotter[0].Groups![1].Title);
    }

    [Fact]
    public void Status_is_coloured_red_for_past_amber_for_today_grey_for_upcoming_and_not_at_all_without_a_settle_date()
    {
        var blotter = Tables(Mixed)[1];

        Assert.Equal(
            [("Past settle date", Tone.Break), ("Settles today", Tone.Tolerance), ("Upcoming", Tone.Shade), ("No settle date", Tone.None)],
            blotter.Rows.Select(row => (CellText.Of(Cell(blotter, row, "Status")), Cell(blotter, row, "Status").Tone)));
    }

    [Fact]
    public void Items_rank_by_status_then_currency_then_impact_and_the_rank_counts_from_one()
    {
        var entries = string.Join(
            ", ",
            Break("late", PriceBreak, "2026-03-30", "USD"),
            Break("eur", $"{PriceBreak}, {CashConsequence}", "2026-03-30", "EUR"),
            Break("usd", $"{PriceBreak}, {CashConsequence}", "2026-03-30", "USD"),
            Open("soon", "RightOnly", "null", """{ "Currency": "USD", "NetCash": 1, "SettleDate": "2026-04-05" }"""));

        var blotter = Tables(entries)[1];

        Assert.Equal(["usd", "late", "eur", "soon"], Column(blotter, "Entry"));
        Assert.Equal(["1", "2", "3", "4"], Column(blotter, "Rank"));
        Assert.Equal(["10.00", "-", "10.00", "1.00"], Column(blotter, "Impact"));
    }

    [Fact]
    public void An_unmatched_item_shows_its_own_side_net_cash_and_leaves_the_other_side_and_the_delta_blank()
    {
        var blotter = Tables(Mixed)[1];

        var ours = Row(blotter, "2");
        var theirs = Row(blotter, "3");
        Assert.Equal(["700.00", "", ""], new[] { "Ours", "Theirs", "Delta" }.Select(column => CellText.Of(Cell(blotter, ours, column))));
        Assert.Equal(["", "(300.00)", ""], new[] { "Ours", "Theirs", "Delta" }.Select(column => CellText.Of(Cell(blotter, theirs, column))));
        Assert.Equal("700.00", CellText.Of(Cell(blotter, ours, "Impact")));
        Assert.Equal("300.00", CellText.Of(Cell(blotter, theirs, "Impact")));
        Assert.Equal("(whole trade)", CellText.Of(Cell(blotter, ours, "Field")));
        Assert.Equal("Chase confirmation: not in their file", CellText.Of(Cell(blotter, ours, "Suggested action")));
        Assert.Equal("DK: investigate alleged trade", CellText.Of(Cell(blotter, theirs, "Suggested action")));
        Assert.Equal("Unmatched ours", CellText.Of(Cell(blotter, ours, "Category")));
    }

    [Fact]
    public void An_ambiguous_item_has_no_amounts_and_no_impact()
    {
        var blotter = Tables(Mixed)[1];

        var row = Row(blotter, "4");
        Assert.Equal(["", "", "", ""], new[] { "Ours", "Theirs", "Delta", "Impact" }.Select(column => CellText.Of(Cell(blotter, row, column))));
        Assert.Equal("Resolve ambiguity: choose the rows that belong together", CellText.Of(Cell(blotter, row, "Suggested action")));
    }

    [Fact]
    public void Without_a_net_cash_role_unmatched_items_show_quantity_and_have_no_impact()
    {
        var roles = ReportTesting.Roles.Replace("\"NetCash\": \"NetCash\", ", "");
        var entries = Open("left1", "LeftOnly", """{ "Counterparty": "JPM", "Currency": "USD", "Quantity": 70, "SettleDate": "2026-03-31" }""", "null");

        var blotter = Tables(entries, roles: roles)[1];

        var row = Assert.Single(blotter.Rows);
        Assert.Equal("70", CellText.Of(Cell(blotter, row, "Ours")));
        Assert.Equal("", CellText.Of(Cell(blotter, row, "Impact")));
    }

    [Fact]
    public void A_settle_date_break_shows_dates_the_delta_in_whole_days_and_the_full_net_cash_as_impact()
    {
        var blotter = Tables(Break("brk1", SettleBreak))[1];

        var row = Assert.Single(blotter.Rows);
        Assert.Equal(["SettleDate", "2026-03-30", "2026-04-01", "2", "5,000.00", "Align settlement date / SSI convention"],
            new[] { "Field", "Ours", "Theirs", "Delta", "Impact", "Suggested action" }.Select(column => CellText.Of(Cell(blotter, row, column))));
        Assert.Equal(NumberFormat.Integer, Cell(blotter, row, "Delta").Format);
    }

    [Fact]
    public void A_break_with_no_net_cash_difference_has_zero_impact_and_reference_data_problems_prefix_the_action()
    {
        const string rows = """{ "source": "left", "rowKey": "L-1", "disposition": "Matched", "entryId": "brk1", "values": { "Currency": "USD" }, "diagnostics": ["UnknownParty"] }""";
        var entry = Break("brk1", PriceBreak).Replace("\"committedBy\"", "\"left\": [\"L-1\"], \"committedBy\"");

        var blotter = Tables(entry, rows)[1];

        var row = Assert.Single(blotter.Rows);
        Assert.Equal("-", CellText.Of(Cell(blotter, row, "Impact")));
        Assert.Equal("Set up reference data (UnknownParty), then: Confirm execution price with counterparty", CellText.Of(Cell(blotter, row, "Suggested action")));
    }

    [Fact]
    public void A_break_whose_net_cash_is_missing_on_one_side_has_a_blank_impact_and_not_a_zero()
    {
        var entry = Break("brk1", PriceBreak).Replace("\"NetCash\": 5010", "\"Quantity\": 1");

        var blotter = Tables(entry)[1];

        var row = Assert.Single(blotter.Rows);
        Assert.Equal("", CellText.Of(Cell(blotter, row, "Impact")));
        Assert.Equal(CellValue.Blank.Instance, Cell(blotter, row, "Impact").Value);
    }

    [Fact]
    public void A_row_the_engine_left_without_an_entry_is_listed_as_unmatched_and_adds_the_rows_data_section()
    {
        const string rows =
            """{ "source": "left", "rowKey": "L-9", "disposition": "Quarantined", "validity": "Invalid", "diagnostics": ["BadDate"], "values": { "Counterparty": "UBS", "Currency": "CHF", "NetCash": -40, "SettleDate": "2026-03-29", "TradeDate": "2026-03-26" } }""";

        var document = Compose("BO1", Input(Body(Break("brk1", PriceBreak), rows)));

        var blotter = ReportTesting.Tables(document)[1];
        var row = Row(blotter, "2");
        Assert.Equal(["L-9", "Past settle date", "Unmatched ours", "UBS", "(excluded: invalid)", "(40.00)", "40.00", "Fix the input row: the engine excluded it as invalid (BadDate)"],
            new[] { "Entry", "Status", "Item type", "Counterparty", "Field", "Ours", "Impact", "Suggested action" }.Select(column => CellText.Of(Cell(blotter, row, column))));
        Assert.Equal(["Result entries", "Differences", "Result rows"], document.Blocks.OfType<DataBlock>().Select(data => data.Name));
    }

    [Fact]
    public void Without_orphan_rows_only_the_entry_and_difference_data_sections_follow_the_table()
    {
        var document = Compose("BO1", Input(Body(Mixed, "")));

        Assert.Equal(["Result entries", "Differences"], document.Blocks.OfType<DataBlock>().Select(data => data.Name));
        Assert.IsType<DataBlock>(document.Blocks[^1]);
    }

    [Fact]
    public void Age_is_business_days_from_the_trade_date_to_the_as_of_date_and_blank_without_a_trade_date()
    {
        var blotter = Tables(Mixed)[1];

        Assert.Equal(["2", "1", "", ""], Column(blotter, "Age (bus. days)"));
    }

    [Fact]
    public void The_report_is_never_unavailable_and_an_empty_run_gives_a_summary_that_still_ties()
    {
        var input = Input(Body("", ""));

        Assert.Null(Unavailable("BO1", input));
        var tables = ReportTesting.Tables(Compose("BO1", input));
        Assert.Empty(tables[1].Rows);
        Assert.Equal(["Total", "-", "Total", "-", "✓"], Texts(tables[0].Rows[^1]));
    }

    [Fact]
    public void The_sample_gives_one_line_per_open_item_with_counts_that_match_the_input()
    {
        var input = Sample();
        var asOf = input.Run.AsOf;
        var breaks = input.Entries.Where(entry => entry.Outcome == Outcome.Break).ToList();
        var whole = input.Entries.Where(entry => entry.Outcome != Outcome.Break && entry.Alignment != Alignment.Matched).ToList();
        var orphans = input.Rows.Where(row => row.EntryId is null && row.ItemLead && row.Disposition is Disposition.LeftOnly or Disposition.RightOnly or Disposition.Quarantined).ToList();
        var theirsOrphans = orphans.Count(row => row.Disposition == Disposition.RightOnly || row is { Disposition: Disposition.Quarantined, Source: SourceSide.Right });
        var settles = breaks.Select(entry => entry.Ours?[ReportRole.SettleDate].Date)
            .Concat(whole.Select(entry => entry.Alignment == Alignment.RightOnly ? entry.Theirs?[ReportRole.SettleDate].Date : (entry.Ours ?? entry.Theirs)?[ReportRole.SettleDate].Date))
            .Concat(orphans.Select(row => row.Values[ReportRole.SettleDate].Date))
            .ToList();
        static int Rank(string status) => status switch { "Past settle date" => 0, "Settles today" => 1, "Upcoming" => 2, _ => 3 };

        var tables = ReportTesting.Tables(Compose("BO1", input));

        var summary = tables[0];
        var blotter = tables[1];
        Assert.Equal(breaks.Count + whole.Count + orphans.Count, blotter.Rows.Count);
        Assert.Equal(
            [settles.Count(date => date < asOf), settles.Count(date => date == asOf), settles.Count(date => date > asOf), settles.Count(date => date is null)],
            new[] { "Past settle date", "Settles today", "Upcoming", "No settle date" }.Select(status => int.Parse(CellText.Of(summary.Rows.Single(row => CellText.Of(row.Cells[0]) == status).Cells[1]))));
        Assert.Equal(
            [breaks.Count, whole.Count(entry => entry.Alignment == Alignment.LeftOnly) + orphans.Count - theirsOrphans, whole.Count(entry => entry.Alignment == Alignment.RightOnly) + theirsOrphans, whole.Count(entry => entry.Alignment == Alignment.Ambiguous)],
            new[] { "Field break", "Unmatched ours", "Unmatched theirs", "Ambiguous" }.Select(type => Column(blotter, "Item type").Count(text => text == type)));
        Assert.Equal(Enumerable.Range(1, blotter.Rows.Count).Select(rank => rank.ToString()), Column(blotter, "Rank"));
        Assert.Equal(Column(blotter, "Status").Select(Rank).Order(), Column(blotter, "Status").Select(Rank));
    }
}
