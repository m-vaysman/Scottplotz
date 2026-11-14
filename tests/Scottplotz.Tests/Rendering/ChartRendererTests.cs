using System.Globalization;
using System.Security.Cryptography;
using Scottplotz.Charting;
using Scottplotz.Meaning;
using Scottplotz.Rendering;

namespace Scottplotz.Tests.Rendering;

public sealed class ChartRendererTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Renders_the_same_bytes_twice()
    {
        var first = Render(LineTable(), title: "Numbers");
        var second = Render(LineTable(), title: "Numbers");

        Assert.Equal(PngSignature, first.Take(PngSignature.Length));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Uses_the_bundled_font()
    {
        Render(LineTable(), title: "Numbers");

        Assert.Equal(EmbeddedFont.FamilyName, ScottPlot.Fonts.Default);
    }

    [Fact]
    public void Dark_theme_changes_the_image()
    {
        var light = Render(LineTable(), title: "Numbers", theme: ChartTheme.Light);
        var dark = Render(LineTable(), title: "Numbers", theme: ChartTheme.Dark);

        Assert.NotEqual(Hash(light), Hash(dark));
    }

    [Fact]
    public void A_comma_culture_renders_the_same_bytes()
    {
        var invariant = Render(LineTable(), title: "Numbers");
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        var german = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.CurrentCulture = german;
        CultureInfo.CurrentUICulture = german;
        try
        {
            Assert.Equal(invariant, Render(LineTable(), title: "Numbers"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void Matches_the_golden_hashes()
    {
        if (!OperatingSystem.IsWindows())
            throw Xunit.Sdk.SkipException.ForSkip(
                "Golden hashes are captured on Windows. ScottPlot text layout differs on Linux.");

        var images = new Dictionary<string, byte[]>
        {
            ["line"] = Render(LineTable(), title: "Numbers"),
            ["bar"] = Render(BarTable(), title: "Fruit"),
            ["scatter"] = Render(ScatterTable(), title: "Points"),
            ["histogram"] = Render(HistogramTable(), title: "Readings"),
            ["timeseries"] = Render(TimeTable(), title: "Price"),
            ["pie"] = Render(BarTable(), ChartKind.Pie, "Fruit")
        };
        var actual = images.ToDictionary(pair => pair.Key, pair => Hash(pair.Value), StringComparer.Ordinal);

        var expected = new Dictionary<string, string>
        {
            ["line"] = "96d59d03623bc1ea739514cfd192f31218f7bd612441c5359e48b866a9438bba",
            ["bar"] = "6f20f18238d10449a66599eee1c903cb0fa20c823efe9d3411cfe6368225f6fa",
            ["scatter"] = "8adeae20afe335aa5ed8c0e5399be6f63345da3976601a85f52768c5061bf546",
            ["histogram"] = "34a6492fa09c3b0b5dadd0be7020f8e165b565a952ceef3dbd39cab169d28a2d",
            ["timeseries"] = "240f92067c576f58ef28a0136d25bf6c0e6c180ad91fa974ca192e22ae502c53",
            ["pie"] = "f2562473715f5f2815e12923b4a057fd0e65e1876a2a0bcbbde3a122bc4da844"
        };

        try
        {
            Assert.Equal(expected, actual);
        }
        catch (Xunit.Sdk.XunitException)
        {
            SaveMismatches(images, actual);
            throw;
        }
    }

    private static void SaveMismatches(IReadOnlyDictionary<string, byte[]> images, IReadOnlyDictionary<string, string> hashes)
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "golden");
        Directory.CreateDirectory(folder);
        foreach (var (name, png) in images)
            File.WriteAllBytes(Path.Combine(folder, $"{name}.png"), png);
        File.WriteAllLines(
            Path.Combine(folder, "hashes.txt"),
            hashes.Select(pair => $"{pair.Key} {pair.Value}"));
    }

    private static byte[] Render(ChartTable table, ChartKind? type = null, string? title = null, ChartTheme theme = ChartTheme.Light)
    {
        var request = new ChartRequest(table, new ChartHints(type, title, "X", "Y", 480, 270, theme));
        var rendered = ChartRenderer.Render(
            request,
            ChartingLimits.Default,
            new DisabledColumnMeaningAdvisor(),
            CancellationToken.None).GetAwaiter().GetResult();
        return Assert.IsType<Result<ChartImage>.Ok>(rendered).Value.Png;
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static ChartTable LineTable() =>
        new(DataOrigin.PlainArray, [new Column.Numeric(null, [1, 3, 2, 5, 4])]);

    private static ChartTable BarTable() =>
        new(DataOrigin.Rows,
        [
            new Column.Categorical("fruit", ["apples", "pears", "oranges"]),
            new Column.Numeric("count", [4, 7, 2])
        ]);

    private static ChartTable ScatterTable() =>
        new(DataOrigin.Columns,
        [
            new Column.Numeric("x", [1, 2, 3, 4]),
            new Column.Numeric("y", [2, 3, 5, 4])
        ]);

    private static ChartTable HistogramTable() =>
        new(DataOrigin.Columns, [new Column.Numeric("reading", Enumerable.Range(0, 40).Select(index => index % 2 == 0 ? 0d : 100d).ToArray())]);

    private static ChartTable TimeTable() =>
        new(DataOrigin.Columns,
        [
            new Column.Temporal("date",
            [
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
            ]),
            new Column.Numeric("price", [100.5, 101.25, 99.75])
        ]);
}
