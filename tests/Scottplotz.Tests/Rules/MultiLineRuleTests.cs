using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class MultiLineRuleTests
{
    [Fact]
    public void Matches_three_or_more_numeric_columns()
    {
        var table = RuleTables.Columns(
            new Column.Numeric("x", [1, 2, 3]),
            new Column.Numeric("a", [2, 3, 4]),
            new Column.Numeric("b", [4, 3, 2]));

        var match = MultiLineRule.Match(table);

        Assert.NotNull(match);
        Assert.Equal("multi-line", match.Name);
        Assert.Equal(ChartKind.MultiLine, match.Kind);
    }

    [Fact]
    public void Does_not_match_two_numeric_columns()
    {
        Assert.Null(MultiLineRule.Match(RuleTables.Columns(
            new Column.Numeric("x", [1, 2]),
            new Column.Numeric("y", [3, 4]))));
    }
}
