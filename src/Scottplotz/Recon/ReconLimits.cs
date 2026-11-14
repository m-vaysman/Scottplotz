using System.Globalization;

namespace Scottplotz.Recon;

public sealed record ReconLimits(int MaxRecords, int MaxFields)
{
    public static ReconLimits Default { get; } = new(MaxRecords: 20_000, MaxFields: 40);

    public static ReconLimits Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection("Recon");
        return new ReconLimits(
            ReadInt(section, "MaxRecords", Default.MaxRecords),
            ReadInt(section, "MaxFields", Default.MaxFields));
    }

    private static int ReadInt(IConfiguration section, string key, int fallback)
    {
        var text = section[key];
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"Recon:{key} is '{text}'. It must be a positive integer.");
    }
}
