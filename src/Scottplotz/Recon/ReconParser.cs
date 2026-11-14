using System.Text.Json;
using Scottplotz.Charting;

namespace Scottplotz.Recon;

public static class ReconParser
{
    private static readonly string[] RootNames =
    [
        "left", "right", "unit", "asOf", "tolerance", "fields", "measure", "records", "groups", "totals",
        "text", "labels", "theme", "palette", "options"
    ];
    private static readonly string[] UnitNames = ["one", "many"];
    private static readonly string[] TotalNames = ["left", "right"];

    public static Result<ReconModel> Parse(string body, ReconView view, ReconLimits limits)
    {
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
            return Root(document.RootElement, view, limits);
        }
        catch (JsonException error)
        {
            return Result<ReconModel>.Error(ChartProblem.MalformedJson(error.Message));
        }
    }

    private static Result<ReconModel> Root(JsonElement root, ReconView view, ReconLimits limits) =>
        root.ValueKind != JsonValueKind.Object
            ? ReconRead.Fail<ReconModel>(ReconProblem.Shape("The body must be a JSON object with a records array."))
            : from known in ReconRead.OnlyKnown(root, RootNames, "the request")
              from records in Records(root, limits)
              from left in ReconWording.Side(root, "left", "Left")
              from right in ReconWording.Side(root, "right", "Right")
              from unit in Unit(root)
              from asOf in ReconRead.Date(root, "asOf", "asOf")
              from tolerance in ReconFields.Tolerance(root, "tolerance")
              from drafts in ReconFields.Parse(root, records, limits)
              from measure in ReconFields.Measure(root, drafts)
              from rows in ReconRows.Parse(records, drafts, measure)
              from groups in ReconRows.Groups(root, drafts, measure)
              from totals in Totals(root)
              let fields = ReconFields.Finish(drafts, rows)
              let measureIndex = ReconRows.MeasureIndex(drafts, measure)
              from built in rows.Select(row => ReconRows.Build(row, fields, measureIndex, tolerance ?? ReconTolerance.None)).Collect()
              let measureField = fields.FirstOrDefault(field => field.Key == measure)
              let tokens = ReconWording.Tokens(
                  left,
                  right,
                  unit.Many,
                  unit.One,
                  asOf,
                  measureField?.Label,
                  ReconWording.ToleranceText(measureField?.Tolerance ?? tolerance ?? ReconTolerance.None, measureField?.Decimals ?? 2))
              from labels in ReconWording.Labels(root, tokens)
              from text in ReconWording.Text(root, view, tokens)
              from options in ReconOptions.Resolve(root, view)
              from theme in ReconWording.Theme(root)
              from palette in ReconWording.Palette(root)
              select new ReconModel(
                  view.Name,
                  view.Title,
                  left,
                  right,
                  unit.Many,
                  unit.One,
                  asOf,
                  tolerance ?? ReconTolerance.None,
                  fields,
                  measure,
                  built,
                  groups,
                  ReconRows.Summarize(built, measureIndex, totals.Left, totals.Right),
                  text,
                  labels,
                  options,
                  theme,
                  palette);

    private static Result<JsonElement> Records(JsonElement root, ReconLimits limits) =>
        ReconRead.Get(root, "records") switch
        {
            null => ReconRead.Fail<JsonElement>(ReconProblem.MissingRecords()),
            { ValueKind: JsonValueKind.Array } records when records.GetArrayLength() == 0 =>
                ReconRead.Fail<JsonElement>(ReconProblem.EmptyRecords()),
            { ValueKind: JsonValueKind.Array } records when records.GetArrayLength() > limits.MaxRecords =>
                ReconRead.Fail<JsonElement>(ReconProblem.TooManyRecords(records.GetArrayLength(), limits.MaxRecords)),
            { ValueKind: JsonValueKind.Array } records => ReconResult.Ok(records),
            _ => ReconRead.Fail<JsonElement>(ReconProblem.Invalid("records", "must be an array"))
        };

    private static Result<(string One, string Many)> Unit(JsonElement root) =>
        ReconRead.Get(root, "unit") switch
        {
            null => ReconResult.Ok(("record", "records")),
            { ValueKind: JsonValueKind.String } text => ReconResult.Ok((text.GetString() ?? "", text.GetString() ?? "")),
            { ValueKind: JsonValueKind.Object } both =>
                from known in ReconRead.OnlyKnown(both, UnitNames, "unit")
                from one in ReconRead.Text(both, "one", "unit.one")
                from many in ReconRead.Text(both, "many", "unit.many")
                select (one ?? many ?? "record", many ?? one ?? "records"),
            _ => ReconRead.Fail<(string, string)>(ReconProblem.Invalid("unit", "must be a noun or an object with one and many"))
        };

    private static Result<(double? Left, double? Right)> Totals(JsonElement root) =>
        ReconRead.Get(root, "totals") switch
        {
            null => ReconResult.Ok<(double?, double?)>((null, null)),
            { ValueKind: JsonValueKind.Object } totals =>
                from known in ReconRead.OnlyKnown(totals, TotalNames, "totals")
                from left in ReconRead.Number(totals, "left", "totals.left")
                from right in ReconRead.Number(totals, "right", "totals.right")
                select (left, right),
            _ => ReconRead.Fail<(double?, double?)>(ReconProblem.Invalid("totals", "must be an object with left and right"))
        };
}
