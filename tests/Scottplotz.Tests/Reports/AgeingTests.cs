using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class AgeingTests
{
    // The as-of date 2026-03-31 is a Tuesday.
    private static string Open(string id, string? trade, string? settle, string alignment = "LeftOnly", string counterparty = "BAML")
    {
        var dates = string.Concat(
            trade is null ? "" : $", \"TradeDate\": \"{trade}\"",
            settle is null ? "" : $", \"SettleDate\": \"{settle}\"");
        return $$"""{ "id": "{{id}}", "alignment": "{{alignment}}", "outcome": "NotCompared", "ours": { "Counterparty": "{{counterparty}}"{{dates}} }, "theirs": null }""";
    }

    private static string Break(string id, string field, string role, string trade, string settle) =>
        $$"""{ "id": "{{id}}", "alignment": "Matched", "outcome": "Break", "ours": { "Counterparty": "BAML", "TradeDate": "{{trade}}", "SettleDate": "{{settle}}" }, "theirs": { "TradeDate": "{{trade}}", "SettleDate": "{{settle}}" }, "diffs": [ { "field": "{{field}}", "role": "{{role}}", "type": "decimal", "outcome": "Break", "ours": 1, "theirs": 2, "deltaKind": "Number", "delta": 1 } ] }""";

    private static TableBlock Ageing(string entries, string options = "{}", string roles = ReportTesting.Roles) =>
        Assert.Single(Tables(Compose("MO5", Input(Body(entries, "", options, roles: roles)))));

    private static string Entries(params string[] entries) => string.Join(", ", entries);

    private static ReportRow Line(TableBlock table, string category) => Row(table, category);

    private static string Text(TableBlock table, string category, string column) => CellText.Of(Cell(table, Line(table, category), column));

    private static readonly string Ages = Entries(
        Open("a", "2026-03-31", "2026-03-31"),
        Open("b", "2026-03-30", "2026-03-31"),
        Open("c", "2026-03-27", "2026-03-31"),
        Open("d", "2026-03-26", "2026-03-31"),
        Open("e", "2026-03-25", "2026-03-31"),
        Open("f", "2026-01-02", "2026-03-31"));

    [Fact]
    public void The_columns_are_the_category_five_age_buckets_four_statuses_the_item_count_and_the_check()
    {
        var table = Ageing(Ages);

        Assert.Equal(
            ["Category", "Age 0 or less", "Age 1 bus. day", "Age 2 bus. days", "Age 3 bus. days", "Age 4+ bus. days", "Past settle date", "Settles today", "Upcoming", "No settle date", "Items", "Check"],
            Header(table));
    }

    [Fact]
    public void Age_is_counted_in_business_days_from_the_trade_date_to_the_as_of_date_and_falls_into_its_bucket()
    {
        var table = Ageing(Ages);

        Assert.Equal(["1", "1", "1", "1", "2", "6"], new[] { "Age 0 or less", "Age 1 bus. day", "Age 2 bus. days", "Age 3 bus. days", "Age 4+ bus. days", "Items" }.Select(column => Text(table, "Unmatched ours", column)));
    }

    [Fact]
    public void A_trade_date_after_the_as_of_date_and_a_weekend_trade_date_age_by_weekdays_only()
    {
        var entries = Entries(
            Open("future", "2026-04-02", "2026-04-03"),
            Open("weekend", "2026-03-28", "2026-03-31"),
            Open("sunday", "2026-03-29", "2026-03-31"));

        var table = Ageing(entries);

        Assert.Equal("1", Text(table, "Unmatched ours", "Age 0 or less"));
        Assert.Equal("2", Text(table, "Unmatched ours", "Age 1 bus. day"));
    }

    [Fact]
    public void Status_compares_the_settle_date_with_the_as_of_date()
    {
        var entries = Entries(
            Open("p1", "2026-03-30", "2026-03-30"),
            Open("p2", "2026-03-30", "2026-03-27"),
            Open("t1", "2026-03-30", "2026-03-31"),
            Open("u1", "2026-03-30", "2026-04-01"),
            Open("u2", "2026-03-30", "2026-04-15"),
            Open("u3", "2026-03-30", "2026-04-20"),
            Open("n1", "2026-03-30", null));

        var table = Ageing(entries);

        var line = Line(table, "Unmatched ours");
        Assert.Equal(["2", "1", "3", "1", "7"], new[] { "Past settle date", "Settles today", "Upcoming", "No settle date", "Items" }.Select(column => CellText.Of(Cell(table, line, column))));
    }

    [Fact]
    public void Only_a_past_settle_date_count_above_zero_is_marked_as_a_break()
    {
        var entries = Entries(Open("p", "2026-03-30", "2026-03-30"), Open("t", "2026-03-30", "2026-03-31"));

        var table = Ageing(entries);

        Assert.Equal(Tone.Break, Cell(table, Line(table, "Unmatched ours"), "Past settle date").Tone);
        Assert.Equal(Tone.None, Cell(table, Line(table, "Unmatched theirs"), "Past settle date").Tone);
        Assert.Equal(Tone.None, Cell(table, Line(table, "Unmatched ours"), "Settles today").Tone);
        Assert.Equal(Tone.None, Cell(table, table.Rows[^1], "Past settle date").Tone);
        Assert.Equal(Tone.None, Cell(table, Line(table, "Unmatched ours"), "Age 1 bus. day").Tone);
    }

    [Fact]
    public void An_item_without_a_trade_date_is_in_no_bucket_and_the_row_still_ties_to_its_item_count()
    {
        var entries = Entries(Open("a", "2026-03-30", "2026-03-31"), Open("b", null, "2026-03-31"));

        var table = Ageing(entries);

        var line = Line(table, "Unmatched ours");
        Assert.Equal(["-", "1", "-", "-", "-"], new[] { "Age 0 or less", "Age 1 bus. day", "Age 2 bus. days", "Age 3 bus. days", "Age 4+ bus. days" }.Select(column => CellText.Of(Cell(table, line, column))));
        Assert.Equal("2", CellText.Of(Cell(table, line, "Items")));
        Assert.Equal(("✓", Tone.Ok), (CellText.Of(Cell(table, line, "Check")), Cell(table, line, "Check").Tone));
    }

    [Fact]
    public void Categories_follow_the_heatmap_order_with_unmatched_ours_and_theirs_always_present()
    {
        var entries = Entries(
            Break("b1", "Quantity", "Quantity", "2026-03-30", "2026-03-31"),
            Break("b2", "Price", "Price", "2026-03-30", "2026-03-31"),
            Open("a1", "2026-03-30", "2026-03-31", "Ambiguous"));

        var table = Ageing(entries);

        Assert.Equal(["Price", "Quantity", "Unmatched ours", "Unmatched theirs", "Ambiguous", "Total"], table.Rows.Select(row => Texts(row)[0]));
        Assert.Equal(["1", "1", "0", "0", "1", "3"], table.Rows.Select(row => Texts(row)[10].Replace("-", "0")));
    }

    [Fact]
    public void Each_bucket_cell_is_shaded_on_the_violet_scale_by_its_share_of_the_largest_bucket_and_other_cells_are_not()
    {
        var entries = Entries(
            Open("a", "2026-03-30", "2026-03-31"),
            Open("b", "2026-03-30", "2026-03-31"),
            Open("c", "2026-03-30", "2026-03-31"),
            Open("d", "2026-03-30", "2026-03-31"),
            Open("e", "2026-03-27", "2026-03-31"),
            Open("f", "2026-03-27", "2026-03-31"));

        var table = Ageing(entries);

        var line = Line(table, "Unmatched ours");
        var heats = new[] { "Age 0 or less", "Age 1 bus. day", "Age 2 bus. days", "Age 3 bus. days", "Age 4+ bus. days" }.Select(column => Cell(table, line, column)).ToList();
        Assert.Equal([0.0, 1.0, 0.5, 0.0, 0.0], heats.Select(cell => cell.Heat));
        Assert.All(heats, cell => Assert.Equal(HeatColour.Violet, cell.HeatColour));
        Assert.All(new[] { "Upcoming", "Items", "Check" }, column => Assert.Null(Cell(table, line, column).Heat));
        Assert.All(table.Rows[^1].Cells, cell => Assert.Null(cell.Heat));
    }

    [Fact]
    public void The_total_row_sums_every_column_and_its_check_ties_the_items_to_the_open_item_count()
    {
        var entries = Entries(
            Break("b1", "Price", "Price", "2026-03-30", "2026-03-30"),
            Open("a", "2026-03-27", "2026-03-31"),
            Open("t", "2026-03-31", "2026-04-02"));

        var table = Ageing(entries);

        var total = table.Rows[^1];
        Assert.Equal(RowKind.Total, total.Kind);
        Assert.Equal(["Total", "1", "1", "1", "-", "-", "1", "1", "1", "-", "3", "✓"], Texts(total));
        Assert.Equal(Tone.Ok, total.Cells[^1].Tone);
    }

    [Fact]
    public void A_run_with_no_open_items_gives_zero_rows_for_the_unmatched_categories_and_a_total_that_ties()
    {
        var table = Ageing("");

        Assert.Equal(["Unmatched ours", "Unmatched theirs", "Total"], table.Rows.Select(row => Texts(row)[0]));
        Assert.Equal(["Total", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "✓"], Texts(table.Rows[^1]));
        Assert.All(table.Rows.SelectMany(row => row.Cells).Where(cell => cell.Heat is not null), cell => Assert.Equal(0.0, cell.Heat));
    }

    [Fact]
    public void The_report_is_unavailable_unless_both_trade_date_and_settle_date_are_mapped()
    {
        var noTrade = ReportTesting.Roles.Replace("\"TradeDate\": \"TradeDate\", ", "");
        var noSettle = ReportTesting.Roles.Replace("\"SettleDate\": \"SettleDate\", ", "");

        Assert.Contains("TradeDate", Unavailable("MO5", Input(Body(Ages, "", roles: noTrade))));
        Assert.Contains("SettleDate", Unavailable("MO5", Input(Body(Ages, "", roles: noSettle))));
        Assert.Null(Unavailable("MO5", Input(Body(Ages, ""))));
    }

    [Fact]
    public void The_as_of_date_decides_the_age_and_the_status()
    {
        var entries = Entries(Open("a", "2026-03-30", "2026-03-31"));

        var table = Tables(Compose("MO5", Input(Body(entries, "", asOf: "2026-04-01")))).Single();

        Assert.Equal("1", Text(table, "Unmatched ours", "Age 2 bus. days"));
        Assert.Equal(["1", "-"], [Text(table, "Unmatched ours", "Past settle date"), Text(table, "Unmatched ours", "Settles today")]);
    }

    [Fact]
    public void Labels_replace_the_headings_the_statuses_and_the_total_text()
    {
        var options = """{ "labels": { "Category": "Kind", "Age 1 bus. day": "1 day", "Upcoming": "Later", "Total": "All", "Items": "Count" } }""";

        var table = Ageing(Ages, options);

        Assert.Equal(["Kind", "1 day", "Later", "Count"], new[] { 0, 2, 8, 10 }.Select(index => Header(table)[index]));
        Assert.Equal("All", CellText.Of(table.Rows[^1].Cells[0]));
    }

    [Fact]
    public void The_note_explains_the_age_and_the_check_and_the_open_items_data_section_follows()
    {
        var document = Compose("MO5", Input(Body(Ages, "")));

        var table = Assert.IsType<TableBlock>(document.Blocks[0]);
        Assert.Equal("Age = business days from trade date to the as-of date (Monday to Friday, no holiday calendar). The check ties each row's age buckets and its status columns to its item count.", table.Note);
        var data = Assert.IsType<DataBlock>(Assert.Single(document.Blocks.OfType<DataBlock>()));
        Assert.Equal("Open items", data.Name);
        Assert.Equal(6, data.Table.Rows.Count);
    }

    [Fact]
    public void The_sample_totals_match_ages_and_statuses_worked_out_from_the_input()
    {
        var input = Sample();
        var asOf = input.Run.AsOf;
        FieldValue Of(EntryInput entry, ReportRole role) =>
            entry.Ours is { } ours && !ours[role].IsNull ? ours[role] : (entry.Theirs ?? RoleValues.None)[role];
        int Weekdays(DateOnly from, DateOnly to) => Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Count(offset => from.AddDays(offset).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        int Age(DateOnly trade) => (trade <= asOf ? Weekdays(trade, asOf) : -Weekdays(asOf, trade)) - 1;
        var fromEntries = input.Entries
            .Where(entry => entry.Outcome == Outcome.Break || entry.Alignment != Alignment.Matched)
            .Select(entry => (
                Type: entry.Outcome == Outcome.Break ? "break" : entry.Alignment.ToString(),
                Trade: Of(entry, ReportRole.TradeDate).Date,
                Settle: entry.Alignment == Alignment.RightOnly ? (entry.Theirs ?? RoleValues.None)[ReportRole.SettleDate].Date : (entry.Ours ?? RoleValues.None)[ReportRole.SettleDate].Date));
        var orphans = input.Rows
            .Where(row => row.EntryId is null && row.ItemLead && row.Disposition is Disposition.LeftOnly or Disposition.RightOnly or Disposition.Quarantined)
            .Select(row => (Type: row.Source == SourceSide.Left ? "LeftOnly" : "RightOnly", Trade: row.Values[ReportRole.TradeDate].Date, Settle: row.Values[ReportRole.SettleDate].Date));
        var items = fromEntries.Concat(orphans).ToList();
        int Aged(Func<int, bool> holds) => items.Count(item => item.Trade is { } trade && holds(Age(trade)));

        var table = Assert.Single(Tables(Compose("MO5", input)));

        var total = table.Rows[^1];
        Assert.Equal(items.Count, int.Parse(CellText.Of(Cell(table, total, "Items"))));
        Assert.Equal(
            [Aged(age => age <= 0), Aged(age => age == 1), Aged(age => age == 2), Aged(age => age == 3), Aged(age => age >= 4)],
            new[] { "Age 0 or less", "Age 1 bus. day", "Age 2 bus. days", "Age 3 bus. days", "Age 4+ bus. days" }.Select(column => int.Parse(CellText.Of(Cell(table, total, column)).Replace("-", "0"))));
        Assert.Equal(
            [items.Count(item => item.Settle is { } date && date < asOf), items.Count(item => item.Settle == asOf), items.Count(item => item.Settle is { } date && date > asOf), items.Count(item => item.Settle is null)],
            new[] { "Past settle date", "Settles today", "Upcoming", "No settle date" }.Select(column => int.Parse(CellText.Of(Cell(table, total, column)).Replace("-", "0"))));
        Assert.Equal(
            items.Count(item => item.Type == "Ambiguous"),
            int.Parse(CellText.Of(Cell(table, Line(table, "Ambiguous"), "Items"))));
        Assert.Equal(items.Count(item => item.Trade is null), items.Count - Aged(_ => true));
    }
}
