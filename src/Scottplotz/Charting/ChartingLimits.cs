using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Scottplotz.Charting;

public sealed record ChartingLimits(
    int MaxPoints,
    int MinDimension,
    int MaxDimension,
    int DefaultWidth,
    int DefaultHeight,
    int MaxRequestBodyBytes,
    InvalidNumberPolicy InvalidNumbers,
    int Port)
{
    public static ChartingLimits Default { get; } = new(
        MaxPoints: 100_000,
        MinDimension: 100,
        MaxDimension: 4000,
        DefaultWidth: 800,
        DefaultHeight: 450,
        MaxRequestBodyBytes: 5 * 1024 * 1024,
        InvalidNumbers: InvalidNumberPolicy.Reject,
        Port: 5240);

    public ParseLimits ToParseLimits() => new(MaxPoints, InvalidNumbers);

    public static ChartingLimits ForParser(ParseLimits limits) =>
        Default with { MaxPoints = limits.MaxPoints, InvalidNumbers = limits.InvalidNumbers };

    public static ChartingLimits Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection("Charting");
        var limits = new ChartingLimits(
            ReadInt(section, "MaxPoints", Default.MaxPoints),
            ReadInt(section, "MinDimension", Default.MinDimension),
            ReadInt(section, "MaxDimension", Default.MaxDimension),
            ReadInt(section, "DefaultWidth", Default.DefaultWidth),
            ReadInt(section, "DefaultHeight", Default.DefaultHeight),
            ReadInt(section, "MaxRequestBodyBytes", Default.MaxRequestBodyBytes),
            ParsePolicy(section["InvalidNumberPolicy"]),
            ReadInt(section, "Port", Default.Port));

        if (limits.DefaultWidth < limits.MinDimension || limits.DefaultWidth > limits.MaxDimension)
            throw new InvalidOperationException(
                $"Charting:DefaultWidth is {limits.DefaultWidth}. It must be between {limits.MinDimension} and {limits.MaxDimension}.");

        if (limits.DefaultHeight < limits.MinDimension || limits.DefaultHeight > limits.MaxDimension)
            throw new InvalidOperationException(
                $"Charting:DefaultHeight is {limits.DefaultHeight}. It must be between {limits.MinDimension} and {limits.MaxDimension}.");

        return limits;
    }

    public static InvalidNumberPolicy ParsePolicy(string? text) =>
        text?.Trim() switch
        {
            null or "" or "Reject" or "reject" => InvalidNumberPolicy.Reject,
            "Drop" or "drop" => InvalidNumberPolicy.Drop,
            _ => throw new InvalidOperationException(
                $"Charting:InvalidNumberPolicy is '{text}'. Allowed values: Reject, Drop.")
        };

    private static int ReadInt(IConfiguration section, string key, int fallback)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"Charting:{key} is '{text}'. It must be an integer.");
    }
}
