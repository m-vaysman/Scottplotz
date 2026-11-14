using Scottplotz.Charting;

namespace Scottplotz.Recon;

public static class ReconProblem
{
    public static ChartProblem UnknownView(string name, string available) =>
        new("unknown-view", "Unknown view", $"Unknown view '{name}'. Available views: {available}.");

    public static ChartProblem Shape(string detail) =>
        new("invalid-recon", "Invalid reconciliation request", detail);

    public static ChartProblem UnknownProperty(string where, string name, string allowed) =>
        new("unknown-property", "Unknown property", $"Unknown property '{name}' in {where}. Allowed properties: {allowed}.");

    public static ChartProblem Invalid(string path, string expected) =>
        new("invalid-value", "Invalid value", $"'{path}' {expected}.");

    public static ChartProblem MissingRecords() =>
        new("missing-records", "Missing records", "The body must have a records array. Example: { \"records\": [ { \"key\": \"A1\", \"left\": 100, \"right\": 101.5 } ] }.");

    public static ChartProblem EmptyRecords() =>
        new("empty-records", "Empty records", "The records array is empty. Send at least one record.");

    public static ChartProblem TooManyRecords(int count, int max) =>
        new("too-many-records", "Too many records", $"This request has {count} records, which is above the limit of {max}.");

    public static ChartProblem TooManyFields(int count, int max) =>
        new("too-many-fields", "Too many fields", $"This request has {count} fields, which is above the limit of {max}.");

    public static ChartProblem DuplicateField(string key) =>
        new("duplicate-field", "Duplicate field", $"Field '{key}' is declared more than once.");

    public static ChartProblem UnknownField(string path, string name, string allowed) =>
        new("unknown-field", "Unknown field", $"'{path}' uses field '{name}', which is not declared. Declared fields: {allowed}.");

    public static ChartProblem UnknownMeasure(string name, string allowed) =>
        new("unknown-measure", "Unknown measure", $"Measure '{name}' is not a numeric field. Numeric fields: {allowed}.");

    public static ChartProblem MissingSides(string path) =>
        new("missing-sides", "Record has no sides", $"'{path}' has neither left nor right. A record needs at least one side.");

    public static ChartProblem StatusMismatch(string path, string status, string reason) =>
        new("status-mismatch", "Status does not fit the record", $"'{path}' has status '{status}', but {reason}.");

    public static ChartProblem UnknownLabel(string name, string allowed) =>
        new("unknown-label", "Unknown label", $"Unknown label '{name}'. Allowed labels: {allowed}.");

    public static ChartProblem UnknownText(string view, string name, string allowed) =>
        new("unknown-text", "Unknown text", $"View '{view}' has no text '{name}'. Allowed text: {allowed}.");

    public static ChartProblem UnknownOption(string view, string name, string allowed) =>
        new("unknown-option", "Unknown option", $"View '{view}' has no option '{name}'. Allowed options: {allowed}.");

    public static ChartProblem InvalidOption(string name, string expected) =>
        new("invalid-option", "Invalid option", $"Option '{name}' {expected}.");

    public static ChartProblem InvalidTheme(string value) =>
        new("invalid-theme", "Invalid theme", $"Theme is '{value}'. Allowed values: auto, light, dark.");

    public static ChartProblem UnknownPaletteToken(string name, string allowed) =>
        new("invalid-palette", "Invalid palette", $"Unknown palette colour '{name}'. Allowed colours: {allowed}.");

    public static ChartProblem InvalidPaletteValue(string name) =>
        new("invalid-palette", "Invalid palette", $"Palette colour '{name}' must be a hex colour such as #2563eb or #26e.");
}
