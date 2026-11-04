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

If the environment variable `SCOTTPLOTZ_API_KEY` is set, `POST /chart` and `POST /chart/explain` require the same value in the `X-Api-Key` header. Leave the variable unset and those calls need no key. The key is not stored in the program and is not written to the log.

## Endpoints

| Request | Result |
| --- | --- |
| `GET /health` | `200` and a small JSON object with `status` and `version` |
| `GET /chart-types` | `200` and JSON for every chart type: id, name, description, data shape, parameters, and an example body |
| `POST /chart` | `200` and a PNG. The header `X-Chart-Rule` names the rule that fired. `X-Chart-Type-Source` is `rules`, `model`, or `user` |
| `POST /chart/explain` | `200` and JSON: the detected shape, the rule, and the final settings. No image |
| `GET /` | The test page |
| `GET /swagger` | The API document |

A bad request is `400` with a problem document. A missing or wrong API key is `401`.

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
