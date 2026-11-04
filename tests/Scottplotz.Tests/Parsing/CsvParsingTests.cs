using Scottplotz.Charting;

namespace Scottplotz.Tests.Parsing;

public sealed class CsvParsingTests
{
    [Fact]
    public void Parses_a_header_and_rows()
    {
        var table = ParseAssert.Ok(
            """
            date,price,volume
            2026-11-03,101.2,1200
            2026-11-04,102,1500
            """,
            "text/csv; charset=utf-8");

        Assert.Equal(DataOrigin.Csv, table.Origin);
        Assert.Equal(["date", "price", "volume"], table.Columns.Select(column => column.Name));
        var dates = Assert.IsType<Column.Temporal>(table.Columns[0]);
        Assert.Equal(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc), dates.Values[0]);
        var price = Assert.IsType<Column.Numeric>(table.Columns[1]);
        Assert.Equal([101.2d, 102d], price.Values);
    }

    [Fact]
    public void Keeps_quoted_commas_inside_a_field()
    {
        var table = ParseAssert.Ok(
            """
            category,count
            "red, delicious",4
            pears,7
            """,
            "application/csv");

        var categories = Assert.IsType<Column.Categorical>(table.Columns[0]);
        Assert.Equal(["red, delicious", "pears"], categories.Values);
    }

    [Fact]
    public void Does_not_treat_a_comma_decimal_as_a_number()
    {
        var table = ParseAssert.Ok("value\n\"1,5\"\n", "text/csv");

        Assert.IsType<Column.Categorical>(Assert.Single(table.Columns));
    }

    [Fact]
    public void Requires_a_header_and_at_least_one_row()
    {
        var problem = ParseAssert.Fail("price,volume\n", "text/csv");

        Assert.Equal("empty-data", problem.Code);
    }

    [Fact]
    public void Rejects_an_unclosed_quote()
    {
        var problem = ParseAssert.Fail("name\n\"abc\n", "text/csv");

        Assert.Equal("malformed-csv", problem.Code);
    }

    [Fact]
    public void Rejects_a_duplicate_header()
    {
        var problem = ParseAssert.Fail("price,price\n1,2\n", "text/csv");

        Assert.Equal("duplicate-column", problem.Code);
    }

    [Fact]
    public void Rejects_a_row_that_is_wider_than_the_header()
    {
        var problem = ParseAssert.Fail("price\n1,2\n", "text/csv");

        Assert.Equal("mismatched-columns", problem.Code);
        Assert.Contains("row 1", problem.Detail, StringComparison.Ordinal);
    }
}
