namespace Scottplotz.Rendering;

public static class EmbeddedFont
{
    public const string FamilyName = "Noto Sans";

    private static readonly object Gate = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
                return;

            var regular = Extract("Scottplotz.Fonts.NotoSans-Regular.ttf", "NotoSans-Regular.ttf");
            var bold = Extract("Scottplotz.Fonts.NotoSans-Bold.ttf", "NotoSans-Bold.ttf");
            ScottPlot.Fonts.AddFontFile(FamilyName, regular, bold: false, italic: false);
            ScottPlot.Fonts.AddFontFile(FamilyName, bold, bold: true, italic: false);
            ScottPlot.Fonts.Default = FamilyName;
            _registered = true;
        }
    }

    private static string Extract(string resourceName, string fileName)
    {
        var directory = Path.Combine(Hosting.UserPaths.Root, "fonts");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        var bytes = Read(resourceName);
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            File.WriteAllBytes(path, bytes);

        return path;
    }

    private static byte[] Read(string resourceName)
    {
        var assembly = typeof(EmbeddedFont).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            var found = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException($"Missing embedded font '{resourceName}'. Found: {found}");
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
