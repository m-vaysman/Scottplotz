using Scottplotz.Charting;

namespace Scottplotz.Tests.Parsing;

public sealed class RowParsingTests
{
    [Fact]
    public void Parses_rows_of_objects()
    {
        var table = ParseAssert.Ok(
            """
            {
              "data": [
                { "category": "apples", "count": 4 },
                { "category": "pears", "count": 7 }
              ]
            }
            """);

        Assert.Equal(DataOrigin.Rows, table.Origin);
        var categories = Assert.IsType<Column.Categorical>(table.Columns[0]);
        Assert.Equal(["apples", "pears"], categories.Values);
        var counts = Assert.IsType<Column.Numeric>(table.Columns[1]);
        Assert.Equal([4d, 7d], counts.Values);
    }

    [Fact]
    public void Rejects_a_missing_field_as_null()
    {
        var problem = ParseAssert.Fail(
            """
            {
              "data": [
                { "category": "apples", "count": 4 },
                { "category": "pears" }
              ]
            }
            """);

        Assert.Equal("null-value", problem.Code);
        Assert.Contains("count", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("row 2", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_first_seen_field_order()
    {
        var table = ParseAssert.Ok(
            """
            {
              "data": [
                { "count": 4, "category": "apples" },
                { "count": 7, "category": "pears" }
              ]
            }
            """);

        Assert.Equal("count", table.Columns[0].Name);
        Assert.Equal("category", table.Columns[1].Name);
    }
}
