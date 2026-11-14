using System.Reflection;
using System.Text.Json;

namespace Scottplotz.Recon;

public enum ReconOptionType
{
    Number,
    Integer,
    Choice,
    Text,
    Bool,
    Numbers,
    Json
}

public sealed record ReconOption(
    string Name,
    ReconOptionType Type,
    ReconValue? Default,
    double? Min,
    double? Max,
    IReadOnlyList<string> Allowed,
    string Description);

public sealed record ReconViewAssets(string Css, string Html, string Script, string Sample);

public sealed record ReconView(
    string Name,
    string Title,
    string Description,
    string Needs,
    IReadOnlyList<KeyValuePair<string, string>> Text,
    IReadOnlyList<ReconOption> Options,
    ReconViewAssets Assets);

public sealed record ReconKit(string Css, string Script);

public sealed record ReconOptionInfo(
    string Name,
    string Type,
    object? Default,
    double? Min,
    double? Max,
    IReadOnlyList<string> Allowed,
    string Description);

public sealed record ReconTextInfo(string Name, string Default);

public sealed record ReconViewInfo(
    string Name,
    string Title,
    string Description,
    string Needs,
    IReadOnlyList<ReconOptionInfo> Options,
    IReadOnlyList<ReconTextInfo> Text);

public static class ReconViews
{
    private const string ViewPrefix = "recon/views/";
    private const string KitPrefix = "recon/kit/";
    private static readonly Assembly Home = typeof(ReconViews).Assembly;

    private static readonly IReadOnlyDictionary<string, string> Resources =
        Home.GetManifestResourceNames()
            .Select(name => (Path: name.Replace('\\', '/'), Name: name))
            .Where(item => item.Path.StartsWith("recon/", StringComparison.Ordinal))
            .ToDictionary(item => item.Path, item => Read(item.Name), StringComparer.Ordinal);

    public static ReconKit Kit { get; } = new(Resources[KitPrefix + "kit.css"], Resources[KitPrefix + "kit.js"]);

    public static IReadOnlyList<ReconView> All { get; } =
        Resources.Keys
            .Where(path => path.StartsWith(ViewPrefix, StringComparison.Ordinal) && path.EndsWith("/view.json", StringComparison.Ordinal))
            .Select(path => path[ViewPrefix.Length..^"/view.json".Length])
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(Load)
            .ToList();

    public static string NameList => string.Join(", ", All.Select(view => view.Name));

    public static ReconView? Find(string name) =>
        All.FirstOrDefault(view => view.Name == name);

    public static IReadOnlyList<ReconViewInfo> Describe() =>
        All.Select(view => new ReconViewInfo(
                view.Name,
                view.Title,
                view.Description,
                view.Needs,
                view.Options.Select(option => new ReconOptionInfo(
                    option.Name,
                    TypeToken(option.Type),
                    Plain(option.Default),
                    option.Min,
                    option.Max,
                    option.Allowed,
                    option.Description)).ToList(),
                view.Text.Select(text => new ReconTextInfo(text.Key, text.Value)).ToList()))
            .ToList();

    public static string TypeToken(ReconOptionType type) =>
        type switch
        {
            ReconOptionType.Number => "number",
            ReconOptionType.Integer => "integer",
            ReconOptionType.Choice => "choice",
            ReconOptionType.Text => "text",
            ReconOptionType.Bool => "bool",
            ReconOptionType.Numbers => "numbers",
            ReconOptionType.Json => "json",
            _ => throw new InvalidOperationException("Unknown option type.")
        };

    private static object? Plain(ReconValue? value) =>
        value switch
        {
            ReconValue.Number number => number.Value,
            ReconValue.Text text => text.Value,
            ReconValue.Flag flag => flag.Value,
            ReconValue.Json json => JsonSerializer.Deserialize<JsonElement>(json.Raw),
            _ => null
        };

    private static string Read(string resource)
    {
        using var stream = Home.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing resource {resource}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n").Trim('﻿').Trim();
    }

    private static string Asset(string view, string file) =>
        Resources.TryGetValue($"{ViewPrefix}{view}/{file}", out var text)
            ? text
            : throw new InvalidOperationException($"View '{view}' is missing {file}.");

    private static ReconView Load(string name)
    {
        using var document = JsonDocument.Parse(Asset(name, "view.json"));
        var root = document.RootElement;
        var declared = root.GetProperty("name").GetString();
        if (declared != name)
            throw new InvalidOperationException($"View folder '{name}' declares the name '{declared}'. They must match.");

        return new ReconView(
            name,
            root.GetProperty("title").GetString() ?? name,
            root.GetProperty("description").GetString() ?? "",
            root.TryGetProperty("needs", out var needs) ? needs.GetString() ?? "" : "",
            root.TryGetProperty("text", out var text)
                ? text.EnumerateObject().Select(item => new KeyValuePair<string, string>(item.Name, item.Value.GetString() ?? "")).ToList()
                : [],
            root.TryGetProperty("options", out var options)
                ? options.EnumerateArray().Select(option => Option(name, option)).ToList()
                : [],
            new ReconViewAssets(Asset(name, "view.css"), Asset(name, "view.html"), Asset(name, "view.js"), Asset(name, "sample.json")));
    }

    private static ReconOption Option(string view, JsonElement option)
    {
        var name = option.GetProperty("name").GetString() ?? "";
        var type = option.GetProperty("type").GetString() switch
        {
            "number" => ReconOptionType.Number,
            "integer" => ReconOptionType.Integer,
            "choice" => ReconOptionType.Choice,
            "text" => ReconOptionType.Text,
            "bool" => ReconOptionType.Bool,
            "numbers" => ReconOptionType.Numbers,
            "json" => ReconOptionType.Json,
            var other => throw new InvalidOperationException($"View '{view}' option '{name}' has the unknown type '{other}'.")
        };
        return new ReconOption(
            name,
            type,
            option.TryGetProperty("default", out var fallback) ? Default(fallback) : null,
            option.TryGetProperty("min", out var min) ? min.GetDouble() : null,
            option.TryGetProperty("max", out var max) ? max.GetDouble() : null,
            option.TryGetProperty("allowed", out var allowed)
                ? allowed.EnumerateArray().Select(item => item.GetString() ?? "").ToList()
                : [],
            option.TryGetProperty("description", out var description) ? description.GetString() ?? "" : "");
    }

    private static ReconValue? Default(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number => new ReconValue.Number(value.GetDouble()),
            JsonValueKind.String => new ReconValue.Text(value.GetString() ?? ""),
            JsonValueKind.True => new ReconValue.Flag(true),
            JsonValueKind.False => new ReconValue.Flag(false),
            JsonValueKind.Array or JsonValueKind.Object => new ReconValue.Json(value.GetRawText()),
            _ => null
        };
}
