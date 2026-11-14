# Reconciliation views

There are 30 views. Each takes the same request (see [HOWTO.md](../HOWTO.md#reconciliation-views)) at `POST /recon/<name>`, and `GET /recon/<name>/sample` returns a request that works with it. This page is generated from the view folders; `GET /recon/views` returns the same facts as JSON.

| View | Shows | Needs |
| --- | --- | --- |
| [`ageing-pyramid`](#ageing-pyramid) | Back-to-back horizontal bars comparing unpaired items across age buckets. | A date field or age attribute on records, with records on one or both sides. |
| [`bin-map`](#bin-map) | Grid of cells by group and sub-group. Grey cells agree; tinted cells show the direction and size of the difference. | A numeric measure. Records with group [row, sub-row] and attrs.slot for the position along the row; attrs.cost optionally prices the difference. |
| [`break-trends`](#break-trends) | Small multiples of open breaks over time per group, showing whether discrepancies are growing or shrinking. | Records with group, date, and status over multiple reconciliation runs. |
| [`bridge`](#bridge) | Waterfall chart walking from the left total to the right total through reconciling item groups. | A numeric measure; the left and right totals are the opening and closing points. |
| [`calendar-heatmap`](#calendar-heatmap) | Daily matrix of reconciliation differences across groups and dates, with detailed field breakdown on click. | A numeric measure, dates on records, and row grouping via group[0]. |
| [`coverage-timelines`](#coverage-timelines) | Paired interval bars on a shared date calendar for each record, showing periods of agreement and uncovered or disputed stretches. | Two date fields marking the start and end of each record's coverage period. |
| [`delta-histogram`](#delta-histogram) | Histogram of differences on a shared measure for matched records, with a tolerance band and side counts. | A numeric measure and records on both sides. |
| [`delta-matrix`](#delta-matrix) | Matrix of records against compared numeric fields with cells tinted by relative difference. | At least one compared numeric field. |
| [`difference-plot`](#difference-plot) | Position size against percentage difference on a compressed vertical scale, with a tolerance corridor and rails for one-sided records. | A numeric measure. |
| [`discrepancy-lollipops`](#discrepancy-lollipops) | Lollipop plot of percentage difference against a tolerance band, trailing stems to zero for breaks. | A numeric measure and percentage tolerance. |
| [`dumbbell`](#dumbbell) | One row per record with a dot for each side on a shared scale. The bar between the dots is the gap, in the colour of the side that is higher. | A numeric measure. Reads best with up to a few hundred records; group them with `group`. |
| [`duplicate-clusters`](#duplicate-clusters) | A strip of dots shows where every candidate's match score falls against the match and review thresholds. Below it, candidate records believed to be one entity are gathered into clusters, with the lines from each side and the fields that differ. | Records with attrs.score (0 to 100) to place on the strip. Cluster records carry their candidate lines as arrays in left and right, and send status. Optional attrs named sim_<fieldKey> (and sim_name) draw per-field similarity bars. Bulk rows may be status-only with a count and a score. |
| [`exception-board`](#exception-board) | A Kanban board sorting records into columns by reconciliation status, with detail on click. | Records with reconciliation status. A numeric measure is optional but recommended. |
| [`field-break-upset`](#field-break-upset) | UpSet plot showing which fields break together across records, with combination frequencies and per-field totals. | Comparable fields where breaks can be identified across records. |
| [`flow-diagram`](#flow-diagram) | A Sankey-style flow through successive stages. Each block is one value of a stage, each band is the number of records taking that route, and a block takes the colour of the status most of its records have. | Records with `count` (or one each) and one attr per stage naming the block it passes through. The stage attrs are named by the `stages` option; two or more stages. |
| [`icicle`](#icicle) | Hierarchical partition display with columns for each group level and individual records. Block height shows measure and colour shows difference. | A numeric measure and grouped records. |
| [`interval-overlay`](#interval-overlay) | Overlay of two time series with a shaded gap and a residual difference strip. | A numeric measure, grouped records with dates. |
| [`line-diff`](#line-diff) | Each line on the left side-by-side with the right, grouped by parent record. Differing cells are tinted on both sides, and missing lines show as gaps. | Records grouped by a parent entity with comparable fields. |
| [`marimekko`](#marimekko) | Columns by group with widths proportional to record count, divided vertically by status share. | Records with groups; a numeric measure is optional for amounts. |
| [`match-pass-funnel`](#match-pass-funnel) | Funnel plot showing records remaining before and after each matching pass, with paired and unmatched records. | Records grouped or tagged by matching pass, or matching pass groups. |
| [`match-ribbons`](#match-ribbons) | Each match group draws its left records and right records as cards in two columns, joined by ribbons: one to one, one to many, or many to one. A card with no ribbon has no partner. | Records that carry `left` and `right` as arrays of lines ({ key, name, ...fields }). A numeric measure shows the amount on each card. Reads best with up to a few dozen groups. |
| [`mirror-matrix`](#mirror-matrix) | Square matrix comparing reciprocal balances between entities, with billed entities across columns and billing entities down rows. | A numeric measure and two-level groups ([rowEntity, columnEntity]). |
| [`priority-quadrant`](#priority-quadrant) | Scatter of open records by age and amount on a log scale to prioritize old and large breaks. | A numeric measure. |
| [`row-minimap`](#row-minimap) | Every record as a small mark on one strip, in the order sent and coloured by status. A draggable lens lists the records under it, and a profile counts the records that differ in each field. | Records in a meaningful order (such as key order). Runs of agreeing records can be sent as one status-only record with a `count`. The server accepts up to 20,000 records per request. |
| [`slopegraph`](#slopegraph) | One line per record between left and right values on a shared vertical scale to highlight discrepancies. | A numeric measure. |
| [`tie-out-sheet`](#tie-out-sheet) | Per-group sheet of control totals across number fields with foot, tolerance, and break marks, followed by exception records. | Numeric fields and records organized by group. |
| [`tile-map`](#tile-map) | Each group is a tile on a configurable grid. Tile colours show the net difference on the measure. | A numeric measure. Each record's first group element is its tile code. |
| [`timing-lanes`](#timing-lanes) | Two horizontal lanes by date showing the lag between matching events. | A date field with a value on each side, such as capture date and settlement date. A second date for a duplicate goes in attrs.dupDay. |
| [`treemap`](#treemap) | A two-level treemap. Column width and tile height show the measure, and colour shows how far the right side sits from the left, in the colour of the side that is higher. Select a column heading to open that group. | A numeric measure and records with a two-level group path such as [group, subgroup]. |
| [`unit-chart`](#unit-chart) | One small square per record, grouped by category and sorted so agreed records come first. | Categorical records, optionally with groups and text or date fields. |

## ageing-pyramid

**Ageing pyramid.** Back-to-back horizontal bars comparing unpaired items across age buckets.

Needs: A date field or age attribute on records, with records on one or both sides.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `buckets` | numbers | `[7,30,60,90]` | Upper bounds in days for age buckets. |
| `mode` | choice: `count`, `amount` | `"count"` | Initial pyramid metric: count of items or measure amount. |
| `maxDetailRows` | integer (1 to 1000) | `100` | Maximum number of rows to display in the detail table. |

Text keys: `title`, `lede`, `chartTitleCount`, `chartTitleAmount`, `chartSub`, `chartSubUndated`, `chartAria`, `modeCount`, `modeAmount`, `leftHeader`, `rightHeader`, `bucketFirst`, `bucketRange`, `bucketLast`, `detailTitle`, `detailSub`, `leftNote`, `rightNote`, `colReference`, `colName`, `colAge`, `colReason`, `noReason`.

## bin-map

**Cell map.** Grid of cells by group and sub-group. Grey cells agree; tinted cells show the direction and size of the difference.

Needs: A numeric measure. Records with group [row, sub-row] and attrs.slot for the position along the row; attrs.cost optionally prices the difference.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `colourScale` | number (1 to 1000) | `30` | Percentage difference at which cell colour reaches full intensity. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `leftHigher`, `rightHigher`, `duplicateLegend`, `groupSummary`, `allSummary`, `groupTotal`, `bin`, `item`, `units`, `atCost`, `expected`, `inTolerance`, `recount`.

## break-trends

**Break trends.** Small multiples of open breaks over time per group, showing whether discrepancies are growing or shrinking.

Needs: Records with group, date, and status over multiple reconciliation runs.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `recentRuns` | integer (2 to 50) | `6` | Number of recent runs to display in the detail table. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `openSummary`, `detailTitle`, `detailTitleTotal`, `needsDates`, `dateCol`, `totalCol`, `changeCol`, `noNote`.

## bridge

**Reconciliation bridge.** Waterfall chart walking from the left total to the right total through reconciling item groups.

Needs: A numeric measure; the left and right totals are the opening and closing points.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `showUnexplained` | bool | `true` | Include a residual unexplained step if the bridge does not fully balance. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `leftEnding`, `rightEnding`, `unexplained`, `unexplainedNote`, `noItems`, `stepTotal`, `effect`, `date`, `reference`, `description`, `other`.

## calendar-heatmap

**Calendar heatmap.** Daily matrix of reconciliation differences across groups and dates, with detailed field breakdown on click.

Needs: A numeric measure, dates on records, and row grouping via group[0].

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `sortBy` | choice: `input`, `net`, `name` | `"input"` | Row order: as sent, largest absolute net difference first, or alphabetical. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `leftHigher`, `rightHigher`, `duplicateLegend`, `total`, `noDates`, `field`, `difference`, `agreed`.

## coverage-timelines

**Coverage timelines.** Paired interval bars on a shared date calendar for each record, showing periods of agreement and uncovered or disputed stretches.

Needs: Two date fields marking the start and end of each record's coverage period.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `startField` | text | `"start"` | Date field key for interval start date. |
| `endField` | text | `"end"` | Date field key for interval end date. |
| `sortBy` | choice: `input`, `status`, `name` | `"input"` | Row order: as sent, by status, or alphabetical by name. |
| `maxRows` | integer (1 to 1000) | `200` | Draw at most this many records. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `bothAgree`, `leftOnlyLegend`, `rightOnlyLegend`, `breakLegend`, `daysDiffer`, `otherBreak`, `field`, `noReason`, `noDates`, `truncated`.

## delta-histogram

**Delta histogram.** Histogram of differences on a shared measure for matched records, with a tolerance band and side counts.

Needs: A numeric measure and records on both sides.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `binWidth` | number | none | Width of each histogram bin in measure units; defaults to the tolerance or an auto-calculated step. |
| `maxBins` | integer (1 to 100) | `20` | Maximum number of bins to display on each side of zero before grouping into beyond bins. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `bothAgree`, `withinTol`, `leftHigher`, `rightHigher`, `unmatchedTitle`, `unmatchedSub`, `leftOnlyDesc`, `rightOnlyDesc`, `dupDesc`, `axisLabel`, `beyond`, `noRecords`, `showingFirst`, `topGroup`, `detailTitle`, `detailTitleBeyond`, `detailTitleExact`, `difference`, `field`, `group`.

## delta-matrix

**Delta matrix.** Matrix of records against compared numeric fields with cells tinted by relative difference.

Needs: At least one compared numeric field.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `colourScale` | number (1 to 1000) | `25` | Percent difference between sides that yields maximum colour saturation. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `leftHigher`, `rightHigher`, `netDifference`, `record`, `component`, `difference`, `leftOnlyBand`, `rightOnlyBand`, `leftOnlyDetail`, `rightOnlyDetail`, `followsFrom`, `allAgree`.

## difference-plot

**Difference plot.** Position size against percentage difference on a compressed vertical scale, with a tolerance corridor and rails for one-sided records.

Needs: A numeric measure.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `yCap` | number (1 to 100) | `10` | Maximum percentage difference shown before compression capping. |
| `labelTop` | integer (0 to 20) | `3` | Number of largest breaks to label directly on the chart. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `agrees`, `rightHigher`, `leftHigher`, `duplicate`, `railsNote`, `leftOnlyRail`, `rightOnlyRail`, `field`, `noPartner`, `noCompare`, `netDiff`, `defaultReasonAgrees`, `defaultReasonBreak`.

## discrepancy-lollipops

**Discrepancy lollipops.** Lollipop plot of percentage difference against a tolerance band, trailing stems to zero for breaks.

Needs: A numeric measure and percentage tolerance.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `sortBy` | choice: `difference`, `input` | `"difference"` | Row order: largest percentage difference first, or as sent. |
| `maxRows` | integer (1 to 1000) | `200` | Draw at most this many records. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `legendBand`, `legendRight`, `legendLeft`, `difference`, `field`, `line`, `noReason`, `agreesReason`, `truncated`.

## dumbbell

**Dumbbell plot.** One row per record with a dot for each side on a shared scale. The bar between the dots is the gap, in the colour of the side that is higher.

Needs: A numeric measure. Reads best with up to a few hundred records; group them with `group`.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `sortBy` | choice: `input`, `difference` | `"input"` | Row order within each group: as sent, or largest difference first. |
| `maxRows` | integer (1 to 2000) | `400` | Draw at most this many records. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `leftLegend`, `rightLegend`, `bothAgree`, `difference`, `field`, `noReason`, `truncated`.

## duplicate-clusters

**Match scores and duplicate clusters.** A strip of dots shows where every candidate's match score falls against the match and review thresholds. Below it, candidate records believed to be one entity are gathered into clusters, with the lines from each side and the fields that differ.

Needs: Records with attrs.score (0 to 100) to place on the strip. Cluster records carry their candidate lines as arrays in left and right, and send status. Optional attrs named sim_<fieldKey> (and sim_name) draw per-field similarity bars. Bulk rows may be status-only with a count and a score.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `matchFrom` | number (0 to 100) | `92` | Score from which a candidate counts as matched without review. |
| `reviewFrom` | number (0 to 100) | `70` | Score from which a candidate goes to review. Below it the candidates are treated as different. |

Text keys: `title`, `lede`, `stripTitle`, `stripSub`, `lowLabel`, `reviewLabel`, `matchLabel`, `noScores`, `listTitle`, `listSub`, `noClusters`, `scoreWord`, `sideColumn`, `recordColumn`, `nameColumn`, `noReason`.

## exception-board

**Exception board.** A Kanban board sorting records into columns by reconciliation status, with detail on click.

Needs: Records with reconciliation status. A numeric measure is optional but recommended.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `maxCards` | integer (1 to 200) | `30` | Maximum number of cards to display per column. |

Text keys: `title`, `lede`, `boardTitle`, `boardSub`, `colExact`, `colExactNote`, `colTolerance`, `colToleranceNote`, `colBreak`, `colBreakNote`, `colLeftOnly`, `colLeftOnlyNote`, `colRightOnly`, `colRightOnlyNote`, `colDuplicate`, `colDuplicateNote`, `exactSummary`, `nextStep`, `field`, `ageUnit`.

## field-break-upset

**Field-break UpSet plot.** UpSet plot showing which fields break together across records, with combination frequencies and per-field totals.

Needs: Comparable fields where breaks can be identified across records.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `maxCombos` | integer (1 to 50) | `9` | Maximum number of field combinations to display. |
| `sampleRows` | integer (1 to 100) | `6` | Maximum number of sample records to show in detail table. |
| `captionAttr` | text | `""` | Attribute key whose most common value is named in the subtitle. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `unpairedTitle`, `unpairedSub`, `leftOnlyDesc`, `rightOnlyDesc`, `duplicateDesc`, `detailTitle`, `showing`, `mostly`, `and`, `trade`, `name`, `noBreaks`.

## flow-diagram

**Flow diagram.** A Sankey-style flow through successive stages. Each block is one value of a stage, each band is the number of records taking that route, and a block takes the colour of the status most of its records have.

Needs: Records with `count` (or one each) and one attr per stage naming the block it passes through. The stage attrs are named by the `stages` option; two or more stages.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `stages` | json | `["pairing","outcome","action"]` | The attr names, in order, that name the block a record passes through at each stage. Two or more. |
| `selected` | text | `""` | Name of the block to select when the page opens. Empty selects the block with the most broken records. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `stage1`, `stage2`, `stage3`, `stage4`, `stage5`, `stage6`, `detailTitle`, `flowTitle`, `blockHead`, `countHead`, `shareHead`, `cameFrom`, `wentTo`.

## icicle

**Icicle plot.** Hierarchical partition display with columns for each group level and individual records. Block height shows measure and colour shows difference.

Needs: A numeric measure and grouped records.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `sortBy` | choice: `input`, `difference` | `"input"` | Sort order of siblings within each group: as sent, or largest difference first. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `difference`, `reason`, `item`, `name`, `total`, `all`, `differ`, `detailTitle`.

## interval-overlay

**Interval overlay.** Overlay of two time series with a shaded gap and a residual difference strip.

Needs: A numeric measure, grouped records with dates.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `maxTableRows` | integer (1 to 100) | `12` | Maximum number of differing intervals shown in the detail table. |

Text keys: `title`, `lede`, `groupsTitle`, `groupsSub`, `leftLegend`, `rightLegend`, `leftOnlyLegend`, `rightOnlyLegend`, `residualTitle`, `measurePerInterval`, `tableTime`, `tableResult`, `tableDiff`, `diffCount`, `allAgree`, `groupAgrees`, `differTitle`, `firstShown`, `nothingDiffers`, `pickGroup`, `interval`.

## line-diff

**Side-by-side line diff.** Each line on the left side-by-side with the right, grouped by parent record. Differing cells are tinted on both sides, and missing lines show as gaps.

Needs: Records grouped by a parent entity with comparable fields.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `sortBy` | choice: `input`, `breaks` | `"input"` | Order of groups in the list: input order, or groups with breaks first. |

Text keys: `title`, `lede`, `listTitle`, `listSub`, `leftSide`, `rightSide`, `linesDiffer`, `allAgree`, `gapLeft`, `gapRight`, `defaultGroup`.

## marimekko

**Marimekko plot.** Columns by group with widths proportional to record count, divided vertically by status share.

Needs: Records with groups; a numeric measure is optional for amounts.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `sortBy` | choice: `input`, `count`, `issues` | `"input"` | Column order: as sent, largest volume first, or most issues first. |
| `initialGroup` | text | `""` | Key of the group to select initially; defaults to the group with the most issues. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `detailTitle`, `acceptedTwice`, `result`, `share`, `usualCause`, `total`, `amount`, `noReason`.

## match-pass-funnel

**Match-pass funnel.** Funnel plot showing records remaining before and after each matching pass, with paired and unmatched records.

Needs: Records grouped or tagged by matching pass, or matching pass groups.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `showUnmatchedRow` | bool | `true` | Include a final row for records remaining unmatched after all passes. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `legLeft`, `legRight`, `legPaired`, `headerLeft`, `headerRight`, `colLeft`, `colRight`, `whyPaired`, `whyUnmatched`, `passDetail`, `unmatchedDetail`, `unmatchedName`, `unmatchedRule`, `defaultPassName`.

## match-ribbons

**Match ribbons.** Each match group draws its left records and right records as cards in two columns, joined by ribbons: one to one, one to many, or many to one. A card with no ribbon has no partner.

Needs: Records that carry `left` and `right` as arrays of lines ({ key, name, ...fields }). A numeric measure shows the amount on each card. Reads best with up to a few dozen groups.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `attentionOnly` | bool | `false` | Start with the groups that agree (exact and within tolerance) faded out, so only what needs attention stands out. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `chartLabel`, `leftTitle`, `rightTitle`, `showAll`, `showAttention`, `leftOnlyNote`, `rightOnlyNote`, `duplicateNote`, `leftOne`, `leftMany`, `rightOne`, `rightMany`, `detailTitle`, `leftHead`, `rightHead`, `nameHead`, `amountHead`, `total`, `nothing`, `verdict`, `noReason`.

## mirror-matrix

**Intercompany mirror matrix.** Square matrix comparing reciprocal balances between entities, with billed entities across columns and billing entities down rows.

Needs: A numeric measure and two-level groups ([rowEntity, columnEntity]).

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `divisor` | number | `1` | Divide matrix cell amounts by this number (e.g. 1000 for thousands). |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `scaleLeft`, `scaleRight`, `axisLabel`, `rowHeader`, `pairTitle`, `pairSub`, `pairAgrees`, `pairDiff`, `document`, `total`, `difference`.

## priority-quadrant

**Priority quadrant.** Scatter of open records by age and amount on a log scale to prioritize old and large breaks.

Needs: A numeric measure.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `ageFrom` | number (0 to ) | `30` | Age threshold in days for the priority quadrant. |
| `amountFrom` | number (0 to ) | `100000` | Amount threshold for the priority quadrant. |
| `ageCap` | number (1 to ) | `120` | Maximum age in days displayed on the horizontal scale. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `priorityZone`, `clearZone`, `daysOpen`, `openDays`, `field`, `difference`, `nothingToCompare`, `escalate`, `noReason`.

## row-minimap

**Row minimap.** Every record as a small mark on one strip, in the order sent and coloured by status. A draggable lens lists the records under it, and a profile counts the records that differ in each field.

Needs: Records in a meaningful order (such as key order). Runs of agreeing records can be sent as one status-only record with a `count`. The server accepts up to 20,000 records per request.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `lensRows` | integer (1 to 1000) | `40` | How many consecutive rows the lens covers. |

Text keys: `title`, `lede`, `mapTitle`, `mapSub`, `mapLabel`, `profTitle`, `profSub`, `profNone`, `lensTitle`, `lensAllAgree`, `keyHead`, `sideHead`, `noRow`, `empty`.

## slopegraph

**Slopegraph.** One line per record between left and right values on a shared vertical scale to highlight discrepancies.

Needs: A numeric measure.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `scale` | choice: `sqrt`, `linear` | `"sqrt"` | Scale transformation for the vertical axis: square root to compress high values, or linear. |
| `labelOnly` | choice: `exceptions`, `all` | `"exceptions"` | Which lines to label on the chart: exceptions only, or all records. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `field`, `noReason`, `duplicateSuffix`.

## tie-out-sheet

**Tie-out sheet.** Per-group sheet of control totals across number fields with foot, tolerance, and break marks, followed by exception records.

Needs: Numeric fields and records organized by group.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `totalTolerance` | number | `1` | Allowed difference on a control total to count as within tolerance. |

Text keys: `title`, `lede`, `groupsTitle`, `groupsSub`, `sheetSub`, `totalsBreak`, `totalsFoot`, `controlTotal`, `difference`, `exceptionsTitle`, `noExceptionsTitle`, `pickGroupPrompt`, `result`, `record`, `name`, `why`, `noReason`, `allGroups`.

## tile-map

**Tile-grid map.** Each group is a tile on a configurable grid. Tile colours show the net difference on the measure.

Needs: A numeric measure. Each record's first group element is its tile code.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `grid` | json | `{"AK":[0,0],"ME":[11,0],"VT":[10,1],"NH":[11,1],"WA":[1,2],"ID":[2,2],"MT":[3,2],"ND":[4,2],"MN":[5,2],"IL":[6,2],"WI":[7,2],"MI":[8,2],"NY":[9,2],"RI":[10,2],"MA":[11,2],"OR":[1,3],"NV":[2,3],"WY":[3,3],"SD":[4,3],"IA":[5,3],"IN":[6,3],"OH":[7,3],"PA":[8,3],"NJ":[9,3],"CT":[10,3],"CA":[1,4],"UT":[2,4],"CO":[3,4],"NE":[4,4],"MO":[5,4],"KY":[6,4],"WV":[7,4],"VA":[8,4],"MD":[9,4],"DE":[10,4],"AZ":[2,5],"NM":[3,5],"KS":[4,5],"AR":[5,5],"TN":[6,5],"NC":[7,5],"SC":[8,5],"DC":[9,5],"OK":[4,6],"LA":[5,6],"MS":[6,6],"AL":[7,6],"GA":[8,6],"HI":[0,7],"TX":[4,7],"FL":[9,7]}` | Grid layout mapping tile codes to [column, row] coordinates. |
| `colourScale` | number (0.1 to 1000) | `15` | Percent difference of a tile (summed difference over summed left) that gives full colour. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `scaleLeft`, `scaleRight`, `detailTitle`, `detailAgrees`, `agreesNote`, `item`, `total`, `noTiles`, `difference`.

## timing-lanes

**Timing lanes.** Two horizontal lanes by date showing the lag between matching events.

Needs: A date field with a value on each side, such as capture date and settlement date. A second date for a duplicate goes in attrs.dupDay.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `dateField` | text | `""` | Date field to plot on the lanes. Defaults to the first date field. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `exactLegend`, `toleranceLegend`, `breakLegend`, `leftOnlyLegend`, `rightOnlyLegend`, `duplicateLegend`, `dayTitle`, `daySummary`, `lateSummary`, `emptyDay`, `selectAnother`, `resultCol`, `recordCol`, `daysCol`, `amountCol`, `noteCol`, `statusExact`, `statusTolerance`, `statusBreak`, `statusLeftOnly`, `statusRightOnly`, `duplicateSuffix`, `defaultBreakReason`, `and`, `noDates`.

## treemap

**Treemap with drill-down.** A two-level treemap. Column width and tile height show the measure, and colour shows how far the right side sits from the left, in the colour of the side that is higher. Select a column heading to open that group.

Needs: A numeric measure and records with a two-level group path such as [group, subgroup].

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `colourScale` | number (0.01 to 1000) | `12` | The percent difference between the sides that gives a tile its full colour. |

Text keys: `title`, `lede`, `chartTitle`, `zoomTitle`, `chartSub`, `allGroups`, `back`, `ungrouped`, `noData`, `tipValue`, `tipDiff`, `tipNoPairs`, `unpaired`, `detailTitle`, `detailSub`, `detailDiff`, `colRecord`, `colName`, `colDifference`, `colWhy`, `noReason`.

## unit-chart

**Unit chart.** One small square per record, grouped by category and sorted so agreed records come first.

Needs: Categorical records, optionally with groups and text or date fields.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `maxSquares` | integer (1 to 2000) | `600` | Maximum number of unit squares to draw across all groups. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `groupSub`, `field`, `allAgree`, `truncated`.
