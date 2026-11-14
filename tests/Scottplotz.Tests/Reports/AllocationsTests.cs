using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class AllocationsTests
{
    private static string Entry(string left, string right, string composition = "\"ManyToOne\"", string outcome = "Exact", string alignment = "Matched", string id = "e1234567890123") =>
        $$"""{ "id": "{{id}}", "pathId": "trades", "alignment": "{{alignment}}", "composition": [{{composition}}], "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "ours": { "Counterparty": "BAML", "Instrument": "VOD", "Side": "BUY" }, "theirs": { "Counterparty": "BAML", "Instrument": "VOD", "Side": "BUY" }, "committedBy": "P3" }""";

    private static string Row(string source, string key, string quantity, string netCash = "null", string book = "", string id = "e1234567890123") =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "Matched", "entryId": "{{id}}", "values": { "Quantity": {{quantity}}, "Price": 2.5, "NetCash": {{netCash}}{{(book.Length > 0 ? $", \"Book\": \"{book}\"" : "")}} } }""";

    private static TableBlock CardTable(ReportInput input, int card = 0) =>
        Compose("BO5", input).Blocks.OfType<SectionBlock>().ElementAt(card).Blocks.OfType<TableBlock>().Single();

    private static ReportInput ManyToOne(string quantities, string blockQuantity, string blockCash = "null", string cash = "null") =>
        Input(Body(
            Entry("\"L-1\", \"L-2\"", "\"R-1\""),
            string.Join(", ", quantities.Split(',').Select((quantity, index) => Row("left", $"L-{index + 1}", quantity, cash, $"B{index + 1}")).Append(Row("right", "R-1", blockQuantity, blockCash)))));

    [Fact]
    public void Allocations_that_add_up_to_the_block_show_a_tick_and_no_gap()
    {
        var input = ManyToOne("600,400", "1000", blockCash: "250", cash: "125");

        var section = Assert.IsType<SectionBlock>(Compose("BO5", input).Blocks[0]);
        var table = CardTable(input);

        Assert.Equal("e12345678901  ·  BAML  ·  VOD  ·  BUY  ·  matched by P3  ·  outcome Exact", section.Title);
        Assert.Equal(["Line", "Row", "Book", "Quantity", "Price", "Net cash", "Quantity check", "Net cash check"], Header(table));
        Assert.Equal(["Block (theirs)", "R-1", "", "1,000", "2.5", "250.00", "", ""], Texts(table.Rows[0]));
        Assert.Equal(["allocation", "L-1", "B1", "600", "2.5", "125.00", "", ""], Texts(table.Rows[1]));
        Assert.Equal(["Allocations total", "", "", "1,000", "", "250.00", "✓ equal", "✓ equal"], Texts(table.Rows[3]));
        Assert.Equal(Tone.Ok, Cell(table, table.Rows[3], "Quantity check").Tone);
        Assert.Equal(Tone.Ok, Cell(table, table.Rows[3], "Net cash check").Tone);
    }

    [Fact]
    public void The_block_line_is_bold_and_the_allocation_line_indented_and_no_figure_is_coloured()
    {
        var table = CardTable(ManyToOne("600,400", "1000", blockCash: "250", cash: "125"));

        Assert.All(table.Rows.Take(5).SelectMany(row => row.Cells.Take(6)), cell => Assert.Equal(Tone.None, cell.Tone));
        Assert.Equal(RowKind.Total, table.Rows[0].Kind);
        Assert.All(table.Rows[0].Cells.Take(6), cell => Assert.True(cell.Bold));
        Assert.All(table.Rows[1].Cells.Take(6), cell => Assert.False(cell.Bold));
        Assert.Equal(1, table.Rows[1].Cells[0].Indent);
    }

    [Fact]
    public void A_short_allocation_shows_a_cross_and_the_signed_difference_total_minus_block()
    {
        var input = ManyToOne("600,350", "1000", blockCash: "250", cash: "100");

        var total = CardTable(input).Rows[^1];

        Assert.Equal("✗ -50.00", CellText.Of(total.Cells[6]));
        Assert.Equal(Tone.Fail, total.Cells[6].Tone);
        Assert.Equal("✗ -50.00", CellText.Of(total.Cells[7]));
        Assert.Equal(Tone.Fail, total.Cells[7].Tone);
    }

    [Fact]
    public void An_over_allocation_shows_a_positive_difference_with_thousands_separators()
    {
        var total = CardTable(ManyToOne("1500000,200.5", "1000")).Rows[^1];

        Assert.Equal("✗ 1,499,200.50", CellText.Of(total.Cells[6]));
    }

    [Fact]
    public void Net_cash_is_compared_after_rounding_to_two_places_but_quantity_exactly()
    {
        var cashOnly = CardTable(ManyToOne("600,400", "1000", blockCash: "250", cash: "125.002")).Rows[^1];
        var quantity = CardTable(ManyToOne("600,400.0001", "1000")).Rows[^1];

        Assert.Equal("✓ equal", CellText.Of(cashOnly.Cells[7]));
        Assert.Equal("✗ 0.00", CellText.Of(quantity.Cells[6]));
        Assert.Equal("✗ 0.01", CellText.Of(CardTable(ManyToOne("600,400", "1000", blockCash: "250", cash: "125.005")).Rows[^1].Cells[7]));
    }

    [Fact]
    public void A_missing_quantity_counts_as_zero_in_the_sum()
    {
        var input = Input(Body(
            Entry("\"L-1\", \"L-2\"", "\"R-1\""),
            string.Join(", ", Row("left", "L-1", "600"), """{ "source": "left", "rowKey": "L-2", "disposition": "Matched", "entryId": "e1234567890123", "values": { } }""", Row("right", "R-1", "600"))));

        var table = CardTable(input);

        Assert.Equal("", CellText.Of(table.Rows[2].Cells[3]));
        Assert.Equal("✓ equal", CellText.Of(table.Rows[^1].Cells[6]));
    }

    [Fact]
    public void A_one_to_many_entry_has_its_block_on_our_side_and_the_allocations_on_theirs()
    {
        var input = Input(Body(
            Entry("\"L-1\"", "\"R-1\", \"R-2\"", composition: "\"OneToMany\""),
            string.Join(", ", Row("left", "L-1", "1000"), Row("right", "R-1", "700"), Row("right", "R-2", "300"))));

        var table = CardTable(input);

        Assert.Equal("Block (ours)", CellText.Of(table.Rows[0].Cells[0]));
        Assert.Equal("L-1", CellText.Of(table.Rows[0].Cells[1]));
        Assert.Equal(["R-1", "R-2"], table.Rows.Skip(1).Take(2).Select(row => CellText.Of(row.Cells[1])));
        Assert.Equal("✓ equal", CellText.Of(table.Rows[^1].Cells[6]));
    }

    [Fact]
    public void The_block_side_follows_the_composition_even_when_the_member_counts_say_otherwise()
    {
        var input = Input(Body(
            Entry("\"L-1\"", "\"R-1\", \"R-2\"", composition: "\"ManyToOne\""),
            string.Join(", ", Row("left", "L-1", "1000"), Row("right", "R-1", "1000"), Row("right", "R-2", "1000"))));

        var table = CardTable(input);

        Assert.Equal("Block (theirs)", CellText.Of(table.Rows[0].Cells[0]));
        Assert.Equal("R-1", CellText.Of(table.Rows[0].Cells[1]));
        Assert.Equal(["L-1"], table.Rows.Skip(1).Take(1).Select(row => CellText.Of(row.Cells[1])));
    }

    [Fact]
    public void Packages_open_entries_and_one_to_one_entries_are_left_out_and_cards_follow_entry_order()
    {
        var entries = string.Join(", ",
            Entry("\"L-1\"", "\"R-1\"", composition: "\"OneToOne\"", id: "e1"),
            Entry("\"L-2\", \"L-3\"", "\"R-2\"", composition: "\"ManyToOne\", \"Package\"", id: "e2"),
            Entry("\"L-4\", \"L-5\"", "\"R-4\"", alignment: "Ambiguous", outcome: "NotCompared", id: "e3"),
            Entry("\"L-6\"", "\"R-6\", \"R-7\"", composition: "\"OneToMany\"", id: "e4"),
            Entry("\"L-8\", \"L-9\"", "\"R-8\"", id: "e5"));
        var rows = string.Join(", ",
            Row("left", "L-1", "1", id: "e1"), Row("right", "R-1", "1", id: "e1"),
            Row("left", "L-2", "1", id: "e2"), Row("left", "L-3", "1", id: "e2"), Row("right", "R-2", "2", id: "e2"),
            Row("left", "L-4", "1", id: "e3"), Row("left", "L-5", "1", id: "e3"), Row("right", "R-4", "2", id: "e3"),
            Row("left", "L-6", "2", id: "e4"), Row("right", "R-6", "1", id: "e4"), Row("right", "R-7", "1", id: "e4"),
            Row("left", "L-8", "1", id: "e5"), Row("left", "L-9", "1", id: "e5"), Row("right", "R-8", "2", id: "e5"));

        var cards = Compose("BO5", Input(Body(entries, rows))).Blocks.OfType<SectionBlock>().ToList();

        Assert.Equal(["e4", "e5"], cards.Select(card => card.Title[..2]));
    }

    [Fact]
    public void Unmapped_price_and_net_cash_leave_their_columns_and_cash_check_blank()
    {
        var roles = """{ "Counterparty": "Counterparty", "Instrument": "Instrument", "Side": "Side", "Quantity": "Quantity", "Book": "Book" }""";
        var input = Input(Body(
            Entry("\"L-1\", \"L-2\"", "\"R-1\""),
            string.Join(", ", Row("left", "L-1", "600"), Row("left", "L-2", "400"), Row("right", "R-1", "1000")), roles: roles));

        var table = CardTable(input);

        Assert.All(table.Rows, row => Assert.Equal("", CellText.Of(row.Cells[4]) + CellText.Of(row.Cells[5])));
        Assert.Equal("", CellText.Of(table.Rows[^1].Cells[7]));
        Assert.Equal("✓ equal", CellText.Of(table.Rows[^1].Cells[6]));
    }

    [Fact]
    public void The_report_is_unavailable_without_a_block_entry_or_the_quantity_role()
    {
        var oneToOne = Input(Body(Entry("\"L-1\"", "\"R-1\"", composition: "\"OneToOne\""), string.Join(", ", Row("left", "L-1", "1"), Row("right", "R-1", "1"))));
        var noQuantity = Input(Body(Entry("\"L-1\"", "\"R-1\""), string.Join(", ", Row("left", "L-1", "1"), Row("right", "R-1", "1")), roles: """{ "Counterparty": "Counterparty" }"""));

        Assert.Contains("No many-to-one or one-to-many entries (outside packages) in this run.", Unavailable("BO5", oneToOne));
        Assert.Contains("Quantity", Unavailable("BO5", noQuantity));
    }

    [Fact]
    public void The_page_ends_with_the_result_rows_data_section()
    {
        var document = Compose("BO5", ManyToOne("600,400", "1000"));

        var data = Assert.IsType<DataBlock>(document.Blocks[^1]);

        Assert.Equal("Result rows", data.Name);
        Assert.Equal(3, data.Table.Rows.Count);
    }

    [Fact]
    public void The_sample_shows_each_block_with_the_quantity_and_net_cash_sums_its_rows_give()
    {
        var input = Sample();
        var blocks = input.Entries
            .Where(entry => entry.Alignment == Alignment.Matched && !entry.Composition.HasFlag(Composition.Package)
                && (entry.Composition.HasFlag(Composition.ManyToOne) || entry.Composition.HasFlag(Composition.OneToMany)))
            .Select(entry =>
            {
                var many = entry.Composition.HasFlag(Composition.ManyToOne);
                var members = (many ? entry.Left : entry.Right).Select(key => input.Rows.Single(row => row.Source == (many ? SourceSide.Left : SourceSide.Right) && row.RowKey == key)).ToList();
                var block = input.Rows.Single(row => row.Source == (many ? SourceSide.Right : SourceSide.Left) && row.RowKey == (many ? entry.Right : entry.Left)[0]);
                return (Title: entry.Id[..12], Sum: members.Sum(row => row.Values[ReportRole.Quantity].Number ?? 0m), Block: block.Values[ReportRole.Quantity].Number ?? 0m,
                    Cash: members.Sum(row => row.Values[ReportRole.NetCash].Number ?? 0m) - (block.Values[ReportRole.NetCash].Number ?? 0m));
            })
            .ToList();

        var cards = Compose("BO5", input).Blocks.OfType<SectionBlock>().ToList();
        var totals = cards.Select(card => card.Blocks.OfType<TableBlock>().Single().Rows[^1]).ToList();

        Assert.Equal(6, cards.Count);
        Assert.Equal(blocks.Select(block => block.Title), cards.Select(card => card.Title[..12]));
        Assert.Equal(blocks.Select(block => block.Sum), totals.Select(total => ((CellValue.Number)total.Cells[3].Value).Value));
        Assert.Equal(blocks.Select(block => block.Sum == block.Block), totals.Select(total => CellText.Of(total.Cells[6]) == "✓ equal"));
        Assert.Equal(blocks.Select(block => Math.Round(block.Cash, 2, MidpointRounding.AwayFromZero) == 0), totals.Select(total => CellText.Of(total.Cells[7]) == "✓ equal"));
        Assert.Equal("✗ -100.00", CellText.Of(totals[0].Cells[6]));
    }
}
