# Scottplotz

[![Build](https://github.com/m-vaysman/Scottplotz/actions/workflows/build.yml/badge.svg)](https://github.com/m-vaysman/Scottplotz/actions/workflows/build.yml)

[Download the latest installer](https://github.com/m-vaysman/Scottplotz/releases/latest/download/Scottplotz.msi) · [Releases](https://github.com/m-vaysman/Scottplotz/releases)

Scottplotz turns posted data into a PNG chart. You send numbers, columns, rows, or CSV. It picks a chart and sends the picture back. Nothing is stored.

New here? Read [HOWTO.md](HOWTO.md). The same requests are in [Scottplotz.http](Scottplotz.http).

## Run it on your machine

You need the .NET 8 SDK.

```powershell
dotnet run --project src/Scottplotz
```

It listens on http://localhost:5240. Open that address for the test page, http://localhost:5240/health for a version check, and http://localhost:5240/swagger for the API document.

## Call the API

`POST /chart` returns a PNG and sets `X-Chart-Rule` to the rule that fired. `POST /chart/explain` returns the same choice as JSON and does not draw a picture.

Send JSON as an object with `data`. Dates are ISO 8601. Numbers use a dot as the decimal separator.

```json
{ "data": [1, 3, 2, 5, 4] }
```

```json
{
  "data": {
    "date": ["2026-11-01", "2026-11-02", "2026-11-03"],
    "price": [100.5, 101.25, 99.75]
  }
}
```

```json
{
  "data": [
    { "category": "apples", "count": 4 },
    { "category": "pears", "count": 7 }
  ]
}
```

CSV uses `Content-Type: text/csv` and needs a header row. Put hints for CSV in the query string (`?type=line&title=Prices`). Put hints for JSON in the body. A hint in the query string of a JSON request is rejected.

```csv
date,price
2026-11-03,101.2
2026-11-04,102
```

Optional hints override the rules: `type` (`line`, `bar`, `scatter`, `histogram`, `timeseries`, `pie`), `title`, `xLabel`, `yLabel`, `width`, `height`, and `theme` (`light` or `dark`). The default size is 800 by 450, and the default theme is light. Pie is drawn only when you ask for it.

Copy-paste commands for curl and PowerShell are in [HOWTO.md](HOWTO.md).

## Install and uninstall

The installer is unsigned. Windows SmartScreen shows a warning. Choose **More info**, then **Run anyway**.

The wizard asks you to accept the info page, pick a folder (the default is `C:\Program Files\Scottplotz`), and whether to start with Windows (on by default). It adds a Start menu shortcut and a firewall rule that allows TCP port 5240 from the local subnet only.

Silent install:

```text
msiexec /i Scottplotz.msi /qn INSTALLFOLDER="G:\Apps\Scottplotz"
```

Start **Scottplotz** from the Start menu. The tray menu opens the test page, opens the logs folder, toggles start-with-Windows, and quits. If port 5240 is already taken, a balloon says so.

Uninstall from **Settings > Apps**. That removes the program, the shortcut, the startup entry, and the firewall rule. Logs and settings in `%LOCALAPPDATA%\Scottplotz` stay.

## Add a rule

Each rule is one pure function. The engine in `src/Scottplotz/Rules/ChartRuleEngine.cs` runs them in order and keeps the first confident match.

1. Add `src/Scottplotz/Rules/YourRule.cs` with `public static RuleMatch? Match(ChartTable table)`. Return `null` when the rule does not apply. Return `new RuleMatch("your-rule", confident: true, ChartKind.Line)` when it does.
2. Put `new OrderedRule(YourRule.Name, YourRule.Match)` in `ChartRuleEngine.Ordered` where it should be considered.
3. Add a test class under `tests/Scottplotz.Tests/Rules` with one case that matches and one that does not.

A caller hint still wins over every rule.

## Configuration

`appsettings.json` next to the program sets the limits and the port. `%LOCALAPPDATA%\Scottplotz\appsettings.user.json` overrides that file. Environment variables override both. Logs are written to `%LOCALAPPDATA%\Scottplotz\logs`.

The service binds on port 5240. The firewall rule is what keeps it on the local subnet. A Cloudflare tunnel is not set up. If you put the port on the internet, set the `SCOTTPLOTZ_API_KEY` environment variable first. Clients then send that value in the `X-Api-Key` header. The key is never written into the program or the log. Health, Swagger, and the test page stay open.

## Regenerate the chart hashes

`ChartRendererTests.Matches_the_golden_hashes` compares SHA-256 of six small PNGs. Those hashes are captured on Windows. The test skips elsewhere, because ScottPlot lays out text differently on Linux.

When the test fails, it writes the actual PNGs and `hashes.txt` to `TestResults/golden`. CI uploads that folder as the `golden-mismatches` artifact. Open the PNGs and review them before you change the hashes. If the pictures are the ones you meant to ship, copy the lines from `hashes.txt` into the test. A hash change without looking at the images hides a rendering bug.
