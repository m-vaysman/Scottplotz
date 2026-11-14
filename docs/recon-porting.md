# Porting a reconciliation view

A view is a folder of five files under `src/Scottplotz/Recon/Views/<name>/`. Adding one needs no C#: the registry finds the folder, and the tests in `ReconViewTests` run against every view.

| File | Holds |
|---|---|
| `view.json` | Name, title, description, text defaults, option definitions |
| `view.html` | The markup that goes inside `<main>` |
| `view.css` | Styles for this view only |
| `view.js` | Drawing code; reads the model, never generates data |
| `sample.json` | A request body that works with `POST /recon/<name>` |

The reference port is `Views/dumbbell`. Its source was `p06.html` in the sample set. Read all five of its files before you start.

## What a port changes

The sample pages draw well but generate their own data and hard-code their wording. A port keeps the drawing and replaces those two things.

1. Delete the generated data (`RD.rng`, hard-coded arrays, hard-coded counts). Read `RD.model` instead.
2. Move the page's data into `sample.json` as a request in the contract below, so the ported view shows the same picture the sample did.
3. Replace every display string with a text key or a label. No side name, industry word or unit may remain in `view.html` or `view.js`.
4. Compute scales, axis ranges and tick values from the data. The samples hard-code them.
5. Declare the settings a caller may change as options in `view.json`.
6. Handle the edges: one record, no groups, a side missing, no measure, no `reason`.

Do not redesign the chart, and do not edit the kit, the shell, or any C#.

## The request (what `sample.json` holds)

Only `records` is required.

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
    { "key": "PO-1", "name": "Corner boards", "group": "Tidewell Supply",
      "left": { "qty": 400, "ext": 1240 }, "right": { "qty": 400, "ext": 1380 },
      "reason": "Invoiced at 3.45 each." }
  ],
  "groups": [ { "key": "Tidewell Supply", "note": "Paid weekly." } ],
  "text": { "title": "Purchase order lines against supplier invoices" },
  "labels": { "break": "Dispute" },
  "options": { "sortBy": "difference" },
  "theme": "auto",
  "palette": { "left": "#2563eb" }
}
```

- **Sides.** `left` and `right` are a name, or `{ name, short }`. `short` is the word used in generated phrases.
- **`unit`.** A noun, or `{ one, many }`.
- **`tolerance`.** A number (absolute), or `{ absolute, percent }`. It applies to every compared number field unless the field has its own.
- **`fields`.** A key, or `{ key, label, type, decimals, compare, tolerance }`. `type` is `number`, `text` or `date`. A date field's tolerance is in days. When `fields` is left out they are inferred from the records.
- **`measure`.** The number field that totals, `delta` and `pct` use. Defaults to the first number field. A request with no number field has no measure.
- **A record** has a `key` and at least one of `left`, `right` or `status`.
  - A side is a bare number (the measure), an object of field values, or an array of lines `{ key, name, ...fields }` whose numbers are summed.
  - `status` is `leftOnly`, `exact`, `tolerance`, `break` or `rightOnly`. Leave it out and it is derived from the sides. Send it when the rule is not an amount rule; it then wins.
  - `count` makes one row stand for that many records. Use it with a bare `status` for bulk rows such as "1,182 exact" instead of sending them all. A row with a `count` and sides carries the totals for those records. A view that counts records adds `rec.count`, never 1.
  - `group` is a name or a path such as `["Equities", "Tech"]`. `date` is an ISO date or date-time.
  - `duplicate` is `true`, `"left"` or `"right"`. `reason` is free text. `attrs` holds any other single values.
- **`groups`** is optional: `{ key, name, note, left, right, attrs }`. It sets group order, gives a group a note, and can carry control totals in `left` and `right`.
- **`totals`** `{ left, right }` overrides the summed totals when the control total is not the sum of the records sent.

## The model (what `view.js` reads)

`RD.model` is the request after validation, with everything resolved:

```js
{
  view: { name, title },
  left: { name, short }, right: { name, short },
  unit, unitOne, asOf,
  tolerance: { absolute, percent },
  fields: [{ key, label, type, decimals, compare, tolerance }],
  measure,                       // field key or null
  records: [{
    key, name, status, count,
    left, right,                 // { fieldKey: value|null } or null when the side is missing
    leftLines, rightLines,       // [{ key, name, values: { fieldKey: value } }], empty unless lines were sent
    delta, pct,                  // right minus left on the measure, and as a percent of left; null when not comparable
    breaks,                      // keys of the fields that differ beyond tolerance
    group,                       // always an array, possibly empty
    date, reason,                // string or null
    duplicate,                   // null, "left", "right" or "either"
    attrs                        // { name: value }
  }],
  groups: [{ path, name, note, left, right, attrs }],
  summary: { counts: { leftOnly, exact, tolerance, break, rightOnly }, duplicates, leftCount, rightCount, leftTotal, rightTotal, net },
  text: { ... },                 // this view's text keys, placeholders already filled
  labels: { leftOnly, exact, tolerance, break, rightOnly, duplicate, agrees, leftHigher, rightHigher, net, none },
  options: { ... }               // this view's options, defaults applied
}
```

Summary counts are weighted by `count`. A record's `left` holds every declared field, with `null` where no value was sent.

## The kit (`RD`)

| Member | Use |
|---|---|
| `RD.model` | The model above |
| `RD.h(tag, attrs, kids)`, `RD.s(tag, attrs, kids)` | Build HTML and SVG elements. `attrs.text` sets text; `attrs.on` adds listeners |
| `RD.text(key)`, `RD.label(token)`, `RD.opt(name)` | Resolved text, label and option |
| `RD.field(key)`, `RD.measure` | Field definition; the measure's definition or `null` |
| `RD.val(rec, 'left' \| 'right', key?)` | A side's value for a field (the measure by default), or `null` |
| `RD.fmt(key, value)`, `RD.fmtSigned(key, value)` | Format with the field's decimals; `null` becomes the `none` label |
| `RD.money(n, d)`, `RD.signed(n, d)`, `RD.int(n)` | Plain number formatting |
| `RD.cls[status]` | `l`, `ex`, `tol`, `brk`, `r`: suffix for the `f-*`, `s-*`, `sw` and `seg` classes |
| `RD.statusWord(rec)` | The status label, with the duplicate label when set |
| `RD.diff(t)` | Colour for a difference, `t` from -1 (left higher) to 1 (right higher) |
| `RD.ticks(max, n)` | Round tick values from 0 to at least `max` |
| `RD.groupNames(records, level)`, `RD.groupInfo(path)` | Group names in order; a group's entry from `model.groups` |
| `RD.clip(text, n)` | Shorten text to `n` characters |
| `RD.hover(el, title, lines)`, `RD.tip` | Tooltip |
| `RD.tieOut(el)` | The standard tie-out panel, built from `model.summary` |

An element with `data-text="key"` or `data-label="token"` in `view.html` is filled in for you.

## `view.json`

```json
{
  "name": "dumbbell",
  "title": "Dumbbell plot",
  "description": "One sentence on what the picture shows.",
  "needs": "What the data must have, e.g. a numeric measure.",
  "text": { "title": "{left} against {right}", "lede": "..." },
  "options": [
    { "name": "sortBy", "type": "choice", "allowed": ["input", "difference"], "default": "input", "description": "..." }
  ]
}
```

- `name` equals the folder name: lower case, words joined by hyphens.
- `text` must have a `title`. Defaults are generic and may use `{left}`, `{right}`, `{leftShort}`, `{rightShort}`, `{unit}`, `{unitOne}`, `{measure}`, `{asOf}` and `{tolerance}`. The sample's own wording goes in `sample.json` under `text`, not here.
- Option types are `number`, `integer`, `choice`, `text`, `bool`, `numbers` (an array of numbers) and `json` (an object or array). `number` and `integer` take `min` and `max`. Every option needs a `description` and a `default`.

## Rules the tests enforce

- No `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `eval`, `Function(`, `fetch`, `XMLHttpRequest`, `WebSocket`, `import(`, `localStorage`, `Math.random`, `Date.now` or `<script`. Put text on the page with `attrs.text` or `textContent`.
- No URLs and no `url(` in the CSS. The page loads nothing.
- Colours come from the kit's CSS variables (`--left`, `--right`, `--exact`, `--tol`, `--brk`, `--ink`, `--ink2`, `--muted`, `--grid`, `--axis`, `--surface`, `--plane`, `--wash`, `--mid`). No literal colours, so both themes work.
- `sample.json` must parse as a request for the view.

## Things that go wrong

- `rec.pct` and `tolerance.percent` are in percent: 27.4 means 27.4%. Do not multiply by 100 again.
- `duplicate` is a flag. Keep the side values as they are and mark the record (a ring, a note, an extra row). Do not double a value in `sample.json` to stand for a duplicate.
- `RD` members are not globals. Take what you use from it first: `const { h, s, model, fmt } = RD;`.
- Where the sample page lists its data, copy those values into `sample.json`. Where it generates data, write literal values that tell the same story and give counts close to the ones the page shows.
- When the page opens, select the record or group that best shows the picture, usually the largest break.

## Check your port

```powershell
dotnet test tests/Scottplotz.Tests --filter "FullyQualifiedName~Recon"
dotnet run --project src/Scottplotz --urls http://127.0.0.1:5251
```

Then post the sample and look at the page in both themes:

```powershell
curl.exe -s http://127.0.0.1:5251/recon/<name>/sample -o sample.json
curl.exe -s -X POST http://127.0.0.1:5251/recon/<name> -H "Content-Type: application/json" --data-binary "@sample.json" -o page.html
powershell -File tools/shoot.ps1 -Html page.html -Png light.png -Theme light
powershell -File tools/shoot.ps1 -Html page.html -Png dark.png -Theme dark
```

`shoot.ps1` prints the browser console. A port is done when the console is clean, the picture matches the sample page, and no sample wording is left in `view.html` or `view.js`.
