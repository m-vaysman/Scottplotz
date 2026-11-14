using Scottplotz.Charting;
using Scottplotz.Recon;

namespace Scottplotz.Tests.Recon;

public sealed class ReconParserTests
{
    private static readonly ReconView View = ReconViews.Find("dumbbell")!;

    private static ReconModel Ok(string body, ReconLimits? limits = null) =>
        ReconParser.Parse(body, View, limits ?? ReconLimits.Default) switch
        {
            Result<ReconModel>.Ok ok => ok.Value,
            Result<ReconModel>.Fail fail => throw new Xunit.Sdk.XunitException($"{fail.Problem.Code}: {fail.Problem.Detail}"),
            _ => throw new InvalidOperationException()
        };

    private static ChartProblem Fail(string body, ReconLimits? limits = null) =>
        ReconParser.Parse(body, View, limits ?? ReconLimits.Default) switch
        {
            Result<ReconModel>.Fail fail => fail.Problem,
            _ => throw new Xunit.Sdk.XunitException("Expected a problem, but the request parsed.")
        };

    private static string Records(string records, string rest = "") =>
        $$"""{ {{rest}} "records": [ {{records}} ] }""";

    [Fact]
    public void The_smallest_request_is_records_with_bare_numbers()
    {
        var model = Ok(Records("""{ "key": "A", "left": 100, "right": 100 }"""));

        Assert.Equal("Left", model.Left.Name);
        Assert.Equal("Right", model.Right.Short);
        Assert.Equal("records", model.Unit);
        Assert.Equal("value", model.Measure);
        Assert.Equal(ReconStatus.Exact, model.Records[0].Status);
        Assert.Equal("A", model.Records[0].Name);
        Assert.Equal(ReconTheme.Auto, model.Theme);
    }

    [Theory]
    [InlineData("100", "100", "exact")]
    [InlineData("100", "100.5", "tolerance")]
    [InlineData("100", "101.01", "break")]
    [InlineData("100", "null", "leftOnly")]
    [InlineData("null", "100", "rightOnly")]
    public void Status_comes_from_the_two_sides_and_the_tolerance(string left, string right, string expected)
    {
        var model = Ok(Records($$"""{ "key": "A", "left": {{left}}, "right": {{right}} }""", "\"tolerance\": 1,"));

        Assert.Equal(expected, ReconComparison.Token(model.Records[0].Status));
    }

    [Fact]
    public void Percent_tolerance_is_measured_against_the_left_value()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": 200, "right": 201 }, { "key": "B", "left": 200, "right": 201.1 }""",
            "\"tolerance\": { \"percent\": 0.5 },"));

        Assert.Equal(ReconStatus.Tolerance, model.Records[0].Status);
        Assert.Equal(ReconStatus.Break, model.Records[1].Status);
        Assert.Equal(0.5, model.Records[0].Percent);
    }

    [Fact]
    public void A_status_the_caller_sends_wins_over_the_derived_one()
    {
        var model = Ok(Records("""{ "key": "A", "left": 100, "right": 250, "status": "tolerance" }"""));

        Assert.Equal(ReconStatus.Tolerance, model.Records[0].Status);
        Assert.Equal(150, model.Records[0].Delta);
    }

    [Fact]
    public void A_status_that_contradicts_the_sides_is_rejected()
    {
        var problem = Fail(Records("""{ "key": "A", "left": 100, "status": "exact" }"""));

        Assert.Equal("status-mismatch", problem.Code);
        Assert.Contains("records[0]", problem.Detail);
    }

    [Fact]
    public void A_record_with_only_a_status_stands_for_a_count_of_records()
    {
        var model = Ok(Records(
            """{ "key": "bulk", "status": "exact", "count": 1182 }, { "key": "A", "left": 5, "right": 9, "duplicate": "right" }"""));

        Assert.Equal(1182, model.Summary.Exact);
        Assert.Equal(1, model.Summary.Break);
        Assert.Equal(1183, model.Summary.LeftCount);
        Assert.Equal(1, model.Summary.Duplicates);
        Assert.Equal(ReconDuplicate.Right, model.Records[1].Duplicate);
    }

    [Fact]
    public void A_record_with_no_side_and_no_status_is_rejected()
    {
        Assert.Equal("missing-sides", Fail(Records("""{ "key": "A" }""")).Code);
    }

    [Fact]
    public void Fields_are_inferred_from_the_records_when_not_declared()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": { "qty": 2, "paid": "2026-03-01", "who": "Ann" }, "right": { "qty": 2.5, "paid": "2026-03-02", "who": "Ann" } }"""));

        Assert.Equal(["qty", "paid", "who"], model.Fields.Select(field => field.Key));
        Assert.Equal([ReconFieldType.Number, ReconFieldType.Date, ReconFieldType.Text], model.Fields.Select(field => field.Type));
        Assert.Equal(2, model.Fields[0].Decimals);
        Assert.Equal("qty", model.Measure);
        Assert.Equal(["qty", "paid"], model.Records[0].Breaks);
    }

    [Fact]
    public void A_date_field_tolerance_is_counted_in_days()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": { "paid": "2026-03-01" }, "right": { "paid": "2026-03-02" } }, { "key": "B", "left": { "paid": "2026-03-01" }, "right": { "paid": "2026-03-04" } }""",
            """ "fields": [ { "key": "paid", "type": "date", "tolerance": 1 } ],"""));

        Assert.Equal(ReconStatus.Tolerance, model.Records[0].Status);
        Assert.Equal(ReconStatus.Break, model.Records[1].Status);
        Assert.Null(model.Measure);
        Assert.Null(model.Summary.Net);
    }

    [Fact]
    public void A_field_marked_not_compared_never_causes_a_break()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": { "qty": 1, "ext": 10 }, "right": { "qty": 9, "ext": 10 } }""",
            """ "fields": [ { "key": "qty", "compare": false }, "ext" ], "measure": "ext","""));

        Assert.Equal(ReconStatus.Exact, model.Records[0].Status);
    }

    [Fact]
    public void A_side_given_as_lines_is_summed_and_the_lines_are_kept()
    {
        var model = Ok(Records(
            """{ "key": "G1", "left": [ { "key": "R1", "amt": 40 }, { "key": "R2", "amt": 60 } ], "right": [ { "key": "I1", "name": "Invoice 1", "amt": 100 } ] }"""));

        var record = model.Records[0];
        Assert.Equal(ReconStatus.Exact, record.Status);
        Assert.Equal(2, record.Left!.Lines.Count);
        Assert.Equal("Invoice 1", record.Right!.Lines[0].Name);
        Assert.Equal(new ReconValue.Number(100), record.Left.Values[0]);
    }

    [Fact]
    public void Totals_and_net_are_summed_unless_the_caller_overrides_them()
    {
        var summed = Ok(Records("""{ "key": "A", "left": 10, "right": 12 }, { "key": "B", "left": 5 }"""));
        var overridden = Ok(Records("""{ "key": "A", "left": 10, "right": 12 }""", """ "totals": { "left": 1000, "right": 1500 },"""));

        Assert.Equal(15, summed.Summary.LeftTotal);
        Assert.Equal(-3, summed.Summary.Net);
        Assert.Equal(500, overridden.Summary.Net);
    }

    [Fact]
    public void Side_names_fill_the_labels_and_the_view_text()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": 1, "right": 2 }""",
            """ "left": { "name": "Our books", "short": "Ours" }, "right": { "name": "Custodian statement", "short": "Custodian" }, "unit": { "one": "position", "many": "positions" }, "tolerance": 0.5,"""));

        var labels = model.Labels.ToDictionary(item => item.Key, item => item.Value);
        var text = model.Text.ToDictionary(item => item.Key, item => item.Value);
        Assert.Equal("Ours only", labels["leftOnly"]);
        Assert.Equal("Custodian minus Ours", labels["net"]);
        Assert.Equal("Our books against Custodian statement", text["title"]);
        Assert.Equal("Tolerance is 0.5 per position.", text["chartSub"]);
    }

    [Fact]
    public void Labels_text_and_options_can_be_overridden()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": 1, "right": 2 }""",
            """ "labels": { "break": "Dispute" }, "text": { "title": "Orders, {asOf}" }, "asOf": "2026-03-31", "options": { "sortBy": "difference" },"""));

        Assert.Contains(new KeyValuePair<string, string>("break", "Dispute"), model.Labels);
        Assert.Contains(new KeyValuePair<string, string>("title", "Orders, 2026-03-31"), model.Text);
        Assert.Contains(new KeyValuePair<string, ReconValue?>("sortBy", new ReconValue.Text("difference")), model.Options);
        Assert.Contains(new KeyValuePair<string, ReconValue?>("maxRows", new ReconValue.Number(400)), model.Options);
    }

    [Theory]
    [InlineData(""" "colour": "red", """, "unknown-property", "Allowed properties")]
    [InlineData(""" "labels": { "brk": "x" }, """, "unknown-label", "break")]
    [InlineData(""" "text": { "headline": "x" }, """, "unknown-text", "title")]
    [InlineData(""" "options": { "sort": "x" }, """, "unknown-option", "sortBy")]
    [InlineData(""" "options": { "sortBy": "size" }, """, "invalid-option", "input, difference")]
    [InlineData(""" "options": { "maxRows": 0 }, """, "invalid-option", "at least 1")]
    [InlineData(""" "theme": "sepia", """, "invalid-theme", "auto, light, dark")]
    [InlineData(""" "palette": { "accent": "#fff" }, """, "invalid-palette", "left")]
    [InlineData(""" "palette": { "left": "red; background:url(x)" }, """, "invalid-palette", "hex")]
    [InlineData(""" "measure": "nope", """, "unknown-measure", "value")]
    [InlineData(""" "asOf": "31/03/2026", """, "invalid-value", "ISO 8601")]
    [InlineData(""" "tolerance": -1, """, "invalid-value", "zero or more")]
    [InlineData(""" "fields": [ "a", "a" ], """, "duplicate-field", "'a'")]
    public void Anything_unknown_or_invalid_is_rejected_and_says_what_is_allowed(string extra, string code, string mentions)
    {
        var problem = Fail(Records("""{ "key": "A", "left": 1, "right": 2 }""", extra));

        Assert.Equal(code, problem.Code);
        Assert.Equal(400, problem.Status);
        Assert.Contains(mentions, problem.Detail);
    }

    [Theory]
    [InlineData("""{ "left": 1 }""", "invalid-value", "records[0].key")]
    [InlineData("""{ "key": "A", "left": "ten" }""", "invalid-value", "records[0].left")]
    [InlineData("""{ "key": "A", "left": { "amt": 1 }, "right": { "amount": 1 } }""", "unknown-field", "amount")]
    [InlineData("""{ "key": "A", "left": 1, "colour": "red" }""", "unknown-property", "colour")]
    [InlineData("""{ "key": "A", "left": 1, "status": "matched" }""", "invalid-value", "leftOnly, exact")]
    [InlineData("""{ "key": "A", "left": 1, "count": 0 }""", "invalid-value", "one or more")]
    [InlineData("""{ "key": "A", "left": 1, "date": "yesterday" }""", "invalid-value", "ISO 8601")]
    [InlineData("""{ "key": "A", "left": 1, "attrs": { "x": [1] } }""", "invalid-value", "records[0].attrs.x")]
    public void A_bad_record_is_rejected_with_its_path(string record, string code, string mentions)
    {
        var problem = Fail(Records(record, """ "fields": [ "amt" ],"""));

        Assert.Equal(code, problem.Code);
        Assert.Contains(mentions, problem.Detail);
    }

    [Theory]
    [InlineData("{ \"records\": [", "malformed-json")]
    [InlineData("[1, 2]", "invalid-recon")]
    [InlineData("{ }", "missing-records")]
    [InlineData("{ \"records\": [] }", "empty-records")]
    [InlineData("{ \"records\": {} }", "invalid-value")]
    public void A_body_without_usable_records_is_rejected(string body, string code)
    {
        Assert.Equal(code, Fail(body).Code);
    }

    [Fact]
    public void Record_and_field_limits_are_enforced()
    {
        var tight = new ReconLimits(MaxRecords: 1, MaxFields: 1);

        Assert.Equal("too-many-records", Fail(Records("""{ "key": "A", "left": 1 }, { "key": "B", "left": 1 }"""), tight).Code);
        Assert.Equal("too-many-fields", Fail(Records("""{ "key": "A", "left": { "a": 1, "b": 2 } }"""), tight).Code);
    }

    [Fact]
    public void Groups_carry_a_name_a_note_and_control_totals_in_the_order_sent()
    {
        var model = Ok(Records(
            """{ "key": "A", "left": 1, "right": 2, "group": ["North", "Fees"] }""",
            """ "groups": [ { "key": "South", "note": "Closed in March." }, { "key": ["North", "Fees"], "name": "Bank fees", "left": 10, "right": 12 } ],"""));

        Assert.Equal(["South", "Bank fees"], model.Groups.Select(group => group.Name));
        Assert.Equal("Closed in March.", model.Groups[0].Note);
        Assert.Equal(new ReconValue.Number(12), model.Groups[1].Right!.Values[0]);
        Assert.Equal(["North", "Fees"], model.Records[0].Group);
    }
}
