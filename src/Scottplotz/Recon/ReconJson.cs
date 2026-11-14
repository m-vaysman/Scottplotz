using System.Text;
using System.Text.Json;

namespace Scottplotz.Recon;

public static class ReconJson
{
    public static string Write(ReconModel model)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Model(writer, model);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Model(Utf8JsonWriter writer, ReconModel model)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("view");
        writer.WriteString("name", model.ViewName);
        writer.WriteString("title", model.ViewTitle);
        writer.WriteEndObject();
        Side(writer, "left", model.Left);
        Side(writer, "right", model.Right);
        writer.WriteString("unit", model.Unit);
        writer.WriteString("unitOne", model.UnitOne);
        Nullable(writer, "asOf", model.AsOf);
        Tolerance(writer, "tolerance", model.Tolerance);

        writer.WriteStartArray("fields");
        foreach (var field in model.Fields)
        {
            writer.WriteStartObject();
            writer.WriteString("key", field.Key);
            writer.WriteString("label", field.Label);
            writer.WriteString("type", field.Type.ToString().ToLowerInvariant());
            writer.WriteNumber("decimals", field.Decimals);
            writer.WriteBoolean("compare", field.Compare);
            if (field.Tolerance is { } own)
                Tolerance(writer, "tolerance", own);
            else
                writer.WriteNull("tolerance");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        Nullable(writer, "measure", model.Measure);

        writer.WriteStartArray("records");
        foreach (var record in model.Records)
            Record(writer, record, model.Fields);
        writer.WriteEndArray();

        writer.WriteStartArray("groups");
        foreach (var group in model.Groups)
        {
            writer.WriteStartObject();
            Strings(writer, "path", group.Path);
            writer.WriteString("name", group.Name);
            Nullable(writer, "note", group.Note);
            Values(writer, "left", group.Left, model.Fields);
            Values(writer, "right", group.Right, model.Fields);
            Pairs(writer, "attrs", group.Attributes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        var summary = model.Summary;
        writer.WriteStartObject("summary");
        writer.WriteStartObject("counts");
        writer.WriteNumber("leftOnly", summary.LeftOnly);
        writer.WriteNumber("exact", summary.Exact);
        writer.WriteNumber("tolerance", summary.Tolerance);
        writer.WriteNumber("break", summary.Break);
        writer.WriteNumber("rightOnly", summary.RightOnly);
        writer.WriteEndObject();
        writer.WriteNumber("duplicates", summary.Duplicates);
        writer.WriteNumber("leftCount", summary.LeftCount);
        writer.WriteNumber("rightCount", summary.RightCount);
        Nullable(writer, "leftTotal", summary.LeftTotal);
        Nullable(writer, "rightTotal", summary.RightTotal);
        Nullable(writer, "net", summary.Net);
        writer.WriteEndObject();

        Texts(writer, "text", model.Text);
        Texts(writer, "labels", model.Labels);
        Pairs(writer, "options", model.Options);
        writer.WriteString("theme", ReconWording.ThemeToken(model.Theme));
        Texts(writer, "palette", model.Palette);
        writer.WriteEndObject();
    }

    private static void Record(Utf8JsonWriter writer, ReconRecord record, IReadOnlyList<ReconField> fields)
    {
        writer.WriteStartObject();
        writer.WriteString("key", record.Key);
        writer.WriteString("name", record.Name);
        writer.WriteString("status", ReconComparison.Token(record.Status));
        writer.WriteNumber("count", record.Count);
        Values(writer, "left", record.Left, fields);
        Values(writer, "right", record.Right, fields);
        Lines(writer, "leftLines", record.Left, fields);
        Lines(writer, "rightLines", record.Right, fields);
        Nullable(writer, "delta", record.Delta);
        Nullable(writer, "pct", record.Percent);
        Strings(writer, "breaks", record.Breaks);
        Strings(writer, "group", record.Group);
        Nullable(writer, "date", record.Date);
        Nullable(writer, "reason", record.Reason);
        Nullable(writer, "duplicate", record.Duplicate == ReconDuplicate.None ? null : record.Duplicate.ToString().ToLowerInvariant());
        Pairs(writer, "attrs", record.Attributes);
        writer.WriteEndObject();
    }

    private static void Values(Utf8JsonWriter writer, string name, ReconSideValues? side, IReadOnlyList<ReconField> fields)
    {
        if (side is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteStartObject(name);
        foreach (var (field, value) in fields.Zip(side.Values))
            Value(writer, field.Key, value);
        writer.WriteEndObject();
    }

    private static void Lines(Utf8JsonWriter writer, string name, ReconSideValues? side, IReadOnlyList<ReconField> fields)
    {
        writer.WriteStartArray(name);
        foreach (var line in side?.Lines ?? [])
        {
            writer.WriteStartObject();
            Nullable(writer, "key", line.Key);
            Nullable(writer, "name", line.Name);
            writer.WriteStartObject("values");
            foreach (var (field, value) in fields.Zip(line.Values))
                Value(writer, field.Key, value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void Side(Utf8JsonWriter writer, string name, ReconSide side)
    {
        writer.WriteStartObject(name);
        writer.WriteString("name", side.Name);
        writer.WriteString("short", side.Short);
        writer.WriteEndObject();
    }

    private static void Tolerance(Utf8JsonWriter writer, string name, ReconTolerance tolerance)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("absolute", tolerance.Absolute);
        writer.WriteNumber("percent", tolerance.Percent);
        writer.WriteEndObject();
    }

    private static void Strings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void Texts(Utf8JsonWriter writer, string name, IReadOnlyList<KeyValuePair<string, string>> items)
    {
        writer.WriteStartObject(name);
        foreach (var item in items)
            writer.WriteString(item.Key, item.Value);
        writer.WriteEndObject();
    }

    private static void Pairs(Utf8JsonWriter writer, string name, IReadOnlyList<KeyValuePair<string, ReconValue?>> items)
    {
        writer.WriteStartObject(name);
        foreach (var item in items)
            Value(writer, item.Key, item.Value);
        writer.WriteEndObject();
    }

    private static void Value(Utf8JsonWriter writer, string name, ReconValue? value)
    {
        switch (value)
        {
            case ReconValue.Number number:
                writer.WriteNumber(name, number.Value);
                break;
            case ReconValue.Text text:
                writer.WriteString(name, text.Value);
                break;
            case ReconValue.Flag flag:
                writer.WriteBoolean(name, flag.Value);
                break;
            case ReconValue.Json json:
                writer.WritePropertyName(name);
                using (var document = JsonDocument.Parse(json.Raw))
                    document.RootElement.WriteTo(writer);
                break;
            default:
                writer.WriteNull(name);
                break;
        }
    }

    private static void Nullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
            writer.WriteNull(name);
        else
            writer.WriteString(name, value);
    }

    private static void Nullable(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is double number)
            writer.WriteNumber(name, number);
        else
            writer.WriteNull(name);
    }
}
