namespace Scottplotz.Hosting;

public static class StartupRegistration
{
    public const string ValueName = "Scottplotz";

    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string QuotedCommand(string exePath) => $"\"{exePath}\"";
}
