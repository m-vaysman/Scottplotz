using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class LifecycleChainsTests
{
    private static string Entry(string left, string right, string id = "e1234567890123", string composition = "\"OneToOne\", \"LifecycleCollapsed\"", string outcome = "Exact") =>
        $$"""{ "id": "{{id}}", "pathId": "trades", "alignment": "Matched", "composition": [{{composition}}], "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "ours": { "Counterparty": "BAML", "Instrument": "VOD" }, "theirs": { "Counterparty": "BAML", "Instrument": "VOD" }, "committedBy": "P1" }""";

    private static string Row(string source, string key, string disposition, string action, string target, string quantity, string price, string settle = "2026-04-01", string netCash = "100", string suppression = "null", string id = "e1234567890123")
    {
        var actionPart = action.Length > 0 ? $", \"LifecycleAction\": \"{action}\"" : "";
        var targetPart = target.Length > 0 ? $", \"LifecycleTarget\": \"{target}\"" : "";
        var quantityPart = quantity.Length > 0 ? $", \"Quantity\": {quantity}" : "";
        return $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "{{disposition}}", "suppression": {{suppression}}, "entryId": "{{id}}", "values": { "Price": {{price}}, "SettleDate": "{{settle}}", "NetCash": {{netCash}}{{quantityPart}}{{actionPart}}{{targetPart}} } }""";
    }

    private static string Superseded(string key, string quantity = "100", string price = "10", string target = "") =>
        Row("left", key, "Suppressed", "NEW", target, quantity, price, suppression: "\"Superseded\"");

    private static ReportInput Chain(params string[] rows) =>
        Input(Body(Entry("\"L-2\"", "\"R-1\""), string.Join(", ", rows)));

    private static TableBlock CardTable(ReportInput input, int card = 0) =>
        Compose("BO4", input).Blocks.OfType<SectionBlock>().ElementAt(card).Blocks.OfType<TableBlock>().Single();

    [Fact]
    public void A_correction_chain_lists_the_old_version_the_survivor_and_what_it_matched()
    {
        var input = Chain(
            Superseded("L-1", price: "10"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "100", "11", netCash: "110"),
            Row("right", "R-1", "Matched", "", "", "100", "11", netCash: "110"));

        var section = Assert.IsType<SectionBlock>(Compose("BO4", input).Blocks[0]);
        var table = CardTable(input);

        Assert.Equal("e12345678901  ·  BAML  ·  VOD  ·  2 rows in the chain", section.Title);
        Assert.Equal(["", "Report", "Action", "Target", "Quantity", "Price", "Settle date", "Net cash", "Disposition"], Header(table));
        Assert.Equal(["↳", "L-1", "NEW", "", "100", "10", "2026-04-01", "100.00", "Superseded"], Texts(table.Rows[0]));
        Assert.Equal(["↳↳", "L-2", "CORR", "L-1", "100", "11", "2026-04-01", "110.00", "Survivor (Matched)"], Texts(table.Rows[1]));
        Assert.Equal(["Matched with", "R-1", "pass P1", "outcome Exact", "100", "11", "2026-04-01", "110.00", "Matched"], Texts(table.Rows[2]));
        Assert.Equal("What the correction changed", CellText.Of(table.Rows[3].Cells[0]));
        Assert.Equal("Price 10 → 11, NetCash 100 → 110", CellText.Of(table.Rows[3].Cells[1]));
        Assert.Equal(8, table.Rows[3].Cells[1].Span);
    }

    [Fact]
    public void The_survivor_is_bold_and_its_changed_fields_are_amber_while_older_rows_are_muted()
    {
        var table = CardTable(Chain(
            Superseded("L-1", price: "10"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "100", "11", netCash: "100"),
            Row("right", "R-1", "Matched", "", "", "100", "11")));

        var survivor = table.Rows[1];

        Assert.All(table.Rows[0].Cells.Where(cell => CellText.Of(cell).Length > 0), cell => Assert.Equal(Tone.Muted, cell.Tone));
        Assert.All(survivor.Cells.Take(4), cell => Assert.True(cell.Bold));
        Assert.Equal(Tone.Tolerance, Cell(table, survivor, "Price").Tone);
        Assert.Equal(Tone.Link, Cell(table, survivor, "Quantity").Tone);
        Assert.Equal(Tone.Link, Cell(table, survivor, "Net cash").Tone);
        Assert.All(table.Rows[2].Cells.Take(4), cell => Assert.Equal(Tone.Ok, cell.Tone));
    }

    [Fact]
    public void Rows_that_are_not_the_survivor_are_struck_through_and_the_survivor_and_matched_rows_are_not()
    {
        var table = CardTable(Chain(
            Superseded("L-1", price: "10"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "100", "11", netCash: "100"),
            Row("right", "R-1", "Matched", "", "", "100", "11")));

        Assert.All(table.Rows[0].Cells, cell => Assert.True(cell.Strike));
        Assert.All(table.Rows[1].Cells, cell => Assert.False(cell.Strike));
        Assert.All(table.Rows[2].Cells, cell => Assert.False(cell.Strike));
    }

    [Fact]
    public void A_chain_whose_side_has_no_member_row_to_survive_is_skipped_instead_of_failing()
    {
        var input = Input(Body(
            Entry("", "\"R-1\"") + ", " + Entry("\"L-5\"", "\"R-5\"", id: "e3"),
            string.Join(", ",
                Superseded("L-1"),
                Row("right", "R-1", "Matched", "", "", "1", "1"),
                Row("left", "L-4", "Suppressed", "NEW", "", "1", "1", suppression: "\"Superseded\"", id: "e3"),
                Row("left", "L-5", "Matched", "CORR", "L-4", "1", "2", id: "e3"),
                Row("right", "R-5", "Matched", "", "", "1", "2", id: "e3"))));

        var card = Assert.Single(Compose("BO4", input).Blocks.OfType<SectionBlock>());
        Assert.StartsWith("e3", card.Title);
    }

    [Fact]
    public void Rows_follow_their_targets_so_new_comes_before_cancel_and_ties_sort_by_row_key()
    {
        var input = Input(Body(Entry("\"L-2\"", "\"R-1\""), string.Join(", ",
            Row("left", "L-3", "Suppressed", "CANC", "L-1", "100", "10", suppression: "\"CancelEvent\""),
            Superseded("L-1"),
            Row("left", "L-2", "Matched", "NEW", "", "150", "10"),
            Row("right", "R-1", "Matched", "", "", "150", "10"))));

        var table = CardTable(input);

        Assert.Equal(["L-1", "L-2", "L-3"], table.Rows.Take(3).Select(row => CellText.Of(row.Cells[1])));
        Assert.Equal("Cancel event", CellText.Of(table.Rows[2].Cells[8]));
        Assert.Equal("↳↳↳", CellText.Of(table.Rows[2].Cells[0]));
        Assert.Equal("Quantity 100 → 150", CellText.Of(table.Rows[^1].Cells[1]));
    }

    [Fact]
    public void The_first_version_skips_cancel_rows_when_working_out_what_changed()
    {
        var input = Input(Body(Entry("\"L-3\"", "\"R-1\""), string.Join(", ",
            Row("left", "L-1", "Suppressed", "CANC", "", "100", "10", suppression: "\"CancelEvent\""),
            Row("left", "L-2", "Suppressed", "NEW", "", "100", "10", suppression: "\"FullyCancelled\""),
            Row("left", "L-3", "Matched", "NEW", "", "100", "10"),
            Row("right", "R-1", "Matched", "", "", "100", "10"))));

        var table = CardTable(input);

        Assert.Equal("No compared field changed between the first version and the survivor.", CellText.Of(table.Rows[^1].Cells[1]));
        Assert.Equal("Fully cancelled", CellText.Of(table.Rows[1].Cells[8]));
    }

    [Fact]
    public void Equal_numbers_written_differently_are_not_a_change()
    {
        var table = CardTable(Chain(
            Superseded("L-1", price: "10.50"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "100", "10.5"),
            Row("right", "R-1", "Matched", "", "", "100", "10.5")));

        Assert.Equal("No compared field changed between the first version and the survivor.", CellText.Of(table.Rows[^1].Cells[1]));
    }

    [Fact]
    public void Any_member_row_that_is_not_suppressed_is_called_a_survivor_but_only_the_first_is_bold()
    {
        var input = Input(Body(Entry("\"L-2\", \"L-3\"", "\"R-1\"", composition: "\"ManyToOne\", \"LifecycleCollapsed\""), string.Join(", ",
            Superseded("L-1"),
            Row("left", "L-2", "Matched", "NEW", "", "60", "10"),
            Row("left", "L-3", "Matched", "NEW", "", "40", "10"),
            Row("right", "R-1", "Matched", "", "", "100", "10"))));

        var table = CardTable(input);

        Assert.Equal(["L-1", "L-2", "L-3"], table.Rows.Take(3).Select(row => CellText.Of(row.Cells[1])));
        Assert.Equal("Survivor (Matched)", CellText.Of(table.Rows[2].Cells[8]));
        Assert.True(table.Rows[1].Cells[1].Bold);
        Assert.False(table.Rows[2].Cells[1].Bold);
        Assert.Equal(Tone.Muted, table.Rows[2].Cells[1].Tone);
    }

    [Fact]
    public void An_entry_with_a_chain_on_each_side_gives_two_cards()
    {
        var input = Input(Body(Entry("\"L-2\"", "\"R-2\""), string.Join(", ",
            Superseded("L-1"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "100", "11"),
            Row("right", "R-1", "Suppressed", "NEW", "", "100", "10", suppression: "\"Superseded\""),
            Row("right", "R-2", "Matched", "CORR", "R-1", "100", "11"))));

        var tables = Tables(Compose("BO4", input));

        Assert.Equal(2, tables.Count);
        Assert.Equal(["R-1", "R-2", "L-2"], tables[1].Rows.Take(3).Select(row => CellText.Of(row.Cells[1])));
        Assert.Equal("Matched with", CellText.Of(tables[1].Rows[2].Cells[0]));
    }

    [Fact]
    public void A_side_without_a_suppressed_row_gives_no_card_and_an_entry_without_the_flag_is_ignored()
    {
        var input = Input(Body(
            Entry("\"L-2\"", "\"R-1\"") + ", " + Entry("\"L-5\"", "\"R-5\"", id: "e2", composition: "\"OneToOne\""),
            string.Join(", ",
                Superseded("L-1"),
                Row("left", "L-2", "Matched", "CORR", "L-1", "100", "11"),
                Row("right", "R-1", "Matched", "NEW", "", "100", "11"),
                Row("left", "L-5", "Matched", "NEW", "", "1", "1", id: "e2"),
                Row("right", "R-5", "Matched", "", "", "1", "1", id: "e2"))));

        Assert.Single(Compose("BO4", input).Blocks.OfType<SectionBlock>());
    }

    [Fact]
    public void Unmapped_compared_roles_leave_their_cells_blank_and_are_never_reported_as_changed()
    {
        var roles = """{ "Counterparty": "Counterparty", "Instrument": "Instrument", "Price": "Price", "LifecycleAction": "EventAction", "LifecycleTarget": "EventTarget" }""";
        var input = Input(Body(Entry("\"L-2\"", "\"R-1\""), string.Join(", ",
            Superseded("L-1", price: "10"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "200", "11"),
            Row("right", "R-1", "Matched", "", "", "200", "11")), roles: roles));

        var table = CardTable(input);

        Assert.Equal("", CellText.Of(Cell(table, table.Rows[1], "Quantity")));
        Assert.Equal("", CellText.Of(Cell(table, table.Rows[1], "Net cash")));
        Assert.Equal("Price 10 → 11", CellText.Of(table.Rows[^1].Cells[1]));
    }

    [Fact]
    public void The_report_is_unavailable_without_a_lifecycle_chain_or_the_lifecycle_roles()
    {
        var noFlag = Input(Body(Entry("\"L-1\"", "\"R-1\"", composition: "\"OneToOne\""), string.Join(", ",
            Row("left", "L-1", "Matched", "NEW", "", "1", "1"),
            Row("right", "R-1", "Matched", "", "", "1", "1"))));
        var noSuppressed = Input(Body(Entry("\"L-1\"", "\"R-1\""), string.Join(", ",
            Row("left", "L-1", "Matched", "NEW", "", "1", "1"),
            Row("right", "R-1", "Matched", "", "", "1", "1"))));
        var unmapped = Input(Body(Entry("", ""), "", roles: """{ "Counterparty": "Counterparty" }"""));

        Assert.Contains("No lifecycle-collapsed entries in this run.", Unavailable("BO4", noFlag));
        Assert.Contains("No lifecycle-collapsed entries in this run.", Unavailable("BO4", noSuppressed));
        Assert.Contains("LifecycleAction", Unavailable("BO4", unmapped));
    }

    [Fact]
    public void The_page_ends_with_the_result_rows_data_section()
    {
        var document = Compose("BO4", Chain(
            Superseded("L-1"),
            Row("left", "L-2", "Matched", "CORR", "L-1", "100", "11"),
            Row("right", "R-1", "Matched", "", "", "100", "11")));

        var data = Assert.IsType<DataBlock>(document.Blocks[^1]);

        Assert.Equal("Result rows", data.Name);
        Assert.Equal(3, data.Table.Rows.Count);
    }

    [Fact]
    public void The_sample_draws_one_card_per_collapsed_side_with_the_chain_sizes_the_rows_give()
    {
        var input = Sample();
        var expected = input.Entries
            .Where(entry => entry.Composition.HasFlag(Composition.LifecycleCollapsed))
            .SelectMany(entry => new[] { SourceSide.Left, SourceSide.Right }
                .Where(side => input.Rows.Any(row => row.EntryId == entry.Id && row.Source == side && row.Suppression is not null))
                .Select(side => $"{entry.Id[..12]}  ·  " + input.Rows.Count(row => row.EntryId == entry.Id && row.Source == side
                    && (row.Suppression is not null || row.Values[ReportRole.LifecycleAction].Text.Length > 0))))
            .ToList();

        var cards = Compose("BO4", input).Blocks.OfType<SectionBlock>().ToList();

        Assert.Equal(10, cards.Count);
        Assert.Equal(expected, cards.Select(card => card.Title[..12] + "  ·  " + card.Title.Split("  ·  ")[3].Split(' ')[0]));
        Assert.Equal("Survivor (Matched)", CellText.Of(CardTable(input).Rows[1].Cells[8]));
    }
}
