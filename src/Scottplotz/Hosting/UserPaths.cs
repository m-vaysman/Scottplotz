namespace Scottplotz.Hosting;

public static class UserPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Scottplotz");

    public static string SettingsFile { get; } = Path.Combine(Root, "appsettings.user.json");

    public static string LogsFolder { get; } = Path.Combine(Root, "logs");

    public static string LogFile { get; } = Path.Combine(LogsFolder, "scottplotz.log");
}
