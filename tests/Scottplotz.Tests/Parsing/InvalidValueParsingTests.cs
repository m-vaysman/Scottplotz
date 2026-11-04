using Scottplotz.Charting;

namespace Scottplotz.Tests.Parsing;

public sealed class InvalidValueParsingTests
{
    [Fact]
    public void Rejects_malformed_json()
    {
        var problem = ParseAssert.Fail("{");

        Assert.Equal("malformed-json", problem.Code);
    }

    [Fact]
    public void Rejects_an_unknown_top_level_property()
    {
        var problem = ParseAssert.Fail("""{ "values": [1] }""");

        Assert.Equal("unknown-property", problem.Code);
        Assert.Contains("values", problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "hints": {} }""")]
    public void Rejects_an_object_without_data(string body)
    {
        var problem = ParseAssert.Fail(body);

        Assert.Equal("missing-data", problem.Code);
    }

    [Fact]
    public void Rejects_an_empty_body()
    {
        var problem = ParseAssert.Fail("   ");

        Assert.Equal("empty-data", problem.Code);
    }

    [Fact]
    public void Rejects_null_by_default_and_can_drop_the_row()
    {
        const string body = """{ "data": [1, null, 3] }""";

        var rejected = ParseAssert.Fail(body);
        Assert.Equal("null-value", rejected.Code);
        Assert.Contains("row 2", rejected.Detail, StringComparison.Ordinal);
        Assert.Contains("Drop", rejected.Detail, StringComparison.Ordinal);

        var table = ParseAssert.Ok(body, policy: InvalidNumberPolicy.Drop);
        var column = Assert.IsType<Column.Numeric>(Assert.Single(table.Columns));
        Assert.Equal([1d, 3d], column.Values);
    }

    [Fact]
    public void Rejects_nan_and_infinity_by_default()
    {
        var nan = ParseAssert.Fail("""{ "data": [1, NaN, 3] }""");
        Assert.Equal("non-finite", nan.Code);
        Assert.Contains("NaN", nan.Detail, StringComparison.Ordinal);

        var infinity = ParseAssert.Fail("""{ "data": [1, Infinity] }""");
        Assert.Equal("non-finite", infinity.Code);
        Assert.Contains("Infinity", infinity.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Drops_non_finite_rows_when_configured()
    {
        var table = ParseAssert.Ok("""{ "data": [1, NaN, 3, Infinity] }""", policy: InvalidNumberPolicy.Drop);

        var column = Assert.IsType<Column.Numeric>(Assert.Single(table.Columns));
        Assert.Equal([1d, 3d], column.Values);
    }

    [Fact]
    public void Rejects_a_non_numeric_cell_in_a_numeric_column()
    {
        var problem = ParseAssert.Fail("""{ "data": { "price": [1, "n/a", 3] } }""");

        Assert.Equal("non-numeric", problem.Code);
        Assert.Contains("n/a", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("row 2", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_booleans()
    {
        var problem = ParseAssert.Fail("""{ "data": { "price": [1, true] } }""");

        Assert.Equal("non-numeric", problem.Code);
    }

    [Fact]
    public void Rejects_requests_over_the_point_limit()
    {
        var problem = ParseAssert.Fail("""{ "data": [1, 2, 3, 4] }""", maxPoints: 3);

        Assert.Equal("too-many-points", problem.Code);
        Assert.Contains("4", problem.Detail, StringComparison.Ordinal);
        Assert.Contains("3", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_every_cell_toward_the_point_limit()
    {
        var problem = ParseAssert.Fail(
            """{ "data": { "a": [1, 2, 3], "b": [4, 5, 6] } }""",
            maxPoints: 5);

        Assert.Equal("too-many-points", problem.Code);
        Assert.Contains("6", problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "data": { "a": [1, null, 3], "b": [4, 5, 6] } }""", "application/json")]
    [InlineData("""{ "data": [{ "a": 1, "b": 4 }, { "a": null, "b": 5 }, { "a": 3, "b": 6 }] }""", "application/json")]
    [InlineData("a,b\n1,4\nNaN,5\n3,6\n", "text/csv")]
    public void Drop_removes_the_whole_row_and_keeps_columns_aligned(string body, string contentType)
    {
        var table = ParseAssert.Ok(body, contentType, InvalidNumberPolicy.Drop);

        var a = Assert.IsType<Column.Numeric>(table.Columns.Single(column => column.Name == "a"));
        var b = Assert.IsType<Column.Numeric>(table.Columns.Single(column => column.Name == "b"));
        Assert.Equal([1d, 3d], a.Values);
        Assert.Equal([4d, 6d], b.Values);
    }

    [Fact]
    public void Dropping_every_row_is_empty_data()
    {
        var problem = ParseAssert.Fail("""{ "data": [null, null] }""", policy: InvalidNumberPolicy.Drop);

        Assert.Equal("empty-data", problem.Code);
    }

    [Fact]
    public void Drop_still_rejects_text_in_a_numeric_column()
    {
        var problem = ParseAssert.Fail(
            """{ "data": { "price": [1, "n/a", 3] } }""",
            policy: InvalidNumberPolicy.Drop);

        Assert.Equal("non-numeric", problem.Code);
        Assert.Contains("n/a", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_the_word_nan_as_text_beside_other_labels()
    {
        var table = ParseAssert.Ok("""{ "data": { "label": ["apple", "NaN"] } }""");

        var labels = Assert.IsType<Column.Categorical>(Assert.Single(table.Columns));
        Assert.Equal(["apple", "NaN"], labels.Values);
    }
}
