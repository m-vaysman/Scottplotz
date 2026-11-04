using Scottplotz.Charting;

namespace Scottplotz.Tests.Parsing;

public sealed class ColumnParsingTests
{
    [Fact]
    public void Parses_named_columns_of_dates_and_numbers()
    {
        var table = ParseAssert.Ok(
            """
            {
              "data": {
                "date": ["2026-11-01", "2026-11-02T01:00:00-05:00"],
                "price": [100.5, 101],
                "volume": ["1200", "1500"]
              }
            }
            """);

        Assert.Equal(DataOrigin.Columns, table.Origin);
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(6, table.PointCount);

        var dates = Assert.IsType<Column.Temporal>(table.Columns[0]);
        Assert.Equal(
            [
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 2, 6, 0, 0, DateTimeKind.Utc)
            ],
            dates.Values);

        var price = Assert.IsType<Column.Numeric>(table.Columns[1]);
        Assert.Equal([100.5d, 101d], price.Values);
        var volume = Assert.IsType<Column.Numeric>(table.Columns[2]);
        Assert.Equal([1200d, 1500d], volume.Values);
    }

    [Fact]
    public void Rejects_columns_of_different_lengths()
    {
        var problem = ParseAssert.Fail("""{ "data": { "price": [1, 2], "volume": [3] } }""");

        Assert.Equal("mismatched-columns", problem.Code);
        Assert.Contains("volume", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("price", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_column_that_is_not_an_array()
    {
        var problem = ParseAssert.Fail("""{ "data": { "price": 4 } }""");

        Assert.Equal("malformed-json", problem.Code);
        Assert.Contains("price", problem.Detail, StringComparison.Ordinal);
    }
}
