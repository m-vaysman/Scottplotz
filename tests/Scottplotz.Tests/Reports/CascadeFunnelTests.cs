using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class CascadeFunnelTests
{
    private static string Entry(string id, string left, string right, string committedBy = "P1") =>
        $$"""{ "id": "{{id}}", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "Exact", "left": [{{left}}], "right": [{{right}}], "committedBy": "{{committedBy}}" }""";

    private static string R(string source, string key, string disposition, string entry = "", string extra = "") =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "{{disposition}}"{{(entry.Length > 0 ? $", \"entryId\": \"{entry}\"" : "")}}{{extra}} }""";

    private static TableBlock Funnel(string entries, string rows, string passes = "[\"P1\", \"P2\"]") =>
        Tables(Compose("MO3", Input(Body(entries, rows, passes: passes))))[0];

    private static IReadOnlyList<string> Stage(TableBlock table, string name) => Texts(Row(table, name));

    private static readonly string Plain = string.Join(", ",
        R("left", "L-1", "Matched", "e1"), R("right", "R-1", "Matched", "e1"),
        R("left", "L-2", "Matched", "e2"), R("right", "R-2", "Matched", "e2"),
        R("left", "L-3", "LeftOnly"), R("left", "L-4", "LeftOnly"), R("right", "R-3", "RightOnly"));

    private static readonly string PlainEntries = $"{Entry("e1", "\"L-1\"", "\"R-1\"")}, {Entry("e2", "\"L-2\"", "\"R-2\"", "P2")}";

    [Fact]
    public void Stages_count_rows_per_side_and_walk_down_to_the_residue()
    {
        var table = Funnel(PlainEntries, Plain);

        Assert.Equal(["Stage", "Ours: change", "Ours: remaining", "Theirs: change", "Theirs: remaining", "Note"], Header(table));
        Assert.Equal(["Rows read", "", "4", "", "3", "Every input row"], Stage(table, "Rows read"));
        Assert.Equal(["After lifecycle collapse", "-", "4", "-", "3", "Superseded and cancel rows of a chain are suppressed"], Stage(table, "After lifecycle collapse"));
        Assert.Equal(["Items after packages", "-", "4", "-", "3", "The legs of a package become one item"], Stage(table, "Items after packages"));
        Assert.Equal(["P1", "-1", "3", "-1", "2", "commits 1 entries"], Stage(table, "P1"));
        Assert.Equal(["P2", "-1", "2", "-1", "1", "commits 1 entries"], Stage(table, "P2"));
        Assert.Equal(["Residue", "", "2", "", "1", "Unmatched ours and unmatched theirs"], Stage(table, "Residue"));
        Assert.DoesNotContain(table.Rows, row => Texts(row)[0] is "After quarantine" or "Unresolved");
    }

    [Fact]
    public void Suppressed_rows_leave_after_lifecycle_collapse_and_quarantine_and_unresolved_stages_appear_only_when_needed()
    {
        var rows = string.Join(", ",
            R("left", "L-1", "Suppressed"), R("left", "L-2", "Suppressed"), R("right", "R-1", "Suppressed"),
            R("left", "L-3", "Quarantined"), R("right", "R-2", "Quarantined"), R("right", "R-3", "Quarantined"),
            R("left", "L-4", "Ambiguous"), R("left", "L-5", "SearchIncomplete"), R("right", "R-4", "Ambiguous"),
            R("left", "L-6", "LeftOnly"));

        var table = Funnel("", rows, "[]");

        Assert.Equal(["Rows read", "", "6", "", "4", "Every input row"], Stage(table, "Rows read"));
        Assert.Equal("-2", Stage(table, "After lifecycle collapse")[1]);
        Assert.Equal("-1", Stage(table, "After lifecycle collapse")[3]);
        Assert.Equal(["After quarantine", "-1", "3", "-2", "1", "Rows the engine quarantined (duplicate conflicts, broken chains or packages)"], Stage(table, "After quarantine"));
        Assert.Equal(["Unresolved", "-2", "1", "-1", "-", "Ambiguous or search-incomplete items"], Stage(table, "Unresolved"));
        Assert.Equal("1", Stage(table, "Residue")[2]);
        Assert.Equal("-", Stage(table, "Residue")[4]);
    }

    [Fact]
    public void A_package_counts_once_as_an_item_and_only_lead_rows_are_committed()
    {
        var rows = string.Join(", ",
            R("left", "L-1", "Matched", "e1"), R("left", "L-2", "Matched", "e1", ", \"itemLead\": false"),
            R("right", "R-1", "Matched", "e1"));
        var entries = Entry("e1", "\"L-1\", \"L-2\"", "\"R-1\"");

        var table = Funnel(entries, rows, "[\"P1\"]");

        Assert.Equal(["Rows read", "", "2", "", "1", "Every input row"], Stage(table, "Rows read"));
        Assert.Equal(["Items after packages", "-1", "1", "-", "1", "The legs of a package become one item"], Stage(table, "Items after packages"));
        Assert.Equal(["P1", "-1", "-", "-1", "-", "commits 1 entries"], Stage(table, "P1"));
        Assert.Equal("-", Stage(table, "Residue")[2]);
    }

    [Fact]
    public void Passes_follow_the_declared_order_then_any_other_committing_pass()
    {
        var table = Funnel(PlainEntries, Plain, "[\"P2\"]");

        Assert.Equal(["Rows read", "After lifecycle collapse", "Items after packages", "P2", "P1", "Residue"], table.Rows.Take(6).Select(row => Texts(row)[0]));
    }

    [Fact]
    public void The_residue_is_marked_and_the_check_passes_when_the_last_stage_equals_it()
    {
        var table = Funnel(PlainEntries, Plain);

        var residue = Row(table, "Residue");
        Assert.Equal([Tone.Tolerance, Tone.Tolerance], [residue.Cells[2].Tone, residue.Cells[4].Tone]);
        var check = table.Rows[^1];
        Assert.Equal("Check: remaining after the last stage = residue", CellText.Of(check.Cells[0]));
        Assert.Equal(["✓", "✓"], [CellText.Of(check.Cells[2]), CellText.Of(check.Cells[4])]);
    }

    [Fact]
    public void The_check_fails_when_matched_rows_were_not_committed_by_a_listed_pass()
    {
        var rows = string.Join(", ", R("left", "L-1", "Matched", "e1"), R("right", "R-1", "Matched", "e1"));
        var entries = """{ "id": "e1", "alignment": "Matched", "composition": ["OneToOne"], "outcome": "Exact", "left": ["L-1"], "right": ["R-1"] }""";

        var check = Funnel(entries, rows, "[]").Rows[^1];

        Assert.Equal(["✗", "✗"], [CellText.Of(check.Cells[2]), CellText.Of(check.Cells[4])]);
        Assert.Equal([Tone.Fail, Tone.Fail], [check.Cells[2].Tone, check.Cells[4].Tone]);
    }

    [Fact]
    public void The_chart_follows_the_table_with_ours_and_theirs_remaining_for_every_stage()
    {
        var document = Compose("MO3", Input(Body(PlainEntries, Plain, passes: "[\"P1\", \"P2\"]")));

        Assert.Equal(["TableBlock", "BarsBlock", "DataBlock"], document.Blocks.Select(block => block.GetType().Name));
        var bars = (BarsBlock)document.Blocks[1];
        Assert.Equal("Items remaining after each stage", bars.Title);
        Assert.Equal("Items", bars.AxisTitle);
        Assert.Equal(
            ["Rows read · Ours", "Rows read · Theirs", "After lifecycle collapse · Ours", "After lifecycle collapse · Theirs", "Items after packages · Ours", "Items after packages · Theirs", "P1 · Ours", "P1 · Theirs", "P2 · Ours", "P2 · Theirs", "Residue · Ours", "Residue · Theirs"],
            bars.Bars.Select(bar => bar.Label));
        Assert.Equal([4m, 3m, 4m, 3m, 4m, 3m, 3m, 2m, 2m, 1m, 2m, 1m], bars.Bars.Select(bar => bar.Value));
        Assert.Equal(["Result rows"], document.Blocks.OfType<DataBlock>().Select(data => data.Name));
    }

    [Fact]
    public void The_report_has_no_required_roles_and_labels_replace_stage_names()
    {
        var table = Tables(Compose("MO3", Input(Body("", R("left", "L-1", "LeftOnly"), options: """{ "labels": { "Rows read": "Read in", "Stage": "Step" } }""")))).First();

        Assert.Empty(MoRequired());
        Assert.Equal("Step", Header(table)[0]);
        Assert.Equal("Read in", CellText.Of(table.Rows[0].Cells[0]));
    }

    private static IReadOnlyList<ReportRole> MoRequired() => Scottplotz.Reports.ReportRegistry.Find("MO3")!.Required;

    [Fact]
    public void The_sample_funnel_matches_counts_worked_out_from_the_input()
    {
        var input = Sample();
        var committed = input.Entries.ToDictionary(entry => entry.Id, entry => entry.CommittedBy);
        int Rows(SourceSide side, Func<RowInput, bool> where) => input.Rows.Count(row => row.Source == side && where(row));
        int Passed(SourceSide side, string pass) =>
            Rows(side, row => row.ItemLead && row.Disposition == Disposition.Matched && row.EntryId is { } id && committed[id] == pass);

        var table = Tables(Compose("MO3", input))[0];

        foreach (var (side, change, remaining) in new[] { (SourceSide.Left, 1, 2), (SourceSide.Right, 3, 4) })
        {
            Assert.Equal(CellText.Number(Rows(side, _ => true), NumberFormat.Count), Texts(Row(table, "Rows read"))[remaining]);
            Assert.Equal(CellText.Number(-Rows(side, row => row.Disposition == Disposition.Suppressed), NumberFormat.SignedCount), Texts(Row(table, "After lifecycle collapse"))[change]);
            Assert.Equal(CellText.Number(-Passed(side, input.Passes[0]), NumberFormat.SignedCount), Texts(Row(table, input.Passes[0]))[change]);
            Assert.Equal(
                CellText.Number(Rows(side, row => row.ItemLead && row.Disposition == (side == SourceSide.Left ? Disposition.LeftOnly : Disposition.RightOnly)), NumberFormat.Count),
                Texts(Row(table, "Residue"))[remaining]);
        }

        Assert.Equal($"commits {input.Entries.Count(entry => entry.Alignment == Alignment.Matched && entry.CommittedBy == input.Passes[0])} entries", Texts(Row(table, input.Passes[0]))[5]);
    }
}
