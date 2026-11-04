namespace Scottplotz;

public sealed record HealthReport(string Status, string Version)
{
    public static HealthReport Create(Version? version) =>
        new("ok", Format(version));

    public static string Format(Version? version) =>
        version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}
