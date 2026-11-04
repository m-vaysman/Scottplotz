using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class TimeSeriesRuleTests
{
    [Fact]
    public void Matches_a_date_column_plus_numbers()
    {
        var table = RuleTables.Columns(
            RuleTables.Days(1, 2, 3),
            new Column.Numeric("price", [10, 12, 11]),
            new Column.Numeric("volume", [5, 6, 4]));

        var match = TimeSeriesRule.Match(table);

        Assert.NotNull(match);
        Assert.Equal("timeseries", match.Name);
        Assert.True(match.Confident);
        Assert.Equal(Charting.ChartKind.TimeSeries, match.Kind);
    }

    [Fact]
    public void Does_not_match_numbers_without_dates()
    {
        Assert.Null(TimeSeriesRule.Match(RuleTables.Plain(1, 2, 3)));
    }

    [Fact]
    public void Does_not_match_dates_without_numbers()
    {
        Assert.Null(TimeSeriesRule.Match(RuleTables.Columns(RuleTables.Days(1, 2, 3))));
    }
}
