namespace Scottplotz.Tests;

public sealed class HealthReportTests
{
    [Fact]
    public void Format_uses_three_part_version()
    {
        var report = HealthReport.Create(new Version(1, 0, 12));

        Assert.Equal("ok", report.Status);
        Assert.Equal("1.0.12", report.Version);
    }

    [Fact]
    public void Format_treats_a_missing_version_as_zero()
    {
        Assert.Equal("0.0.0", HealthReport.Format(null));
    }
}
