# How to use Scottplotz

Scottplotz draws a chart from data you post. It listens on your machine, picks a chart type, and returns a PNG. It does not keep the data.

The same samples are in [Scottplotz.http](Scottplotz.http). Installers are on the [releases page](https://github.com/m-vaysman/Scottplotz/releases). [Download the latest installer](https://github.com/m-vaysman/Scottplotz/releases/latest/download/Scottplotz.msi).

## How it picks a chart

It looks at the columns, in this order, and stops at the first confident match:

| What you sent | Chart |
| --- | --- |
| Dates and numbers | Time series |
| Text labels and numbers | Bar |
| Exactly two numeric columns | Scatter |
| Three or more numeric columns | Several lines |
| One long numeric column that is not in order | Histogram |
| A plain list of numbers | Line |

A plain list of numbers is always a line, even when the values jump around. A pie chart is drawn only when you set the hint `type` to `pie`. Any hint overrides these rules. When the rules are not sure, a small built-in model picks the type. `X-Chart-Type-Source` says which one decided: `rules`, `model`, or `user`.

Leave `type` out and Scottplotz chooses. Set `type` to an id from `GET /chart-types` when you want that chart. `Auto` on the test page means no type.

## Where it runs

Base URL: `http://localhost:5240`

The port is 5240 unless `Charting:Port` is changed. The firewall rule allows that port from the local subnet only.

If the environment variable `SCOTTPLOTZ_API_KEY` is set, `POST /chart`, `POST /chart/explain`, and `/mcp` require the same value in the `X-Api-Key` header. `GET /chart-types` stays open so the test page can list types. Leave the variable unset and those calls need no key. The key is not stored in the program and is not written to the log.

## Endpoints

| Request | Result |
| --- | --- |
| `GET /health` | `200` and a small JSON object with `status` and `version` |
| `GET /chart-types` | `200` and JSON for every chart type: id, name, description, data shape, parameters, and an example body |
| `POST /chart` | `200` and a PNG. The header `X-Chart-Rule` names the rule that fired. `X-Chart-Type-Source` is `rules`, `model`, or `user` |
| `POST /chart/explain` | `200` and JSON: the detected shape, the rule, and the final settings. No image |
| `GET /` | The test page |
| `GET /swagger` | The API document |
| `POST /mcp` | The MCP endpoint. Tools: `list_chart_types`, `suggest_chart_type`, `render_chart` |

A bad request is `400` with a problem document. A missing or wrong API key is `401`.

## Chat tools (MCP)

The same process serves MCP at `http://localhost:5240/mcp`. `list_chart_types` matches `GET /chart-types`. `suggest_chart_type` ranks types and does not draw. `render_chart` returns the PNG from the same render call as `POST /chart`.

Claude Desktop, in `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "scottplotz": {
      "url": "http://localhost:5240/mcp"
    }
  }
}
```

Cursor, in `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "scottplotz": {
      "url": "http://localhost:5240/mcp"
    }
  }
}
```

Use the machine's address instead of `localhost` when the client is on another computer on the LAN. If `SCOTTPLOTZ_API_KEY` is set, send it as the `X-Api-Key` header.

## Input formats

JSON is an object with `data` and, if you want them, `hints`. Dates look like `2026-11-03` or `2026-11-03T00:00:00Z`. Numbers use a dot, such as `101.2`.

### Number array

```json
{ "data": [1, 3, 2, 5, 4] }
```

### Named columns

```json
{
  "data": {
    "date": ["2026-11-01", "2026-11-02", "2026-11-03"],
    "price": [100.5, 101.25, 99.75],
    "volume": [1200, 1500, 1100]
  }
}
```

Every column must be the same length.

### Rows of objects

```json
{
  "data": [
    { "category": "apples", "count": 4 },
    { "category": "pears", "count": 7 },
    { "category": "oranges", "count": 2 }
  ]
}
```

### CSV

`Content-Type` is `text/csv`. The first row is the header and is required.

```csv
date,price
2026-11-03,101.2
2026-11-04,102
```

For CSV, hints go in the query string, not the body. For JSON, hints go in the body. A hint query on a JSON request is rejected.

## Hints

| Hint | Allowed values | Default |
| --- | --- | --- |
| `type` | `line`, `bar`, `scatter`, `histogram`, `timeseries`, `pie` | chosen from the data |
| `title` | text, up to 500 characters | none |
| `xLabel` | text, up to 500 characters | none |
| `yLabel` | text, up to 500 characters | none |
| `width` | integer from 100 to 4000 | 800 |
| `height` | integer from 100 to 4000 | 450 |
| `theme` | `light`, `dark` | `light` |

Names and values are lowercase. `type` must be one of the tokens above. An unknown name, such as `colour`, is a 400 that names the hint and the names that are allowed.

```json
{
  "data": [1, 3, 2, 5, 4],
  "hints": { "type": "bar", "title": "Numbers", "theme": "dark" }
}
```

## Limits

These live in the `Charting` section of `appsettings.json`:

| Setting | Default |
| --- | --- |
| Request body | 5 MB (`5242880` bytes) |
| Points | 100,000 cells |
| Width and height | 100 to 4000 |

Null, `NaN`, and `Infinity` are rejected. To drop those rows instead, set `InvalidNumberPolicy` to `Drop`. A number written with a comma, such as `1,5`, is not a number. Mixed text and numbers in one column are rejected either way.

## When a request fails

The body is a problem document. `code` is a short name, `title` is a summary, and `detail` says what to change.

| `code` | What to do |
| --- | --- |
| `malformed-json` | Send one JSON object. Check quotes and commas |
| `malformed-csv` | Check quotes. A quote inside a field is written twice |
| `missing-data` | JSON needs a `data` property |
| `empty-data` | Send at least one value |
| `mismatched-columns` | Make every column the same length |
| `non-numeric` | Use a number, or quote the text so the column stays text |
| `null-value` | Remove the null, or set `InvalidNumberPolicy` to `Drop` |
| `non-finite` | Remove `NaN` or `Infinity`, or set `InvalidNumberPolicy` to `Drop` |
| `unknown-hint` | Use a hint name from the table above |
| `invalid-hint` | Use an allowed value. Width and height are whole numbers |
| `hint-in-query` | Put JSON hints in the body. Query hints are for CSV |
| `too-many-points` | Send fewer cells, or raise `MaxPoints` |
| `body-too-large` | Send a smaller body, or raise `MaxRequestBodyBytes` |
| `incompatible-hint` | That `type` does not fit this data. Pie needs one numeric series of zero or greater |
| `no-rule` | The shape matched nothing. Set a `type` hint |
| `unauthorized` | Send the `X-Api-Key` header, or unset `SCOTTPLOTZ_API_KEY` |

## curl

These are bash commands. They assume Scottplotz is already running.

Save a PNG for each format:

```bash
curl -sS -D - -o numbers.png http://localhost:5240/chart \
  -H "Content-Type: application/json" \
  -d '{"data":[1,3,2,5,4]}'

curl -sS -D - -o columns.png http://localhost:5240/chart \
  -H "Content-Type: application/json" \
  -d '{"data":{"date":["2026-11-01","2026-11-02","2026-11-03"],"price":[100.5,101.25,99.75],"volume":[1200,1500,1100]}}'

curl -sS -D - -o rows.png http://localhost:5240/chart \
  -H "Content-Type: application/json" \
  -d '{"data":[{"category":"apples","count":4},{"category":"pears","count":7},{"category":"oranges","count":2}]}'

curl -sS -D - -o prices.png http://localhost:5240/chart \
  -H "Content-Type: text/csv" \
  --data-binary $'date,price\n2026-11-03,101.2\n2026-11-04,102\n'
```

The response headers include `X-Chart-Rule`.

One request with hints:

```bash
curl -sS -D - -o bars.png http://localhost:5240/chart \
  -H "Content-Type: application/json" \
  -d '{"data":[1,3,2,5,4],"hints":{"type":"bar","title":"Numbers","theme":"dark"}}'
```

Explain, without an image:

```bash
curl -sS http://localhost:5240/chart/explain \
  -H "Content-Type: application/json" \
  -d '{"data":[1,3,2,5,4]}'
```

A deliberate error (`colour` is not a hint):

```bash
curl -sS http://localhost:5240/chart \
  -H "Content-Type: application/json" \
  -d '{"data":[1,2,3],"hints":{"colour":"red"}}'
```

That returns 400 and `"code": "unknown-hint"`.

## PowerShell

These use `Invoke-WebRequest`. `-OutFile` writes the PNG bytes unchanged.

```powershell
Invoke-WebRequest http://localhost:5240/chart -Method POST -ContentType "application/json" -Body '{"data":[1,3,2,5,4]}' -OutFile numbers.png

Invoke-WebRequest http://localhost:5240/chart -Method POST -ContentType "application/json" -Body '{"data":{"date":["2026-11-01","2026-11-02","2026-11-03"],"price":[100.5,101.25,99.75],"volume":[1200,1500,1100]}}' -OutFile columns.png

Invoke-WebRequest http://localhost:5240/chart -Method POST -ContentType "application/json" -Body '{"data":[{"category":"apples","count":4},{"category":"pears","count":7},{"category":"oranges","count":2}]}' -OutFile rows.png

Invoke-WebRequest http://localhost:5240/chart -Method POST -ContentType "text/csv" -Body "date,price`n2026-11-03,101.2`n2026-11-04,102`n" -OutFile prices.png
```

Read the rule while saving the file:

```powershell
$response = Invoke-WebRequest http://localhost:5240/chart -Method POST -ContentType "application/json" -Body '{"data":[1,3,2,5,4],"hints":{"type":"bar","title":"Numbers","theme":"dark"}}' -OutFile bars.png -PassThru
$response.Headers["X-Chart-Rule"]
```

Explain:

```powershell
(Invoke-WebRequest http://localhost:5240/chart/explain -Method POST -ContentType "application/json" -Body '{"data":[1,3,2,5,4]}').Content
```

A deliberate error:

```powershell
try {
  Invoke-WebRequest http://localhost:5240/chart -Method POST -ContentType "application/json" -Body '{"data":[1,2,3],"hints":{"colour":"red"}}'
} catch {
  $stream = $_.Exception.Response.GetResponseStream()
  $reader = New-Object System.IO.StreamReader($stream)
  $reader.ReadToEnd()
}
```

## Test page

1. Open http://localhost:5240/.
2. Click **Numbers**, **Columns**, **Rows**, or **CSV** to load a sample, or paste your own data.
3. Set a chart type, title, labels, size, or theme if you want to override the choice. Leave the type on **Auto** to let the rules decide.
4. Click **Render**. The picture appears, with the rule name above it.
5. Click **Explain**. The page shows the JSON: the shape, the rule, and the settings.
6. A bad request replaces the picture with the status, the `code`, and the detail. Empty data (`{ "data": [] }`) is an easy one to try.
7. Fill **API key** only when the server has `SCOTTPLOTZ_API_KEY` set. It is sent as `X-Api-Key`.

## Reconciliation views

Scottplotz also draws a two-sided reconciliation: records from a left source compared with records from a right source. You post the records and get back one HTML page. The page is self-contained, works offline, and can be saved or opened in any browser. It does not use ScottPlot.

### Endpoints

| Request | Result |
| --- | --- |
| `GET /recon/views` | `200` and JSON for every view: name, title, description, what the data needs, options, and text keys |
| `GET /recon/{view}/sample` | `200` and a request body that works with that view |
| `POST /recon/{view}` | `200` and an HTML page |
| `POST /recon/{view}/explain` | `200` and JSON: the request after validation, with statuses, totals, labels, text and options resolved. No page |
| `GET /recon.html` | The gallery page for trying views |

An unknown view is `400` and lists the views. When `SCOTTPLOTZ_API_KEY` is set, the two `POST` routes need `X-Api-Key`; the `GET` routes stay open.

Every view is listed, with its options, in [docs/recon-views.md](docs/recon-views.md).

### The smallest request

Only `records` is required. Each record needs a `key` and at least one side.

```json
{
  "records": [
    { "key": "A-1", "left": 100, "right": 100 },
    { "key": "A-2", "left": 250, "right": 262.5 },
    { "key": "A-3", "left": 80 },
    { "key": "A-4", "right": 45 }
  ]
}
```

Scottplotz works out each record's status from the two sides:

| Status | Meaning |
| --- | --- |
| `exact` | Both sides are present and equal |
| `tolerance` | They differ, within the tolerance |
| `break` | They differ by more than the tolerance |
| `leftOnly` | Only the left side is present |
| `rightOnly` | Only the right side is present |

### A fuller request

```json
{
  "left":  { "name": "Purchase order lines", "short": "Ordered" },
  "right": { "name": "Supplier invoice lines", "short": "Invoiced" },
  "unit": { "one": "line", "many": "lines" },
  "asOf": "2026-03-31",
  "tolerance": 2,
  "fields": [
    { "key": "qty", "label": "Quantity", "decimals": 0, "compare": false },
    { "key": "ext", "label": "Extended amount", "decimals": 2 }
  ],
  "measure": "ext",
  "records": [
    {
      "key": "PO-5512-2", "name": "Corner boards", "group": "Tidewell Supply",
      "left": { "qty": 400, "ext": 1240 }, "right": { "qty": 400, "ext": 1380 },
      "reason": "Invoiced at 3.45 each. The order says 3.10."
    },
    { "key": "PO-5530-2", "name": "Label rolls", "group": "Tidewell Supply", "left": { "qty": 150, "ext": 1860 } }
  ],
  "text": { "title": "Purchase orders against supplier invoices" },
  "labels": { "break": "Dispute" },
  "options": { "sortBy": "difference" },
  "theme": "dark",
  "palette": { "left": "#2563eb", "right": "#d97706" }
}
```

| Property | What it is | Default |
| --- | --- | --- |
| `left`, `right` | A name, or `{ "name", "short" }`. `short` is the word used in phrases such as "Ordered only" | `Left`, `Right` |
| `unit` | The noun for a record: a word, or `{ "one", "many" }` | `record`, `records` |
| `asOf` | An ISO date shown on the page and used for ages | none |
| `tolerance` | A number (absolute), or `{ "absolute", "percent" }`. A record is within tolerance when either holds | `0` |
| `fields` | The values a side carries: a key, or `{ "key", "label", "type", "decimals", "compare", "tolerance" }`. `type` is `number`, `text` or `date`. A date field's tolerance is in days. `compare: false` shows the field without comparing it | worked out from the records |
| `measure` | The number field used for totals and differences | the first number field |
| `records` | See below | required |
| `groups` | `{ "key", "name", "note", "left", "right", "attrs" }` per group: sets the order, adds a note, and can carry control totals | none |
| `totals` | `{ "left", "right" }`, when the control totals are not the sum of the records sent | summed |
| `text` | Replaces a view's wording. Keys differ by view; `GET /recon/views` lists them | the view's defaults |
| `labels` | Replaces a status or phrase label: `leftOnly`, `exact`, `tolerance`, `break`, `rightOnly`, `duplicate`, `agrees`, `leftHigher`, `rightHigher`, `net`, `none` | built from the side names |
| `options` | Settings for the view | the view's defaults |
| `theme` | `auto` (follows the reader's system), `light` or `dark` | `auto` |
| `palette` | Hex colours for `left`, `right`, `exact`, `tolerance`, `break`, `plane`, `surface`, `ink` | the built-in colours |

A record:

| Property | What it is |
| --- | --- |
| `key` | Required. A string or number |
| `name` | A display name. Defaults to the key |
| `left`, `right` | A bare number (the measure), an object of field values, or an array of lines `{ "key", "name", ...fields }` whose numbers are added up |
| `status` | Send it to override the worked-out status, for example when the rule is not about amounts |
| `count` | One row that stands for this many records. With only a `status`, it is a bulk row such as "1,182 exact" |
| `group` | A name, or a path such as `["Equities", "Tech"]` |
| `date` | An ISO date or date and time |
| `reason` | Why the record differs |
| `duplicate` | `true`, `"left"` or `"right"` |
| `attrs` | Any other single values, such as `{ "owner": "Ops", "age": 12 }` |

Text and labels may use `{left}`, `{right}`, `{leftShort}`, `{rightShort}`, `{unit}`, `{unitOne}`, `{measure}`, `{asOf}` and `{tolerance}`.

### Reconciliation limits

| Limit | Default | Setting |
| --- | --- | --- |
| Request body | 5 MB | `Charting:MaxRequestBodyBytes` |
| Records | 20,000 | `Recon:MaxRecords` |
| Fields | 40 | `Recon:MaxFields` |

### When a reconciliation request fails

The body is the same problem document. Unknown names are rejected, and the detail lists what is allowed.

| `code` | What to do |
| --- | --- |
| `unknown-view` | Use a name from `GET /recon/views` |
| `malformed-json`, `invalid-recon` | Send one JSON object with a `records` array |
| `missing-records`, `empty-records` | Send at least one record |
| `unknown-property` | Remove the property, or check its spelling against the tables above |
| `invalid-value` | The detail names the path, such as `records[3].left.qty`, and what it must be |
| `missing-sides` | Give the record a `left`, a `right`, or a `status` |
| `status-mismatch` | The `status` contradicts the sides, such as `leftOnly` on a record with a right side |
| `unknown-field`, `duplicate-field`, `unknown-measure` | Declare the field once in `fields`, and use a number field as the measure |
| `unknown-label`, `unknown-text`, `unknown-option` | Use a name the detail lists |
| `invalid-option`, `invalid-theme`, `invalid-palette` | Use an allowed value. Palette colours are hex, such as `#2563eb` |
| `too-many-records`, `too-many-fields` | Send fewer, or raise the limit |

### Reconciliation with curl

```bash
curl -sS http://localhost:5240/recon/views

curl -sS -o sample.json http://localhost:5240/recon/dumbbell/sample

curl -sS -o dumbbell.html http://localhost:5240/recon/dumbbell \
  -H "Content-Type: application/json" \
  --data-binary @sample.json

curl -sS -o small.html http://localhost:5240/recon/dumbbell \
  -H "Content-Type: application/json" \
  -d '{"records":[{"key":"A-1","left":100,"right":100},{"key":"A-2","left":250,"right":262.5},{"key":"A-3","left":80},{"key":"A-4","right":45}]}'

curl -sS http://localhost:5240/recon/dumbbell/explain \
  -H "Content-Type: application/json" \
  -d '{"tolerance":{"percent":5},"records":[{"key":"A-2","left":250,"right":262.5}]}'

curl -sS http://localhost:5240/recon/dumbbell \
  -H "Content-Type: application/json" \
  -d '{"records":[{"key":"A-1","left":100}],"options":{"sortBy":"size"}}'
```

The last call fails on purpose: `sortBy` must be `input` or `difference`.

### Reconciliation with PowerShell

```powershell
Invoke-WebRequest http://localhost:5240/recon/dumbbell/sample -OutFile sample.json

Invoke-WebRequest http://localhost:5240/recon/dumbbell -Method POST -ContentType "application/json" -InFile sample.json -OutFile dumbbell.html

Start-Process dumbbell.html
```

### Gallery page

1. Open http://localhost:5240/recon.html. The main page links to it.
2. Every view is a card with a live preview, drawn by the service from that view's sample request. A preview is drawn when you scroll to it.
3. Type in **Filter views** to narrow the cards. Set **Previews** to force light or dark.
4. Click a preview or **Open full size** to see the page in a new tab. You can save it from there.
5. Click **Edit request** to load that view's sample into the editor at the bottom and render it. Change the request and click **Render** again.
6. Click **Explain** to see the resolved model as JSON.
7. A bad request shows the status, the title, the detail, and the `code`.
8. Fill **API key** only when the server has `SCOTTPLOTZ_API_KEY` set. The previews need it too.

## Reconciliation reports

Scottplotz writes the 13 RecDiffy reports as HTML. You post one reconciliation result as JSON and name the report. Scottplotz works out every figure (counts, sums, differences, checks, ageing) and returns a page that holds finished values only: no formulas and no script. The same input feeds every report.

| Id | Report | Answers |
| --- | --- | --- |
| `BO1` | Exception blotter | What do I fix first today? |
| `BO2` | Side-by-side field compare | Exactly which fields disagree on each matched pair? |
| `BO3` | Raw versus normalized | Why did these match when the raw values look different? |
| `BO4` | Lifecycle chain | Which version of the trade is live, and what changed? |
| `BO5` | Block and allocations | Do the allocations add up to the block? |
| `BO6` | Settlement cash ladder | How much cash moves each day, and how much of it is in doubt? |
| `MO1` | Control totals | Do the two books agree in total, and where does each difference come from? |
| `MO2` | Match-quality scorecard | How good is matching, overall and by counterparty? |
| `MO3` | Cascade funnel | How did the engine reach these results? |
| `MO4` | Break heatmap | Where are breaks concentrated? |
| `MO5` | Ageing and settlement status | How old are open items, and have they reached settlement? |
| `MO6` | Tolerance usage | Are tolerances hiding drift? |
| `MO7` | Match audit trail | Why was this matched, or not? |

### Report endpoints

| Request | Result |
| --- | --- |
| `GET /reports` | `200` and JSON for every report: id, slug, title, office, users, question, cadence, description, and the roles it needs |
| `GET /reports/sample` | `200` and a made-up input that works with every report |
| `POST /reports/{id}` | `200` and the report as an HTML page |
| `POST /reports/{id}/data` | `200` and the report's computed values as JSON: every block, row and cell |
| `GET /reports.html` | The reports gallery |

`{id}` is the id (`MO5`) or the slug (`ageing`); case does not matter. An unknown report is `400` and lists the reports. When `SCOTTPLOTZ_API_KEY` is set, the two `POST` routes need `X-Api-Key`.

### The input

The full format, with every property, is in [docs/reports-input.md](docs/reports-input.md). In outline:

```json
{
  "run": { "asOf": "2026-03-31", "profileId": "trade-recon", "roles": { "Counterparty": "Counterparty", "Quantity": "Quantity" } },
  "options": { "theme": "auto" },
  "passes": ["P1", "P2"],
  "entries": [ { "id": "…", "alignment": "Matched", "outcome": "Break", "left": ["L-1"], "right": ["R-1"], "ours": { "Quantity": 1000 }, "theirs": { "Quantity": 900 }, "committedBy": "P1", "diffs": [ { "field": "Quantity", "role": "Quantity", "type": "decimal", "outcome": "Break", "ours": 1000, "theirs": 900, "deltaKind": "Number", "delta": -100 } ] } ],
  "rows": [ { "source": "left", "rowKey": "L-1", "disposition": "Matched", "entryId": "…", "values": { "Quantity": 1000 } } ]
}
```

- `run.roles` names the fields that play each role (counterparty, quantity, settle date and so on). A report that needs a role you did not map says so and is not written.
- `entries` are the engine's results; `rows` are the input rows with their dispositions. Their order is kept.
- Values are numbers, ISO dates (`yyyy-MM-dd`) or text, by role.

### When a report request fails

| `code` | What to do |
| --- | --- |
| `unknown-report` | Use an id or slug from `GET /reports` |
| `malformed-json` | Send one JSON object |
| `invalid-report-input` | The detail names the path, such as `$.entries[3].alignment`, and what is wrong: an unknown property, a missing required one, or a value that is not allowed |
| `duplicate-key` | An entry id, or a row's source and key, appears twice |
| `unknown-row`, `unknown-entry` | An entry names a row that is not in `rows`, or a row names an entry that is not in `entries` |
| `report-unavailable` | The input lacks something this report needs. The detail says what, such as an unmapped role or missing `options.tolerances` |
| `too-many-records` | Send fewer, or raise `Reports:MaxRows` or `Reports:MaxEntries` |

### Reports with curl

```bash
curl -sS http://localhost:5240/reports

curl -sS -o result.json http://localhost:5240/reports/sample

curl -sS -o blotter.html http://localhost:5240/reports/BO1 \
  -H "Content-Type: application/json" \
  --data-binary @result.json

curl -sS -o ageing.json http://localhost:5240/reports/ageing/data \
  -H "Content-Type: application/json" \
  --data-binary @result.json

curl -sS http://localhost:5240/reports/MO5 \
  -H "Content-Type: application/json" \
  -d '{"run":{"asOf":"2026-03-31","profileId":"demo","roles":{}},"entries":[],"rows":[]}'
```

The last call fails on purpose: MO5 needs the trade date and settle date roles, and none are mapped.

### Reports with PowerShell

```powershell
Invoke-WebRequest http://localhost:5240/reports/sample -OutFile result.json

Invoke-WebRequest http://localhost:5240/reports/BO1 -Method POST -ContentType "application/json" -InFile result.json -OutFile blotter.html

Start-Process blotter.html
```

### Reports gallery

1. Open http://localhost:5240/reports.html. The main page links to it.
2. Each report is a card with a live preview drawn from the sample, grouped by back office and middle office.
3. Type in **Filter reports** to narrow the cards. Set **Previews** to force light or dark.
4. Click a preview or **Open full size** to see the report in a new tab. You can save or print it from there.
5. Click **Use my data** to go to the editor with that report selected. Paste your own input and click **Render**.
6. Click **Values as JSON** to see the computed values behind the report.
7. A bad input shows the status, the title, the detail, and the `code`.
