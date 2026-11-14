# Report input

`POST /reports/{id}` takes one JSON document: a reconciliation result, already resolved to roles. Scottplotz computes every figure in the report from it. The same document feeds all 13 reports.

Unknown properties are rejected. Enum values are spelled exactly as shown. The order of `entries` and `rows` is kept and matters: several reports list things in that order.

```jsonc
{
  "run": {
    "asOf": "2026-03-31",               // required. ISO date. Ageing, settlement status and ordering use it
    "profileId": "trade-recon",         // required
    "title": "RecDiffy reconciliation", // optional, this is the default
    "engineVersion": "1.2.0",           // optional
    "roles": {                          // required. role -> the field's display name. A role that is absent is unmapped
      "Counterparty": "Counterparty", "Instrument": "Instrument", "Asset": "AssetClass", "Side": "Side",
      "TradeDate": "TradeDate", "SettleDate": "SettleDate", "Quantity": "Quantity", "Price": "Price",
      "Currency": "Currency", "NetCash": "NetCash", "Accrued": "Accrued", "Fees": "Fees",
      "Book": "Book", "LifecycleAction": "EventAction", "LifecycleTarget": "EventTarget"
    }
  },
  "options": {                          // optional, every member optional
    "currencyOrder": ["USD", "EUR", "GBP", "CHF", "CAD", "JPY"], // default shown; other currencies follow in ordinal order
    "payingSides": ["BUY", "PAY_FIXED", "PAY"],                  // default shown; a row on a paying side has cash sign -1
    "selectedEntries": null,            // null, or entry-id prefixes (case-insensitive). BO3 and MO7 show these entries
    "maxCards": 50,                     // default 50. Cap for BO3 and MO7 when selectedEntries is null
    "labels": {},                       // default heading text -> replacement
    "rawColumns": null,                 // null, or { "left": [column, ...], "right": [column, ...] } in display order. BO3 needs it
    "tolerances": null,                 // null, or [{ "path", "field", "kind", "absolute", "relative" }] in declaration order. MO6 needs it
                                        //   kind: Absolute | Relative | AbsoluteOrRelative (any other kind gives no amount)
    "theme": "auto"                     // auto | light | dark
  },
  "passes": ["P1", "P2", "P3"],         // optional. Declared pass ids in order. A committedBy not listed is appended in first-seen order
  "entries": [{
    "id": "e5f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4",  // required, unique
    "pathId": "trades",                 // optional, "" when the entry was not routed
    "alignment": "Matched",             // required. Matched | Ambiguous | SearchIncomplete | LeftOnly | RightOnly
    "composition": ["OneToOne"],        // optional. Any of OneToOne, ManyToOne, OneToMany, Package, LifecycleCollapsed. [] is "None"
    "outcome": "Exact",                 // required. NotCompared | Exact | WithinTolerance | Break. Entries that are not Matched are NotCompared
    "left": ["L-1"],                    // row keys of the left members, in engine order
    "right": ["R-1"],
    "ours":   { "Counterparty": "BAML", "Quantity": 1000, "SettleDate": "2026-04-02" },
    "theirs": { "Counterparty": "BAML", "Quantity": 1000, "SettleDate": "2026-04-02" },
                                        // role -> value. For a Matched entry: that side's projection (an N:1 side is the aggregate).
                                        // Otherwise: the values of that side's first member. null when the entry has no member on that side
    "committedBy": "P1",                // pass id or null. Open entries carry the pass that left them open
    "trail": [{ "pass": "P1", "verdict": "Committed", "key": null, "detail": null,
                "candidates": [{ "source": "left", "rowKey": "L-1" }] }],
                                        // optional. verdict: Committed | NoKey | NoCandidate | Vetoed | Contest | SearchIncomplete.
                                        // An entry whose trail is one attempt with detail "Unrouted" is an unrouted entry
    "hints": [{ "code": "NearestCandidate", "detail": "..." }],   // optional
    "diffs": [{                         // optional. Matched entries only. Fields that agree are absent
      "field": "Price",                 // the field's display name
      "role": "Price",                  // role or null
      "type": "decimal",                // decimal | date | text
      "outcome": "Break",               // WithinTolerance | Break
      "ours": 99.5, "theirs": 99.6,     // that field's value on each side, or null
      "deltaKind": "Number",            // None | Number | Days
      "delta": 0.1,                     // theirs minus ours (whole days for Days); null when deltaKind is None
      "attributedTo": []                // fields that explain this difference. Non-empty makes it a consequence, not a root break
    }]
  }],
  "rows": [{                            // one per input row, every disposition, in engine order
    "source": "left",                   // required. left | right
    "rowKey": "L-1",                    // required. (source, rowKey) is unique
    "disposition": "Matched",           // required. Matched | Ambiguous | SearchIncomplete | LeftOnly | RightOnly | Quarantined | Suppressed
    "suppression": null,                // null | Superseded | CancelEvent | FullyCancelled
    "validity": "Valid",                // optional, default Valid. Valid | ValidWithWarnings | Invalid
    "entryId": "e5f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4",  // null for Quarantined rows and rows with no entry. A suppressed lifecycle row carries its survivor's entry
    "itemLead": true,                   // optional, default true. false for every leg of a consolidated package except the first
    "values": { "Counterparty": "BAML", "Side": "BUY", "Quantity": 1000, "Currency": "USD", "LifecycleAction": "NEW" },  // role -> value
    "diagnostics": ["UnknownParty"],    // optional. Diagnostic codes
    "raw":   { "Trade_Date": "10/03/2026" },         // optional. BO3 only: the row's original columns
    "trace": [{ "ruleId": "date.parse", "field": "TradeDate", "raw": "10/03/2026", "canonical": "2026-03-10" }]  // optional. BO3 only
  }]
}
```

## Values

A role value is `null`, a JSON number, or a string.

| Roles | Value |
|---|---|
| `Quantity`, `Price`, `NetCash`, `Accrued`, `Fees` | A number, or a string holding a number with a dot as the decimal separator |
| `TradeDate`, `SettleDate` | An ISO date string, `yyyy-MM-dd` |
| Every other role | Text |

`null`, `""` and a missing role all mean "no value". A diff's `ours`, `theirs` and `delta` follow the diff's `type` the same way.

## What Scottplotz works out

None of this is sent: each entry's currencies and whether its two sides are in different currencies, root break fields, each difference's state (break, within tolerance, consequence) and tolerance amount, open items with their type, cash impact, suggested action and rank, settlement status, age in business days, cash sign, the side-by-side totals, and every count, sum and check on a report.

## Limits

The request body limit (5 MB by default, `Charting:MaxRequestBodyBytes`) applies. `Reports:MaxRows` caps `rows` and `Reports:MaxEntries` caps `entries`; both default to 50,000.
