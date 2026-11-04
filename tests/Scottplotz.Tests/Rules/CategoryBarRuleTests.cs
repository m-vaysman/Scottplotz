using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class CategoryBarRuleTests
{
    [Fact]
    public void Matches_categories_plus_numbers()
    {
        var table = RuleTables.Columns(
            new Column.Categorical("fruit", ["apples", "pears"]),
            new Column.Numeric("count", [4, 7]));

        var match = CategoryBarRule.Match(table);

        Assert.NotNull(match);
        Assert.Equal("category-bar", match.Name);
        Assert.True(match.Confident);
    }

    [Fact]
    public void Does_not_match_a_plain_number_list_or_a_time_series()
    {
        Assert.Null(CategoryBarRule.Match(RuleTables.Plain(1, 2, 3)));
        var series = RuleTables.Columns(RuleTables.Days(1, 2), new Column.Numeric("price", [1, 2]));
        Assert.Null(CategoryBarRule.Match(series));
    }
}
