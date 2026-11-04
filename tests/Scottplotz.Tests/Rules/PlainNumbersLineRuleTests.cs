using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class PlainNumbersLineRuleTests
{
    [Fact]
    public void Matches_a_plain_list_even_when_the_values_are_unordered()
    {
        var match = PlainNumbersLineRule.Match(RuleTables.Plain(RuleTables.Alternating(40)));

        Assert.NotNull(match);
        Assert.Equal("plain-numbers-line", match.Name);
        Assert.True(match.Confident);
        Assert.Equal(ChartKind.Line, match.Kind);
    }

    [Fact]
    public void Matches_a_short_named_column()
    {
        var match = PlainNumbersLineRule.Match(RuleTables.Column("price", 1, 3, 2, 5));

        Assert.NotNull(match);
        Assert.True(match.Confident);
    }

    [Fact]
    public void Is_unsure_when_a_long_named_series_is_only_partly_ordered()
    {
        var values = Enumerable.Range(0, 40).Select(index => (index % 8) + (index * 0.05)).ToArray();
        var correlation = Math.Abs(SeriesStats.CorrelationWithIndex(values));
        Assert.InRange(correlation, HistogramRule.MaximumOrderedCorrelation, 0.7);

        var match = PlainNumbersLineRule.Match(RuleTables.Column("reading", values));

        Assert.NotNull(match);
        Assert.False(match.Confident);
    }

    [Fact]
    public void Does_not_match_unordered_samples_or_two_columns()
    {
        Assert.Null(PlainNumbersLineRule.Match(RuleTables.Column("reading", RuleTables.Alternating(40))));
        Assert.Null(PlainNumbersLineRule.Match(RuleTables.Columns(
            new Column.Numeric("x", [1, 2, 3]),
            new Column.Numeric("y", [3, 2, 1]))));
    }
}
