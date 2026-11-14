using Scottplotz.Recon;
using Scottplotz.Reports;
using Scottplotz.Reports.Facts;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using Scottplotz.Reports.Rendering;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class ReportFoundationTests
{
    private const string LeftRow = """{ "source": "left", "rowKey": "L-1", "disposition": "Matched", "entryId": "e1", "values": { "Counterparty": "BAML", "Currency": "USD", "Quantity": "1000", "TradeDate": "2026-03-27", "NetCash": 5000 } }""";
    private const string RightRow = """{ "source": "right", "rowKey": "R-1", "disposition": "Matched", "entryId": "e1", "values": { "Counterparty": "BAML", "Currency": "USD", "Quantity": 1000 } }""";

    private static string Matched(string diffs = "", string outcome = "Exact") =>
        $$"""{ "id": "e1", "pathId": "trades", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "{{outcome}}", "left": ["L-1"], "right": ["R-1"], "ours": { "Counterparty": "BAML", "Currency": "USD", "NetCash": 5000, "SettleDate": "2026-03-30", "TradeDate": "2026-03-27" }, "theirs": { "Counterparty": "BAML", "Currency": "USD", "NetCash": 5010 }, "committedBy": "P1", "diffs": [ {{diffs}} ] }""";

    [Fact]
    public void An_input_reads_roles_values_and_defaults()
    {
        var input = Input(Body(Matched(), $"{LeftRow}, {RightRow}"));

        Assert.Equal(new DateOnly(2026, 3, 31), input.Run.AsOf);
        Assert.Equal("AssetClass", input.Run.Field(ReportRole.Asset));
        Assert.Equal(["USD", "EUR", "GBP", "CHF", "CAD", "JPY"], input.Options.CurrencyOrder);
        Assert.Equal(50, input.Options.MaxCards);
        Assert.Equal(ReconTheme.Auto, input.Options.Theme);
        Assert.Equal(Composition.OneToOne, input.Entries[0].Composition);
        Assert.Equal(1000m, input.Rows[0].Values[ReportRole.Quantity].Number);
        Assert.Equal(1000m, input.Rows[1].Values[ReportRole.Quantity].Number);
        Assert.Equal(new DateOnly(2026, 3, 27), input.Rows[0].Values[ReportRole.TradeDate].Date);
        Assert.True(input.Rows[0].ItemLead);
        Assert.Equal(Validity.Valid, input.Rows[0].Validity);
        Assert.True(input.Rows[0].Values[ReportRole.Price].IsNull);
    }

    [Theory]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [], "rows": [], "extra": 1 }""", "invalid-report-input", "extra")]
    [InlineData("""{ "run": { "asOf": "31/03/2026", "profileId": "p", "roles": {} }, "entries": [], "rows": [] }""", "invalid-report-input", "run.asOf")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "roles": {} }, "entries": [], "rows": [] }""", "invalid-report-input", "run.profileId")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": { "Colour": "x" } }, "entries": [], "rows": [] }""", "invalid-report-input", "Roles:")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "rows": [] }""", "invalid-report-input", "entries")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [ { "id": "a", "alignment": "Matchd", "outcome": "Exact" } ], "rows": [] }""", "invalid-report-input", "Matched, Ambiguous")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [ { "id": "a", "alignment": "LeftOnly", "outcome": "NotCompared", "left": ["L-9"] } ], "rows": [] }""", "unknown-row", "L-9")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [], "rows": [ { "source": "left", "rowKey": "L-1", "disposition": "Matched", "entryId": "zz" } ] }""", "unknown-entry", "zz")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [], "rows": [ { "source": "left", "rowKey": "L-1", "disposition": "LeftOnly" }, { "source": "left", "rowKey": "L-1", "disposition": "LeftOnly" } ] }""", "duplicate-key", "L-1")]
    [InlineData("""{ "run": { "asOf": "2026-03-31", "profileId": "p", "roles": {} }, "entries": [], "rows": [ { "source": "left", "rowKey": "L-1", "disposition": "LeftOnly", "values": { "Quantity": "many" } } ] }""", "invalid-report-input", "must be a number")]
    [InlineData("{ \"run\": ", "malformed-json", "")]
    public void A_bad_input_is_rejected_and_says_where(string body, string code, string mentions)
    {
        var problem = Problem(body);

        Assert.Equal(code, problem.Code);
        Assert.Equal(400, problem.Status);
        Assert.Contains(mentions, problem.Detail);
    }

    [Theory]
    [InlineData("2026-03-31", "2026-03-31", 0)]
    [InlineData("2026-03-30", "2026-03-31", 1)]
    [InlineData("2026-03-27", "2026-03-31", 2)]
    [InlineData("2026-03-28", "2026-03-31", 1)]
    [InlineData("2026-03-24", "2026-03-31", 5)]
    [InlineData("2026-04-01", "2026-03-31", -3)]
    public void Age_counts_weekdays_as_Excel_networkdays_does(string trade, string asOf, int expected)
    {
        Assert.Equal(expected, Calendar.BusinessAge(DateOnly.Parse(trade), DateOnly.Parse(asOf)));
    }

    [Fact]
    public void Settlement_status_comes_from_the_settle_date_and_the_as_of_date()
    {
        var asOf = new DateOnly(2026, 3, 31);

        Assert.Equal("Past settle date", Calendar.Status(new DateOnly(2026, 3, 30), asOf));
        Assert.Equal("Settles today", Calendar.Status(asOf, asOf));
        Assert.Equal("Upcoming", Calendar.Status(new DateOnly(2026, 4, 1), asOf));
        Assert.Equal("No settle date", Calendar.Status(null, asOf));
    }

    [Fact]
    public void A_break_becomes_an_open_item_with_its_root_field_impact_and_action()
    {
        const string diffs =
            """{ "field": "Price", "role": "Price", "type": "decimal", "outcome": "Break", "ours": 99.5, "theirs": 99.6, "deltaKind": "Number", "delta": 0.1 }, { "field": "NetCash", "role": "NetCash", "type": "decimal", "outcome": "Break", "ours": 5000, "theirs": 5010, "deltaKind": "Number", "delta": 10, "attributedTo": ["Price"] }""";
        var facts = Facts(Body(Matched(diffs, "Break"), $"{LeftRow}, {RightRow}"));

        var item = Assert.Single(facts.OpenItems);
        Assert.Equal(["Price"], facts.Entries[0].RootFields);
        Assert.Equal("Field break", item.ItemType);
        Assert.Equal("Price", item.Category);
        Assert.Equal(10m, item.Impact);
        Assert.Equal("Confirm execution price with counterparty", item.Action);
        Assert.Equal("Past settle date", item.Status(facts.Input.Run.AsOf));
        Assert.Equal(2, item.Age(facts.Input.Run.AsOf));
        Assert.Equal([DiffState.Break, DiffState.Attributed], facts.Diffs.Select(diff => diff.State));
    }

    [Fact]
    public void When_every_break_is_attributed_the_first_break_stands_in_as_the_root()
    {
        const string diffs =
            """{ "field": "Fees", "role": "Fees", "type": "decimal", "outcome": "Break", "ours": 1, "theirs": 2, "deltaKind": "Number", "delta": 1, "attributedTo": ["NetCash"] }, { "field": "NetCash", "role": "NetCash", "type": "decimal", "outcome": "Break", "ours": 5000, "theirs": 5010, "deltaKind": "Number", "delta": 10, "attributedTo": ["Fees"] }""";

        Assert.Equal(["Fees"], Facts(Body(Matched(diffs, "Break"), $"{LeftRow}, {RightRow}")).Entries[0].RootFields);
    }

    [Theory]
    [InlineData("Absolute", "0.05", "null", 0.05)]
    [InlineData("Relative", "null", "0.001", 0.0996)]
    [InlineData("AbsoluteOrRelative", "0.05", "0.001", 0.0996)]
    [InlineData("Banded", "0.05", "0.001", null)]
    public void A_tolerance_amount_comes_from_the_declared_rule(string kind, string absolute, string relative, double? expected)
    {
        const string diff = """{ "field": "Price", "role": "Price", "type": "decimal", "outcome": "WithinTolerance", "ours": 99.5, "theirs": 99.6, "deltaKind": "Number", "delta": 0.1 }""";
        var options = $$"""{ "tolerances": [ { "path": "trades", "field": "Price", "kind": "{{kind}}", "absolute": {{absolute}}, "relative": {{relative}} } ] }""";

        var facts = Facts(Body(Matched(diff, "WithinTolerance"), $"{LeftRow}, {RightRow}", options));

        Assert.Equal(expected is { } amount ? (decimal)amount : null, facts.Diffs[0].ToleranceAmount);
        Assert.Equal(DiffState.Tolerance, facts.Diffs[0].State);
        Assert.Empty(facts.OpenItems);
    }

    [Fact]
    public void Open_items_rank_by_settlement_status_then_currency_then_impact()
    {
        static string Entry(string id, string settle, string currency, int cash) =>
            $$"""{ "id": "{{id}}", "alignment": "LeftOnly", "outcome": "NotCompared", "left": ["{{id}}"], "ours": { "SettleDate": {{settle}}, "Currency": "{{currency}}", "NetCash": {{cash}} }, "committedBy": "P1" }""";
        static string Row(string id) => $$"""{ "source": "left", "rowKey": "{{id}}", "disposition": "LeftOnly", "entryId": "{{id}}" }""";
        var ids = new[] { "a", "b", "c", "d", "e" };
        var entries = string.Join(", ",
            Entry("a", "\"2026-04-02\"", "USD", 10), Entry("b", "\"2026-03-30\"", "EUR", 10), Entry("c", "\"2026-03-30\"", "USD", 5),
            Entry("d", "\"2026-03-30\"", "USD", 50), Entry("e", "null", "USD", 999));

        var facts = Facts(Body(entries, string.Join(", ", ids.Select(Row))));

        Assert.Equal(["d", "c", "b", "a", "e"], facts.OpenItems.Select(item => item.Key));
        Assert.Equal(["Unmatched ours", "Unmatched theirs"], Categories.Of(facts));
    }

    [Fact]
    public void An_orphan_row_is_an_open_item_and_an_unrouted_entry_says_so()
    {
        const string entry = """{ "id": "u1", "alignment": "RightOnly", "outcome": "NotCompared", "right": ["R-7"], "theirs": { "Currency": "USD" }, "trail": [ { "pass": "", "verdict": "NoCandidate", "detail": "Unrouted" } ] }""";
        const string rows =
            """{ "source": "right", "rowKey": "R-7", "disposition": "RightOnly", "entryId": "u1" }, { "source": "left", "rowKey": "L-Q", "disposition": "Quarantined", "validity": "Invalid", "diagnostics": ["BadDate", "BadDate"] }""";

        var facts = Facts(Body(entry, rows));

        var unrouted = facts.OpenItems.Single(item => item.Entry is not null);
        var orphan = facts.OpenItems.Single(item => item.Row is not null);
        Assert.Equal("(not routed)", unrouted.Field);
        Assert.Equal("Not routed to any matching path: check the profile routes", unrouted.Action);
        Assert.Equal("Unmatched ours", orphan.ItemType);
        Assert.Equal("(excluded: invalid)", orphan.Field);
        Assert.Equal("Fix the input row: the engine excluded it as invalid (BadDate)", orphan.Action);
        Assert.Equal("(no entry) left:L-Q", orphan.Key);
    }

    [Theory]
    [InlineData(1234.5, NumberFormat.Amount, "1,234.50")]
    [InlineData(-1234.5, NumberFormat.Amount, "(1,234.50)")]
    [InlineData(0, NumberFormat.Amount, "-")]
    [InlineData(-2500, NumberFormat.Quantity, "(2,500)")]
    [InlineData(0, NumberFormat.Count, "-")]
    [InlineData(12, NumberFormat.Count, "12")]
    [InlineData(3, NumberFormat.SignedCount, "+3")]
    [InlineData(-3, NumberFormat.SignedCount, "-3")]
    [InlineData(0.9512, NumberFormat.Percent, "95.1%")]
    [InlineData(99.5, NumberFormat.Price, "99.5")]
    [InlineData(4, NumberFormat.Integer, "4")]
    public void Numbers_are_written_in_the_report_formats(double value, NumberFormat format, string expected)
    {
        Assert.Equal(expected, CellText.Number((decimal)value, format));
    }

    [Fact]
    public void A_page_escapes_text_carries_no_script_and_is_the_same_bytes_each_time()
    {
        var document = new ReportDocument(
            ReportCatalogue.Of("MO5"),
            "<script>alert(1)</script>",
            new DateOnly(2026, 3, 31),
            "run & <b>line</b>",
            [new TableBlock("T", [new ReportColumn("<i>col</i>")], [ReportRow.Of("<img src=x>", ReportCell.Check(true))], "note")]);

        var first = ReportHtml.Render(document, ReconTheme.Dark);
        var second = ReportHtml.Render(document, ReconTheme.Dark);

        Assert.Equal(first.Html, second.Html);
        Assert.StartsWith("<!doctype html>", first.Html);
        Assert.Contains("data-theme=\"dark\"", first.Html);
        Assert.DoesNotContain("<script", first.Html);
        Assert.DoesNotContain("<img", first.Html);
        Assert.Contains("&lt;img src=x&gt;", first.Html);
        Assert.Contains($"content=\"{first.ContentSecurityPolicy}\"", first.Html);
        Assert.DoesNotContain("script-src", first.ContentSecurityPolicy);
        Assert.DoesNotContain("\r", first.Html);
    }
}
