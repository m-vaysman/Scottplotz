using System.Text.Json;
using System.Text.RegularExpressions;
using Scottplotz.Charting;

namespace Scottplotz.Recon;

public static partial class ReconWording
{
    private static readonly string[] SideNames = ["name", "short"];
    private static readonly string[] ThemeNames = ["auto", "light", "dark"];

    public static IReadOnlyList<KeyValuePair<string, string>> DefaultLabels { get; } =
    [
        new("leftOnly", "{leftShort} only"),
        new("exact", "Exact"),
        new("tolerance", "Within tolerance"),
        new("break", "Break"),
        new("rightOnly", "{rightShort} only"),
        new("duplicate", "Duplicates"),
        new("agrees", "Agrees"),
        new("leftHigher", "{leftShort} higher"),
        new("rightHigher", "{rightShort} higher"),
        new("net", "{rightShort} minus {leftShort}"),
        new("none", "none")
    ];

    public static IReadOnlyList<KeyValuePair<string, string>> PaletteVariables { get; } =
    [
        new("left", "--left"),
        new("right", "--right"),
        new("exact", "--exact"),
        new("tolerance", "--tol"),
        new("break", "--brk"),
        new("plane", "--plane"),
        new("surface", "--surface"),
        new("ink", "--ink")
    ];

    public static Result<ReconSide> Side(JsonElement root, string name, string fallback) =>
        ReconRead.Get(root, name) switch
        {
            null => ReconResult.Ok(new ReconSide(fallback, fallback)),
            { ValueKind: JsonValueKind.String } text => ReconResult.Ok(new ReconSide(text.GetString() ?? fallback, fallback)),
            { ValueKind: JsonValueKind.Object } side =>
                from known in ReconRead.OnlyKnown(side, SideNames, name)
                from full in ReconRead.Text(side, "name", $"{name}.name")
                from brief in ReconRead.Text(side, "short", $"{name}.short")
                select new ReconSide(full ?? fallback, brief ?? fallback),
            _ => ReconRead.Fail<ReconSide>(ReconProblem.Invalid(name, "must be a name or an object with name and short"))
        };

    public static IReadOnlyList<KeyValuePair<string, string>> Tokens(
        ReconSide left,
        ReconSide right,
        string unit,
        string unitOne,
        string? asOf,
        string? measureLabel,
        string tolerance) =>
    [
        new("leftShort", left.Short),
        new("rightShort", right.Short),
        new("left", left.Name),
        new("right", right.Name),
        new("unit", unit),
        new("unitOne", unitOne),
        new("asOf", asOf ?? ""),
        new("measure", measureLabel ?? "Value"),
        new("tolerance", tolerance)
    ];

    public static string ToleranceText(ReconTolerance tolerance, int decimals)
    {
        var shortest = tolerance.Absolute.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var own = shortest.Contains(".") ? shortest.Length - shortest.IndexOf(".") - 1 : 0;
        var absolute = tolerance.Absolute.ToString("N" + Math.Max(decimals, own), System.Globalization.CultureInfo.InvariantCulture);
        var percent = tolerance.Percent.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "%";
        return tolerance.Absolute > 0 && tolerance.Percent > 0 ? $"{absolute} or {percent}"
            : tolerance.Percent > 0 ? percent
            : absolute;
    }

    public static string Fill(string template, IReadOnlyList<KeyValuePair<string, string>> tokens) =>
        Placeholder().Replace(template, match =>
            tokens.Where(token => token.Key == match.Groups[1].Value).Select(token => token.Value).FirstOrDefault() ?? match.Value);

    public static Result<IReadOnlyList<KeyValuePair<string, string>>> Labels(
        JsonElement root,
        IReadOnlyList<KeyValuePair<string, string>> tokens) =>
        Overrides(root, "labels", DefaultLabels, name => ReconProblem.UnknownLabel(name, Names(DefaultLabels)))
            .Select(labels => Filled(labels, tokens));

    public static Result<IReadOnlyList<KeyValuePair<string, string>>> Text(
        JsonElement root,
        ReconView view,
        IReadOnlyList<KeyValuePair<string, string>> tokens) =>
        Overrides(root, "text", view.Text, name => ReconProblem.UnknownText(view.Name, name, Names(view.Text)))
            .Select(text => Filled(text, tokens));

    public static Result<ReconTheme> Theme(JsonElement root) =>
        ReconRead.Text(root, "theme", "theme").Bind(text => text switch
        {
            null or "auto" => ReconResult.Ok(ReconTheme.Auto),
            "light" => ReconResult.Ok(ReconTheme.Light),
            "dark" => ReconResult.Ok(ReconTheme.Dark),
            _ => ReconRead.Fail<ReconTheme>(ReconProblem.InvalidTheme(text))
        });

    public static string ThemeToken(ReconTheme theme) => ThemeNames[(int)theme];

    public static Result<IReadOnlyList<KeyValuePair<string, string>>> Palette(JsonElement root) =>
        ReconRead.Get(root, "palette") switch
        {
            null => ReconResult.Ok<IReadOnlyList<KeyValuePair<string, string>>>([]),
            { ValueKind: JsonValueKind.Object } palette =>
                palette.EnumerateObject().Select(Colour).Collect()
                    .Select(colours => (IReadOnlyList<KeyValuePair<string, string>>)PaletteVariables
                        .SelectMany(variable => colours.Where(colour => colour.Key == variable.Key).TakeLast(1))
                        .ToList()),
            _ => ReconRead.Fail<IReadOnlyList<KeyValuePair<string, string>>>(ReconProblem.Invalid("palette", "must be an object of hex colours"))
        };

    private static Result<KeyValuePair<string, string>> Colour(JsonProperty colour) =>
        PaletteVariables.All(variable => variable.Key != colour.Name)
            ? ReconRead.Fail<KeyValuePair<string, string>>(ReconProblem.UnknownPaletteToken(colour.Name, Names(PaletteVariables)))
            : colour.Value.ValueKind == JsonValueKind.String && Hex().IsMatch(colour.Value.GetString() ?? "")
                ? ReconResult.Ok(new KeyValuePair<string, string>(colour.Name, colour.Value.GetString()!.ToLowerInvariant()))
                : ReconRead.Fail<KeyValuePair<string, string>>(ReconProblem.InvalidPaletteValue(colour.Name));

    private static Result<IReadOnlyList<KeyValuePair<string, string>>> Overrides(
        JsonElement root,
        string property,
        IReadOnlyList<KeyValuePair<string, string>> defaults,
        Func<string, ChartProblem> unknown) =>
        ReconRead.Get(root, property) switch
        {
            null => ReconResult.Ok(defaults),
            { ValueKind: JsonValueKind.Object } given =>
                given.EnumerateObject()
                    .Select(item =>
                        defaults.All(known => known.Key != item.Name) ? ReconRead.Fail<KeyValuePair<string, string>>(unknown(item.Name))
                        : item.Value.ValueKind != JsonValueKind.String ? ReconRead.Fail<KeyValuePair<string, string>>(ReconProblem.Invalid($"{property}.{item.Name}", "must be a string"))
                        : ReconResult.Ok(new KeyValuePair<string, string>(item.Name, item.Value.GetString() ?? "")))
                    .Collect()
                    .Select(changes => (IReadOnlyList<KeyValuePair<string, string>>)defaults
                        .Select(known => changes.Where(change => change.Key == known.Key).DefaultIfEmpty(known).Last())
                        .ToList()),
            _ => ReconRead.Fail<IReadOnlyList<KeyValuePair<string, string>>>(ReconProblem.Invalid(property, "must be an object of strings"))
        };

    private static IReadOnlyList<KeyValuePair<string, string>> Filled(
        IReadOnlyList<KeyValuePair<string, string>> items,
        IReadOnlyList<KeyValuePair<string, string>> tokens) =>
        items.Select(item => new KeyValuePair<string, string>(item.Key, Fill(item.Value, tokens))).ToList();

    private static string Names(IReadOnlyList<KeyValuePair<string, string>> items) =>
        items.Count == 0 ? "none" : string.Join(", ", items.Select(item => item.Key));

    [GeneratedRegex(@"\{([A-Za-z]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex Hex();
}
