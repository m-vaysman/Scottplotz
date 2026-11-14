namespace Scottplotz.Hosting;

public static class ContentRoots
{
    public static string? Installed(string programFolder, Func<string, bool> folderExists) =>
        folderExists(Path.Combine(programFolder, "wwwroot")) ? programFolder : null;

    public static WebApplicationOptions Options(string[] args) =>
        new()
        {
            Args = args,
            ContentRootPath = Installed(AppContext.BaseDirectory, Directory.Exists)
        };
}
