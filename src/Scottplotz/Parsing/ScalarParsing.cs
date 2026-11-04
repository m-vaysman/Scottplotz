using System.Globalization;

namespace Scottplotz.Parsing;

public static class ScalarParsing
{
    private static readonly string[] DateFormats =
    [
        "yyyy'-'MM'-'dd",
        "yyyy'-'MM'-'dd'T'HH':'mm",
        "yyyy'-'MM'-'dd'T'HH':'mm':'ss",
        "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFF",
        "yyyy'-'MM'-'dd'T'HH':'mmK",
        "yyyy'-'MM'-'dd'T'HH':'mm':'ssK",
        "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFFK"
    ];

    public static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);

    public static bool TryParseNonFiniteToken(string text, out double value)
    {
        switch (text.Trim())
        {
            case "NaN":
                value = double.NaN;
                return true;
            case "Infinity" or "+Infinity":
                value = double.PositiveInfinity;
                return true;
            case "-Infinity":
                value = double.NegativeInfinity;
                return true;
            default:
                value = default;
                return false;
        }
    }

    public static bool TryParseIsoDate(string text, out DateTime utc)
    {
        utc = default;
        if (!DateTimeOffset.TryParseExact(
                text,
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        var shifted = text.EndsWith('Z') || HasNumericOffset(text);
        utc = shifted
            ? parsed.UtcDateTime
            : DateTime.SpecifyKind(parsed.DateTime, DateTimeKind.Utc);
        return true;
    }

    public static string NonFiniteKind(double value) =>
        double.IsNaN(value) ? "NaN" : "Infinity";

    private static bool HasNumericOffset(string text) =>
        text.Length >= 6
        && text[^3] == ':'
        && (text[^6] == '+' || text[^6] == '-');
}
