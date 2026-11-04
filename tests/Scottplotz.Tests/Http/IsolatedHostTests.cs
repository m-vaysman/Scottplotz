using System.Net;
using System.Text;

namespace Scottplotz.Tests.Http;

public sealed class IsolatedHostTests
{
    [Fact]
    public async Task Ignores_the_real_user_folder_api_key_and_charting_env()
    {
        var realRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scottplotz");
        var realSettings = Path.Combine(realRoot, "appsettings.user.json");
        var previousKey = Environment.GetEnvironmentVariable("SCOTTPLOTZ_API_KEY");
        var previousMax = Environment.GetEnvironmentVariable("Charting__MaxPoints");
        var previousFile = File.Exists(realSettings) ? await File.ReadAllBytesAsync(realSettings) : null;
        var createdDirectory = !Directory.Exists(realRoot);

        try
        {
            Directory.CreateDirectory(realRoot);
            await File.WriteAllTextAsync(realSettings, """{ "Charting": { "MaxPoints": 2 } }""");
            Environment.SetEnvironmentVariable("SCOTTPLOTZ_API_KEY", "hostile-key");
            Environment.SetEnvironmentVariable("Charting__MaxPoints", "2");

            var before = Snapshot(realRoot);
            await using var factory = new TestAppFactory();
            var client = factory.CreateClient();
            var response = await client.PostAsync(
                "/chart",
                new StringContent("""{ "data": [1, 2, 3, 4] }""", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith(TestHome.Path, Scottplotz.Hosting.UserPaths.Root, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(TestHome.Path, Scottplotz.Hosting.UserPaths.LogFile, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, Snapshot(realRoot));
            Assert.DoesNotContain("hostile-key", await File.ReadAllTextAsync(Scottplotz.Hosting.UserPaths.LogFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SCOTTPLOTZ_API_KEY", previousKey);
            Environment.SetEnvironmentVariable("Charting__MaxPoints", previousMax);
            if (previousFile is null)
            {
                if (File.Exists(realSettings))
                    File.Delete(realSettings);
            }
            else
            {
                await File.WriteAllBytesAsync(realSettings, previousFile);
            }

            if (createdDirectory && Directory.Exists(realRoot) && Directory.GetFileSystemEntries(realRoot).Length == 0)
                Directory.Delete(realRoot);
        }
    }

    private static string[] Snapshot(string root) =>
        Directory.Exists(root)
            ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
}
