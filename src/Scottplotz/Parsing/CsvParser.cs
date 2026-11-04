using System.Text;
using Scottplotz.Charting;

namespace Scottplotz.Parsing;

public static class CsvParser
{
    public static Result<ChartTable> Parse(string text, ParseLimits limits) =>
        ReadGrid(text).Bind(grid => Materialize(grid, limits));

    private static Result<ChartTable> Materialize(IReadOnlyList<IReadOnlyList<string>> grid, ParseLimits limits)
    {
        var header = grid[0].Select(cell => cell.Trim()).ToList();
        if (header.Count == 0 || header.All(string.IsNullOrEmpty))
            return Result<ChartTable>.Error(ChartProblem.MalformedCsv("A header row is required."));

        if (header.Any(string.IsNullOrEmpty))
            return Result<ChartTable>.Error(ChartProblem.MalformedCsv("Every header column needs a name."));

        var duplicate = header
            .GroupBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            return Result<ChartTable>.Error(ChartProblem.DuplicateColumn(duplicate.Key));

        var dataRows = grid.Skip(1).ToList();
        if (dataRows.Count == 0)
            return Result<ChartTable>.Error(ChartProblem.Empty("CSV has a header but no data rows."));

        var tooWide = dataRows
            .Select((row, index) => (row, index))
            .FirstOrDefault(item => item.row.Count > header.Count);
        if (tooWide.row is not null)
            return Result<ChartTable>.Error(ChartProblem.CsvRowWidth(tooWide.index + 1, tooWide.row.Count, header.Count));

        var rows = dataRows
            .Select(row => (IReadOnlyList<RawCell>)header.Select((name, index) => Cell(row, index)).ToList())
            .ToList();

        return TableMaterializer.FromRaw(new RawTable(DataOrigin.Csv, header, rows), limits);
    }

    private static RawCell Cell(IReadOnlyList<string> row, int index)
    {
        if (index >= row.Count)
            return RawCell.Empty.Instance;

        var text = row[index].Trim();
        return text.Length == 0 ? RawCell.Empty.Instance : new RawCell.Text(text);
    }

    private static Result<IReadOnlyList<IReadOnlyList<string>>> ReadGrid(string text)
    {
        var start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var rowNumber = 1;

        void FinishRow()
        {
            row.Add(field.ToString());
            field.Clear();
            if (row.Any(value => value.Trim().Length > 0))
                rows.Add(row);
            row = [];
            rowNumber++;
        }

        for (var i = start; i < text.Length; i++)
        {
            var current = text[i];
            if (inQuotes)
            {
                if (current != '"')
                {
                    field.Append(current);
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            if (current == '"')
            {
                if (field.Length == 0)
                    inQuotes = true;
                else
                    field.Append(current);
                continue;
            }

            if (current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (current is '\n' || (current == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
            {
                FinishRow();
                continue;
            }

            if (current != '\r')
                field.Append(current);
        }

        if (inQuotes)
            return Result<IReadOnlyList<IReadOnlyList<string>>>.Error(
                ChartProblem.MalformedCsv($"A quoted field on row {rowNumber} is missing its closing quote."));

        if (field.Length > 0 || row.Count > 0)
            FinishRow();

        return rows.Count == 0
            ? Result<IReadOnlyList<IReadOnlyList<string>>>.Error(ChartProblem.MalformedCsv("A header row is required."))
            : Result<IReadOnlyList<IReadOnlyList<string>>>.Success(rows);
    }
}
