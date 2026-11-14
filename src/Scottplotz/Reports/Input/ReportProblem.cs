using Scottplotz.Charting;

namespace Scottplotz.Reports.Input;

public static class ReportProblem
{
    public static ChartProblem UnknownReport(string id, string available) =>
        new("unknown-report", "Unknown report", $"Unknown report '{id}'. Available reports: {available}.");

    public static ChartProblem Invalid(string path, string expected) =>
        new("invalid-report-input", "Invalid report input", $"'{path}' {expected}.");

    public static ChartProblem Unreadable(string path, string reason) =>
        new("invalid-report-input", "Invalid report input", path.Length > 0 ? $"Could not read '{path}'. {reason}" : reason);

    public static ChartProblem Missing(string path) =>
        new("invalid-report-input", "Invalid report input", $"'{path}' is required.");

    public static ChartProblem Duplicate(string what, string key) =>
        new("duplicate-key", "Duplicate key", $"{what} '{key}' appears more than once.");

    public static ChartProblem UnknownRow(string path, string source, string key) =>
        new("unknown-row", "Unknown row", $"'{path}' names the {source} row '{key}', which is not in rows.");

    public static ChartProblem UnknownEntry(string path, string id) =>
        new("unknown-entry", "Unknown entry", $"'{path}' names the entry '{id}', which is not in entries.");

    public static ChartProblem TooMany(string what, int count, int max) =>
        new("too-many-records", "Too many records", $"This request has {count} {what}, which is above the limit of {max}.");

    public static ChartProblem Unavailable(string id, string reason) =>
        new("report-unavailable", "Report not available for this input", $"{id} cannot be written from this input. {reason}");
}
