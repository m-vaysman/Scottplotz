using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class ChartRuleEngineTests
{
    [Fact]
    public void A_date_column_with_numbers_is_a_time_series()
    {
        var table = RuleTables.Columns(
            RuleTables.Days(1, 2, 3),
            new Column.Numeric("price", [1, 2, 3]),
            new Column.Numeric("volume", [4, 5, 6]));

        var decision = ChartRuleEngine.Decide(table);

        Assert.Equal("timeseries", decision.Match?.Name);
        Assert.False(decision.Unsure);
    }

    [Fact]
    public void Each_shape_matches_its_own_rule()
    {
        Assert.Equal("plain-numbers-line", ChartRuleEngine.Decide(RuleTables.Plain(1, 3, 2)).Match?.Name);
        Assert.Equal("histogram", ChartRuleEngine.Decide(RuleTables.Column("reading", RuleTables.Alternating(40))).Match?.Name);
        Assert.Equal("scatter", ChartRuleEngine.Decide(RuleTables.Columns(
            new Column.Numeric("x", [1, 2, 3]),
            new Column.Numeric("y", [3, 1, 2]))).Match?.Name);
        Assert.Equal("multi-line", ChartRuleEngine.Decide(RuleTables.Columns(
            new Column.Numeric("x", [1, 2, 3]),
            new Column.Numeric("a", [1, 2, 3]),
            new Column.Numeric("b", [3, 2, 1]))).Match?.Name);
        Assert.Equal("category-bar", ChartRuleEngine.Decide(RuleTables.Columns(
            new Column.Categorical("fruit", ["apples", "pears"]),
            new Column.Numeric("count", [4, 7]))).Match?.Name);
    }

    [Fact]
    public void A_later_confident_rule_beats_an_earlier_unsure_one()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Plain(1), [Unsure("first"), Confident("second")]);

        Assert.Equal("second", decision.Match?.Name);
        Assert.False(decision.Unsure);
    }

    [Fact]
    public void The_first_confident_rule_wins()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Plain(1), [Confident("first"), Confident("second")]);

        Assert.Equal("first", decision.Match?.Name);
        Assert.False(decision.Unsure);
    }

    [Fact]
    public void The_first_unsure_rule_is_the_fallback()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Plain(1), [Unsure("first"), Unsure("second")]);

        Assert.Equal("first", decision.Match?.Name);
        Assert.True(decision.Unsure);
    }

    [Fact]
    public void Production_order_matches_the_howto_table()
    {
        Assert.Equal(
            ["timeseries", "category-bar", "scatter", "multi-line", "histogram", "plain-numbers-line"],
            ChartRuleEngine.Ordered.Select(rule => rule.Name));
    }

    private static Func<ChartTable, RuleMatch?> Confident(string name) =>
        _ => new RuleMatch(name, Confident: true, ChartKind.Line);

    private static Func<ChartTable, RuleMatch?> Unsure(string name) =>
        _ => new RuleMatch(name, Confident: false, ChartKind.Line);

    [Fact]
    public void Does_not_choose_a_pie()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Columns(
            new Column.Categorical("fruit", ["apples", "pears"]),
            new Column.Numeric("count", [4, 7])));

        Assert.Equal("category-bar", decision.Match?.Name);
        Assert.Equal(ChartKind.Bar, decision.Match?.Kind);
    }

    [Fact]
    public void Reports_text_without_numbers_as_unmatched()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Columns(new Column.Categorical("fruit", ["apples"])));

        Assert.Null(decision.Match);
        Assert.True(decision.Unsure);
    }
}
