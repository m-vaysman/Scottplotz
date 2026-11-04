using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class HistogramRuleTests
{
    [Fact]
    public void Matches_lots_of_unordered_numbers()
    {
        var values = RuleTables.Alternating(40);
        var correlation = Math.Abs(SeriesStats.CorrelationWithIndex(values));
        Assert.True(correlation < HistogramRule.MaximumOrderedCorrelation);

        var match = HistogramRule.Match(RuleTables.Column("reading", values));

        Assert.NotNull(match);
        Assert.Equal("histogram", match.Name);
        Assert.True(match.Confident);
    }

    [Fact]
    public void Does_not_match_a_short_list_a_ramp_or_a_plain_array()
    {
        Assert.Null(HistogramRule.Match(RuleTables.Column("reading", 1, 2, 3, 4, 5)));
        Assert.Null(HistogramRule.Match(RuleTables.Column("reading", RuleTables.Ramp(40))));
        Assert.Null(HistogramRule.Match(RuleTables.Plain(RuleTables.Alternating(40))));
    }
}
