namespace Scottplotz.Reports.Model;

public enum Tone
{
    None,
    Break,
    Tolerance,
    Attributed,
    Ok,
    Fail,
    Muted,
    Link,
    Shade,
    Alert
}

public enum HeatColour
{
    Violet,
    Orange,
    Blue,
    Amber
}

public enum CellAlign
{
    Auto,
    Left,
    Center,
    Right
}

public enum NumberFormat
{
    General,
    Amount,
    Quantity,
    Count,
    SignedCount,
    Percent,
    Integer,
    Price,
    Date
}

public abstract record CellValue
{
    public sealed record Blank : CellValue
    {
        public static Blank Instance { get; } = new();
    }

    public sealed record Text(string Value) : CellValue;

    public sealed record Number(decimal Value) : CellValue;

    public sealed record Date(DateOnly Value) : CellValue;

    public static CellValue Of(object? value) =>
        value switch
        {
            null => Blank.Instance,
            CellValue already => already,
            string { Length: 0 } => Blank.Instance,
            string text => new Text(text),
            decimal number => new Number(number),
            int number => new Number(number),
            long number => new Number(number),
            DateOnly date => new Date(date),
            _ => new Text(value.ToString() ?? "")
        };
}

public sealed record ReportCell(
    CellValue Value,
    NumberFormat Format = NumberFormat.General,
    Tone Tone = Tone.None,
    bool Bold = false,
    bool Italic = false,
    CellAlign Align = CellAlign.Auto,
    int Span = 1,
    double? Heat = null,
    double? Bar = null,
    int Indent = 0,
    HeatColour HeatColour = HeatColour.Violet,
    bool Strike = false,
    bool Wrap = false)
{
    public static ReportCell Empty { get; } = new(CellValue.Blank.Instance);

    public static ReportCell Of(object? value, NumberFormat format = NumberFormat.General) => new(CellValue.Of(value), format);

    public static ReportCell Check(bool passed) =>
        new(new CellValue.Text(passed ? "✓" : "✗"), Tone: passed ? Tone.Ok : Tone.Fail, Bold: true, Align: CellAlign.Center);
}

public enum RowKind
{
    Normal,
    Band,
    SubHeader,
    Total
}

public sealed record ReportRow(IReadOnlyList<ReportCell> Cells, RowKind Kind = RowKind.Normal)
{
    public static ReportRow Of(params object?[] values) =>
        new(values.Select(value => value as ReportCell ?? ReportCell.Of(value)).ToList());

    public static ReportRow Of(RowKind kind, params object?[] values) => Of(values) with { Kind = kind };
}

public sealed record ReportColumn(string Title, CellAlign Align = CellAlign.Auto, bool Wrap = false);

public sealed record ColumnGroup(string Title, int Span);

public sealed record ReportTile(string Label, ReportCell Value, string? Sub = null);

public sealed record BarItem(string Label, decimal Value, Tone Tone = Tone.None, string? Note = null, decimal? Second = null);

public sealed record LegendItem(string Label, Tone Tone, string? Meaning = null);

public abstract record ReportBlock;

public sealed record TableBlock(
    string? Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    string? Note = null,
    string? Empty = null,
    IReadOnlyList<ColumnGroup>? Groups = null) : ReportBlock;

public sealed record TilesBlock(IReadOnlyList<ReportTile> Tiles) : ReportBlock;

public sealed record BarsBlock(
    string Title,
    IReadOnlyList<BarItem> Bars,
    NumberFormat Format = NumberFormat.Count,
    string? AxisTitle = null,
    string? FirstSeries = null,
    string? SecondSeries = null) : ReportBlock;

public sealed record LegendBlock(IReadOnlyList<LegendItem> Items) : ReportBlock;

public sealed record NoteBlock(string Text) : ReportBlock;

public sealed record SectionBlock(string Title, string? Sub, IReadOnlyList<ReportBlock> Blocks) : ReportBlock;

public sealed record DataBlock(string Name, string? About, TableBlock Table) : ReportBlock;

public sealed record ReportInfo(
    string Id,
    string Name,
    string Title,
    string Office,
    string Users,
    string Question,
    string Cadence,
    string About)
{
    public string Slug => string.Concat(Name.Select((letter, index) => char.IsUpper(letter) && index > 0 ? "-" + char.ToLowerInvariant(letter) : char.ToLowerInvariant(letter).ToString()));
}

public sealed record ReportDocument(
    ReportInfo Info,
    string Title,
    DateOnly AsOf,
    string RunLine,
    IReadOnlyList<ReportBlock> Blocks);
