using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class BreakHeatmapTests
{
    private static string Diff(string field, string role) =>
        $$"""{ "field": "{{field}}", "role": "{{role}}", "type": "decimal", "outcome": "Break", "ours": 1, "theirs": 2, "deltaKind": "Number", "delta": 1 }""";

    private static string Break(string id, string counterparty, string field, string role) =>
        $$"""{ "id": "{{id}}", "alignment": "Matched", "outcome": "Break", "ours": { "Counterparty": "{{counterparty}}", "Currency": "USD" }, "theirs": { "Currency": "USD" }, "diffs": [ {{Diff(field, role)}} ] }""";

    private static string Open(string id, string alignment, string counterparty) =>
        $$"""{ "id": "{{id}}", "alignment": "{{alignment}}", "outcome": "NotCompared", "ours": { "Counterparty": "{{counterparty}}" }, "theirs": null }""";

    private static readonly string Entries = string.Join(
        ", ",
        Break("b1", "BAML", "Price", "Price"),
        Break("b2", "BAML", "Price", "Price"),
        Break("b3", "BAML", "Quantity", "Quantity"),
        Break("b4", "GS", "Price", "Price"),
        Break("b5", "GS", "Rating", "Book"),
        Open("l1", "LeftOnly", "GS"),
        Open("l2", "LeftOnly", "ZZ"),
        Open("a1", "Ambiguous", "ZZ"));

    private static TableBlock Heatmap(string entries, string options = "{}", string roles = ReportTesting.Roles) =>
        Assert.Single(Tables(Compose("MO4", Input(Body(entries, "", options, roles: roles)))));

    private static IReadOnlyList<string> Column(TableBlock table, string title) =>
        table.Rows.Select(row => CellText.Of(Cell(table, row, title))).ToList();

    [Fact]
    public void The_columns_are_the_categories_in_report_order_then_the_total_and_the_check()
    {
        var table = Heatmap(Entries);

        Assert.Equal(["Counterparty", "Price", "Quantity", "Rating", "Unmatched ours", "Unmatched theirs", "Ambiguous", "Total", "Check"], Header(table));
    }

    [Fact]
    public void Counterparties_are_ordered_by_item_count_descending_then_by_name()
    {
        var table = Heatmap(Entries);

        Assert.Equal(["BAML", "GS", "ZZ", "Total"], Column(table, "Counterparty"));
        Assert.Equal(["3", "3", "2", "8"], Column(table, "Total"));
    }

    [Fact]
    public void Each_cell_counts_the_counterpartys_open_items_in_the_category_and_zero_shows_as_a_dash()
    {
        var table = Heatmap(Entries);

        Assert.Equal(["2", "1", "-", "3"], Column(table, "Price"));
        Assert.Equal(["1", "-", "-", "1"], Column(table, "Quantity"));
        Assert.Equal(["-", "1", "-", "1"], Column(table, "Rating"));
        Assert.Equal(["-", "1", "1", "2"], Column(table, "Unmatched ours"));
        Assert.Equal(["-", "-", "-", "-"], Column(table, "Unmatched theirs"));
        Assert.Equal(["-", "-", "1", "1"], Column(table, "Ambiguous"));
    }

    [Fact]
    public void The_total_row_sums_each_column_and_the_check_ties_the_grand_total_to_the_item_count()
    {
        var table = Heatmap(Entries);

        var total = table.Rows[^1];
        Assert.Equal(RowKind.Total, total.Kind);
        Assert.Equal(["Total", "3", "1", "1", "2", "-", "1", "8", "✓"], Texts(total));
        Assert.Equal(Tone.Ok, total.Cells[^1].Tone);
    }

    [Fact]
    public void Shading_is_orange_and_scaled_to_the_largest_count_in_the_data_cells_only()
    {
        var table = Heatmap(Entries);

        var price = table.Rows.Take(3).Select(row => Cell(table, row, "Price")).ToList();
        Assert.Equal([1.0, 0.5, 0.0], price.Select(cell => cell.Heat));
        Assert.All(price, cell => Assert.Equal(HeatColour.Orange, cell.HeatColour));
        Assert.All(table.Rows[^1].Cells.Concat(table.Rows.Select(row => Cell(table, row, "Total"))), cell => Assert.Null(cell.Heat));
        Assert.All(table.Rows.Take(3), row => Assert.True(Cell(table, row, "Total").Bold));
    }

    [Fact]
    public void A_mapped_field_name_heads_its_column_in_the_fixed_field_order_and_other_categories_follow_in_name_order()
    {
        var roles = ReportTesting.Roles.Replace("\"Price\": \"Price\"", "\"Price\": \"Px\"");
        var entries = string.Join(", ", Break("b1", "A", "Zeta", "Book"), Break("b2", "A", "Px", "Price"), Break("b3", "A", "Alpha", "Book"), Break("b4", "A", "Fees", "Fees"));

        var table = Heatmap(entries, roles: roles);

        Assert.Equal(["Counterparty", "Px", "Fees", "Alpha", "Zeta", "Unmatched ours", "Unmatched theirs", "Total", "Check"], Header(table));
    }

    [Fact]
    public void Labels_replace_the_headings_and_the_total_text()
    {
        var table = Heatmap(Entries, """{ "labels": { "Counterparty": "Cpty", "Total": "Sum", "Price": "Px" } }""");

        Assert.Equal(["Cpty", "Px"], Header(table).Take(2));
        Assert.Equal("Sum", Header(table)[^2]);
        Assert.Equal("Sum", CellText.Of(table.Rows[^1].Cells[0]));
    }

    [Fact]
    public void An_open_item_without_a_counterparty_is_counted_on_a_blank_row_so_the_check_still_passes()
    {
        var entries = Open("l1", "LeftOnly", "");

        var table = Heatmap(entries);

        Assert.Equal(["", "Total"], Column(table, "Counterparty"));
        Assert.Equal(["✓"], Column(table, "Check").Where(text => text.Length > 0));
    }

    [Fact]
    public void A_run_with_no_open_items_gives_only_a_total_row_that_ties_and_is_never_unavailable()
    {
        var input = Input(Body("", ""));

        Assert.Null(Unavailable("MO4", input));
        var table = Assert.Single(Tables(Compose("MO4", input)));
        Assert.Equal(["Counterparty", "Unmatched ours", "Unmatched theirs", "Total", "Check"], Header(table));
        Assert.Equal(["Total", "-", "-", "-", "✓"], Texts(Assert.Single(table.Rows)));
    }

    [Fact]
    public void A_run_that_does_not_map_the_counterparty_role_is_unavailable()
    {
        var roles = ReportTesting.Roles.Replace("\"Counterparty\": \"Counterparty\", ", "");

        Assert.Contains("Counterparty", Unavailable("MO4", Input(Body(Entries, "", roles: roles))));
    }

    [Fact]
    public void Counterparty_and_category_names_with_markup_characters_pass_through_unchanged()
    {
        var table = Heatmap(Break("b1", "A&B <x>", "Net <cash> &amp;", "Book"));

        Assert.Equal(["A&B <x>", "Total"], Column(table, "Counterparty"));
        Assert.Contains("Net <cash> &amp;", Header(table));
    }

    [Fact]
    public void The_open_items_data_section_follows_the_table()
    {
        var document = Compose("MO4", Input(Body(Entries, "")));

        var data = Assert.IsType<DataBlock>(Assert.Single(document.Blocks.OfType<DataBlock>()));
        Assert.Equal("Open items", data.Name);
        Assert.Equal(8, data.Table.Rows.Count);
    }

    [Fact]
    public void The_sample_counts_match_a_count_of_its_open_entries_and_rows()
    {
        var input = Sample();
        var cpty = (RoleValues? values) => values?[ReportRole.Counterparty].Text ?? "";
        var breaks = input.Entries.Where(entry => entry.Outcome == Outcome.Break).ToList();
        var whole = input.Entries.Where(entry => entry.Outcome != Outcome.Break && entry.Alignment != Alignment.Matched).ToList();
        var orphans = input.Rows.Where(row => row.EntryId is null && row.ItemLead && row.Disposition is Disposition.LeftOnly or Disposition.RightOnly or Disposition.Quarantined).ToList();
        var theirsOrphans = orphans.Where(row => row.Disposition == Disposition.RightOnly || row is { Disposition: Disposition.Quarantined, Source: SourceSide.Right }).ToList();
        var priceBreaks = breaks.Count(entry => entry.Diffs.Where(diff => diff.Outcome == DiffOutcome.Break).ToList() is { } found
            && (found.Where(diff => diff.AttributedTo.Count == 0).Select(diff => diff.Field).FirstOrDefault() ?? found[0].Field) == "Price");
        var itemCounterparties = breaks.Select(entry => cpty(entry.Ours))
            .Concat(whole.Select(entry => cpty(entry.Alignment == Alignment.RightOnly ? entry.Theirs : entry.Ours ?? entry.Theirs)))
            .Concat(orphans.Select(row => row.Values[ReportRole.Counterparty].Text))
            .ToList();

        var table = Assert.Single(Tables(Compose("MO4", input)));

        var total = table.Rows[^1];
        Assert.Equal(itemCounterparties.Count, int.Parse(CellText.Of(Cell(table, total, "Total"))));
        Assert.Equal(priceBreaks, int.Parse(CellText.Of(Cell(table, total, "Price"))));
        Assert.Equal(
            whole.Count(entry => entry.Alignment == Alignment.LeftOnly) + orphans.Count - theirsOrphans.Count,
            int.Parse(CellText.Of(Cell(table, total, "Unmatched ours"))));
        Assert.Equal(whole.Count(entry => entry.Alignment == Alignment.RightOnly) + theirsOrphans.Count, int.Parse(CellText.Of(Cell(table, total, "Unmatched theirs"))));
        Assert.Equal(
            itemCounterparties.GroupBy(name => name).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).Select(group => group.Key),
            Column(table, "Counterparty").SkipLast(1));
    }
}
