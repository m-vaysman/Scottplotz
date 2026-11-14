using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class CashLadderTests
{
    private const string Matched =
        """{ "id": "e1", "pathId": "trades", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "Break", "left": ["L-2"], "right": ["R-2"], "ours": { "Currency": "USD" }, "theirs": { "Currency": "USD" }, "committedBy": "P1" }""";

    private static string Row(string source, string key, string disposition, string side, string cash, string settle = "2026-04-01", string currency = "USD", string extra = "") =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "{{disposition}}", "values": { "Side": "{{side}}", "NetCash": {{cash}}, "SettleDate": {{(settle == "null" ? "null" : $"\"{settle}\"")}}, "Currency": "{{currency}}" }{{extra}} }""";

    private static string Rows(params string[] rows) => string.Join(", ", rows);

    private static (TableBlock Table, ReportRow Row) Ladder(string rows, string entries = "", string options = "{}", string? date = null, string? currency = null)
    {
        var table = Tables(Compose("BO6", Input(Body(entries, rows, options))))[0];
        return (table, date is null ? table.Rows[0] : table.Rows.Single(row => Texts(row)[0] == date && Texts(row)[1] == currency));
    }

    private static string Text(TableBlock table, ReportRow row, string column) => CellText.Of(Cell(table, row, column));

    [Fact]
    public void A_line_splits_the_difference_into_unmatched_ours_unmatched_theirs_and_matched_changes()
    {
        var (table, row) = Ladder(
            Rows(
                Row("left", "L-1", "LeftOnly", "SELL", "100"),
                Row("right", "R-1", "RightOnly", "BUY", "40"),
                Row("left", "L-2", "Matched", "SELL", "500", extra: """, "entryId": "e1" """),
                Row("right", "R-2", "Matched", "SELL", "450", extra: """, "entryId": "e1" """)),
            Matched);

        Assert.Equal(["2026-04-01", "USD", "2", "600.00", "410.00", "(190.00)", "(100.00)", "(40.00)", "(50.00)", "-", "✓"], Texts(row));
        Assert.Equal(["Settle date", "Ccy", "Items (ours)", "Ours: expected cash", "Theirs: expected cash", "Difference", "Unmatched ours", "Unmatched theirs", "Inside matched entries", "Unresolved (ambiguous, search incomplete)", "Check"], Header(table));
    }

    [Fact]
    public void Paying_sides_carry_cash_out_and_a_row_without_a_side_carries_none()
    {
        var (table, row) = Ladder(
            Rows(
                Row("left", "L-1", "LeftOnly", "BUY", "100"),
                Row("left", "L-2", "LeftOnly", "PAY_FIXED", "10"),
                Row("left", "L-3", "LeftOnly", "SELL", "7"),
                Row("left", "L-4", "LeftOnly", "", "1000")));

        Assert.Equal("(103.00)", Text(table, row, "Ours: expected cash"));
        Assert.Equal("4", Text(table, row, "Items (ours)"));
        Assert.Equal("103.00", Text(table, row, "Unmatched ours"));
    }

    [Fact]
    public void The_paying_sides_option_changes_which_rows_carry_cash_out()
    {
        var (table, row) = Ladder(Rows(Row("left", "L-1", "LeftOnly", "SELL", "100"), Row("left", "L-2", "LeftOnly", "BUY", "30")), options: """{ "payingSides": ["SELL"] }""");

        Assert.Equal("(70.00)", Text(table, row, "Ours: expected cash"));
    }

    [Fact]
    public void Unresolved_rows_count_theirs_ambiguous_and_search_incomplete_less_ours()
    {
        var (table, row) = Ladder(
            Rows(
                Row("left", "L-1", "Ambiguous", "SELL", "70"),
                Row("left", "L-2", "SearchIncomplete", "SELL", "5"),
                Row("right", "R-1", "SearchIncomplete", "SELL", "20")));

        Assert.Equal("(55.00)", Text(table, row, "Unresolved (ambiguous, search incomplete)"));
        Assert.Equal("(55.00)", Text(table, row, "Difference"));
        Assert.Equal("✓", Text(table, row, "Check"));
    }

    [Fact]
    public void Suppressed_quarantined_and_non_lead_rows_and_rows_without_a_date_or_currency_are_left_out()
    {
        var table = Tables(Compose("BO6", Input(Body("", Rows(
            Row("left", "L-1", "LeftOnly", "SELL", "100"),
            Row("left", "L-2", "Suppressed", "SELL", "900"),
            Row("left", "L-3", "Quarantined", "SELL", "800"),
            Row("left", "L-4", "LeftOnly", "SELL", "700", extra: ", \"itemLead\": false"),
            Row("left", "L-5", "LeftOnly", "SELL", "600", settle: "null"),
            Row("left", "L-6", "LeftOnly", "SELL", "500", currency: ""))))))[0];

        var row = Assert.Single(table.Rows);
        Assert.Equal("100.00", Text(table, row, "Ours: expected cash"));
        Assert.Equal("1", Text(table, row, "Items (ours)"));
    }

    [Fact]
    public void Lines_run_by_settle_date_then_currency_order_then_currency_text()
    {
        var table = Tables(Compose("BO6", Input(Body("", Rows(
            Row("left", "L-1", "LeftOnly", "SELL", "1", "2026-04-02", "USD"),
            Row("left", "L-2", "LeftOnly", "SELL", "1", "2026-04-01", "SEK"),
            Row("left", "L-3", "LeftOnly", "SELL", "1", "2026-04-01", "EUR"),
            Row("left", "L-4", "LeftOnly", "SELL", "1", "2026-04-01", "AUD"),
            Row("left", "L-5", "LeftOnly", "SELL", "1", "2026-04-01", "USD"))))))[0];

        Assert.Equal(
            ["2026-04-01 USD", "2026-04-01 EUR", "2026-04-01 AUD", "2026-04-01 SEK", "2026-04-02 USD"],
            table.Rows.Select(row => $"{Texts(row)[0]} {Texts(row)[1]}"));
    }

    [Fact]
    public void A_settle_date_before_the_as_of_date_is_muted_and_a_non_zero_difference_is_a_break()
    {
        var table = Tables(Compose("BO6", Input(Body(Matched, Rows(
            Row("left", "L-1", "LeftOnly", "SELL", "100", "2026-03-30"),
            Row("left", "L-2", "Matched", "SELL", "50", "2026-03-31", extra: """, "entryId": "e1" """),
            Row("right", "R-2", "Matched", "SELL", "50", "2026-03-31", extra: """, "entryId": "e1" """),
            Row("right", "R-3", "RightOnly", "SELL", "0.004", "2026-04-01"))))))[0];

        Assert.Equal([Tone.Muted, Tone.None, Tone.None], table.Rows.Select(row => row.Cells[0].Tone));
        Assert.Equal([Tone.Break, Tone.None, Tone.None], table.Rows.Select(row => Cell(table, row, "Difference").Tone));
        Assert.All(table.Rows, row => Assert.True(Cell(table, row, "Difference").Bold));
    }

    [Fact]
    public void Totals_add_each_currency_across_dates_and_never_across_currencies()
    {
        var table = Tables(Compose("BO6", Input(Body("", Rows(
            Row("left", "L-1", "LeftOnly", "SELL", "100", "2026-04-01", "USD"),
            Row("left", "L-2", "LeftOnly", "SELL", "30", "2026-04-02", "USD"),
            Row("right", "R-1", "RightOnly", "SELL", "5", "2026-04-02", "USD"),
            Row("left", "L-3", "LeftOnly", "SELL", "9", "2026-04-01", "EUR"))))))[1];

        Assert.Equal("Totals by currency (never added across currencies)", table.Title);
        Assert.Equal(["Ccy", "", "Items (ours)"], Header(table).Take(3));
        Assert.Equal(["USD", "", "2", "130.00", "5.00", "(125.00)", "(130.00)", "5.00", "-", "-", "✓"], Texts(table.Rows[0]));
        Assert.Equal(["EUR", "", "1", "9.00", "-", "(9.00)", "(9.00)", "-", "-", "-", "✓"], Texts(table.Rows[1]));
        Assert.True(table.Rows[0].Cells[0].Bold);
    }

    [Fact]
    public void The_check_fails_when_the_difference_has_a_cause_the_split_does_not_cover()
    {
        var (table, row) = Ladder(Rows(Row("right", "R-1", "LeftOnly", "SELL", "25")));

        Assert.Equal("✗", Text(table, row, "Check"));
        Assert.Equal(Tone.Fail, Cell(table, row, "Check").Tone);
    }

    [Fact]
    public void Matched_pairs_with_different_currencies_are_listed_and_otherwise_none_is_said()
    {
        const string split =
            """{ "id": "e1234567890123", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "Break", "left": ["L-2"], "right": ["R-2"], "ours": { "Currency": "GBP" }, "theirs": { "Currency": "USD" } }""";
        var rows = Rows(
            Row("left", "L-2", "Matched", "SELL", "5", currency: "GBP", extra: """, "entryId": "e1234567890123" """),
            Row("right", "R-2", "Matched", "SELL", "5", currency: "USD", extra: """, "entryId": "e1234567890123" """));

        var with = Tables(Compose("BO6", Input(Body(split, rows))))[2];
        var without = Tables(Compose("BO6", Input(Body("", Rows(Row("left", "L-1", "LeftOnly", "SELL", "1"))))))[2];

        Assert.Equal(["Entry", "Ours rows", "Theirs rows", "Ours currency", "Theirs currency", "Outcome"], Header(with));
        var row = Assert.Single(with.Rows);
        Assert.Equal(["e12345678901", "L-2", "R-2", "GBP", "USD", "Break"], Texts(row));
        Assert.Equal([Tone.Break, Tone.Break], [row.Cells[3].Tone, row.Cells[4].Tone]);
        Assert.Empty(without.Rows);
        Assert.Equal("None in this run.", without.Empty);
    }

    [Fact]
    public void The_report_is_unavailable_without_a_side_role_and_ends_with_the_result_rows()
    {
        var withoutSide = ReportTesting.Roles.Replace("\"Side\": \"Side\", ", "");

        var skipped = Unavailable("BO6", Input(Body("", "", roles: withoutSide)));
        var document = Compose("BO6", Input(Body("", Rows(Row("left", "L-1", "LeftOnly", "SELL", "1")))));

        Assert.Contains("Side", skipped);
        Assert.Equal(["NoteBlock", "TableBlock", "TableBlock", "TableBlock", "DataBlock"], document.Blocks.Select(block => block.GetType().Name));
        Assert.Equal("Result rows", ((DataBlock)document.Blocks[^1]).Name);
    }

    [Fact]
    public void The_sample_ladder_matches_figures_worked_out_from_the_input()
    {
        var input = Sample();
        var paying = input.Options.PayingSides;
        var rows = input.Rows
            .Where(row => row.ItemLead && row.Disposition is not (Disposition.Suppressed or Disposition.Quarantined))
            .Where(row => row.Values[ReportRole.SettleDate].Date is not null && row.Values[ReportRole.Currency].Text.Length > 0)
            .ToList();
        decimal Signed(RowInput row) =>
            (row.Values[ReportRole.NetCash].Number ?? 0m) * (paying.Contains(row.Values[ReportRole.Side].Text) ? -1 : row.Values[ReportRole.Side].Text.Length > 0 ? 1 : 0);
        var lines = rows.Select(row => (row.Values[ReportRole.SettleDate].Date, row.Values[ReportRole.Currency].Text)).Distinct().ToList();
        var usd = rows.Where(row => row.Values[ReportRole.Currency].Text == "USD").ToList();
        var usdOurs = usd.Where(row => row.Source == SourceSide.Left).Sum(Signed);
        var usdTheirs = usd.Where(row => row.Source == SourceSide.Right).Sum(Signed);

        var tables = Tables(Compose("BO6", input));
        var totalUsd = tables[1].Rows.Single(row => Texts(row)[0] == "USD");

        Assert.Equal(lines.Count, tables[0].Rows.Count);
        Assert.Equal(CellText.Number(usdOurs, NumberFormat.Amount), Text(tables[1], totalUsd, "Ours: expected cash"));
        Assert.Equal(CellText.Number(usdTheirs - usdOurs, NumberFormat.Amount), Text(tables[1], totalUsd, "Difference"));
        Assert.Equal(usd.Count(row => row.Source == SourceSide.Left).ToString(), Text(tables[1], totalUsd, "Items (ours)"));
        Assert.Equal(
            lines.Select(line => line.Text).Distinct().Count(),
            tables[1].Rows.Count);
        Assert.Equal(input.Entries.Count(entry => entry.Alignment == Alignment.Matched && entry.Left.Count > 0 && entry.Right.Count > 0 && CurrencySplit(input, entry)), tables[2].Rows.Count);
    }

    private static bool CurrencySplit(ReportInput input, EntryInput entry)
    {
        IEnumerable<string> Currencies(IEnumerable<string> keys, SourceSide side) =>
            input.Rows.Where(row => row.Source == side && keys.Contains(row.RowKey)).Select(row => row.Values[ReportRole.Currency].Text).Where(text => text.Length > 0).Distinct().Order(StringComparer.Ordinal);
        var ours = Currencies(entry.Left, SourceSide.Left).ToList();
        var theirs = Currencies(entry.Right, SourceSide.Right).ToList();
        return ours.Count > 0 && theirs.Count > 0 && !ours.SequenceEqual(theirs);
    }
}
