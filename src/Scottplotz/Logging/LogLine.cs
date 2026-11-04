using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scottplotz.Logging;

public static class LogLine
{
    public static string Format(DateTimeOffset timestamp, string level, string category, string message, string? exception)
    {
        var payload = new Dictionary<string, string>
        {
            ["timestamp"] = timestamp.ToString("O", CultureInfo.InvariantCulture),
            ["level"] = level,
            ["category"] = category,
            ["message"] = message
        };

        if (!string.IsNullOrEmpty(exception))
            payload["exception"] = exception;

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}
