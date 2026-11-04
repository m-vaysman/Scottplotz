using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class ChartRuleEngineTests
{
    [Fact]
    public void Prefers_a_time_series_over_later_rules()
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
    public void Picks_the_shape_rules_in_order()
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
    public void Does_not_choose_a_pie()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Columns(
            new Column.Categorical("fruit", ["apples", "pears"]),
            new Column.Numeric("count", [4, 7])));

        Assert.NotEqual(ChartKind.Pie, decision.Match?.Kind);
    }

    [Fact]
    public void Reports_text_without_numbers_as_unmatched()
    {
        var decision = ChartRuleEngine.Decide(RuleTables.Columns(new Column.Categorical("fruit", ["apples"])));

        Assert.Null(decision.Match);
        Assert.True(decision.Unsure);
    }
}
