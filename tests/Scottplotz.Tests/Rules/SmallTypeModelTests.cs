using Scottplotz.Charting;
using Scottplotz.Rules;

namespace Scottplotz.Tests.Rules;

public sealed class SmallTypeModelTests
{
    [Fact]
    public void A_confident_rule_does_not_ask_the_model()
    {
        var selected = RuleTables.Selected(RuleTables.Plain(1, 3, 2, 5, 4));

        Assert.Equal(TypeSources.Rules, selected.TypeSource);
        Assert.Equal(ChartKind.Line, selected.Kind);
        Assert.Null(SmallTypeModel.Suggest(RuleTables.Plain(1, 3, 2, 5, 4)));
    }

    [Fact]
    public void An_unsure_line_in_the_lower_band_comes_from_the_model()
    {
        var values = Enumerable.Range(0, 40).Select(index => (index % 8) + (index * 0.05)).ToArray();
        var table = RuleTables.Column("reading", values);

        Assert.Equal(ChartKind.Histogram, SmallTypeModel.Suggest(table));

        var selected = RuleTables.Selected(table);
        Assert.Equal(TypeSources.Model, selected.TypeSource);
        Assert.Equal(ChartKind.Histogram, selected.Kind);
        Assert.Equal("plain-numbers-line", selected.MatchedRule);
    }

    [Fact]
    public void A_user_type_records_user_as_the_source()
    {
        var selected = RuleTables.Selected(RuleTables.Plain(1, 3, 2), ChartKind.Bar);

        Assert.Equal(TypeSources.User, selected.TypeSource);
        Assert.Equal("hint-bar", selected.EffectiveRule);
    }
}
