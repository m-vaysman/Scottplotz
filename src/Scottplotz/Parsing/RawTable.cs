namespace Scottplotz.Parsing;

public abstract record RawCell
{
    public sealed record Number(double Value) : RawCell;

    public sealed record Text(string Value) : RawCell;

    public sealed record Empty : RawCell
    {
        public static Empty Instance { get; } = new();
    }
}

public sealed record RawTable(
    Charting.DataOrigin Origin,
    IReadOnlyList<string?> Names,
    IReadOnlyList<IReadOnlyList<RawCell>> Rows);
