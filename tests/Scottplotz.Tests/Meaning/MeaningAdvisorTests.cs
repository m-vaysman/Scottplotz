using Scottplotz.Charting;
using Scottplotz.Meaning;

namespace Scottplotz.Tests.Meaning;

public sealed class MeaningAdvisorTests
{
    [Fact]
    public async Task Disabled_advisor_returns_nothing()
    {
        var advice = await new DisabledColumnMeaningAdvisor().AdviseAsync(
            new ChartTable(DataOrigin.PlainArray, [new Column.Numeric(null, [1d])]),
            CancellationToken.None);

        Assert.Null(advice);
    }

    [Fact]
    public void Advice_fills_blank_labels_only_when_the_rules_are_unsure()
    {
        var hints = new ResolvedChartHints(null, null, null, "Kept", 800, 450, ChartTheme.Light);
        var advice = new ColumnMeaning("Suggested", "When", "Why");

        var applied = MeaningAdvice.Apply(hints, advice, needsAdvisor: true);
        Assert.Equal("Suggested", applied.Title);
        Assert.Equal("When", applied.XLabel);
        Assert.Equal("Kept", applied.YLabel);

        var ignored = MeaningAdvice.Apply(hints, advice, needsAdvisor: false);
        Assert.Null(ignored.Title);
        Assert.Null(ignored.XLabel);
    }
}
