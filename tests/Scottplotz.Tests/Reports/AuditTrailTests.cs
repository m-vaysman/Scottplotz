using Scottplotz.Reports;
using Scottplotz.Reports.Input;
using Scottplotz.Reports.Model;
using static Scottplotz.Tests.Reports.ReportTesting;

namespace Scottplotz.Tests.Reports;

public sealed class AuditTrailTests
{
    private const string Note = "The engine's explanation records the pass that committed each match, or the pass that left an entry open, with its key, candidates and detail. Passes an entry fell through before that are not recorded by the engine.";

    private static string Matched(string id, string outcome, string composition = "\"OneToOne\"", string left = "\"L-1\"", string right = "\"R-1\"", string diffs = "", string trail = "") =>
        $$"""{ "id": "{{id}}", "alignment": "Matched", "composition": [{{composition}}], "outcome": "{{outcome}}", "left": [{{left}}], "right": [{{right}}], "committedBy": "P1", "trail": [ {{trail}} ], "diffs": [ {{diffs}} ] }""";

    private static string Open(string id, string alignment, string left = "", string right = "", string trail = "", string hints = "") =>
        $$"""{ "id": "{{id}}", "alignment": "{{alignment}}", "outcome": "NotCompared", "left": [{{left}}], "right": [{{right}}], "committedBy": "P2", "trail": [ {{trail}} ], "hints": [ {{hints}} ] }""";

    private static string Attempt(string pass, string verdict, string? key = null, string? detail = null, string candidates = "") =>
        $$"""{ "pass": "{{pass}}", "verdict": "{{verdict}}", "key": {{(key is null ? "null" : $"\"{key}\"")}}, "detail": {{(detail is null ? "null" : $"\"{detail}\"")}}, "candidates": [ {{candidates}} ] }""";

    private static string Row(string source, string key, string entry, string disposition = "Matched", string? suppression = null) =>
        $$"""{ "source": "{{source}}", "rowKey": "{{key}}", "disposition": "{{disposition}}", "entryId": "{{entry}}"{{(suppression is null ? "" : $", \"suppression\": \"{suppression}\"")}} }""";

    private const string BreakDiffs =
        """{ "field": "Price", "role": "Price", "type": "decimal", "outcome": "Break", "ours": 99.5, "theirs": 99.6, "deltaKind": "Number", "delta": 0.1 }, { "field": "Fees", "role": "Fees", "type": "decimal", "outcome": "WithinTolerance", "ours": 10, "theirs": 10.1, "deltaKind": "Number", "delta": 0.1 }""";

    private static ReportInput Make(string entries, string rows, string options = "{}") => ReportTesting.Input(Body(entries, rows, options));

    private static IReadOnlyList<SectionBlock> Cards(ReportDocument document) => document.Blocks.OfType<SectionBlock>().ToList();

    private static TableBlock Steps(SectionBlock card) => Assert.IsType<TableBlock>(Assert.Single(card.Blocks));

    private static IReadOnlyList<string> Stage(string title) => title.Split("  ·  ");

    private const string ToleranceDiffs =
        """{ "field": "Fees", "role": "Fees", "type": "decimal", "outcome": "WithinTolerance", "ours": 10, "theirs": 10.1, "deltaKind": "Number", "delta": 0.1 }""";

    private static ReportInput Mixed() =>
        Make(
            string.Join(", ",
                Matched("e0exactexactexact", "Exact", left: "\"L-1\"", right: "\"R-1\""),
                Open("d4leftonlyleftonly", "LeftOnly", left: "\"L-4\""),
                Matched("b2tolerancetolerance", "WithinTolerance", left: "\"L-2\"", right: "\"R-2\"", diffs: ToleranceDiffs),
                Open("c3ambiguousambiguous", "Ambiguous", left: "\"L-3\"", right: "\"R-3\""),
                Matched("a1breakbreakbreak", "Break", left: "\"L-5\"", right: "\"R-5\"", diffs: BreakDiffs),
                Matched("f6packagepackage", "Exact", composition: "\"ManyToOne\", \"Package\"", left: "\"L-6\", \"L-7\"", right: "\"R-6\""),
                Matched("a0breakbreakearlier", "Break", left: "\"L-8\"", right: "\"R-8\"", diffs: BreakDiffs)),
            string.Join(", ",
                Row("left", "L-1", "e0exactexactexact"), Row("right", "R-1", "e0exactexactexact"),
                Row("left", "L-4", "d4leftonlyleftonly", "LeftOnly"),
                Row("left", "L-2", "b2tolerancetolerance"), Row("right", "R-2", "b2tolerancetolerance"),
                Row("left", "L-3", "c3ambiguousambiguous", "Ambiguous"), Row("right", "R-3", "c3ambiguousambiguous", "Ambiguous"),
                Row("left", "L-5", "a1breakbreakbreak"), Row("right", "R-5", "a1breakbreakbreak"),
                Row("left", "L-6", "f6packagepackage"), Row("left", "L-7", "f6packagepackage"), Row("right", "R-6", "f6packagepackage"),
                Row("left", "L-8", "a0breakbreakearlier"), Row("right", "R-8", "a0breakbreakearlier")));

    [Fact]
    public void Nothing_is_unavailable_only_when_no_entry_is_selected_and_every_entry_is_exact()
    {
        const string rows = """{ "source": "left", "rowKey": "L-1", "disposition": "Matched", "entryId": "e1" }, { "source": "right", "rowKey": "R-1", "disposition": "Matched", "entryId": "e1" }""";

        Assert.Contains("Nothing to explain: no entry is selected and every entry is an exact match.", Unavailable("MO7", Make(Matched("e1", "Exact"), rows)));
        Assert.Null(Unavailable("MO7", Make(Matched("e1", "Exact"), rows, """{ "selectedEntries": ["E1"] }""")));
        Assert.Null(Unavailable("MO7", Mixed()));
    }

    [Fact]
    public void Cards_are_the_entries_that_are_not_exact_matches_or_are_consolidated_ranked_break_tolerance_open_then_package()
    {
        var cards = Cards(Compose("MO7", Mixed()));

        Assert.Equal(["a0breakbreak", "a1breakbreak", "b2tolerancet", "c3ambiguousa", "d4leftonlyle", "f6packagepac"], cards.Select(card => card.Title[..12]));
    }

    [Fact]
    public void A_page_opens_with_the_note_about_what_the_engine_records()
    {
        var document = Compose("MO7", Mixed());

        Assert.Equal(Note, Assert.IsType<NoteBlock>(document.Blocks[0]).Text);
    }

    [Fact]
    public void A_banner_names_the_entry_its_row_keys_and_its_alignment_and_outcome_with_a_dash_for_a_missing_side()
    {
        var cards = Cards(Compose("MO7", Mixed()));

        Assert.Equal("a1breakbreak  ·  ours L-5 / theirs R-5  ·  Matched, Break", cards[1].Title);
        Assert.Equal("d4leftonlyle  ·  ours L-4 / theirs —  ·  LeftOnly, NotCompared", cards[4].Title);
        Assert.Equal("f6packagepac  ·  ours L-6; L-7 / theirs R-6  ·  Matched, Exact", cards[5].Title);
    }

    [Fact]
    public void Each_trail_attempt_is_a_step_with_its_key_and_detail_joined_and_its_candidates_listed()
    {
        const string trail = """{ "pass": "P1", "verdict": "NoKey", "key": null, "detail": "no trade ref", "candidates": [] }, { "pass": "P2", "verdict": "Committed", "key": "BARC|UST|BUY", "detail": null, "candidates": [ { "source": "left", "rowKey": "L-1" }, { "source": "right", "rowKey": "R-1" } ] }, { "pass": "P3", "verdict": "Contest", "key": "K", "detail": "two contenders", "candidates": [] }""";
        var input = Make(Matched("a1", "Break", diffs: BreakDiffs, trail: trail), $"{Row("left", "L-1", "a1")}, {Row("right", "R-1", "a1")}");

        var table = Steps(Assert.Single(Cards(Compose("MO7", input))));

        Assert.Equal(["Step", "Verdict", "Key or detail", "Candidates"], Header(table));
        Assert.Equal(["P1", "NoKey", "no trade ref", ""], Texts(table.Rows[0]));
        Assert.Equal(["P2", "Committed", "key BARC|UST|BUY", "left:L-1, right:R-1"], Texts(table.Rows[1]));
        Assert.Equal(["P3", "Contest", "key K  ·  two contenders", ""], Texts(table.Rows[2]));
    }

    [Theory]
    [InlineData("Committed", Tone.Ok)]
    [InlineData("Contest", Tone.Tolerance)]
    [InlineData("SearchIncomplete", Tone.Tolerance)]
    [InlineData("NoKey", Tone.Shade)]
    [InlineData("NoCandidate", Tone.Shade)]
    [InlineData("Vetoed", Tone.Break)]
    public void A_verdict_is_coloured_by_what_it_says(string verdict, Tone expected)
    {
        var input = Make(
            Matched("a1", "Break", diffs: BreakDiffs, trail: Attempt("P1", verdict)),
            $"{Row("left", "L-1", "a1")}, {Row("right", "R-1", "a1")}");

        var cell = Cell(Steps(Assert.Single(Cards(Compose("MO7", input)))), Steps(Assert.Single(Cards(Compose("MO7", input)))).Rows[0], "Verdict");

        Assert.Equal(expected, cell.Tone);
        Assert.False(cell.Italic);
        Assert.True(cell.Bold);
    }

    [Fact]
    public void The_last_step_is_the_result_and_its_alignment_is_coloured()
    {
        var cards = Cards(Compose("MO7", Mixed()));

        var matched = Steps(cards[1]).Rows[^1];
        var open = Steps(cards[4]).Rows[^1];
        var ambiguous = Steps(cards[3]).Rows[^1];
        Assert.Equal(["Result", "Matched", "OneToOne, Break: Price Break, Fees WithinTolerance", ""], Texts(matched));
        Assert.Equal(Tone.Ok, matched.Cells[1].Tone);
        Assert.Equal(["Result", "LeftOnly", "LeftOnly: no match committed", ""], Texts(open));
        Assert.Equal(Tone.Shade, open.Cells[1].Tone);
        Assert.Equal(["Result", "Ambiguous", "Ambiguous: no match committed", ""], Texts(ambiguous));
        Assert.Equal(Tone.Tolerance, ambiguous.Cells[1].Tone);
    }

    [Fact]
    public void An_exact_pair_is_a_card_only_when_consolidated_and_its_result_says_exact()
    {
        var package = Steps(Cards(Compose("MO7", Mixed()))[5]);

        Assert.Equal(
            [
                ["Consolidation", "Package", "legs consolidated into one item: ours L-6; L-7 / theirs R-6", ""],
                ["Result", "Matched", "ManyToOne, Package, exact", ""]
            ],
            package.Rows.Select(row => Texts(row).ToArray()));
        Assert.Equal(Tone.Shade, package.Rows[0].Cells[1].Tone);
    }

    [Fact]
    public void A_lifecycle_entry_lists_the_rows_that_were_superseded_or_cancelled()
    {
        var input = Make(
            Matched("a1", "Exact", composition: "\"OneToOne\", \"LifecycleCollapsed\""),
            string.Join(", ", Row("left", "L-1", "a1"), Row("left", "L-0", "a1", "Suppressed", "Superseded"), Row("left", "L-9", "a1", "Suppressed", "CancelEvent"), Row("right", "R-1", "a1")));

        var table = Steps(Assert.Single(Cards(Compose("MO7", input))));

        Assert.Equal(["Consolidation", "Lifecycle", "2 row(s) superseded or cancelled: L-0 (Superseded), L-9 (CancelEvent)", ""], Texts(table.Rows[0]));
        Assert.Equal("OneToOne, LifecycleCollapsed, exact", Texts(table.Rows[1])[2]);
    }

    [Fact]
    public void Hints_follow_the_trail_in_italics_and_a_hint_without_detail_is_blank()
    {
        var input = Make(
            Open("o1", "LeftOnly", left: "\"L-1\"", trail: Attempt("P1", "NoCandidate"), hints: """{ "code": "NearestCandidate", "detail": "R-9 differs on Price" }, { "code": "RaiseSearchLimit" }"""),
            Row("left", "L-1", "o1", "LeftOnly"));

        var table = Steps(Assert.Single(Cards(Compose("MO7", input))));

        Assert.Equal(["P1", "Hint", "Hint", "Result"], table.Rows.Select(row => Texts(row)[0]));
        Assert.Equal(["Hint", "NearestCandidate", "R-9 differs on Price", ""], Texts(table.Rows[1]));
        Assert.Equal(["Hint", "RaiseSearchLimit", "", ""], Texts(table.Rows[2]));
        Assert.True(table.Rows[1].Cells[2].Italic);
        Assert.True(table.Rows[1].Cells[1].Italic);
        Assert.Equal(Tone.None, table.Rows[1].Cells[1].Tone);
        Assert.False(table.Rows[0].Cells[2].Italic);
    }

    [Fact]
    public void An_unrouted_entry_shows_its_single_attempt()
    {
        var input = Make(
            Open("u1", "RightOnly", right: "\"R-7\"", trail: Attempt("route", "NoCandidate", detail: "Unrouted")),
            Row("right", "R-7", "u1", "RightOnly"));

        var table = Steps(Assert.Single(Cards(Compose("MO7", input))));

        Assert.Equal(["route", "NoCandidate", "Unrouted", ""], Texts(table.Rows[0]));
        Assert.Equal("RightOnly: no match committed", Texts(table.Rows[1])[2]);
    }

    [Fact]
    public void Selected_entries_match_by_case_insensitive_prefix_in_entry_order_with_no_cap_and_no_filter()
    {
        var options = """{ "selectedEntries": ["E0EXACT", "d4"], "maxCards": 1 }""";
        var input = Make(
            string.Join(", ", Matched("e0exactexact", "Exact"), Open("d4open", "LeftOnly", left: "\"L-4\"")),
            string.Join(", ", Row("left", "L-1", "e0exactexact"), Row("right", "R-1", "e0exactexact"), Row("left", "L-4", "d4open", "LeftOnly")),
            options);

        var cards = Cards(Compose("MO7", input));

        Assert.Equal(["e0exactexact", "d4open"], cards.Select(card => Stage(card.Title)[0]));
    }

    [Fact]
    public void Max_cards_caps_the_default_selection()
    {
        var input = Make(
            string.Join(", ", Open("o1", "LeftOnly", left: "\"L-1\""), Open("o2", "LeftOnly", left: "\"L-2\""), Open("o3", "LeftOnly", left: "\"L-3\"")),
            string.Join(", ", Row("left", "L-1", "o1", "LeftOnly"), Row("left", "L-2", "o2", "LeftOnly"), Row("left", "L-3", "o3", "LeftOnly")),
            """{ "maxCards": 2 }""");

        Assert.Equal(["o1", "o2"], Cards(Compose("MO7", input)).Select(card => Stage(card.Title)[0]));
    }

    [Fact]
    public void A_lifecycle_entry_with_no_suppressed_rows_counts_zero_of_them_and_a_short_id_is_not_cut()
    {
        var input = Make(Matched("a1", "Exact", composition: "\"OneToOne\", \"LifecycleCollapsed\""), $"{Row("left", "L-1", "a1")}, {Row("right", "R-1", "a1")}");

        var card = Assert.Single(Cards(Compose("MO7", input)));

        Assert.StartsWith("a1  ·  ", card.Title);
        Assert.Equal("0 row(s) superseded or cancelled: ", Texts(Steps(card).Rows[0])[2]);
    }

    [Fact]
    public void Column_headings_pass_through_the_label_overrides()
    {
        var input = Make(Open("o1", "LeftOnly", left: "\"L-1\""), Row("left", "L-1", "o1", "LeftOnly"), """{ "labels": { "Step": "Stage", "Verdict": "Outcome" } }""");

        Assert.Equal(["Stage", "Outcome", "Key or detail", "Candidates"], Header(Steps(Assert.Single(Cards(Compose("MO7", input))))));
    }

    [Fact]
    public void The_sample_shows_a_card_for_each_open_break_or_consolidated_entry_with_its_trail_and_result()
    {
        var input = Sample();
        var chosen = input.Entries
            .Where(entry => !(entry.Alignment == Alignment.Matched && entry.Outcome == Outcome.Exact) || entry.Composition.HasFlag(Composition.Package) || entry.Composition.HasFlag(Composition.LifecycleCollapsed))
            .OrderBy(entry => entry.Outcome == Outcome.Break ? 0 : entry.Outcome == Outcome.WithinTolerance ? 1 : entry.Alignment is Alignment.Ambiguous or Alignment.SearchIncomplete ? 2 : entry.Alignment is Alignment.LeftOnly or Alignment.RightOnly ? 3 : 4)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Take(50)
            .ToList();

        var cards = Cards(Compose("MO7", input));

        Assert.Equal(chosen.Count, cards.Count);
        Assert.Equal(chosen.Select(entry => entry.Id[..12]), cards.Select(card => Stage(card.Title)[0]));
        Assert.Equal(
            chosen.Select(entry => entry.Trail.Count + entry.Hints.Count + 1 + (entry.Composition.HasFlag(Composition.Package) ? 1 : 0) + (entry.Composition.HasFlag(Composition.LifecycleCollapsed) ? 1 : 0)),
            cards.Select(card => Steps(card).Rows.Count));
        Assert.All(cards, card => Assert.Equal("Result", Texts(Steps(card).Rows[^1])[0]));
        Assert.Equal(chosen.Count(entry => entry.Outcome == Outcome.Break), cards.Count(card => Texts(Steps(card).Rows[^1])[2].Contains("Break", StringComparison.Ordinal) && card.Title.EndsWith("Break", StringComparison.Ordinal)));
    }
}
