using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scottplotz.Charting;

namespace Scottplotz.Parsing;

public static class JsonInputParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    public static Result<ChartTable> Parse(string json, ParseLimits limits)
    {
        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(QuoteNonFiniteTokens(json), SerializerOptions);
        }
        catch (JsonException exception)
        {
            return Result<ChartTable>.Error(ChartProblem.MalformedJson(exception.Message));
        }

        if (root.ValueKind != JsonValueKind.Object)
            return Result<ChartTable>.Error(ChartProblem.MalformedJson("The body must be a JSON object with a data property."));

        var unknown = root.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => name is not "data" and not "hints");
        if (unknown is not null)
            return Result<ChartTable>.Error(ChartProblem.UnknownProperty(unknown));

        if (!root.TryGetProperty("data", out var data))
            return Result<ChartTable>.Error(ChartProblem.MissingData());

        return ReadData(data).Bind(raw => TableMaterializer.FromRaw(raw, limits));
    }

    private static Result<RawTable> ReadData(JsonElement data) =>
        data.ValueKind switch
        {
            JsonValueKind.Array => ReadArray(data),
            JsonValueKind.Object => ReadColumns(data),
            JsonValueKind.Null => Result<RawTable>.Error(ChartProblem.Empty()),
            _ => Result<RawTable>.Error(ChartProblem.MalformedJson("The data property must be an array or an object of arrays."))
        };

    private static Result<RawTable> ReadArray(JsonElement data)
    {
        var elements = data.EnumerateArray().ToList();
        if (elements.Count == 0)
            return Result<RawTable>.Error(ChartProblem.Empty());

        return elements[0].ValueKind == JsonValueKind.Object
            ? ReadRows(elements)
            : ReadPlainArray(elements);
    }

    private static Result<RawTable> ReadPlainArray(IReadOnlyList<JsonElement> elements)
    {
        if (elements.Any(element => !IsPlainValue(element)))
            return Result<RawTable>.Error(ChartProblem.PlainArrayShape());

        var cells = elements.Select((element, index) => ReadCell(element, "value", index + 1)).ToList();
        var failure = cells.OfType<Result<RawCell>.Fail>().FirstOrDefault();
        if (failure is not null)
            return Result<RawTable>.Error(failure.Problem);

        var rows = cells
            .OfType<Result<RawCell>.Ok>()
            .Select(ok => (IReadOnlyList<RawCell>)[ok.Value])
            .ToList();

        return Result<RawTable>.Success(new RawTable(DataOrigin.PlainArray, [null], rows));
    }

    private static Result<RawTable> ReadRows(IReadOnlyList<JsonElement> elements)
    {
        var nonObject = elements.Select((element, index) => (element, index)).FirstOrDefault(item => item.element.ValueKind != JsonValueKind.Object);
        if (nonObject.element.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Object)
            return Result<RawTable>.Error(ChartProblem.MalformedJson($"Row {nonObject.index + 1} must be an object."));

        var names = elements
            .SelectMany(row => row.EnumerateObject().Select(property => property.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
            return Result<RawTable>.Error(ChartProblem.Empty("Each row is an empty object. Include at least one field."));

        var rows = new List<IReadOnlyList<RawCell>>(elements.Count);
        foreach (var (row, rowIndex) in elements.Select((row, index) => (row, index)))
        {
            var cells = new List<RawCell>(names.Count);
            foreach (var name in names)
            {
                if (!row.TryGetProperty(name, out var value))
                {
                    cells.Add(RawCell.Empty.Instance);
                    continue;
                }

                var read = ReadCell(value, name, rowIndex + 1);
                if (read is Result<RawCell>.Fail fail)
                    return Result<RawTable>.Error(fail.Problem);

                cells.Add(((Result<RawCell>.Ok)read).Value);
            }

            rows.Add(cells);
        }

        return Result<RawTable>.Success(new RawTable(DataOrigin.Rows, names, rows));
    }

    private static Result<RawTable> ReadColumns(JsonElement data)
    {
        var properties = data.EnumerateObject().ToList();
        if (properties.Count == 0)
            return Result<RawTable>.Error(ChartProblem.Empty());

        var duplicate = properties
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            return Result<RawTable>.Error(ChartProblem.DuplicateColumn(duplicate.Key));

        var columns = new List<IReadOnlyList<RawCell>>(properties.Count);
        foreach (var property in properties)
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
                return Result<RawTable>.Error(ChartProblem.MalformedJson($"Column '{property.Name}' must be an array of values."));

            var values = property.Value.EnumerateArray().ToList();
            var cells = new List<RawCell>(values.Count);
            foreach (var (value, index) in values.Select((value, index) => (value, index)))
            {
                var read = ReadCell(value, property.Name, index + 1);
                if (read is Result<RawCell>.Fail fail)
                    return Result<RawTable>.Error(fail.Problem);

                cells.Add(((Result<RawCell>.Ok)read).Value);
            }

            columns.Add(cells);
        }

        var widthProblem = Mismatched(properties.Select(property => property.Name).ToList(), columns);
        if (widthProblem is not null)
            return Result<RawTable>.Error(widthProblem);

        var rowCount = columns[0].Count;
        if (rowCount == 0)
            return Result<RawTable>.Error(ChartProblem.Empty());

        var rows = Enumerable.Range(0, rowCount)
            .Select(row => (IReadOnlyList<RawCell>)columns.Select(column => column[row]).ToList())
            .ToList();

        return Result<RawTable>.Success(new RawTable(
            DataOrigin.Columns,
            properties.Select(property => (string?)property.Name).ToList(),
            rows));
    }

    private static ChartProblem? Mismatched(IReadOnlyList<string> names, IReadOnlyList<IReadOnlyList<RawCell>> columns)
    {
        var baseline = columns[0].Count;
        var mismatch = columns
            .Skip(1)
            .Select((column, index) => (Count: column.Count, Name: names[index + 1]))
            .FirstOrDefault(item => item.Count != baseline);

        return mismatch.Name is null
            ? null
            : ChartProblem.MismatchedLengths(mismatch.Name, mismatch.Count, names[0], baseline);
    }

    private static bool IsPlainValue(JsonElement element) =>
        element.ValueKind is JsonValueKind.Number or JsonValueKind.Null
        || (element.ValueKind == JsonValueKind.String
            && ScalarParsing.TryParseNonFiniteToken(element.GetString() ?? "", out _));

    private static string QuoteNonFiniteTokens(string json)
    {
        var quoted = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        for (var index = 0; index < json.Length; index++)
        {
            var current = json[index];
            if (inString)
            {
                quoted.Append(current);
                if (escaped)
                    escaped = false;
                else if (current == '\\')
                    escaped = true;
                else if (current == '"')
                    inString = false;
                continue;
            }

            if (current == '"')
            {
                inString = true;
                quoted.Append(current);
                continue;
            }

            var token = MatchNonFiniteToken(json, index);
            if (token.Length > 0)
            {
                quoted.Append('"').Append(token).Append('"');
                index += token.Length - 1;
                continue;
            }

            quoted.Append(current);
        }

        return quoted.ToString();
    }

    private static ReadOnlySpan<char> MatchNonFiniteToken(string json, int index)
    {
        ReadOnlySpan<char> rest = json.AsSpan(index);
        string[] tokens = ["-Infinity", "Infinity", "+Infinity", "NaN"];
        foreach (var token in tokens)
        {
            if (!rest.StartsWith(token))
                continue;
            if (index > 0 && IsTokenCharacter(json[index - 1]))
                continue;
            var end = index + token.Length;
            if (end < json.Length && IsTokenCharacter(json[end]))
                continue;
            return rest[..token.Length];
        }

        return [];
    }

    private static bool IsTokenCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value == '_';

    private static Result<RawCell> ReadCell(JsonElement value, string column, int row) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => Result<RawCell>.Success(new RawCell.Number(number)),
            JsonValueKind.Number => Result<RawCell>.Error(ChartProblem.NonNumeric(column, row, value.GetRawText())),
            JsonValueKind.String => Result<RawCell>.Success(new RawCell.Text(value.GetString() ?? "")),
            JsonValueKind.Null => Result<RawCell>.Success(RawCell.Empty.Instance),
            JsonValueKind.True or JsonValueKind.False => Result<RawCell>.Error(ChartProblem.NonNumeric(column, row, value.GetRawText())),
            _ => Result<RawCell>.Error(ChartProblem.UnsupportedValue(column, row, value.ValueKind.ToString().ToLowerInvariant()))
        };
}
