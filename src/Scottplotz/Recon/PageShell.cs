using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Scottplotz.Recon;

public sealed record ReconPage(string Html, string ContentSecurityPolicy, string ModelJson);

public static class PageShell
{
    public static ReconPage Compose(ReconModel model, ReconView view, ReconKit kit)
    {
        var json = ReconJson.Write(model);
        var script = $"\n{kit.Script}\n{{\n{view.Assets.Script}\n}}\n";
        var policy = Policy(script);
        var html = string.Concat(
            "<!doctype html>\n",
            $"<html lang=\"en\"{ThemeAttribute(model.Theme)}{PaletteAttribute(model.Palette)}>\n",
            "<head>\n",
            "<meta charset=\"utf-8\">\n",
            $"<meta http-equiv=\"Content-Security-Policy\" content=\"{policy}\">\n",
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n",
            $"<title>{Encode(Title(model))}</title>\n",
            $"<style>\n{kit.Css}\n{view.Assets.Css}\n</style>\n",
            "</head>\n",
            "<body>\n",
            $"<div class=\"label\"><strong>{Encode(model.ViewTitle)}</strong><span class=\"bar\"></span><span>{Encode(model.Left.Name)}</span><span class=\"bar\"></span><span>{Encode(model.Right.Name)}</span><span class=\"num\">{Encode(model.AsOf ?? "")}</span></div>\n",
            $"<main>\n{view.Assets.Html}\n</main>\n",
            $"<script id=\"rd-model\" type=\"application/json\">{json}</script>\n",
            $"<script>{script}</script>\n",
            "</body>\n",
            "</html>\n");
        return new ReconPage(html, policy, json);
    }

    public static string Policy(string script) =>
        $"default-src 'none'; script-src 'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'; style-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'";

    private static string Title(ReconModel model) =>
        model.Text.Where(item => item.Key == "title").Select(item => item.Value).FirstOrDefault() ?? model.ViewTitle;

    private static string ThemeAttribute(ReconTheme theme) =>
        theme == ReconTheme.Auto ? "" : $" data-theme=\"{ReconWording.ThemeToken(theme)}\"";

    private static string PaletteAttribute(IReadOnlyList<KeyValuePair<string, string>> palette) =>
        palette.Count == 0
            ? ""
            : $" style=\"{string.Join(";", palette.Select(colour => $"{Variable(colour.Key)}:{colour.Value}"))}\"";

    private static string Variable(string token) =>
        ReconWording.PaletteVariables.First(variable => variable.Key == token).Value;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
