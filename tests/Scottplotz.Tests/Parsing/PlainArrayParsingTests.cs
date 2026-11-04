using Scottplotz.Charting;

namespace Scottplotz.Tests.Parsing;

public sealed class PlainArrayParsingTests
{
    [Fact]
    public void Parses_a_plain_number_array()
    {
        var table = ParseAssert.Ok("""{ "data": [1, 3, 2.5] }""");

        Assert.Equal(DataOrigin.PlainArray, table.Origin);
        Assert.Equal(3, table.PointCount);
        var column = Assert.IsType<Column.Numeric>(Assert.Single(table.Columns));
        Assert.Null(column.Name);
        Assert.Equal([1d, 3d, 2.5d], column.Values);
    }

    [Fact]
    public void Rejects_strings_in_a_plain_array()
    {
        var problem = ParseAssert.Fail("""{ "data": [1, "2"] }""");

        Assert.Equal("plain-array", problem.Code);
    }

    [Fact]
    public void Rejects_an_empty_array()
    {
        var problem = ParseAssert.Fail("""{ "data": [] }""");

        Assert.Equal("empty-data", problem.Code);
    }
}
