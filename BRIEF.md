# Scottplotz — build brief (run without asking questions)

Build a small REST service, installed like a normal Windows program (MSI), whose entire job is to produce charts: you send it data, it sends back a PNG.
Make reasonable decisions on your own and note anything you assumed.

## Stack
- ASP.NET Core minimal API (.NET 8+) with **ScottPlot 5** (pin the 5.x version). All C#, readable and maintainable.
- One or two users only. Stateless: throw data at it, get a sensible chart with no required options.
- **Code style: follow the `functional-csharp` skill.** Pure functions, immutable records, LINQ, side effects (HTTP, files, clock, logging) only at the edges. **Each rule is a pure function.**

## API
- `POST /chart` -> `image/png`. Response header `X-Chart-Rule` names the rule that fired.
- `POST /chart/explain` -> JSON: detected data shape, the rule that fired, the final chart settings (no image).
- `GET /health` -> 200 with version.
- Swagger/OpenAPI enabled. Include a `.http` file with sample requests for every input format.

### Input formats (JSON body, `data` field, auto-detected)
1. Plain number array: `[1, 2, 3]`
2. Named columns: `{ "date": [...], "price": [...], "volume": [...] }`
3. Rows of objects: `[{ "date": "2026-11-03", "price": 101.2 }, ...]`
4. Also accept `text/csv` (header row required) on the same endpoint.
Dates are ISO 8601. Numbers parse with invariant culture.

### Hints (optional `hints` object; always override the rules)
- `type` (line | bar | scatter | histogram | timeseries | pie), `title`, `xLabel`, `yLabel`, `width`, `height`, `theme` (light | dark).
- Defaults: 800x450, light theme.
- **Unknown hint names or invalid values are rejected** with a 400 that says which hint and what's allowed.

### Limits and errors
- Max request body 5 MB; max 100,000 points total; width/height 100–4000 px. All configurable in appsettings.
- Return 400 ProblemDetails (RFC 7807) with a clear message for: malformed JSON/CSV, empty data, mismatched column lengths, non-numeric values, nulls/NaN/Infinity (state the policy: reject by default, configurable to drop), limits exceeded.

## Chart selection: rules, one small rule per file
- Each rule is its own pure function in its own file (e.g. `Rules/PlainNumbersLineRule.cs`). A rule engine runs the collection in order; first confident match wins. No if-chains in one file.
- Default rules:
  - Plain list of numbers -> line chart.
  - Lots of numbers where order doesn't matter -> histogram.
  - Dates plus numeric columns -> time-series.
  - Categories (text) plus numbers -> bar chart.
  - Two numeric columns -> scatter plot.
  - Several numeric series sharing an x -> multi-line chart.
  - **Pie only when the hint asks for it.**
- Caller hints always override the rules.

## Determinism: same input, same PNG
- Bundle a fixed font with the app (embedded resource) and use it for all text; never rely on system fonts.
- Invariant culture everywhere (parsing and labels). No timestamps, randomness, or machine-specific values in output.
- Tests prove it: golden-image or SHA-256 hash tests for a set of sample inputs, plus a test that renders the same input twice and compares bytes.

## Small model: optional, not for shape detection
- Do NOT use a small (~1B) model to read the data's shape. Rules are faster, deterministic and debuggable; a model could pick a pie chart for time-series and break "same input, same PNG".
- Leave a clean, optional hook (interface, off by default) where a small model could help with meaning rules can't see, e.g. column names. Only call it when rules are unsure.

## Performance
- ScottPlot renders a PNG in a few ms to ~50 ms; dozens of charts/sec on modest hardware. No caching or queuing needed. Only the optional model could slow it down.

## Tests (xUnit)
- One test class per rule (matching and non-matching inputs).
- Input parsing for all four formats, hint validation, every error case and limit.
- Determinism/golden tests as above. Endpoint tests with `WebApplicationFactory`.

## Installation: a normal Windows program (MSI installer), not a service
- Do **not** run it as a Windows service. Ship it as an installable Windows program: an **MSI built with WiX Toolset v5** (SDK-style `.wixproj` in the solution, built by `dotnet build`), from a **self-contained win-x64** publish so the target machine needs no .NET runtime.
- Normal install and uninstall: an install wizard with a license/info page and a **choosable install folder** (default `C:\Program Files\Scottplotz`; on COBBLER1 install to `G:\Apps\Scottplotz`, never C:), an entry in Settings > Apps / Programs and Features with a clean uninstall (removes files, shortcuts, the startup entry and the firewall rule; keeps user logs and settings), and in-place upgrades (`MajorUpgrade`, version from the build). Silent install works too: `msiexec /i Scottplotz.msi /qn INSTALLFOLDER="G:\Apps\Scottplotz"`.
- How it starts: a small **system-tray app** (`Scottplotz.exe`) hosts the API in-process. A **Start Menu shortcut** launches it, and an installer checkbox (on by default) adds **Start with Windows** for the current user (HKCU Run). Single instance only. The tray menu has Open test page (opens `http://localhost:5240/` in the default browser), Open logs folder, Start with Windows (toggle), and Quit. A tray balloon shows if the port is already in use.
- Fixed port **5240** (configurable), HTTP.
- **LAN-only by default:** the installer adds a Windows Firewall rule for TCP 5240 scoped to the local subnet only. Optional exposure through the existing Cloudflare tunnel is documented but off; when exposed, require a simple API key (`X-Api-Key` header, key from an environment variable, never hardcoded or logged).
- No hardcoded paths; config via appsettings next to the exe plus user overrides and environment variables. Logs and user settings go under `%LOCALAPPDATA%\Scottplotz` (structured file logging), not the install folder.
- The MSI is unsigned (document the SmartScreen "More info > Run anyway" step). The MSI is built and released by CI (see Build automation below).

## Deliverables
- Solution with the API project and the test project.
- README: run locally, call the API (all formats), install/uninstall with the MSI, add a new rule.

## Browser test page
- Serve one minimal static page at `GET /` (`wwwroot/index.html`, plain HTML + vanilla JS, no frameworks or build step, no external CDNs).
- It lets me: paste data in a textarea (with buttons that load a sample for each input format: number array, columns, rows, CSV); pick hints (type dropdown incl. "auto", title, x/y labels, width, height, theme); click **Render** to POST to `/chart` and show the returned PNG plus the `X-Chart-Rule` header; click **Explain** to POST to `/chart/explain` and show the JSON pretty-printed; show 400 ProblemDetails errors clearly.
- Optional API key field (sent as `X-Api-Key`) for when the service is exposed.
- Keep it minimal and functional. Include it in the finish-line verification (the page loads with 200).

## How-to guide (HOWTO.md)
- Add `HOWTO.md` at the repo root, linked from the README, written for someone who has never seen the project. Plain language, short sections, copy-pasteable examples.
- Cover: what Scottplotz does and how it picks a chart; where it runs (base URL, port 5240) and the optional `X-Api-Key`; every endpoint (`POST /chart`, `POST /chart/explain`, `GET /health`, `GET /` test page, Swagger) with what it returns; each input format (number array, named columns, rows of objects, CSV) with a small example; every hint with allowed values and defaults; the limits; and what errors look like (400 ProblemDetails) with a fix for the common ones.
- Example requests that really work against the running service: curl (bash) and PowerShell `Invoke-WebRequest` that save a PNG, one per input format, one with hints, one explain call, and one deliberate error. Point to the `.http` file.
- A short walkthrough of the HTML test page: open it, load a sample or paste data, pick hints, Render, read the chosen rule, Explain.
- Keep examples in sync with the code; check them during the finish-line verification.

## Source control: public GitHub repo
- Version this project as a **public** GitHub repo (not private): https://github.com/m-vaysman/Scottplotz
- Steps: `git init -b master` in I:\Projects\Scottplotz, add a .NET `.gitignore` (exclude bin/, obj/, publish output, logs, local settings), commit, then create and push with `gh repo create m-vaysman/Scottplotz --public --source . --remote origin --push`. If `gh` isn't available, create the public repo on github.com under m-vaysman, then `git remote add origin https://github.com/m-vaysman/Scottplotz.git` and `git push -u origin master`.
- Never commit secrets (API keys, tokens, connection strings); the X-Api-Key comes from an environment variable only.
- Commit as you go and push the final working state. All earlier deployment and finish-line instructions still apply; the final report must include the repo URL and the last pushed commit.

## Build automation and latest-installer download (GitHub Actions)
- Add `.github/workflows/build.yml` running on `windows-latest`. On every pull request: restore, build, and run all tests (block merging on failure). On every push/merge to `master`: build, test, publish self-contained win-x64, build the MSI, and publish it as a **GitHub Release** asset.
- Versioning: `1.0.<run_number>` stamped into the assembly and the MSI (so upgrades work); tag the release `v1.0.<run_number>` and mark it as latest. Use the built-in `GITHUB_TOKEN` with `permissions: contents: write`; no personal tokens or other secrets.
- Always upload the asset with the same file name, `Scottplotz.msi`, so this link always serves the newest build: https://github.com/m-vaysman/Scottplotz/releases/latest/download/Scottplotz.msi
- README, near the top: a **Download the latest installer** link to that URL, a link to the Releases page, and a build-status badge for the workflow. HOWTO.md points to the same link.
- Releases are produced only by CI, never uploaded by hand.
## Workflow: one issue, one branch, one merge per step
- Do not make one giant commit. Break the work into ordered steps (for example: solution skeleton and health endpoint; input parsing; hints and validation; rule engine and rules; rendering and determinism; explain endpoint; browser test page; tests; MSI installer and tray app; GitHub Actions build and release; README and HOWTO.md).
- For each step, in order, one at a time:
  1. Open a GitHub issue in https://github.com/m-vaysman/Scottplotz describing the step (`gh issue create`).
  2. Create a feature branch from up-to-date `master` named for the issue, e.g. `feature/<issue#>-short-name`.
  3. Implement the step on that branch with small, focused commits that reference the issue (e.g. `#3`). Build and run the tests before finishing the step.
  4. Push the branch, merge it into `master` (a pull request that closes the issue, merged with `gh pr merge`, is preferred), push `master`, and make sure the issue is closed.
  5. Only then start the next step.
- The default branch is `master`. Don't start the next step until the current one is merged.

## Finish line: build, run, verify, then report
Do not ask me questions at any point. After implementing everything above:
1. Build the solution and run all tests; fix failures until green.
2. Build the MSI, install it on this desktop with `msiexec` (silent, admin), and confirm Scottplotz starts from the Start Menu shortcut and shows the tray icon, listening on port 5240. After the final merge to `master`, confirm the GitHub Actions run is green, a new release exists, and the README's latest-installer link downloads `Scottplotz.msi`. Then give me the exact one-line `msiexec` command to install it on COBBLER1 into `G:\Apps\Scottplotz`. If installing is blocked by permissions, run the published exe directly on port 5240 to verify, and say what was blocked.
3. Verify it works with quick test calls: `GET /health` returns 200, and `POST /chart` with a small number array returns a valid PNG (check the PNG signature and the `X-Chart-Rule` header).
4. Then notify me that it's done: summarize what you built, how to test it, where it's running, and anything you assumed or are unsure about. Then stop.

