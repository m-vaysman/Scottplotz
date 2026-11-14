using System.Globalization;

namespace Scottplotz.Reports.Model;

public static class CellText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Of(ReportCell cell) => Of(cell.Value, cell.Format);

    public static string Of(CellValue value, NumberFormat format) =>
        value switch
        {
            CellValue.Text text => text.Value,
            CellValue.Date date => date.Value.ToString("yyyy-MM-dd", Invariant),
            CellValue.Number number => Number(number.Value, format),
            _ => ""
        };

    public static string Number(decimal value, NumberFormat format) =>
        format switch
        {
            NumberFormat.Amount => Accounting(value, "#,##0.00"),
            NumberFormat.Quantity => Accounting(value, "#,##0"),
            NumberFormat.Count => Math.Round(value) == 0 ? "-" : (value < 0 ? "-" : "") + Math.Abs(value).ToString("#,##0", Invariant),
            NumberFormat.SignedCount => Math.Round(value) == 0 ? "-" : (value < 0 ? "-" : "+") + Math.Abs(value).ToString("#,##0", Invariant),
            NumberFormat.Percent => (value * 100).ToString("0.0", Invariant) + "%",
            NumberFormat.Integer => value.ToString("0", Invariant),
            _ => General(value)
        };

    public static bool IsNumeric(ReportCell cell) => cell.Value is CellValue.Number or CellValue.Date;

    private static string Accounting(decimal value, string pattern)
    {
        var text = Math.Abs(value).ToString(pattern, Invariant);
        var zero = decimal.Parse(text, NumberStyles.Number, Invariant) == 0;
        return zero ? "-" : value < 0 ? $"({text})" : text;
    }

    private static string General(decimal value) =>
        (value / 1.000000000000000000000000000000000m).ToString(Invariant);
}
