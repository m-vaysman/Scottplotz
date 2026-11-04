namespace Scottplotz.Hosting;

public static class UserPaths
{
    public const string HomeVariable = "SCOTTPLOTZ_HOME";

    public static string Root =>
        Environment.GetEnvironmentVariable(HomeVariable) is { Length: > 0 } home
            ? home
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scottplotz");

    public static string SettingsFile => Path.Combine(Root, "appsettings.user.json");

    public static string LogsFolder => Path.Combine(Root, "logs");

    public static string LogFile => Path.Combine(LogsFolder, "scottplotz.log");
}
