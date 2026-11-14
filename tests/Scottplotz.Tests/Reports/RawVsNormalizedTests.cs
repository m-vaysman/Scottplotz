using Scottplotz.Reports;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class RawVsNormalizedTests
{
    private const string BreakId = "a1b2c3d4e5f6a7b8";
    private const string ToleranceId = "b1b2c3d4e5f6a7b8";
    private const string StepsId = "c1b2c3d4e5f6a7b8";
    private const string OneStepId = "d1b2c3d4e5f6a7b8";
    private const string PlainId = "e1b2c3d4e5f6a7b8";

    private const string RawColumns = """{ "rawColumns": { "left": ["Cpty", "Qty", "Unused", "Cpty"], "right": ["Cpty Name", "Dir"] } }""";

    private static string Pair(string id, string outcome, string left, string right, string ours, string theirs, string diffs = "", string alignment = "Matched", string composition = "OneToOne") =>
        $$"""{ "id": "{{id}}", "pathId": "trades", "alignment": "{{alignment}}", "composition": ["{{composition}}"], "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "ours": {{ours}}, "theirs": {{theirs}}, "committedBy": "P1", "diffs": [ {{diffs}} ] }""";

    private static string Row(string source, string key, string entry, string values, string? raw = null, string? trace = null) =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "Matched", "entryId": "{{entry}}", "values": {{values}}{{(raw is null ? "" : $", \"raw\": {raw}")}}{{(trace is null ? "" : $", \"trace\": {trace}")}} }""";

    private static string Step(string rule, string field, string? raw, string? canonical) =>
        $$"""{ "ruleId": "{{rule}}", "field": "{{field}}", "raw": {{(raw is null ? "null" : $"\"{raw}\"")}}, "canonical": {{(canonical is null ? "null" : $"\"{canonical}\"")}} }""";

    private const string Plain = """{ "Counterparty": "BARC", "Instrument": "UST", "Side": "BUY", "Currency": "USD", "Quantity": 1000, "Price": 99.5, "NetCash": 5000, "Fees": 10, "TradeDate": "2026-03-27", "SettleDate": "2026-03-30" }""";
    private const string TheirsPlain = """{ "Counterparty": "BARC", "Instrument": "UST", "Side": "BUY", "Currency": "USD", "Quantity": 1000, "Price": 99.6, "NetCash": 5010, "Fees": 10.1, "TradeDate": "2026-03-27", "SettleDate": "2026-03-31" }""";

    private const string BreakDiffs =
        """{ "field": "Price", "role": "Price", "type": "decimal", "outcome": "Break", "ours": 99.5, "theirs": 99.6, "deltaKind": "Number", "delta": 0.1 }, { "field": "NetCash", "role": "NetCash", "type": "decimal", "outcome": "Break", "ours": 5000, "theirs": 5010, "deltaKind": "Number", "delta": 10, "attributedTo": ["Price"] }""";

    private const string ToleranceDiffs =
        """{ "field": "SettleDate", "role": "SettleDate", "type": "date", "outcome": "WithinTolerance", "ours": "2026-03-30", "theirs": "2026-03-31", "deltaKind": "Days", "delta": 1 }, { "field": "Fees", "role": "Fees", "type": "decimal", "outcome": "WithinTolerance", "ours": 10, "theirs": 10.1, "deltaKind": "Number", "delta": 0.1 }""";

    private static string AllEntries() =>
        string.Join(", ",
            Pair(PlainId, "Exact", "\"L-5\"", "\"R-5\"", Plain, Plain),
            Pair(StepsId, "Exact", "\"L-3\"", "\"R-3\"", Plain, Plain),
            Pair(ToleranceId, "WithinTolerance", "\"L-2\"", "\"R-2\"", Plain, TheirsPlain, ToleranceDiffs),
            Pair(BreakId, "Break", "\"L-1\"", "\"R-1\"", Plain, TheirsPlain, BreakDiffs),
            Pair(OneStepId, "Exact", "\"L-4\"", "\"R-4\"", Plain, Plain),
            Pair("f1b2c3d4e5f6a7b8", "Exact", "\"L-6\", \"L-7\"", "\"R-6\"", Plain, Plain, composition: "ManyToOne"),
            """{ "id": "g1b2c3d4e5f6a7b8", "alignment": "LeftOnly", "outcome": "NotCompared", "left": ["L-8"], "ours": { "Counterparty": "BARC" } }""");

    private static string AllRows() =>
        string.Join(", ",
            Row("left", "L-1", BreakId, Plain, """{ "Cpty": "BARX", "Qty": "1,000" }"""),
            Row("right", "R-1", BreakId, TheirsPlain, """{ "Cpty Name": "Barclays" }"""),
            Row("left", "L-2", ToleranceId, Plain, """{ "Cpty": "BARC" }"""),
            Row("right", "R-2", ToleranceId, TheirsPlain, """{ "Cpty Name": "Barclays", "Dir": "Buy" }"""),
            Row("left", "L-3", StepsId, Plain, """{ "Cpty": "BARX" }""",
                $"[ {Step("date.parse", "TradeDate", "27/03/2026", "2026-03-27")}, {Step("noop", "Currency", "USD", "USD")}, {Step("blank", "Fees", null, "0")}, {Step("alias", "Counterparty", "BARX", "BARC")} ]"),
            Row("right", "R-3", StepsId, Plain, """{ "Cpty Name": "Barclays", "Dir": "Buy" }""", $"[ {Step("side.map", "Side", "Buy", "BUY")} ]"),
            Row("left", "L-4", OneStepId, Plain, """{ "Cpty": "BARC" }""", $"[ {Step("alias", "Counterparty", "BARX", "BARC")} ]"),
            Row("right", "R-4", OneStepId, Plain, """{ "Cpty Name": "Barclays" }""", "[]"),
            Row("left", "L-5", PlainId, Plain, """{ "Cpty": "BARC" }""", $"[ {Step("noop", "Currency", "USD", "USD")} ]"),
            Row("right", "R-5", PlainId, Plain, """{ "Cpty Name": "Barclays" }"""),
            Row("left", "L-6", "f1b2c3d4e5f6a7b8", Plain, """{ "Cpty": "PKG" }"""),
            Row("left", "L-7", "f1b2c3d4e5f6a7b8", Plain, """{ "Cpty": "PKG" }"""),
            Row("right", "R-6", "f1b2c3d4e5f6a7b8", Plain, """{ "Cpty Name": "PKG" }"""),
            """{ "source": "left", "rowKey": "L-8", "disposition": "LeftOnly", "entryId": "g1b2c3d4e5f6a7b8", "values": {} }""");

    private static ReportInput Input(string options = RawColumns) => ReportTesting.Input(Body(AllEntries(), AllRows(), options));

    private static IReadOnlyList<SectionBlock> Cards(ReportDocument document) => document.Blocks.OfType<SectionBlock>().ToList();

    private static TableBlock TableAt(SectionBlock card, int index) => card.Blocks.OfType<TableBlock>().ElementAt(index);

    private static SectionBlock Card(ReportDocument document, string shortId) => Cards(document).Single(card => card.Title.StartsWith(shortId, StringComparison.Ordinal));

    [Fact]
    public void The_report_is_unavailable_without_raw_columns_and_without_one_to_one_matches()
    {
        Assert.Contains("rawColumns", Unavailable("BO3", Input("{}")));

        var none = ReportTesting.Input(Body("""{ "id": "x1", "alignment": "LeftOnly", "outcome": "NotCompared", "left": ["L-1"] }""", """{ "source": "left", "rowKey": "L-1", "disposition": "LeftOnly", "entryId": "x1" }""", RawColumns));
        Assert.Contains("No one-to-one matched entries to show.", Unavailable("BO3", none));

        var missing = ReportTesting.Input(Body("""{ "id": "x1", "alignment": "LeftOnly", "outcome": "NotCompared", "left": ["L-1"] }""", """{ "source": "left", "rowKey": "L-1", "disposition": "LeftOnly", "entryId": "x1" }"""));
        Assert.Contains("rawColumns", Unavailable("BO3", missing));
        Assert.Null(Unavailable("BO3", Input()));
    }

    [Fact]
    public void Cards_are_one_to_one_pairs_that_differ_or_were_normalized_with_breaks_first_then_tolerance_then_id()
    {
        var cards = Cards(Compose("BO3", Input()));

        Assert.Equal(["a1b2c3d4e5f6", "b1b2c3d4e5f6", "c1b2c3d4e5f6", "d1b2c3d4e5f6"], cards.Select(card => card.Title[..12]));
    }

    [Fact]
    public void A_card_banner_says_why_the_pair_is_shown()
    {
        var document = Compose("BO3", Input());

        Assert.Equal("a1b2c3d4e5f6  ·  break on Price  ·  matched by P1  ·  outcome Break  ·  ours L-1 / theirs R-1", Card(document, "a1b2c3d4e5f6").Title);
        Assert.Equal("b1b2c3d4e5f6  ·  within tolerance on SettleDate, Fees  ·  matched by P1  ·  outcome WithinTolerance  ·  ours L-2 / theirs R-2", Card(document, "b1b2c3d4e5f6").Title);
        Assert.Contains("exact after 3 normalization steps  ·", Card(document, "c1b2c3d4e5f6").Title);
        Assert.Contains("exact after 1 normalization step  ·", Card(document, "d1b2c3d4e5f6").Title);
    }

    [Fact]
    public void Raw_columns_are_deduplicated_and_only_those_with_a_value_on_a_shown_card_remain()
    {
        var card = Card(Compose("BO3", Input()), "a1b2c3d4e5f6");

        var ours = TableAt(card, 0);
        Assert.Equal(["Ours, as received", "Cpty", "Qty"], Header(ours));
        Assert.Equal(["raw", "BARX", "1,000"], Texts(ours.Rows[0]));
        Assert.Equal(RowKind.SubHeader, ours.Rows[0].Kind);
        Assert.All(ours.Rows[0].Cells.Skip(1), cell => Assert.Equal(Tone.Muted, cell.Tone));

        var theirs = TableAt(card, 1);
        Assert.Equal(["Theirs, as received", "Cpty Name", "Dir"], Header(theirs));
        Assert.Equal(["raw", "Barclays", ""], Texts(theirs.Rows[0]));
    }

    [Fact]
    public void A_raw_column_with_no_value_on_any_shown_card_is_left_out()
    {
        var document = Compose("BO3", Input("""{ "rawColumns": { "left": ["Cpty", "Qty"], "right": ["Dir"] }, "selectedEntries": ["e1"] }"""));

        var card = Assert.Single(Cards(document));
        Assert.Equal(["Ours, as received", "Cpty"], Header(TableAt(card, 0)));
        Assert.Equal(["Theirs, as received"], Header(TableAt(card, 1)));
    }

    [Fact]
    public void The_canonical_rows_show_mapped_roles_in_order_with_theirs_coloured_by_the_diff_state()
    {
        var card = Card(Compose("BO3", Input()), "a1b2c3d4e5f6");
        var table = TableAt(card, 2);

        Assert.Equal(
            ["Canonical", "Counterparty", "Instrument", "AssetClass", "Side", "TradeDate", "SettleDate", "Currency", "Quantity", "Price", "Accrued", "Fees", "NetCash"],
            Header(table));
        var ours = table.Rows[0];
        var theirs = table.Rows[1];
        Assert.Equal(["Ours, normalized", "BARC", "UST", "", "BUY", "2026-03-27", "2026-03-30", "USD", "1,000", "99.5", "", "10.00", "5,000.00"], Texts(ours));
        Assert.Equal(["Theirs, normalized", "BARC", "UST", "", "BUY", "2026-03-27", "2026-03-31", "USD", "1,000", "99.6", "", "10.10", "5,010.00"], Texts(theirs));
        Assert.Equal(Tone.Break, Cell(table, theirs, "Price").Tone);
        Assert.Equal(Tone.Attributed, Cell(table, theirs, "NetCash").Tone);
        Assert.All(ours.Cells, cell => Assert.Equal(Tone.None, cell.Tone));
        Assert.Equal(Tone.None, Cell(table, theirs, "Quantity").Tone);
        Assert.Equal(Tone.None, Cell(table, theirs, "Counterparty").Tone);
    }

    [Fact]
    public void A_within_tolerance_difference_colours_the_theirs_cell_as_tolerance()
    {
        var table = TableAt(Card(Compose("BO3", Input()), "b1b2c3d4e5f6"), 2);

        Assert.Equal(Tone.Tolerance, Cell(table, table.Rows[1], "SettleDate").Tone);
        Assert.Equal(Tone.Tolerance, Cell(table, table.Rows[1], "Fees").Tone);
        Assert.Equal(Tone.None, Cell(table, table.Rows[1], "Price").Tone);
    }

    [Fact]
    public void Unmapped_roles_are_not_shown_in_the_canonical_rows()
    {
        const string roles = """{ "Counterparty": "Cpty", "Quantity": "Qty", "Price": "Px" }""";
        var input = ReportTesting.Input(Body(AllEntries(), AllRows(), RawColumns, roles: roles));

        var table = TableAt(Card(Compose("BO3", input), "a1b2c3d4e5f6"), 2);

        Assert.Equal(["Canonical", "Cpty", "Qty", "Px"], Header(table));
    }

    [Fact]
    public void Normalization_steps_list_ours_then_theirs_and_skip_steps_that_changed_nothing()
    {
        var table = TableAt(Card(Compose("BO3", Input()), "c1b2c3d4e5f6"), 3);

        Assert.Equal("Normalization steps that changed a value", table.Title);
        Assert.Equal(
            [
                ["Ours", "date.parse", "TradeDate", "27/03/2026  →  2026-03-27"],
                ["Ours", "alias", "Counterparty", "BARX  →  BARC"],
                ["Theirs", "side.map", "Side", "Buy  →  BUY"]
            ],
            table.Rows.Select(row => Texts(row).ToArray()));
    }

    [Fact]
    public void A_pair_with_no_changed_step_and_an_exact_outcome_is_not_a_card_unless_selected()
    {
        Assert.DoesNotContain(Cards(Compose("BO3", Input())), card => card.Title.StartsWith("e1b2c3d4e5f6", StringComparison.Ordinal));

        var selected = Card(Compose("BO3", Input("""{ "rawColumns": { "left": ["Cpty"], "right": ["Cpty Name"] }, "selectedEntries": ["E1B2"] }""")), "e1b2c3d4e5f6");

        Assert.Contains("exact after 0 normalization steps", selected.Title);
        Assert.Empty(TableAt(selected, 3).Rows);
        Assert.Equal("None: normalization changed no value.", TableAt(selected, 3).Empty);
    }

    [Fact]
    public void Differences_show_each_field_with_its_delta_outcome_tone_and_attribution()
    {
        var document = Compose("BO3", Input());
        var breaks = TableAt(Card(document, "a1b2c3d4e5f6"), 4);

        Assert.Equal(["Field", "Ours", "Theirs", "Delta", "Outcome", "Attributed to"], Header(breaks));
        Assert.Equal(["Price", "99.5", "99.6", "0.1", "Break", ""], Texts(breaks.Rows[0]));
        Assert.Equal(["NetCash", "5,000.00", "5,010.00", "10.00", "Break", "Price"], Texts(breaks.Rows[1]));
        Assert.Equal(Tone.Break, Cell(breaks, breaks.Rows[0], "Outcome").Tone);
        Assert.Equal(Tone.Attributed, Cell(breaks, breaks.Rows[1], "Outcome").Tone);

        var tolerance = TableAt(Card(document, "b1b2c3d4e5f6"), 4);
        Assert.Equal(["SettleDate", "2026-03-30", "2026-03-31", "1", "WithinTolerance", ""], Texts(tolerance.Rows[0]));
        Assert.Equal(NumberFormat.Integer, Cell(tolerance, tolerance.Rows[0], "Delta").Format);
        Assert.Equal(Tone.Tolerance, Cell(tolerance, tolerance.Rows[0], "Outcome").Tone);
    }

    [Fact]
    public void A_pair_with_no_differences_says_it_is_exact_after_normalization()
    {
        var table = TableAt(Card(Compose("BO3", Input()), "c1b2c3d4e5f6"), 4);

        Assert.Empty(table.Rows);
        Assert.Equal("None: exact after normalization", table.Empty);
    }

    [Fact]
    public void Selected_entries_match_by_case_insensitive_prefix_in_entry_order_with_no_outcome_filter_and_no_cap()
    {
        var options = """{ "rawColumns": { "left": ["Cpty"], "right": ["Cpty Name"] }, "selectedEntries": ["E1B2", "a1b2", "f1b2", "g1b2"], "maxCards": 1 }""";

        var cards = Cards(Compose("BO3", Input(options)));

        Assert.Equal(["e1b2c3d4e5f6", "a1b2c3d4e5f6"], cards.Select(card => card.Title[..12]));
    }

    [Fact]
    public void Max_cards_caps_the_cards_when_nothing_is_selected()
    {
        var cards = Cards(Compose("BO3", Input("""{ "rawColumns": { "left": ["Cpty"], "right": ["Cpty Name"] }, "maxCards": 2 }""")));

        Assert.Equal(["a1b2c3d4e5f6", "b1b2c3d4e5f6"], cards.Select(card => card.Title[..12]));
    }

    [Fact]
    public void Headings_pass_through_the_label_overrides()
    {
        var options = """{ "rawColumns": { "left": ["Cpty"], "right": ["Cpty Name"] }, "labels": { "Differences": "Diffs", "Canonical": "Normalized", "raw": "as sent" } }""";

        var card = Card(Compose("BO3", Input(options)), "a1b2c3d4e5f6");

        Assert.Equal("Diffs", TableAt(card, 4).Title);
        Assert.Equal("Normalized", Header(TableAt(card, 2))[0]);
        Assert.Equal("as sent", Texts(TableAt(card, 0).Rows[0])[0]);
    }

    [Fact]
    public void The_cards_are_followed_by_the_entries_and_differences_data()
    {
        var blocks = Compose("BO3", Input()).Blocks;

        Assert.Equal(["Result entries", "Differences"], blocks.OfType<DataBlock>().Select(data => data.Name));
        Assert.IsType<DataBlock>(blocks[^1]);
    }

    [Fact]
    public void The_sample_shows_a_card_for_each_changed_or_differing_pair_with_its_normalization_steps()
    {
        var input = Sample();
        var rows = input.Rows.ToDictionary(row => (row.Source, row.RowKey));
        IEnumerable<TraceStep> Steps(RowInput row) => row.Trace.Where(step => step.Raw is not null && step.Canonical is not null && step.Raw != step.Canonical);
        var pairs = input.Entries
            .Where(entry => entry.Alignment == Alignment.Matched && entry.Left.Count == 1 && entry.Right.Count == 1)
            .Select(entry => (Entry: entry, Steps: Steps(rows[(SourceSide.Left, entry.Left[0])]).Count() + Steps(rows[(SourceSide.Right, entry.Right[0])]).Count()))
            .Where(pair => pair.Entry.Outcome != Outcome.Exact || pair.Steps > 0)
            .OrderBy(pair => pair.Entry.Outcome == Outcome.Break ? 0 : pair.Entry.Outcome == Outcome.WithinTolerance ? 1 : 2)
            .ThenBy(pair => pair.Entry.Id, StringComparer.Ordinal)
            .Take(50)
            .ToList();

        var cards = Cards(Compose("BO3", input));

        Assert.Equal(pairs.Count, cards.Count);
        Assert.Equal(pairs.Select(pair => pair.Entry.Id[..12]), cards.Select(card => card.Title[..12]));
        Assert.Equal(pairs.Select(pair => pair.Steps), cards.Select(card => TableAt(card, 3).Rows.Count));
        Assert.Equal(pairs.Count(pair => pair.Entry.Outcome == Outcome.Break), cards.Count(card => card.Title.Contains("break on ", StringComparison.Ordinal)));
        var leftColumns = input.Options.RawColumns!.Left.Where(column => pairs.Any(pair => rows[(SourceSide.Left, pair.Entry.Left[0])].Raw?.GetValueOrDefault(column) is not null)).ToList();
        Assert.Equal(["Ours, as received", .. leftColumns], Header(TableAt(cards[0], 0)));
    }
}
