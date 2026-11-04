using System.Globalization;
using Scottplotz.Charting;

namespace Scottplotz.Tests.Parsing;

public sealed class CultureParsingTests
{
    [Fact]
    public void Csv_keeps_invariant_numbers_under_a_comma_culture()
    {
        WithCulture("de-DE", () =>
        {
            var table = ParseAssert.Ok("v\n1.5\n2\n", "text/csv");
            var column = Assert.IsType<Column.Numeric>(Assert.Single(table.Columns));
            Assert.Equal([1.5d, 2d], column.Values);

            var problem = ParseAssert.Fail("v\n1.5\n\"1,5\"\n2\n", "text/csv");
            Assert.Equal("non-numeric", problem.Code);
            Assert.Contains("1,5", problem.Detail, StringComparison.Ordinal);
        });
    }

    private static void WithCulture(string name, Action check)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            check();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }
}
