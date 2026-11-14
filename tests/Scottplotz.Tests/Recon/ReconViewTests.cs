using System.Text.Json;
using Scottplotz.Charting;
using Scottplotz.Recon;

namespace Scottplotz.Tests.Recon;

public sealed class ReconViewTests
{
    private static readonly string[] Forbidden =
    [
        "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "eval(", "Function(", "fetch(",
        "XMLHttpRequest", "WebSocket", "import(", "Math.random", "Date.now", "localStorage", "<script"
    ];

    public static TheoryData<string> Names() => new(ReconViews.All.Select(view => view.Name));

    private static ReconView View(string name) => ReconViews.Find(name)!;

    private static ReconModel Sample(string name) =>
        ReconParser.Parse(View(name).Assets.Sample, View(name), ReconLimits.Default) switch
        {
            Result<ReconModel>.Ok ok => ok.Value,
            Result<ReconModel>.Fail fail => throw new Xunit.Sdk.XunitException($"{name}: {fail.Problem.Code}: {fail.Problem.Detail}"),
            _ => throw new InvalidOperationException()
        };

    [Fact]
    public void The_registry_holds_the_views_in_name_order()
    {
        var names = ReconViews.All.Select(view => view.Name).ToList();

        Assert.Contains("dumbbell", names);
        Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal), names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_view_describes_itself(string name)
    {
        var view = View(name);

        Assert.Matches("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", view.Name);
        Assert.False(string.IsNullOrWhiteSpace(view.Title));
        Assert.False(string.IsNullOrWhiteSpace(view.Description));
        Assert.Contains(view.Text, item => item.Key == "title");
        Assert.All(view.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.Description)));
        Assert.Equal(view.Options.Count, view.Options.Select(option => option.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_view_sample_is_a_working_request(string name)
    {
        var model = Sample(name);

        Assert.NotEmpty(model.Records);
        Assert.Equal(name, model.ViewName);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_view_draws_only_from_the_model(string name)
    {
        var assets = View(name).Assets;
        var code = assets.Script + assets.Html + assets.Css;

        Assert.All(Forbidden, word => Assert.DoesNotContain(word, code));
        Assert.DoesNotContain("http://", assets.Script + assets.Html + assets.Css.Replace("http://www.w3.org/", ""));
        Assert.DoesNotContain("https://", code);
        Assert.DoesNotContain("url(", assets.Css);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_view_page_is_the_same_bytes_each_time(string name)
    {
        var first = PageShell.Compose(Sample(name), View(name), ReconViews.Kit);
        var second = PageShell.Compose(Sample(name), View(name), ReconViews.Kit);

        Assert.Equal(first.Html, second.Html);
        Assert.StartsWith("<!doctype html>", first.Html);
        Assert.Contains($"content=\"{first.ContentSecurityPolicy}\"", first.Html);
        Assert.Contains("script-src 'sha256-", first.ContentSecurityPolicy);
        Assert.DoesNotContain("\r", first.Html);
    }

    [Fact]
    public void Text_from_the_caller_cannot_break_out_of_the_page()
    {
        const string attack = "</script><script>alert(1)</script><img src=x onerror=alert(2)>";
        var body = JsonSerializer.Serialize(new
        {
            left = new { name = attack, @short = attack },
            text = new { title = attack },
            records = new[] { new { key = attack, name = attack, left = 1, right = 2, reason = attack, group = attack } }
        });
        var model = ReconParser.Parse(body, View("dumbbell"), ReconLimits.Default) is Result<ReconModel>.Ok ok
            ? ok.Value
            : throw new Xunit.Sdk.XunitException("The request did not parse.");

        var html = PageShell.Compose(model, View("dumbbell"), ReconViews.Kit).Html;

        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Equal(2, html.Split("</script>").Length - 1);
    }

    [Fact]
    public void Theme_and_palette_are_written_on_the_root_element()
    {
        var body = """{ "theme": "dark", "palette": { "right": "#D97706", "left": "#26e" }, "records": [ { "key": "A", "left": 1, "right": 2 } ] }""";
        var model = ((Result<ReconModel>.Ok)ReconParser.Parse(body, View("dumbbell"), ReconLimits.Default)).Value;

        var html = PageShell.Compose(model, View("dumbbell"), ReconViews.Kit).Html;

        Assert.Contains("<html lang=\"en\" data-theme=\"dark\" style=\"--left:#26e;--right:#d97706\">", html);
    }

    [Fact]
    public void The_model_json_escapes_markup_characters()
    {
        var body = """{ "records": [ { "key": "<b>&\"'", "left": 1, "right": 2 } ] }""";
        var model = ((Result<ReconModel>.Ok)ReconParser.Parse(body, View("dumbbell"), ReconLimits.Default)).Value;

        var json = ReconJson.Write(model);

        Assert.DoesNotContain("<", json);
        Assert.DoesNotContain("&", json);
        Assert.Equal("<b>&\"'", JsonDocument.Parse(json).RootElement.GetProperty("records")[0].GetProperty("key").GetString());
    }
}
