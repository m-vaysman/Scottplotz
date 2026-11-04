using System.Runtime.CompilerServices;

namespace Scottplotz.Tests;

internal static class TestHome
{
    internal static string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "scottplotz-tests",
        Guid.NewGuid().ToString("N"));

    [ModuleInitializer]
    internal static void Isolate()
    {
        Directory.CreateDirectory(Path);
        Environment.SetEnvironmentVariable(global::Scottplotz.Hosting.UserPaths.HomeVariable, Path);
    }
}
