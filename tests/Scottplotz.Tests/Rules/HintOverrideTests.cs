using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class HintOverrideTests
{
    [Fact]
    public void Uses_a_pie_only_when_the_hint_asks_for_it()
    {
        var table = RuleTables.Columns(
            new Column.Categorical("fruit", ["apples", "apples", "pears"]),
            new Column.Numeric("count", [1, 3, 7]));

        var selected = RuleTables.Selected(table, ChartKind.Pie);

        Assert.Equal("hint-pie", selected.EffectiveRule);
        Assert.Equal("category-bar", selected.MatchedRule);
        Assert.False(selected.NeedsAdvisor);
        var slices = Assert.IsType<ChartPlan.Slices>(selected.Plan);
        Assert.Equal(["apples", "pears"], slices.Labels);
        Assert.Equal([4d, 7d], slices.Values);
    }

    [Fact]
    public void Lets_a_histogram_hint_override_a_time_series()
    {
        var table = RuleTables.Columns(RuleTables.Days(1, 2, 3), new Column.Numeric("price", [5, 5, 9]));

        var selected = RuleTables.Selected(table, ChartKind.Histogram);

        Assert.Equal("hint-histogram", selected.EffectiveRule);
        Assert.Equal("timeseries", selected.MatchedRule);
        Assert.IsType<ChartPlan.Distribution>(selected.Plan);
    }

    [Fact]
    public void Rejects_a_pie_that_has_more_than_one_numeric_series()
    {
        var table = RuleTables.Columns(
            new Column.Numeric("a", [1, 2]),
            new Column.Numeric("b", [3, 4]));

        var result = ChartSelectionBuilder.Select(table, RuleTables.Auto(ChartKind.Pie));

        var problem = Assert.IsType<Result<ChartSelection>.Fail>(result).Problem;
        Assert.Equal("incompatible-hint", problem.Code);
        Assert.Contains("one numeric series", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_data_that_no_rule_can_chart()
    {
        var result = ChartSelectionBuilder.Select(
            RuleTables.Columns(new Column.Categorical("fruit", ["apples"])),
            RuleTables.Auto());

        Assert.Equal("no-rule", Assert.IsType<Result<ChartSelection>.Fail>(result).Problem.Code);
    }

    [Fact]
    public void Asks_for_advice_only_when_the_line_rule_is_unsure()
    {
        var values = Enumerable.Range(0, 40).Select(index => (index % 8) + (index * 0.05)).ToArray();
        var selected = RuleTables.Selected(RuleTables.Column("reading", values));

        Assert.Equal("plain-numbers-line", selected.EffectiveRule);
        Assert.True(selected.NeedsAdvisor);

        var forced = RuleTables.Selected(RuleTables.Column("reading", values), ChartKind.Line);
        Assert.Equal("hint-line", forced.EffectiveRule);
        Assert.False(forced.NeedsAdvisor);
    }

    [Fact]
    public void Draws_a_shared_numeric_x_for_a_monotonic_multi_line()
    {
        var selected = RuleTables.Selected(RuleTables.Columns(
            new Column.Numeric("x", [1, 2, 3]),
            new Column.Numeric("a", [2, 3, 4]),
            new Column.Numeric("b", [5, 4, 3])));

        var lines = Assert.IsType<ChartPlan.Lines>(selected.Plan);
        Assert.Equal([1d, 2d, 3d], lines.X);
        Assert.Equal(["a", "b"], lines.Series.Select(series => series.Name));
    }
}
