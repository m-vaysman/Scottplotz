namespace Scottplotz.Charting;

public enum DataOrigin
{
    PlainArray,
    Columns,
    Rows,
    Csv
}

public abstract record Column(string? Name)
{
    public abstract int Count { get; }

    public sealed record Numeric(string? Name, IReadOnlyList<double> Values) : Column(Name)
    {
        public override int Count => Values.Count;
    }

    public sealed record Temporal(string? Name, IReadOnlyList<DateTime> Values) : Column(Name)
    {
        public override int Count => Values.Count;
    }

    public sealed record Categorical(string? Name, IReadOnlyList<string> Values) : Column(Name)
    {
        public override int Count => Values.Count;
    }
}

public sealed record ChartTable(DataOrigin Origin, IReadOnlyList<Column> Columns)
{
    public int RowCount => Columns.Count == 0 ? 0 : Columns[0].Count;

    public int PointCount => Columns.Sum(column => column.Count);
}
