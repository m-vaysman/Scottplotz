# Reconciliation views

There are 6 views. Each takes the same request (see [HOWTO.md](../HOWTO.md#reconciliation-views)) at `POST /recon/<name>`, and `GET /recon/<name>/sample` returns a request that works with it. This page is generated from the view folders; `GET /recon/views` returns the same facts as JSON.

| View | Shows | Needs |
| --- | --- | --- |
| [`dumbbell`](#dumbbell) | One row per record with a dot for each side on a shared scale. The bar between the dots is the gap, in the colour of the side that is higher. | A numeric measure. Reads best with up to a few hundred records; group them with `group`. |
| [`duplicate-clusters`](#duplicate-clusters) | A strip of dots shows where every candidate's match score falls against the match and review thresholds. Below it, candidate records believed to be one entity are gathered into clusters, with the lines from each side and the fields that differ. | Records with attrs.score (0 to 100) to place on the strip. Cluster records carry their candidate lines as arrays in left and right, and send status. Optional attrs named sim_<fieldKey> (and sim_name) draw per-field similarity bars. Bulk rows may be status-only with a count and a score. |
| [`flow-diagram`](#flow-diagram) | A Sankey-style flow through successive stages. Each block is one value of a stage, each band is the number of records taking that route, and a block takes the colour of the status most of its records have. | Records with `count` (or one each) and one attr per stage naming the block it passes through. The stage attrs are named by the `stages` option; two or more stages. |
| [`match-ribbons`](#match-ribbons) | Each match group draws its left records and right records as cards in two columns, joined by ribbons: one to one, one to many, or many to one. A card with no ribbon has no partner. | Records that carry `left` and `right` as arrays of lines ({ key, name, ...fields }). A numeric measure shows the amount on each card. Reads best with up to a few dozen groups. |
| [`row-minimap`](#row-minimap) | Every record as a small mark on one strip, in the order sent and coloured by status. A draggable lens lists the records under it, and a profile counts the records that differ in each field. | Records in a meaningful order (such as key order). Runs of agreeing records can be sent as one status-only record with a `count`. The server accepts up to 20,000 records per request. |
| [`treemap`](#treemap) | A two-level treemap. Column width and tile height show the measure, and colour shows how far the right side sits from the left, in the colour of the side that is higher. Select a column heading to open that group. | A numeric measure and records with a two-level group path such as [group, subgroup]. |

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

## flow-diagram

**Flow diagram.** A Sankey-style flow through successive stages. Each block is one value of a stage, each band is the number of records taking that route, and a block takes the colour of the status most of its records have.

Needs: Records with `count` (or one each) and one attr per stage naming the block it passes through. The stage attrs are named by the `stages` option; two or more stages.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `stages` | json | `["pairing","outcome","action"]` | The attr names, in order, that name the block a record passes through at each stage. Two or more. |
| `selected` | text | `""` | Name of the block to select when the page opens. Empty selects the block with the most broken records. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `stage1`, `stage2`, `stage3`, `stage4`, `stage5`, `stage6`, `detailTitle`, `flowTitle`, `blockHead`, `countHead`, `shareHead`, `cameFrom`, `wentTo`.

## match-ribbons

**Match ribbons.** Each match group draws its left records and right records as cards in two columns, joined by ribbons: one to one, one to many, or many to one. A card with no ribbon has no partner.

Needs: Records that carry `left` and `right` as arrays of lines ({ key, name, ...fields }). A numeric measure shows the amount on each card. Reads best with up to a few dozen groups.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `attentionOnly` | bool | `false` | Start with the groups that agree (exact and within tolerance) faded out, so only what needs attention stands out. |

Text keys: `title`, `lede`, `chartTitle`, `chartSub`, `chartLabel`, `leftTitle`, `rightTitle`, `showAll`, `showAttention`, `leftOnlyNote`, `rightOnlyNote`, `duplicateNote`, `leftOne`, `leftMany`, `rightOne`, `rightMany`, `detailTitle`, `leftHead`, `rightHead`, `nameHead`, `amountHead`, `total`, `nothing`, `verdict`, `noReason`.

## row-minimap

**Row minimap.** Every record as a small mark on one strip, in the order sent and coloured by status. A draggable lens lists the records under it, and a profile counts the records that differ in each field.

Needs: Records in a meaningful order (such as key order). Runs of agreeing records can be sent as one status-only record with a `count`. The server accepts up to 20,000 records per request.

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `lensRows` | integer (1 to 1000) | `40` | How many consecutive rows the lens covers. |

Text keys: `title`, `lede`, `mapTitle`, `mapSub`, `mapLabel`, `profTitle`, `profSub`, `profNone`, `lensTitle`, `lensAllAgree`, `keyHead`, `sideHead`, `noRow`, `empty`.

## treemap

**Treemap with drill-down.** A two-level treemap. Column width and tile height show the measure, and colour shows how far the right side sits from the left, in the colour of the side that is higher. Select a column heading to open that group.

Needs: A numeric measure and records with a two-level group path such as [group, subgroup].

| Option | Type | Default | What it does |
| --- | --- | --- | --- |
| `colourScale` | number (0.01 to 1000) | `12` | The percent difference between the sides that gives a tile its full colour. |

Text keys: `title`, `lede`, `chartTitle`, `zoomTitle`, `chartSub`, `allGroups`, `back`, `ungrouped`, `noData`, `tipValue`, `tipDiff`, `tipNoPairs`, `unpaired`, `detailTitle`, `detailSub`, `detailDiff`, `colRecord`, `colName`, `colDifference`, `colWhy`, `noReason`.
