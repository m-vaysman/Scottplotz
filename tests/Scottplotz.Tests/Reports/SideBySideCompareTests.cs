using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class SideBySideCompareTests
{
    private static string Entry(string id, string outcome, string diffs, string ours, string theirs, string pass = "P1", string left = "", string right = "") =>
        $$"""{ "id": "{{id}}", "pathId": "t", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "ours": {{ours}}, "theirs": {{theirs}}, "committedBy": "{{pass}}", "diffs": [ {{diffs}} ] }""";

    private static string Diff(string field, string role, string outcome, string ours, string theirs, string delta, string attributed = "") =>
        $$"""{ "field": "{{field}}", "role": {{role}}, "type": "{{(ours.StartsWith('"') ? "text" : "decimal")}}", "outcome": "{{outcome}}", "ours": {{ours}}, "theirs": {{theirs}}, "deltaKind": "Number", "delta": {{delta}}, "attributedTo": [{{attributed}}] }""";

    private static string Side(string extra, string currency = "USD") =>
        $$"""{ "Counterparty": "BAML", "Instrument": "BUND", "Side": "BUY", "Currency": "{{currency}}"{{extra}} }""";

    private static readonly string CurrencyDiff = Diff("Currency", "\"Currency\"", "Break", "\"USD\"", "\"EUR\"", "null");

    private static readonly string Entries = string.Join(
        ",",
        Entry("b-tol", "WithinTolerance", Diff("Fees", "\"Fees\"", "WithinTolerance", "10", "10.5", "0.5"), Side(""", "Fees": 10"""), Side(""", "Fees": 10.5""")),
        Entry(
            "a-break",
            "Break",
            string.Join(
                ",",
                Diff("Price", "\"Price\"", "Break", "99", "98", "-1"),
                Diff("Quantity", "\"Quantity\"", "WithinTolerance", "1000", "1001", "1"),
                Diff("NetCash", "\"NetCash\"", "Break", "5000", "5100", "100", "\"Price\""),
                Diff("MaturityDate", "null", "Break", "\"a\"", "\"b\"", "null")),
            Side(""", "Price": 99, "Quantity": 1000, "NetCash": 5000"""),
            Side(""", "Price": 98, "Quantity": 1001, "NetCash": 5100"""),
            "P2"),
        Entry("c-exact", "Exact", "", Side(""", "Price": 1"""), Side(""", "Price": 1""")),
        Entry("d-ccy", "Break", CurrencyDiff, Side(""", "NetCash": 7"""), Side(""", "NetCash": 9""", "EUR"), "P3", "\"L1\"", "\"R1\""));

    private const string Rows =
        """{ "source": "left", "rowKey": "L1", "disposition": "Matched", "entryId": "d-ccy", "values": { "Currency": "USD" } }, { "source": "right", "rowKey": "R1", "disposition": "Matched", "entryId": "d-ccy", "values": { "Currency": "EUR" } }""";

    private const string DeltaTitle = "Δ NetCash (theirs − ours)";

    private static ReportDocument Build(string? entries = null, string? rows = null, string? roles = null, string options = "{}") =>
        Compose("BO2", Input(Body(entries ?? Entries, rows ?? Rows, options, roles: roles ?? ReportTesting.Roles)));

    private static TableBlock MainTable(ReportDocument document) => Tables(document)[0];

    private static TableBlock Splits(ReportDocument document) => Tables(document)[1];

    private static string Text(TableBlock table, string entry, string column) => CellText.Of(Cell(table, Row(table, entry), column));

    [Fact]
    public void The_main_table_lists_non_exact_matched_entries_with_breaks_first_then_by_entry_id()
    {
        Assert.Equal(["a-break", "b-tol"], MainTable(Build()).Rows.Select(row => CellText.Of(row.Cells[0])));
    }

    [Fact]
    public void An_entry_whose_only_difference_is_currency_stays_out_of_the_main_table_and_is_listed_as_a_currency_pair()
    {
        var document = Build();

        Assert.DoesNotContain(MainTable(document).Rows, row => CellText.Of(row.Cells[0]) == "d-ccy");
        var split = Assert.Single(Splits(document).Rows);
        Assert.Equal(["d-ccy", "BAML", "BUND", "USD", "EUR", "Break", "P3", ""], Texts(split));
        Assert.Equal(Tone.Break, split.Cells[3].Tone);
        Assert.Equal(Tone.Break, split.Cells[4].Tone);
    }

    [Fact]
    public void The_currency_table_says_none_when_no_pair_has_different_currencies()
    {
        var table = Splits(Build(Entries.Replace(CurrencyDiff, Diff("Price", "\"Price\"", "Break", "1", "2", "1")).Replace("\"EUR\"", "\"USD\""), Rows.Replace("EUR", "USD")));

        Assert.Empty(table.Rows);
        Assert.Equal("None in this run.", table.Empty);
    }

    [Fact]
    public void The_header_groups_each_compared_field_over_ours_and_theirs()
    {
        var table = MainTable(Build());

        Assert.Equal(
            ["Entry", "Counterparty", "Instrument", "Side", "Ccy", "Outcome", "Pass", "Ours", "Theirs", "Ours", "Theirs", "Ours", "Theirs", "Ours", "Theirs", "Ours", "Theirs", "Ours", "Theirs", DeltaTitle, "Other differences"],
            Header(table));
        Assert.Equal(["", "Quantity", "Price", "SettleDate", "Accrued", "Fees", "NetCash", ""], table.Groups!.Select(group => group.Title));
        Assert.Equal([7, 2, 2, 2, 2, 2, 2, 2], table.Groups!.Select(group => group.Span));
        Assert.Equal(table.Columns.Count, table.Groups!.Sum(group => group.Span));
    }

    [Fact]
    public void Only_mapped_compared_fields_get_columns_and_the_delta_needs_net_cash()
    {
        var roles = """{ "Counterparty": "Counterparty", "Instrument": "Instrument", "Side": "Side", "Currency": "Currency", "Price": "Px" }""";
        var table = MainTable(Build(roles: roles));

        Assert.Equal(["Entry", "Counterparty", "Instrument", "Side", "Ccy", "Outcome", "Pass", "Ours", "Theirs", "Other differences"], Header(table));
        Assert.Equal(["", "Px", ""], table.Groups!.Select(group => group.Title));
        Assert.Equal(table.Columns.Count, table.Groups!.Sum(group => group.Span));
    }

    [Fact]
    public void The_theirs_cell_is_coloured_by_the_state_of_its_difference_and_nothing_else_is()
    {
        var table = MainTable(Build());
        var row = Row(table, "a-break");
        var toned = row.Cells.Select((cell, index) => (cell.Tone, index)).Where(pair => pair.Tone != Tone.None).ToList();

        Assert.Equal([(Tone.Break, 5), (Tone.Tolerance, 8), (Tone.Break, 10), (Tone.Attributed, 18)], toned);
        Assert.Equal(["Quantity", "Price", "NetCash"], new[] { table.Groups![1], table.Groups![2], table.Groups![6] }.Select(group => group.Title));
    }

    [Fact]
    public void Outcome_is_red_for_a_break_and_amber_otherwise()
    {
        var table = MainTable(Build());

        Assert.Equal(Tone.Break, Cell(table, Row(table, "a-break"), "Outcome").Tone);
        Assert.Equal(Tone.Tolerance, Cell(table, Row(table, "b-tol"), "Outcome").Tone);
        Assert.Equal("P2", Text(table, "a-break", "Pass"));
    }

    [Fact]
    public void Values_show_in_each_fields_format_and_a_missing_value_is_blank()
    {
        var row = Row(MainTable(Build()), "a-break");

        Assert.Equal(["1,000", "1,001", "99", "98", "", "", "", "", "", "", "5,000.00", "5,100.00"], Texts(row).Skip(7).Take(12));
    }

    [Fact]
    public void The_net_cash_delta_is_theirs_minus_ours_and_a_missing_side_counts_as_zero()
    {
        var table = MainTable(Build());

        Assert.Equal("100.00", Text(table, "a-break", DeltaTitle));
        Assert.True(Cell(table, Row(table, "a-break"), DeltaTitle).Bold);
        Assert.Equal("-", Text(table, "b-tol", DeltaTitle));
        var one = Entry("x", "Break", Diff("NetCash", "\"NetCash\"", "Break", "null", "250", "250"), Side(""), Side(""", "NetCash": 250"""));
        Assert.Equal("250.00", Text(MainTable(Build(one, "")), "x", DeltaTitle));
    }

    [Fact]
    public void Other_differences_name_diff_fields_that_are_not_shown_columns()
    {
        var table = MainTable(Build());

        Assert.Equal("MaturityDate", Text(table, "a-break", "Other differences"));
        Assert.Equal("", Text(table, "b-tol", "Other differences"));
    }

    [Fact]
    public void A_pair_with_a_currency_difference_and_another_difference_appears_in_both_tables()
    {
        var entries = Entry("p", "Break", CurrencyDiff + "," + Diff("MaturityDate", "null", "Break", "\"a\"", "\"b\"", "null"), Side(""), Side("", "EUR"));
        var document = Build(entries, "");

        Assert.Equal("MaturityDate", Texts(Assert.Single(Splits(document).Rows))[7]);
        Assert.Equal("MaturityDate", Text(MainTable(document), "p", "Other differences"));
    }

    [Fact]
    public void The_report_is_unavailable_when_no_compared_field_is_mapped_or_a_required_role_is_not()
    {
        var none = """{ "Counterparty": "Counterparty", "Instrument": "Instrument", "Side": "Side", "Currency": "Currency" }""";
        var noSide = """{ "Counterparty": "C", "Instrument": "I", "Currency": "Ccy", "Price": "Px" }""";

        Assert.Contains("Map at least one of Quantity, Price, SettleDate, Accrued, Fees, NetCash in run.roles.", Unavailable("BO2", Input(Body(Entries, Rows, roles: none))));
        Assert.Contains("Unmapped report roles: Side", Unavailable("BO2", Input(Body(Entries, Rows, roles: noSide))));
    }

    [Fact]
    public void Headings_and_column_titles_follow_the_label_options()
    {
        var document = Build(options: """{ "labels": { "Entry": "Trade", "Ours": "Us", "Matched pairs whose two sides carry different currencies": "Currency pairs" } }""");

        Assert.Equal("Trade", Header(MainTable(document))[0]);
        Assert.Equal("Us", Header(MainTable(document))[7]);
        Assert.Equal("Currency pairs", Splits(document).Title);
    }

    [Fact]
    public void A_colour_key_comes_first_and_names_the_three_tones_the_theirs_cells_use()
    {
        var key = Assert.IsType<LegendBlock>(Build(options: """{ "labels": { "Root break": "Real break" } }""").Blocks[0]);

        Assert.Equal([("Real break", Tone.Break), ("Within tolerance", Tone.Tolerance), ("Consequence of another field", Tone.Attributed)], key.Items.Select(item => (item.Label, item.Tone)));
    }

    [Fact]
    public void An_entry_id_shorter_than_twelve_characters_shows_whole_with_special_characters_intact()
    {
        var entries = Entry("a&b", "Break", Diff("Price", "\"Price\"", "Break", "1", "2", "1"), Side(""", "Price": 1""").Replace("BAML", "<B>"), Side(""", "Price": 2"""));

        var table = MainTable(Build(entries, ""));

        Assert.Equal(["a&b", "<B>"], Texts(Assert.Single(table.Rows)).Take(2));
    }

    [Fact]
    public void The_page_ends_with_the_entries_data_block()
    {
        Assert.Equal("Result entries", Assert.IsType<DataBlock>(Build().Blocks[^1]).Name);
    }

    [Fact]
    public void On_the_sample_the_table_holds_the_non_exact_matched_entries_and_the_currency_pairs()
    {
        var input = Sample();
        var currency = input.Run.Field(ReportRole.Currency);
        var expected = input.Entries
            .Where(entry => entry.Alignment == Alignment.Matched && entry.Outcome != Outcome.Exact && entry.Diffs.Any(diff => diff.Field != currency))
            .OrderBy(entry => entry.Outcome == Outcome.Break ? 0 : 1)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
        var expectedSplits = input.Entries.Where(entry => entry.Diffs.Any(diff => diff.Field == currency)).Select(entry => entry.Id[..12]).Order(StringComparer.Ordinal).ToList();
        var document = Compose("BO2", input);
        var main = MainTable(document);

        Assert.Equal(expected.Select(entry => entry.Id[..12]), main.Rows.Select(row => CellText.Of(row.Cells[0])));
        Assert.Equal(expectedSplits, Splits(document).Rows.Select(row => CellText.Of(row.Cells[0])).Order(StringComparer.Ordinal));
        Assert.Equal(expected.Count(entry => entry.Outcome == Outcome.Break), main.Rows.Count(row => row.Cells[5].Tone == Tone.Break));
        var first = expected[0];
        var net = (first.Theirs!.Values[ReportRole.NetCash].Number ?? 0m) - (first.Ours!.Values[ReportRole.NetCash].Number ?? 0m);
        Assert.Equal(CellText.Number(net, NumberFormat.Amount), Text(main, first.Id[..12], DeltaTitle));
    }
}
