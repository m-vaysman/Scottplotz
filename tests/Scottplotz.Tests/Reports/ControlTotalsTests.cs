using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class ControlTotalsTests
{
    private static string InputRow(string source, string key, string disposition, decimal? quantity, string currency = "USD", string? entry = null, bool lead = true, string counterparty = "BAML") =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "{{disposition}}", "itemLead": {{(lead ? "true" : "false")}}, {{(entry is null ? "" : $"\"entryId\": \"{entry}\",")}} "values": { "Counterparty": "{{counterparty}}", "Asset": "Bond", "Currency": "{{currency}}"{{(quantity is { } number ? $", \"Quantity\": {number}" : "")}} } }""";

    private static string Entry(string id, string alignment, string left, string right, string outcome = "Exact", string? diffs = null) =>
        $$"""{ "id": "{{id}}", "alignment": "{{alignment}}", "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "ours": { "Counterparty": "BAML" }, "theirs": { "Counterparty": "BAML" }, "committedBy": "P1"{{(diffs is null ? "" : $", \"diffs\": [ {diffs} ]")}} }""";

    private static ReportDocument Report(string entries, string rows) => Compose("MO1", Input(Body(entries, rows)));

    private static TableBlock Counts(ReportDocument document) => Tables(document)[0];

    private static TableBlock Quantities(ReportDocument document) => Tables(document)[1];

    private static TableBlock Splits(ReportDocument document) => Tables(document)[2];

    private static string Text(TableBlock table, ReportRow row, string column) => CellText.Of(Cell(table, row, column));

    private static ReportRow Line(TableBlock table, string currency, string asset = "Bond") =>
        table.Rows.Single(row => row.Kind == RowKind.Normal && Text(table, row, "Currency") == currency && Text(table, row, "Asset") == asset);

    private static readonly string[] Plain =
    [
        Entry("e1", "Matched", "\"L-1\"", "\"R-1\"", "Break"),
        Entry("e2", "LeftOnly", "\"L-2\"", ""),
        Entry("e3", "Ambiguous", "\"L-3\"", "")
    ];

    private static readonly string PlainRows = string.Join(", ",
        InputRow("left", "L-1", "Matched", 1000, entry: "e1"),
        InputRow("right", "R-1", "Matched", 900, entry: "e1"),
        InputRow("left", "L-2", "LeftOnly", 50, entry: "e2"),
        InputRow("left", "L-3", "Ambiguous", 7, entry: "e3"));

    [Fact]
    public void Row_counts_split_each_side_into_matched_unmatched_and_ambiguous_and_tie_to_the_files()
    {
        var table = Counts(Report(string.Join(", ", Plain), PlainRows));

        var row = Row(table, "BAML");
        Assert.Equal(
            ["Counterparty", "Asset", "Rows ours", "Rows theirs", "Matched ours", "Matched theirs", "Unmatched ours", "Unmatched theirs", "Ambiguous ours", "Ambiguous theirs", "Check"],
            Header(table));
        Assert.Equal(["BAML", "Bond", "3", "1", "1", "1", "1", "-", "1", "-", "✓"], Texts(row));
        Assert.True(row.Cells[0].Bold);
        var total = Row(table, "Total");
        Assert.Equal(RowKind.Total, total.Kind);
        Assert.Equal("✓", Text(table, total, "Check"));
    }

    [Fact]
    public void Other_columns_appear_only_when_a_row_is_suppressed_quarantined_or_search_incomplete()
    {
        var rows = string.Join(", ", PlainRows, InputRow("left", "L-4", "Quarantined", 5), InputRow("right", "R-4", "SearchIncomplete", 6), InputRow("left", "L-5", "Suppressed", 9));

        var table = Counts(Report(string.Join(", ", Plain), rows));

        Assert.Equal(["Other ours (suppressed, quarantined, search incomplete)", "Other theirs"], Header(table).Skip(10).Take(2));
        var row = Row(table, "BAML");
        Assert.Equal("5", Text(table, row, "Rows ours"));
        Assert.Equal("2", Text(table, row, "Other ours (suppressed, quarantined, search incomplete)"));
        Assert.Equal("1", Text(table, row, "Other theirs"));
        Assert.Equal("✓", Text(table, row, "Check"));
    }

    [Fact]
    public void A_row_in_a_state_its_side_cannot_have_fails_the_count_check_and_the_tie_out()
    {
        var document = Report("", InputRow("left", "L-1", "RightOnly", 1000));

        Assert.Equal("✗", Text(Counts(document), Row(Counts(document), "BAML"), "Check"));
        Assert.Equal("✗", Text(Quantities(document), Line(Quantities(document), "USD"), "Tie-out"));
        Assert.Equal(Tone.Fail, Cell(Quantities(document), Line(Quantities(document), "USD"), "Tie-out").Tone);
    }

    [Fact]
    public void A_currency_line_decomposes_the_difference_and_ties_the_engine_view_to_the_file_totals()
    {
        var table = Quantities(Report(string.Join(", ", Plain), PlainRows));

        var line = Line(table, "USD");
        Assert.Equal(
            ["Currency", "Counterparty", "Asset", "Ours", "Theirs", "Δ (theirs − ours)", "Unmatched ours", "Unmatched theirs", "Inside matched", "Check",
                "Inside matched: currency split", "Inside matched: field differences", "Ambiguous ours (not in totals)", "Ambiguous theirs (not in totals)",
                "File total ours", "File total theirs", "Tie-out"],
            Header(table));
        Assert.Equal(["USD", "BAML", "Bond", "1,050", "900", "(150)", "(50)", "-", "(100)", "✓", "-", "(100)", "7", "-", "1,057", "900", "✓"], Texts(line));
        Assert.Equal(Tone.Break, Cell(table, line, "Δ (theirs − ours)").Tone);
        Assert.Equal(Tone.None, Cell(table, line, "Inside matched: currency split").Tone);
    }

    [Fact]
    public void Each_currency_block_has_a_band_and_a_subtotal_that_sums_its_lines()
    {
        var rows = string.Join(", ", PlainRows, InputRow("left", "L-6", "LeftOnly", 20, counterparty: "DB", entry: "e6"));
        var entries = string.Join(", ", [.. Plain, Entry("e6", "LeftOnly", "\"L-6\"", "")]);

        var table = Quantities(Report(entries, rows));

        Assert.Equal("USD  ·  totals in USD only", CellText.Of(table.Rows[0].Cells[0]));
        Assert.Equal(RowKind.Band, table.Rows[0].Kind);
        Assert.Equal(table.Columns.Count, table.Rows[0].Cells[0].Span);
        var subtotal = table.Rows[^1];
        Assert.Equal(RowKind.Total, subtotal.Kind);
        Assert.Equal("Subtotal USD", Text(table, subtotal, "Counterparty"));
        Assert.Equal("1,070", Text(table, subtotal, "Ours"));
        Assert.Equal("(170)", Text(table, subtotal, "Δ (theirs − ours)"));
        Assert.Equal("1,077", Text(table, subtotal, "File total ours"));
        Assert.Equal(["✓", "✓"], new[] { Text(table, subtotal, "Check"), Text(table, subtotal, "Tie-out") });
        Assert.Equal("Quantities are summed within a currency only. Units differ by asset (face for bonds, shares for equity, notional for FX and swaps), so compare a line across its own row, not down the column.", table.Note);
    }

    [Fact]
    public void Currencies_follow_the_currency_order_and_leave_out_suppressed_rows_and_a_block_with_no_lines_still_shows()
    {
        var rows = string.Join(", ",
            InputRow("left", "L-1", "Quarantined", 5, "GBP"),
            InputRow("left", "L-2", "LeftOnly", 3, "EUR"),
            InputRow("left", "L-3", "LeftOnly", 4, "AAA"),
            InputRow("left", "L-4", "Suppressed", 1, "CHF"));
        var entries = string.Join(", ", Entry("a", "LeftOnly", "\"L-2\"", ""), Entry("b", "LeftOnly", "\"L-3\"", ""));

        var table = Quantities(Report(entries, rows));

        var bands = table.Rows.Where(row => row.Kind == RowKind.Band).Select(row => CellText.Of(row.Cells[0]));
        Assert.Equal(["EUR  ·  totals in EUR only", "GBP  ·  totals in GBP only", "AAA  ·  totals in AAA only"], bands);
        var empty = table.Rows.Single(row => row.Kind == RowKind.Total && Text(table, row, "Counterparty") == "Subtotal GBP");
        Assert.Equal(["✓", "✓"], new[] { Text(table, empty, "Check"), Text(table, empty, "Tie-out") });
    }

    [Fact]
    public void Add_back_columns_cover_suppressed_rows_and_package_legs_and_the_tie_out_still_holds()
    {
        var entries = Entry("e1", "Matched", "\"L-1\", \"L-2\"", "\"R-1\"");
        var rows = string.Join(", ",
            InputRow("left", "L-1", "Matched", 100, entry: "e1"),
            InputRow("left", "L-2", "Matched", 40, entry: "e1", lead: false),
            InputRow("right", "R-1", "Matched", 140, entry: "e1"),
            InputRow("left", "L-3", "Suppressed", 5));

        var table = Quantities(Report(entries, rows));

        Assert.Equal(
            ["Suppressed, quarantined, search incomplete ours (not in totals)", "Suppressed, quarantined, search incomplete theirs (not in totals)",
                "Package legs merged into one item ours (not in totals)", "Package legs merged into one item theirs (not in totals)"],
            Header(table).Skip(14).Take(4));
        var line = Line(table, "USD");
        Assert.Equal("100", Text(table, line, "Ours"));
        Assert.Equal("5", Text(table, line, "Suppressed, quarantined, search incomplete ours (not in totals)"));
        Assert.Equal("40", Text(table, line, "Package legs merged into one item ours (not in totals)"));
        Assert.Equal("-", Text(table, line, "Package legs merged into one item theirs (not in totals)"));
        Assert.Equal("145", Text(table, line, "File total ours"));
        Assert.Equal("✓", Text(table, line, "Tie-out"));
        Assert.Equal("✓", Text(table, table.Rows[^1], "Tie-out"));
    }

    [Fact]
    public void Package_legs_on_their_side_are_added_back_in_the_theirs_column_and_the_tie_out_holds()
    {
        var entries = Entry("e1", "Matched", "\"L-1\"", "\"R-1\", \"R-2\"");
        var rows = string.Join(", ",
            InputRow("left", "L-1", "Matched", 140, entry: "e1"),
            InputRow("right", "R-1", "Matched", 100, entry: "e1"),
            InputRow("right", "R-2", "Matched", 40, entry: "e1", lead: false));

        var table = Quantities(Report(entries, rows));
        var line = Line(table, "USD");

        Assert.Equal("-", Text(table, line, "Package legs merged into one item ours (not in totals)"));
        Assert.Equal("40", Text(table, line, "Package legs merged into one item theirs (not in totals)"));
        Assert.Equal(["100", "140", "✓"], [Text(table, line, "Theirs"), Text(table, line, "File total theirs"), Text(table, line, "Tie-out")]);
    }

    [Fact]
    public void A_non_zero_difference_and_currency_split_keep_their_tones_on_the_subtotal_row()
    {
        var table = Quantities(Report(string.Join(", ", Plain), PlainRows));

        var subtotal = table.Rows[^1];

        Assert.Equal(Tone.Break, Cell(table, subtotal, "Δ (theirs − ours)").Tone);
        Assert.Equal(Tone.None, Cell(table, subtotal, "Inside matched: currency split").Tone);
    }

    [Fact]
    public void A_pair_booked_in_two_currencies_puts_its_halves_in_two_blocks_and_is_listed_in_part_three()
    {
        var entries = Entry("e1aaaaaaaaaaaaaaaa", "Matched", "\"L-1\"", "\"R-1\"", "Break", """{ "field": "Currency", "role": "Currency", "type": "text", "outcome": "Break", "ours": "USD", "theirs": "EUR", "deltaKind": "None" }""");
        var rows = string.Join(", ", InputRow("left", "L-1", "Matched", 1000, entry: "e1aaaaaaaaaaaaaaaa"), InputRow("right", "R-1", "Matched", 900, "EUR", "e1aaaaaaaaaaaaaaaa"));

        var document = Report(entries, rows);

        var usd = Line(Quantities(document), "USD");
        Assert.Equal("(1,000)", Text(Quantities(document), usd, "Inside matched"));
        Assert.Equal("(1,000)", Text(Quantities(document), usd, "Inside matched: currency split"));
        Assert.Equal(Tone.Tolerance, Cell(Quantities(document), usd, "Inside matched: currency split").Tone);
        Assert.Equal("-", Text(Quantities(document), usd, "Inside matched: field differences"));
        var eur = Line(Quantities(document), "EUR");
        Assert.Equal("900", Text(Quantities(document), eur, "Inside matched: currency split"));
        var part3 = Splits(document);
        Assert.Equal(
            ["Entry", "Ours rows", "Theirs rows", "Counterparty", "Asset", "Ours currency", "Theirs currency", "Quantity ours", "Quantity theirs", "Matched by", "Outcome", "Break fields"],
            Header(part3));
        var row = Assert.Single(part3.Rows);
        Assert.Equal(["e1aaaaaaaaaa", "L-1", "R-1", "BAML", "Bond", "USD", "EUR", "1,000", "900", "P1", "Break", "Currency"], Texts(row));
        Assert.Equal([Tone.Alert, Tone.Alert], [row.Cells[5].Tone, row.Cells[6].Tone]);
        Assert.All(row.Cells.Skip(5).Take(2), cell => Assert.False(cell.Bold && cell.Tone == Tone.Break));
    }

    [Fact]
    public void The_reading_notes_name_columns_by_heading_and_change_with_what_the_run_contains()
    {
        var plain = Report(string.Join(", ", Plain), PlainRows).Blocks.OfType<NoteBlock>().Select(note => note.Text).ToList();
        var wide = Report(string.Join(", ", Plain), string.Join(", ", PlainRows, InputRow("left", "L-4", "Quarantined", 5))).Blocks.OfType<NoteBlock>().Select(note => note.Text).ToList();

        Assert.Equal("Reading the engine view:", plain[0]);
        Assert.Contains("\"Inside matched: currency split\" column", plain[1]);
        Assert.Contains("leaves Ambiguous rows out", plain[2]);
        Assert.Contains("from \"Ambiguous ours (not in totals)\" to \"File total theirs\"", plain[2]);
        Assert.Contains("Ambiguous, SearchIncomplete, Quarantined and Suppressed rows", wide[2]);
        Assert.All(plain.Concat(wide), note => Assert.DoesNotMatch(@"[Cc]olumns? [A-Q]\b", note));
    }

    [Fact]
    public void An_empty_run_gives_a_total_row_and_no_currency_blocks_and_the_data_sections_follow()
    {
        var document = Report("", "");

        Assert.Equal(["Total"], Counts(document).Rows.Select(row => CellText.Of(row.Cells[0])));
        Assert.Empty(Quantities(document).Rows);
        Assert.Empty(Splits(document).Rows);
        Assert.Equal(["Result rows", "Engine side-by-side"], document.Blocks.OfType<DataBlock>().Select(block => block.Name));
        Assert.All(Checks(document), check => Assert.Equal("✓", CellText.Of(check)));
    }

    [Fact]
    public void The_report_is_unavailable_without_a_mapped_currency()
    {
        var roles = ReportTesting.Roles.Replace("\"Currency\": \"Currency\", ", "");

        var message = Unavailable("MO1", Input(Body("", "", roles: roles)));

        Assert.Contains("Currency", message);
    }

    [Fact]
    public void The_sample_figures_match_a_count_made_straight_from_the_input()
    {
        var input = Sample();
        var document = Compose("MO1", input);
        var counts = Counts(document);
        var quantities = Quantities(document);
        var total = counts.Rows[^1];
        decimal Quantity(RowInput row) => row.Values[ReportRole.Quantity].Number ?? 0m;
        string Currency(RowInput row) => row.Values[ReportRole.Currency].Text;

        Assert.Equal(input.Rows.Count(row => row.Source == SourceSide.Left).ToString(), Text(counts, total, "Rows ours"));
        Assert.Equal(input.Rows.Count(row => row.Source == SourceSide.Right).ToString(), Text(counts, total, "Rows theirs"));
        Assert.Equal(
            input.Rows.Count(row => row.Source == SourceSide.Left && row.Disposition == Disposition.Matched).ToString(),
            Text(counts, total, "Matched ours"));
        var currencies = input.Rows.Where(row => row.Disposition != Disposition.Suppressed).Select(Currency).Where(code => code.Length > 0).Distinct().ToList();
        Assert.Equal(
            input.Options.CurrencyOrder.Where(currencies.Contains).Concat(currencies.Except(input.Options.CurrencyOrder).Order(StringComparer.Ordinal)).Select(code => $"{code}  ·  totals in {code} only"),
            quantities.Rows.Where(row => row.Kind == RowKind.Band).Select(row => CellText.Of(row.Cells[0])));
        var usd = quantities.Rows.Single(row => row.Kind == RowKind.Total && Text(quantities, row, "Counterparty") == "Subtotal USD");
        Assert.Equal(
            CellText.Number(input.Rows.Where(row => row.Source == SourceSide.Left && Currency(row) == "USD").Sum(Quantity), NumberFormat.Quantity),
            Text(quantities, usd, "File total ours"));
        var splitPairs = input.Entries.Count(entry =>
            entry.Alignment == Alignment.Matched
            && Currencies(input, entry.Left) is { Count: > 0 } ours
            && Currencies(input, entry.Right) is { Count: > 0 } theirs
            && !ours.SequenceEqual(theirs));
        Assert.Equal(splitPairs, Splits(document).Rows.Count);
        Assert.True(splitPairs > 0);
    }

    private static List<string> Currencies(ReportInput input, IReadOnlyList<string> keys) =>
        input.Rows.Where(row => keys.Contains(row.RowKey)).Select(row => row.Values[ReportRole.Currency].Text).Where(code => code.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList();
}
