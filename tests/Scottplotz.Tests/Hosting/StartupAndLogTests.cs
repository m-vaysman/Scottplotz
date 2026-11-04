using Scottplotz.Hosting;
using Scottplotz.Logging;

namespace Scottplotz.Tests.Hosting;

public sealed class StartupAndLogTests
{
    [Fact]
    public void Startup_command_quotes_the_executable_path()
    {
        Assert.Equal("\"C:\\Program Files\\Scottplotz\\Scottplotz.exe\"", StartupRegistration.QuotedCommand(@"C:\Program Files\Scottplotz\Scottplotz.exe"));
    }

    [Fact]
    public void Log_lines_are_json_and_keep_the_supplied_time()
    {
        var line = LogLine.Format(
            new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero),
            "Information",
            "Scottplotz.Chart",
            "Chart plain-numbers-line from 5 points",
            exception: null);

        Assert.Contains("\"timestamp\":\"2026-11-03T12:00:00.0000000+00:00\"", line);
        Assert.Contains("\"level\":\"Information\"", line);
        Assert.Contains("\"message\":\"Chart plain-numbers-line from 5 points\"", line);
        Assert.DoesNotContain("exception", line);
    }
}
