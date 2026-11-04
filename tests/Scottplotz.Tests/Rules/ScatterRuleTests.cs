using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class ScatterRuleTests
{
    [Fact]
    public void Matches_exactly_two_numeric_columns()
    {
        var table = RuleTables.Columns(
            new Column.Numeric("x", [1, 2, 3]),
            new Column.Numeric("y", [2, 3, 5]));

        var match = ScatterRule.Match(table);

        Assert.NotNull(match);
        Assert.Equal("scatter", match.Name);
        Assert.True(match.Confident);
    }

    [Fact]
    public void Does_not_match_one_column_or_three_columns_or_categories()
    {
        Assert.Null(ScatterRule.Match(RuleTables.Plain(1, 2, 3)));
        Assert.Null(ScatterRule.Match(RuleTables.Columns(
            new Column.Numeric("a", [1, 2]),
            new Column.Numeric("b", [2, 3]),
            new Column.Numeric("c", [3, 4]))));
        Assert.Null(ScatterRule.Match(RuleTables.Columns(
            new Column.Categorical("name", ["a", "b"]),
            new Column.Numeric("x", [1, 2]),
            new Column.Numeric("y", [3, 4]))));
    }
}
