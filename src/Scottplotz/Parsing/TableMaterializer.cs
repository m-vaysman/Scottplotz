using Scottplotz.Charting;

namespace Scottplotz.Parsing;

public static class TableMaterializer
{
    public static Result<ChartTable> FromRaw(RawTable raw, ParseLimits limits)
    {
        if (raw.Names.Count == 0 || raw.Rows.Count == 0)
            return Result<ChartTable>.Error(ChartProblem.Empty());

        var interpreted = raw.Rows
            .Select(row => (IReadOnlyList<Interpreted>)row.Select(Interpret).ToList())
            .ToList();

        var kinds = raw.Names
            .Select((name, index) => (Name: Label(name), Kind: DecideKind(interpreted.Select(row => row[index]))))
            .ToList();

        var mixed = MixedProblem(raw.Names, interpreted, kinds.Select(column => column.Kind).ToList());
        if (mixed is not null)
            return Result<ChartTable>.Error(mixed);

        var violation = limits.InvalidNumbers == InvalidNumberPolicy.Reject
            ? FirstPolicyViolation(raw.Names, interpreted, kinds.Select(column => column.Kind).ToList())
            : null;
        if (violation is not null)
            return Result<ChartTable>.Error(violation);

        var kept = interpreted
            .Select((row, index) => (Row: row, Index: index))
            .Where(item => limits.InvalidNumbers != InvalidNumberPolicy.Drop || !IsDroppable(item.Row, kinds.Select(column => column.Kind)))
            .ToList();

        if (kept.Count == 0)
            return Result<ChartTable>.Error(ChartProblem.Empty("Data is empty after dropping null and non-finite values."));

        var typed = TypeColumns(raw.Names, kinds.Select(column => column.Kind).ToList(), kept);
        return typed switch
        {
            Result<IReadOnlyList<Column>>.Fail fail => Result<ChartTable>.Error(fail.Problem),
            Result<IReadOnlyList<Column>>.Ok ok when ok.Value.Sum(column => column.Count) > limits.MaxPoints =>
                Result<ChartTable>.Error(ChartProblem.TooManyPoints(ok.Value.Sum(column => column.Count), limits.MaxPoints)),
            Result<IReadOnlyList<Column>>.Ok ok => Result<ChartTable>.Success(new ChartTable(raw.Origin, ok.Value)),
            _ => throw new InvalidOperationException("Unknown result type.")
        };
    }

    private static Result<IReadOnlyList<Column>> TypeColumns(
        IReadOnlyList<string?> names,
        IReadOnlyList<ColumnKind> kinds,
        IReadOnlyList<(IReadOnlyList<Interpreted> Row, int Index)> kept)
    {
        var columns = names
            .Select((name, index) => BuildColumn(name, kinds[index], kept.Select(item => item.Row[index]).ToList()))
            .ToList();
        var failure = columns.OfType<Result<Column>.Fail>().FirstOrDefault();
        return failure is not null
            ? Result<IReadOnlyList<Column>>.Error(failure.Problem)
            : Result<IReadOnlyList<Column>>.Success(columns.OfType<Result<Column>.Ok>().Select(ok => ok.Value).ToList());
    }

    private static Result<Column> BuildColumn(
        string? name,
        ColumnKind kind,
        IReadOnlyList<Interpreted> cells)
    {
        return kind switch
        {
            ColumnKind.Numeric => Result<Column>.Success(new Column.Numeric(name, cells.Cast<Interpreted.Finite>().Select(cell => cell.Value).ToList())),
            ColumnKind.Date => Result<Column>.Success(new Column.Temporal(name, cells.Cast<Interpreted.Date>().Select(cell => cell.Value).ToList())),
            ColumnKind.Text => Result<Column>.Success(new Column.Categorical(name, cells.Select(AsText).ToList())),
            _ => Result<Column>.Error(ChartProblem.Empty($"Column '{Label(name)}' has no values."))
        };
    }

    private static ChartProblem? MixedProblem(
        IReadOnlyList<string?> names,
        IReadOnlyList<IReadOnlyList<Interpreted>> rows,
        IReadOnlyList<ColumnKind> kinds)
    {
        var mixedIndex = kinds.Select((kind, index) => (kind, index)).FirstOrDefault(column => column.kind == ColumnKind.Mixed);
        if (mixedIndex is { kind: not ColumnKind.Mixed })
            return null;

        var column = mixedIndex.index;
        var offender = rows
            .Select((row, index) => (Cell: row[column], Row: index + 1))
            .First(item => item.Cell is Interpreted.Text or Interpreted.Date or Interpreted.NonFinite);
        return ChartProblem.NonNumeric(Label(names[column]), offender.Row, Display(offender.Cell));
    }

    private static ChartProblem? FirstPolicyViolation(
        IReadOnlyList<string?> names,
        IReadOnlyList<IReadOnlyList<Interpreted>> rows,
        IReadOnlyList<ColumnKind> kinds)
    {
        return rows
            .SelectMany((row, rowIndex) => row.Select((cell, column) => (cell, column, row: rowIndex + 1)))
            .Select(item => Violation(Label(names[item.column]), item.row, item.cell, kinds[item.column]))
            .FirstOrDefault(problem => problem is not null);
    }

    private static ChartProblem? Violation(string column, int row, Interpreted cell, ColumnKind kind) =>
        cell switch
        {
            Interpreted.Blank => ChartProblem.NullValue(column, row),
            Interpreted.NonFinite nonFinite when kind == ColumnKind.Numeric => ChartProblem.NonFinite(column, row, nonFinite.Kind),
            _ => null
        };

    private static bool IsDroppable(IReadOnlyList<Interpreted> row, IEnumerable<ColumnKind> kinds) =>
        row.Zip(kinds, (cell, kind) => (cell, kind)).Any(item =>
            item.cell is Interpreted.Blank
            || (item.cell is Interpreted.NonFinite && item.kind == ColumnKind.Numeric));

    private static ColumnKind DecideKind(IEnumerable<Interpreted> column)
    {
        var cells = column.Where(cell => cell is not Interpreted.Blank).ToList();
        if (cells.Count == 0)
            return ColumnKind.Empty;

        var hasFinite = cells.Any(cell => cell is Interpreted.Finite);
        var hasNonFinite = cells.Any(cell => cell is Interpreted.NonFinite);
        var hasDate = cells.Any(cell => cell is Interpreted.Date);
        var hasText = cells.Any(cell => cell is Interpreted.Text);

        if (hasFinite && (hasDate || hasText))
            return ColumnKind.Mixed;

        if (hasNonFinite && hasDate)
            return ColumnKind.Mixed;

        if ((hasFinite || hasNonFinite) && !hasDate && !hasText)
            return ColumnKind.Numeric;

        if (hasDate && !hasText && !hasFinite && !hasNonFinite)
            return ColumnKind.Date;

        return ColumnKind.Text;
    }

    private static Interpreted Interpret(RawCell cell) =>
        cell switch
        {
            RawCell.Empty => Interpreted.Blank.Instance,
            RawCell.Number number when double.IsFinite(number.Value) => new Interpreted.Finite(number.Value),
            RawCell.Number number => new Interpreted.NonFinite(ScalarParsing.NonFiniteKind(number.Value)),
            RawCell.Text text when ScalarParsing.TryParseNonFiniteToken(text.Value, out var nonFinite) =>
                new Interpreted.NonFinite(ScalarParsing.NonFiniteKind(nonFinite)),
            RawCell.Text text when ScalarParsing.TryParseNumber(text.Value.Trim(), out var number) =>
                new Interpreted.Finite(number),
            RawCell.Text text when ScalarParsing.TryParseIsoDate(text.Value.Trim(), out var date) =>
                new Interpreted.Date(text.Value.Trim(), date),
            RawCell.Text text => new Interpreted.Text(text.Value.Trim()),
            _ => Interpreted.Blank.Instance
        };

    private static string AsText(Interpreted cell) =>
        cell switch
        {
            Interpreted.Text text => text.Value,
            Interpreted.Date date => date.Original,
            Interpreted.NonFinite nonFinite => nonFinite.Kind,
            Interpreted.Finite finite => finite.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => ""
        };

    private static string Display(Interpreted cell) =>
        cell switch
        {
            Interpreted.Text text => text.Value,
            Interpreted.Date date => date.Original,
            Interpreted.NonFinite nonFinite => nonFinite.Kind,
            Interpreted.Finite finite => finite.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => ""
        };

    private static string Label(string? name) =>
        string.IsNullOrEmpty(name) ? "value" : name;

    private abstract record Interpreted
    {
        public sealed record Blank : Interpreted
        {
            public static Blank Instance { get; } = new();
        }

        public sealed record Finite(double Value) : Interpreted;

        public sealed record NonFinite(string Kind) : Interpreted;

        public sealed record Date(string Original, DateTime Value) : Interpreted;

        public sealed record Text(string Value) : Interpreted;
    }

    private enum ColumnKind
    {
        Empty,
        Numeric,
        Date,
        Text,
        Mixed
    }
}
