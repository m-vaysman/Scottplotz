// Deterministic generator for sample.json: a made-up, internally consistent reconciliation result
// (ours = asset manager's blotter = left, theirs = counterparty confirmations = right) that feeds
// the 13 Scottplotz reports. Run: node make-sample.mjs   (Node 22, no dependencies, no clock, fixed seed)
import { createHash } from 'node:crypto';
import { writeFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

// ============================================================================================
// 1. Small utilities: seeded PRNG, rounding, ISO dates (UTC arithmetic only, no clock)
// ============================================================================================
function mulberry32(seed) {
  let a = seed | 0;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const rand = mulberry32(20260331);
const ri = (a, b) => a + Math.floor(rand() * (b - a + 1));
const pick = (arr) => arr[Math.floor(rand() * arr.length)];
const sgn = () => (rand() < 0.5 ? -1 : 1);
function shuffle(arr) {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(rand() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}
function pickWeighted(pairs) {
  const total = pairs.reduce((s, [, w]) => s + w, 0);
  let x = rand() * total;
  for (const [v, w] of pairs) { if ((x -= w) < 0) return v; }
  return pairs[pairs.length - 1][0];
}
const rd = (x, dp) => { const f = 10 ** dp; return Math.round(x * f + (x < 0 ? -1e-9 : 1e-9)) / f; };
const r2 = (x) => rd(x, 2);
const trunc = (x, dp) => { const f = 10 ** dp; return Math.trunc(x * f + (x < 0 ? -1e-9 : 1e-9)) / f; };
const EPS = 1e-9;

const D = (s) => new Date(s + 'T00:00:00Z');
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (s, n) => iso(new Date(D(s).getTime() + n * 86400000));
const isBiz = (s) => { const w = D(s).getUTCDay(); return w > 0 && w < 6; };
const addBiz = (s, n) => { let c = s; while (n > 0) { c = addDays(c, 1); if (isBiz(c)) n--; } return c; };
const daysBetween = (a, b) => Math.round((D(b) - D(a)) / 86400000);

// ============================================================================================
// 2. The contract's fixed vocabulary
// ============================================================================================
const AS_OF = '2026-03-31';
const ROLES = {
  Counterparty: 'Counterparty', Instrument: 'Instrument', Asset: 'AssetClass', Side: 'Side',
  TradeDate: 'TradeDate', SettleDate: 'SettleDate', Quantity: 'Quantity', Price: 'Price',
  Currency: 'Currency', NetCash: 'NetCash', Accrued: 'Accrued', Fees: 'Fees',
  Book: 'Book', LifecycleAction: 'EventAction', LifecycleTarget: 'EventTarget',
};
const ROLE_ORDER = Object.keys(ROLES);
const DEC = new Set(['Quantity', 'Price', 'NetCash', 'Accrued', 'Fees']);
const DATE = new Set(['TradeDate', 'SettleDate']);
const PROJ = ROLE_ORDER.filter((r) => !['Book', 'LifecycleAction', 'LifecycleTarget'].includes(r));
const DIFF_ROLES = ['TradeDate', 'SettleDate', 'Quantity', 'Price', 'Currency', 'Accrued', 'Fees', 'NetCash'];
const PAYING = ['BUY', 'PAY_FIXED', 'PAY'];
const CURRENCY_ORDER = ['USD', 'EUR', 'GBP', 'CHF', 'CAD', 'JPY'];

const PASS = { P1: 'P1-exact-id', P2: 'P2-economic', P3: 'P3-allocations', P4: 'P4-fuzzy' };
const PASSES = Object.values(PASS);

// Tolerances as the profile would declare them (declaration order matters; first match wins).
const TOLERANCES = [
  { path: 'trades', field: 'Price', kind: 'AbsoluteOrRelative', absolute: 0.005, relative: 0.0001 },
  { path: 'trades', field: 'NetCash', kind: 'AbsoluteOrRelative', absolute: 1, relative: 0.0001 },
  { path: 'trades', field: 'Fees', kind: 'Relative', absolute: null, relative: 0.02 },
  { path: 'trades', field: 'Accrued', kind: 'Absolute', absolute: 0.5, relative: null },
  { path: 'trades', field: 'SettleDate', kind: 'Days', absolute: null, relative: null }, // kind with no amount
  { path: 'derivs', field: 'Price', kind: 'Relative', absolute: null, relative: 0.0002 },
  { path: 'derivs', field: 'NetCash', kind: 'Absolute', absolute: 5, relative: null },
  { path: 'derivs', field: 'Fees', kind: 'Absolute', absolute: 2, relative: null },
  { path: 'derivs', field: 'Accrued', kind: 'Absolute', absolute: 0.25, relative: null },
  { path: 'allocations', field: 'NetCash', kind: 'Absolute', absolute: 1, relative: null },
  { path: 'allocations', field: 'Price', kind: 'Absolute', absolute: 0.0001, relative: null },
];
const tolOf = (path, field) => TOLERANCES.find((t) => t.path === path && t.field === field);
function tolAmount(decl, a, b) { // spec section 2, DiffFact.ToleranceAmount
  if (!decl) return null;
  const scale = Math.max(Math.abs(a ?? 0), Math.abs(b ?? 0));
  if (decl.kind === 'Absolute') return decl.absolute;
  if (decl.kind === 'Relative') return decl.relative * scale;
  if (decl.kind === 'AbsoluteOrRelative') return Math.max(decl.absolute, decl.relative * scale);
  return null;
}

// ============================================================================================
// 3. Reference data: counterparties, instruments, books
// ============================================================================================
const CPS = ['BAML', 'JPM', 'GS', 'BARC', 'DB', 'NORD'];
const CP_RAW_LEFT = { BAML: 'BAML-LN', JPM: 'JPMS', GS: 'GSI', BARC: 'BARX', DB: 'DBAG', NORD: 'NDEA' };
const CP_RAW_RIGHT = {
  BAML: 'Bank of America Merrill Lynch', JPM: 'J.P. Morgan Securities plc', GS: 'Goldman Sachs International',
  BARC: 'Barclays Bank PLC', DB: 'Deutsche Bank AG', NORD: 'Nordea Bank Abp',
};
const POOL = [ // px = price range, dp = price decimals as booked
  { sym: 'AAPL', asset: 'Equity', ccy: 'USD', px: [185, 215], dp: 2, bbg: 'AAPL US Equity' },
  { sym: 'MSFT', asset: 'Equity', ccy: 'USD', px: [395, 430], dp: 2, bbg: 'MSFT US Equity' },
  { sym: 'SAP', asset: 'Equity', ccy: 'EUR', px: [170, 195], dp: 2, bbg: 'SAP GY Equity' },
  { sym: 'ASML', asset: 'Equity', ccy: 'EUR', px: [850, 980], dp: 2, bbg: 'ASML NA Equity' },
  { sym: 'VOD', asset: 'Equity', ccy: 'GBP', px: [0.68, 0.76], dp: 4, bbg: 'VOD LN Equity', pence: true },
  { sym: 'AZN', asset: 'Equity', ccy: 'GBP', px: [105, 120], dp: 2, bbg: 'AZN LN Equity' },
  { sym: '7203', asset: 'Equity', ccy: 'JPY', px: [2650, 2950], dp: 1, bbg: '7203 JP Equity' },
  { sym: 'ERIC-B', asset: 'Equity', ccy: 'SEK', px: [88, 102], dp: 2, bbg: 'ERICB SS Equity' },
  { sym: 'VOLV-B', asset: 'Equity', ccy: 'SEK', px: [270, 300], dp: 2, bbg: 'VOLVB SS Equity' },
  { sym: 'UST 4.25 2031', asset: 'Bond', ccy: 'USD', px: [97, 101], dp: 3, bbg: 'T 4.25 05/15/31 Govt', mat: '2031-05-15' },
  { sym: 'UST 3.5 2029', asset: 'Bond', ccy: 'USD', px: [96, 100], dp: 3, bbg: 'T 3.5 02/15/29 Govt', mat: '2029-02-15' },
  { sym: 'BUND 2.4 2033', asset: 'Bond', ccy: 'EUR', px: [96, 100], dp: 3, bbg: 'DBR 2.4 10/15/33 Govt', mat: '2033-10-15' },
  { sym: 'OAT 3.0 2034', asset: 'Bond', ccy: 'EUR', px: [95, 99], dp: 3, bbg: 'FRTR 3 05/25/34 Govt', mat: '2034-05-25' },
  { sym: 'GILT 4.25 2032', asset: 'Bond', ccy: 'GBP', px: [98, 102], dp: 3, bbg: 'UKT 4.25 06/07/32 Govt', mat: '2032-06-07' },
  { sym: 'SGB 1.0 2030', asset: 'Bond', ccy: 'SEK', px: [94, 98], dp: 3, bbg: 'SGB 1 11/12/30 Govt', mat: '2030-11-12' },
  { sym: 'JGB 0.8 2034', asset: 'Bond', ccy: 'JPY', px: [93, 97], dp: 3, bbg: 'JGB 0.8 09/20/34 Govt', mat: '2034-09-20' },
  { sym: 'EURUSD', asset: 'FX', ccy: 'USD', px: [1.07, 1.1], dp: 4, bbg: 'EURUSD Curncy' },
  { sym: 'GBPUSD', asset: 'FX', ccy: 'USD', px: [1.26, 1.29], dp: 4, bbg: 'GBPUSD Curncy' },
  { sym: 'USDJPY', asset: 'FX', ccy: 'JPY', px: [148, 152], dp: 3, bbg: 'USDJPY Curncy' },
  { sym: 'EURSEK', asset: 'FX', ccy: 'SEK', px: [11.1, 11.5], dp: 4, bbg: 'EURSEK Curncy' },
  { sym: 'EURGBP', asset: 'FX', ccy: 'GBP', px: [0.84, 0.86], dp: 4, bbg: 'EURGBP Curncy' },
  { sym: 'IRS USD 5Y', asset: 'Swap', ccy: 'USD', px: [0.2, 1.4], dp: 4, bbg: 'USSW5 Curncy' },
  { sym: 'IRS EUR 10Y', asset: 'Swap', ccy: 'EUR', px: [0.2, 1.4], dp: 4, bbg: 'EUSA10 Curncy' },
  { sym: 'IRS GBP 3Y', asset: 'Swap', ccy: 'GBP', px: [0.2, 1.4], dp: 4, bbg: 'BPSW3 Curncy' },
  { sym: 'IRS SEK 5Y', asset: 'Swap', ccy: 'SEK', px: [0.2, 1.4], dp: 4, bbg: 'SKSW5 Curncy' },
];
const poolOf = (sym) => POOL.find((p) => p.sym === sym);
const ISO_CC = { USD: 'US', EUR: 'DE', GBP: 'GB', JPY: 'JP', SEK: 'SE' };
const isinOf = (p) => ISO_CC[p.ccy] + createHash('sha256').update(p.sym).digest('hex').replace(/[a-f]/g, (c) => String('abcdef'.indexOf(c) + 1)).slice(0, 9) + '0';
const BOOKS = { Equity: ['EQ-CORE', 'EQ-INTL'], Bond: ['FI-GOV', 'FI-CREDIT'], FX: ['FX-HEDGE'], Swap: ['RATES-1', 'RATES-2'] };
const LAG = { Equity: 2, Bond: 1, FX: 2, Swap: 2 };
const SIDES_OF = { Swap: ['PAY_FIXED', 'RECEIVE_FIXED'] };
const pathOf = (asset) => (asset === 'Equity' || asset === 'Bond' ? 'trades' : 'derivs');
const TRADE_DAYS = ['2026-03-17', '2026-03-18', '2026-03-19', '2026-03-20', '2026-03-23', '2026-03-24',
  '2026-03-25', '2026-03-26', '2026-03-27', '2026-03-30', '2026-03-31'];

// ============================================================================================
// 4. Trades: a canonical trade, its cash, and how it turns into a row's `values`
// ============================================================================================
const isPaying = (side) => PAYING.includes(side);
function grossOf(t) {
  if (t.asset === 'Equity' || t.asset === 'FX') return t.qty * t.px;
  return (t.qty * t.px) / 100; // Bond face, Swap notional: price per 100
}
function feesOf(t) {
  if (t.asset === 'Equity') return r2(Math.max(1, grossOf(t) * 0.0006));
  if (t.asset === 'Bond') return r2((t.qty / 1e6) * 10);
  if (t.asset === 'Swap') return r2((t.qty / 1e6) * 2.5);
  return undefined; // FX carries no fees
}
function recalc(t, { refees = false } = {}) {
  if (refees && t.fees !== undefined) t.fees = feesOf(t);
  const sign = isPaying(t.side) ? 1 : -1;
  t.netCash = r2(grossOf(t) + (t.accrued ?? 0) + sign * (t.fees ?? 0));
  return t;
}
let tradeSeq = 0;
function newTrade(o = {}) {
  const asset = o.asset ?? pickWeighted([['Equity', 45], ['Bond', 25], ['FX', 18], ['Swap', 12]]);
  let cands = POOL.filter((p) => p.asset === asset && (!o.ccy || p.ccy === o.ccy) && (!o.minPx || p.px[0] >= o.minPx));
  if (o.sym) cands = POOL.filter((p) => p.sym === o.sym);
  const ins = pick(cands);
  const side = o.side ?? pick(SIDES_OF[asset] ?? ['BUY', 'SELL']);
  const td = o.td ?? pick(TRADE_DAYS);
  const mult = ins.ccy === 'JPY' ? 100 : 1;
  const qty = o.qty ?? (asset === 'Equity' ? ri(2, 100) * 50
    : asset === 'Bond' ? ri(1, 20) * 250000 * mult
    : asset === 'FX' ? ri(1, 20) * 500000 : ri(1, 10) * 5000000);
  const px = rd(ins.px[0] + rand() * (ins.px[1] - ins.px[0]), ins.dp);
  const t = {
    seq: ++tradeSeq, cp: o.cp ?? (ins.ccy === 'SEK' && rand() < 0.7 ? 'NORD' : pick(CPS)), sym: ins.sym, asset,
    ccy: ins.ccy, side, td, sd: o.noSettle ? null : addBiz(td, LAG[asset]), qty, px,
    book: o.book ?? (rand() < 0.35 ? pick(BOOKS[asset]) : undefined),
  };
  if (asset === 'Bond' && !o.noAccrued && (o.withAccrued || rand() < 0.45)) t.accrued = r2((qty * 3 / 100) * ri(5, 150) / 360);
  if (asset === 'Swap' && !o.noAccrued && (o.withAccrued || rand() < 0.45)) t.accrued = r2(qty * 0.0001 * ri(1, 9));
  if (!o.noFees && (o.withFees || rand() < 0.3)) t.fees = feesOf(t);
  return recalc(t);
}
function valuesOf(t, side, { book = side === 'left' } = {}) {
  const v = {
    Counterparty: t.cp, Instrument: t.sym, Asset: t.asset, Side: t.side, TradeDate: t.td, SettleDate: t.sd,
    Quantity: t.qty, Price: t.px, Currency: t.ccy, NetCash: t.netCash,
  };
  if (t.accrued !== undefined) v.Accrued = t.accrued;
  if (t.fees !== undefined) v.Fees = t.fees;
  if (book && t.book) v.Book = t.book;
  return ordered(v);
}
function ordered(v) {
  const out = {};
  for (const r of ROLE_ORDER) if (r in v && v[r] !== undefined) out[r] = v[r];
  return out;
}
const project = (v) => { const o = {}; for (const r of PROJ) if (r in v) o[r] = v[r]; return o; };

// ============================================================================================
// 5. Diffs: derive them from the two projections, classify with the tolerance declarations
// ============================================================================================
const sameVal = (a, b) => {
  a = a ?? null; b = b ?? null;
  if (a === '' ) a = null;
  if (b === '') b = null;
  if (typeof a === 'number' && typeof b === 'number') return Math.abs(a - b) < EPS;
  return a === b;
};
function classify(path, field, a, b) { // -> 'WithinTolerance' | 'Break'
  const decl = tolOf(path, field);
  if (!decl) return 'Break';
  if (decl.kind === 'Days') return a && b && Math.abs(daysBetween(a, b)) <= 1 ? 'WithinTolerance' : 'Break';
  const amt = tolAmount(decl, a, b);
  return amt !== null && Math.abs(b - a) <= amt + EPS ? 'WithinTolerance' : 'Break';
}
function buildDiffs(path, L, R, { override = {}, extra = [] } = {}) {
  const differs = (r) => !sameVal(L[r], R[r]);
  const attribution = (r) => {
    if (r === 'NetCash') return ['Quantity', 'Price', 'Fees', 'Accrued', 'Currency'].filter(differs);
    if ((r === 'Fees' || r === 'Accrued') && differs('Quantity')) return ['Quantity'];
    return [];
  };
  const diffs = [];
  for (const role of DIFF_ROLES) {
    if (!differs(role)) continue;
    const a = L[role] ?? null, b = R[role] ?? null;
    const type = DATE.has(role) ? 'date' : DEC.has(role) ? 'decimal' : 'text';
    let deltaKind = 'None', delta = null;
    if (a !== null && b !== null) {
      if (type === 'date') { deltaKind = 'Days'; delta = daysBetween(a, b); }
      else if (type === 'decimal') { deltaKind = 'Number'; delta = rd(b - a, 8); }
    }
    diffs.push({
      field: ROLES[role], role, type, outcome: classify(path, ROLES[role], a, b), ours: a, theirs: b,
      deltaKind, delta, attributedTo: override[role] ?? attribution(role),
    });
  }
  return [...diffs, ...extra];
}
const outcomeOf = (diffs) => (!diffs.length ? 'Exact' : diffs.some((d) => d.outcome === 'Break') ? 'Break' : 'WithinTolerance');
function rootFields(diffs) { // spec section 2, EntryFact.RootFields
  const br = diffs.filter((d) => d.outcome === 'Break');
  const roots = br.filter((d) => d.attributedTo.length === 0).map((d) => d.field);
  return roots.length || !br.length ? roots : [br[0].field];
}

// ============================================================================================
// 6. The world: rows and entries are appended in creation (= engine) order
// ============================================================================================
const W = { entries: [], rows: [], l: 1, r: 1, e: 0, ref: 7400 };
function mkRow(source, values, o = {}) {
  const rowKey = source === 'left' ? `L-${String(W.l++).padStart(4, '0')}` : `R-${String(W.r++).padStart(4, '0')}`;
  const row = { source, rowKey, disposition: o.disposition ?? 'Matched' };
  if (o.suppression) row.suppression = o.suppression;
  if (o.validity && o.validity !== 'Valid') row.validity = o.validity;
  row.entryId = null;
  if (o.itemLead === false) row.itemLead = false;
  row.values = values;
  if (o.diagnostics?.length) row.diagnostics = o.diagnostics;
  if (o.raw) { const { raw, trace } = o.raw(rowKey); row.raw = raw; row.trace = trace; }
  W.rows.push(row);
  return row;
}
function addEntry(e, members = {}, linked = []) {
  const id = createHash('sha256').update(`recon-entry-${++W.e}`).digest('hex').slice(0, 32);
  const left = members.left ?? [], right = members.right ?? [];
  const entry = {
    id, pathId: e.pathId, alignment: e.alignment, ...(e.composition?.length ? { composition: e.composition } : {}), outcome: e.outcome ?? 'NotCompared',
    left: left.map((r) => r.rowKey), right: right.map((r) => r.rowKey), ours: e.ours ?? null, theirs: e.theirs ?? null,
    committedBy: e.committedBy ?? null,
  };
  if (e.trail) entry.trail = e.trail;
  if (e.hints?.length) entry.hints = e.hints;
  if (e.diffs) entry.diffs = e.diffs;
  for (const r of [...left, ...right, ...linked]) r.entryId = id;
  W.entries.push(entry);
  return entry;
}
const anyRow = (source, not = []) => {
  const c = W.rows.filter((r) => r.source === source && !not.includes(r.rowKey) && r.disposition !== 'Suppressed');
  return c.length ? c[Math.floor(rand() * c.length)] : null;
};

// ---- explanation trails ----------------------------------------------------------------------
const cand = (r) => ({ source: r.source, rowKey: r.rowKey });
const att = (pass, verdict, key = null, detail = null, cands = []) => ({ pass, verdict, key, detail, candidates: cands });
const econKey = (v) => [v.Counterparty, v.Instrument, v.Side, v.Quantity].join('|');
const uniqCands = (rows) => { const seen = new Set(); return rows.filter(Boolean).filter((r) => !seen.has(r.rowKey) && seen.add(r.rowKey)).map(cand); };
const variant = { P2: 0, P4: 0 };
function committedTrail(pass, Ls, Rs) {
  const both = uniqCands(Rs);
  const v = Ls[0]?.values ?? Rs[0].values;
  if (pass === PASS.P1) return [att(PASS.P1, 'Committed', `ref:TR-${W.ref++}`, null, both)];
  if (pass === PASS.P3) return [att(PASS.P3, 'Committed', `block:BK-${W.ref++}`, 'allocations under one block', both)];
  const n = variant[pass === PASS.P2 ? 'P2' : 'P4']++ % 3;
  if (pass === PASS.P2) {
    if (n === 0) return [att(PASS.P1, 'NoKey', null, 'no trade ref'), att(PASS.P2, 'Committed', econKey(v), null, both)];
    if (n === 1) return [att(PASS.P1, 'NoCandidate', `ref:TR-${W.ref++}`, 'ref not on other side'), att(PASS.P2, 'Committed', econKey(v), null, both)];
    return [att(PASS.P1, 'NoKey', null, 'no trade ref'),
      att(PASS.P2, 'Contest', econKey(v), 'two rows share the key', uniqCands([...Ls, ...Rs, anyRow('right', Rs.map((r) => r.rowKey))])),
      att(PASS.P2, 'Committed', econKey(v), null, both)];
  }
  // P4 fuzzy: reached after P1 and P2 fell through
  const score = (0.9 + rand() * 0.09).toFixed(2);
  const fuzzy = att(PASS.P4, 'Committed', null, `fuzzy score ${score}`, both);
  if (n === 0) return [att(PASS.P1, 'NoKey', null, 'no trade ref'), att(PASS.P2, 'NoCandidate', econKey(v), 'no row with the same key'), fuzzy];
  if (n === 1) return [att(PASS.P1, 'NoKey', null, 'no trade ref'),
    att(PASS.P2, 'Vetoed', econKey(v), 'veto: settle dates 3+ days apart', uniqCands(Rs)), fuzzy];
  return [att(PASS.P1, 'NoCandidate', `ref:TR-${W.ref++}`, 'ref not on other side'),
    att(PASS.P2, 'Contest', econKey(v), 'two candidates tied', uniqCands([...Rs, anyRow('right', Rs.map((r) => r.rowKey))])), fuzzy];
}
const hintsFor = (pass) => (pass === PASS.P4 ? [{ code: 'FuzzyScore', detail: 'matched on similarity' }] : []);

// ---- raw columns, normalization trace (BO3) ----------------------------------------------------
const RAW_COLS = {
  left: ['TradeRef', 'Cpty', 'Security', 'B/S', 'TradeDt', 'SettleDt', 'Qty', 'Px', 'Ccy', 'NetAmt', 'AccrInt', 'Comm', 'Portfolio', 'EventType', 'OrigRef'],
  right: ['ConfirmId', 'Counterparty Name', 'Security ID', 'Direction', 'Trade Date', 'Value Date', 'Nominal', 'Price', 'Currency', 'Settlement Amount', 'Accrued Interest', 'Brokerage', 'Account', 'Action', 'Linked Id'],
};
const MON = ['Jan', 'Feb', 'Mar', 'Apr'];
const dmy = (s) => (s ? `${s.slice(8)}/${s.slice(5, 7)}/${s.slice(0, 4)}` : null);
const dMonY = (s) => (s ? `${s.slice(8)}-${MON[+s.slice(5, 7) - 1]}-${s.slice(0, 4)}` : null);
function fmtNum(n, dp) {
  if (n === null || n === undefined) return null;
  const [i, f] = Math.abs(n).toFixed(dp).split('.');
  return (n < 0 ? '-' : '') + i.replace(/\B(?=(\d{3})+(?!\d))/g, ',') + (f ? '.' + f : '');
}
function rawFor(side, v) {
  return (rowKey) => {
    const p = poolOf(v.Instrument);
    const L = side === 'left';
    const bond = v.Asset === 'Bond', paying = isPaying(v.Side);
    const sideTxt = L ? (v.Asset === 'Swap' ? (paying ? 'PF' : 'RF') : paying ? 'B' : 'S')
      : (v.Asset === 'Swap' ? (paying ? 'Pay Fixed' : 'Receive Fixed') : paying ? 'Purchase' : 'Sale');
    const dateFn = L ? dmy : dMonY;
    const scaleBond = !L && bond && rowKey.endsWith('1') || (!L && bond && rowKey.endsWith('5'));
    let priceRaw = String(v.Price), priceRule = 'price.parse';
    if (!L && p?.pence) { priceRaw = fmtNum(v.Price * 100, 2); priceRule = 'price.scale'; }
    else if (scaleBond) { priceRaw = String(rd(v.Price / 100, 5)); priceRule = 'price.scale'; }
    const ccyRaw = !L && rowKey.endsWith('3') ? v.Currency.toLowerCase() : v.Currency;
    const cashRaw = L ? (paying ? '-' : '') + fmtNum(v.NetCash, 2) : fmtNum(v.NetCash, 2);
    const raw = L ? {
      TradeRef: rowKey, Cpty: CP_RAW_LEFT[v.Counterparty], Security: p?.bbg ?? v.Instrument, 'B/S': sideTxt,
      TradeDt: dateFn(v.TradeDate), SettleDt: dateFn(v.SettleDate), Qty: fmtNum(v.Quantity, 0), Px: priceRaw, Ccy: ccyRaw,
      NetAmt: cashRaw, AccrInt: v.Accrued === undefined ? null : fmtNum(v.Accrued, 2), Comm: v.Fees === undefined ? null : fmtNum(v.Fees, 2),
      Portfolio: v.Book ?? null,
    } : {
      ConfirmId: rowKey, 'Counterparty Name': CP_RAW_RIGHT[v.Counterparty], 'Security ID': p ? isinOf(p) : v.Instrument, Direction: sideTxt,
      'Trade Date': dateFn(v.TradeDate), 'Value Date': dateFn(v.SettleDate), Nominal: fmtNum(v.Quantity, 0), Price: priceRaw, Currency: ccyRaw,
      'Settlement Amount': cashRaw, 'Accrued Interest': v.Accrued === undefined ? null : fmtNum(v.Accrued, 2), Brokerage: v.Fees === undefined ? null : fmtNum(v.Fees, 2),
      Account: `ACC-${(v.Counterparty.charCodeAt(0) * 37 + v.Quantity) % 9000 + 1000}`
    };
    const step = (ruleId, field, rawV, canon) => ({ ruleId, field: ROLES[field] ?? field, raw: rawV, canonical: canon === null ? null : String(canon) });
    const rawCol = (a, b) => raw[L ? a : b];
    const trace = [
      step(L ? 'date.parse.dmy' : 'date.parse.dMonY', 'TradeDate', rawCol('TradeDt', 'Trade Date'), v.TradeDate),
      step('counterparty.alias', 'Counterparty', rawCol('Cpty', 'Counterparty Name'), v.Counterparty),
      step('symbol.map', 'Instrument', rawCol('Security', 'Security ID'), v.Instrument),
      step('side.map', 'Side', rawCol('B/S', 'Direction'), v.Side),
      ...(priceRule === 'price.scale' ? [step(priceRule, 'Price', rawCol('Px', 'Price'), v.Price)] : []),
      ...(ccyRaw !== v.Currency ? [step('currency.upper', 'Currency', ccyRaw, v.Currency)] : []),
      ...(L ? [step('cash.sign', 'NetCash', rawCol('NetAmt', 'Settlement Amount'), v.NetCash)] : []),
    ].filter((s) => s.raw !== null);
    return { raw, trace };
  };
}

// ============================================================================================
// 7. Building blocks for the story: one-to-one pairs
// ============================================================================================
function rowOpts(cfg, side, v) {
  const o = {};
  if (cfg.raw) o.raw = rawFor(side, v);
  if (cfg.warn) { o.validity = 'ValidWithWarnings'; o.diagnostics = [side === 'left' ? 'DefaultedFees' : 'PriceRounded']; }
  return o;
}
function pairPlan(cfg) { // cfg: {name, pass, expect, roots?, check?, make, raw?, warn?}
  return () => {
    let spec = null;
    for (let k = 0; k < 300 && !spec; k++) {
      const s = cfg.make();
      const path = pathOf(s.Lt.asset);
      const Lv = valuesOf(s.Lt, 'left'), Rv = valuesOf(s.Rt, 'right');
      const diffs = buildDiffs(path, Lv, Rv, s.opts);
      if (outcomeOf(diffs) !== cfg.expect) continue;
      if (cfg.roots && [...rootFields(diffs)].sort().join() !== [...cfg.roots].sort().join()) continue;
      if (cfg.check && !cfg.check(diffs, s)) continue;
      spec = { ...s, path, Lv, Rv, diffs };
    }
    if (!spec) throw new Error('scenario cannot be satisfied: ' + cfg.name);
    const L = mkRow('left', spec.Lv, rowOpts(cfg, 'left', spec.Lv));
    const R = mkRow('right', spec.Rv, rowOpts(cfg, 'right', spec.Rv));
    addEntry({
      pathId: spec.path, alignment: 'Matched', composition: ['OneToOne'], outcome: cfg.expect, ours: project(spec.Lv), theirs: project(spec.Rv),
      committedBy: cfg.pass, trail: committedTrail(cfg.pass, [L], [R]), hints: hintsFor(cfg.pass), diffs: spec.diffs,
    }, { left: [L], right: [R] });
  };
}
const same = (o = {}) => () => { const Lt = newTrade(o); return { Lt, Rt: { ...Lt }, opts: {} }; };
const mutate = (o, f) => () => {
  const Lt = newTrade(typeof o === 'function' ? o() : o);
  const Rt = f({ ...Lt }, Lt);
  return { Lt, Rt, opts: Rt.opts ?? {} };
};

// ---- within-tolerance recipes (frac = share of the tolerance used) ------------------------------
const ASSETS_T = ['Equity', 'Bond'];
const wtPrice = (frac, assets, { cash = true, minPx } = {}) => mutate(() => ({ asset: pick(assets), minPx }), (R, L) => {
  const amt = tolAmount(tolOf(pathOf(L.asset), 'Price'), L.px, L.px);
  R.px = rd(L.px + sgn() * Math.max(trunc(frac * amt, 5), 0.0001), 5);
  return cash ? recalc(R) : R;
});
const wtFees = (frac, assets) => mutate(() => ({ asset: pick(assets), withFees: true }), (R, L) => {
  const amt = L.asset === 'Swap' ? 2 : 0.02 * L.fees;
  R.fees = r2(L.fees + sgn() * Math.max(trunc(frac * amt, 2), 0.01));
  return recalc(R);
});
const wtAccrued = (frac, assets) => mutate(() => ({ asset: pick(assets), withAccrued: true }), (R, L) => {
  const amt = tolOf(pathOf(L.asset), 'Accrued').absolute;
  R.accrued = r2(L.accrued + sgn() * trunc(frac * amt, 2));
  return recalc(R);
});
const wtNetCash = (frac, assets) => mutate(() => ({ asset: pick(assets) }), (R, L) => {
  const amt = tolAmount(tolOf(pathOf(L.asset), 'NetCash'), L.netCash, L.netCash);
  R.netCash = r2(L.netCash + sgn() * trunc(frac * amt, 2));
  return R;
});
const wtSettle = (assets) => mutate(() => ({ asset: pick(assets) }), (R, L) => { R.sd = addBiz(L.sd, 1); return R; });
const wtPriceAndFees = () => mutate({ asset: 'Equity', minPx: 100, withFees: true }, (R, L) => {
  R.px = rd(L.px + 0.012, 4);
  R.fees = r2(L.fees + 0.1);
  return recalc(R);
});

// ---- break recipes ------------------------------------------------------------------------------
const bkPrice = (assets) => mutate(() => ({ asset: pick(assets) }), (R, L) => {
  const p = poolOf(L.sym);
  R.px = rd(L.px * (1 + sgn() * ri(5, 40) / 1000), p.dp);
  return recalc(R);
});
const bkQty = (assets) => mutate(() => ({ asset: pick(assets) }), (R, L) => {
  const unit = L.asset === 'Equity' ? 50 : L.asset === 'Bond' ? 250000 : L.asset === 'FX' ? 500000 : 5000000;
  R.qty = Math.max(unit, Math.round((L.qty * 0.9) / unit) * unit);
  return recalc(R, { refees: true });
});
const bkSettle = (assets, n = ri(2, 4)) => mutate(() => ({ asset: pick(assets) }), (R, L) => { R.sd = addBiz(L.sd, n); return R; });
const bkSettleNull = () => mutate({ asset: 'Bond' }, (R) => { R.sd = null; return R; });
const bkFees = (assets) => mutate(() => ({ asset: pick(assets), withFees: true }), (R, L) => { R.fees = r2(L.fees + Math.max(5, 0.1 * L.fees) + 0.5); return recalc(R); });
const bkAccrued = (assets) => mutate(() => ({ asset: pick(assets), withAccrued: true }), (R, L) => { R.accrued = r2(L.accrued + ri(8, 40) + 0.35); return recalc(R); });
const bkNetCash = (assets, delta) => mutate(() => ({ asset: pick(assets) }), (R, L) => { R.netCash = r2(L.netCash + delta); return R; });
const USD_PER = { USD: 1, EUR: 1.083, GBP: 1.268, JPY: 0.0067, SEK: 0.095 };
const ALT_CCY = { USD: 'EUR', EUR: 'GBP', GBP: 'USD', JPY: 'USD', SEK: 'EUR' };
const bkCurrency = (assets, to) => mutate(() => ({ asset: pick(assets) }), (R, L) => {
  const alt = to && to !== L.ccy ? to : ALT_CCY[L.ccy];
  R.ccy = alt;
  R.netCash = r2((L.netCash * USD_PER[L.ccy]) / USD_PER[alt]);
  return R;
});
const bkTradeDate = (td, rtd) => mutate({ asset: 'Equity', td }, (R, L) => { R.td = rtd ?? addBiz(L.td, 1); return R; });
const bkMaturity = () => mutate({ asset: 'Bond' }, (R, L) => {
  const p = poolOf(L.sym);
  R.opts = { extra: [{ field: 'MaturityDate', role: null, type: 'date', outcome: 'Break', ours: p.mat, theirs: addDays(p.mat, 2), deltaKind: 'Days', delta: 2, attributedTo: [] }] };
  return R;
});
const bkPriceAndSettle = () => mutate({ asset: 'Equity' }, (R, L) => { R.px = rd(L.px * 1.02, 2); R.sd = addBiz(L.sd, 3); return recalc(R); });
const bkQtyAndSettle = () => mutate({ asset: 'Bond' }, (R, L) => { R.qty = L.qty - 250000; R.sd = addBiz(L.sd, 2); return recalc(R, { refees: true }); });
const bkAllAttributed = () => mutate({ asset: 'Equity', minPx: 100 }, (R, L) => {
  R.px = rd(L.px * 1.015, 2);
  recalc(R);
  R.opts = { override: { Price: ['NetCash'], NetCash: ['Price'] } };
  return R;
});
const bkSettleAndAccruedWT = () => mutate({ asset: 'Bond', withAccrued: true }, (R, L) => { R.sd = addBiz(L.sd, 3); R.accrued = r2(L.accrued + 0.3); return recalc(R); });
const bkSettleAndFeesWT = () => mutate({ asset: 'Equity', withFees: true }, (R, L) => { R.sd = addBiz(L.sd, 2); R.fees = r2(L.fees + 0.1); return recalc(R); });

// ============================================================================================
// 8. Lifecycle chains, blocks and allocations, packages
// ============================================================================================
const lc = (v, action, target = null) => ordered({ ...v, LifecycleAction: action, LifecycleTarget: target });
const other = (s) => (s === 'left' ? 'right' : 'left');

// kind: 'corr' (NEW then CORR), 'rebook' (NEW, CANC, NEW). side: where the chain lives. both: chain on both sides.
function lifecycleChain({ kind, side, pass, both = false, breakIt = false }) {
  const t0 = newTrade({ asset: 'Equity' });
  const t1 = { ...t0 };
  if (kind === 'corr') { t1.px = rd(t0.px * 1.004, 2); } else { t1.qty = t0.qty + 100; }
  recalc(t1, { refees: true });
  const Ot = { ...t1 }; // the other side, matches the survivor
  if (breakIt) { Ot.px = rd(t1.px + 0.35, 2); recalc(Ot); }
  const build = (s, survivorTrade) => {
    const v0 = valuesOf(t0, s), v1 = valuesOf(survivorTrade, s);
    const first = mkRow(s, lc(v0, 'NEW'), { disposition: 'Suppressed', suppression: 'Superseded' });
    const rows = [first];
    let live;
    if (kind === 'corr') {
      live = mkRow(s, lc(v1, 'CORR', first.rowKey), {});
      rows.push(live);
    } else {
      const canc = mkRow(s, lc(v0, 'CANC', first.rowKey), { disposition: 'Suppressed', suppression: 'CancelEvent' });
      live = mkRow(s, lc(v1, 'NEW'), {});
      rows.push(canc, live);
    }
    return { rows, live, linked: rows.filter((r) => r !== live) };
  };
  const A = build(side, t1);
  let B;
  if (both) B = build(other(side), Ot);
  else B = { live: mkRow(other(side), valuesOf(Ot, other(side)), {}), linked: [] };
  const Lrow = side === 'left' ? A.live : B.live, Rrow = side === 'left' ? B.live : A.live;
  const path = pathOf('Equity');
  const diffs = buildDiffs(path, Lrow.values, Rrow.values);
  const outcome = outcomeOf(diffs);
  addEntry({
    pathId: path, alignment: 'Matched', composition: ['OneToOne', 'LifecycleCollapsed'], outcome,
    ours: project(Lrow.values), theirs: project(Rrow.values), committedBy: pass, trail: committedTrail(pass, [Lrow], [Rrow]),
    hints: [{ code: 'LifecycleCollapsed', detail: 'chain collapsed to its live version' }], diffs,
  }, { left: [Lrow], right: [Rrow] }, [...A.linked, ...(B.linked ?? [])]);
}
function fullyCancelled(side) { // NEW then CANC with no survivor: both suppressed, no entry
  const v = valuesOf(newTrade(), side);
  const first = mkRow(side, lc(v, 'NEW'), { disposition: 'Suppressed', suppression: 'FullyCancelled' });
  mkRow(side, lc(v, 'CANC', first.rowKey), { disposition: 'Suppressed', suppression: 'CancelEvent' });
}

const BOOKS_ALLOC = ['PF-ALPHA', 'PF-BETA', 'PF-GAMMA', 'PF-DELTA'];
// dir 'M2O': allocations on ours (left), block on theirs. 'O2M': block on ours, allocations on theirs.
// mode: 'equal' | 'rounding' (cash off by 3 cents) | 'short' (last allocation smaller: Quantity break)
function allocationBlock({ dir, n, mode, asset, cancelledAlloc = false, pass = PASS.P3 }) {
  const t = newTrade({ asset, noFees: true, noAccrued: true, qty: asset === 'Bond' ? 4000000 : ri(40, 120) * 50 });
  const unit = asset === 'Bond' ? 250000 : 50;
  const quantities = [];
  let left = t.qty;
  for (let i = 0; i < n - 1; i++) { const q = Math.floor((t.qty / n) * (0.6 + rand() * 0.8) / unit) * unit; quantities.push(q); left -= q; }
  quantities.push(left);
  if (mode === 'short') quantities[n - 1] -= unit * 2;
  const allocTrades = quantities.map((q, i) => {
    const a = { ...t, qty: q, book: BOOKS_ALLOC[i] };
    a.netCash = i < n - 1 ? r2(t.netCash * q / t.qty) : r2(t.netCash * q / t.qty);
    return a;
  });
  if (mode === 'equal') allocTrades[n - 1].netCash = r2(t.netCash - allocTrades.slice(0, n - 1).reduce((s, a) => s + a.netCash, 0));
  if (mode === 'rounding') allocTrades[n - 1].netCash = r2(t.netCash - allocTrades.slice(0, n - 1).reduce((s, a) => s + a.netCash, 0) + 0.03);
  const aSide = dir === 'M2O' ? 'left' : 'right', bSide = other(aSide);
  const linked = [];
  const allocRows = [];
  if (cancelledAlloc) { // one allocation was booked, then cancelled
    const ghost = { ...allocTrades[0], qty: unit * 4, netCash: r2(t.netCash * unit * 4 / t.qty), book: 'PF-OMEGA' };
    const gNew = mkRow(aSide, lc(valuesOf(ghost, aSide, { book: true }), 'NEW'), { disposition: 'Suppressed', suppression: 'Superseded' });
    const gCanc = mkRow(aSide, lc(valuesOf(ghost, aSide, { book: true }), 'CANC', gNew.rowKey), { disposition: 'Suppressed', suppression: 'CancelEvent' });
    linked.push(gNew, gCanc);
  }
  allocTrades.forEach((a) => {
    const v = valuesOf(a, aSide, { book: true });
    allocRows.push(mkRow(aSide, cancelledAlloc ? lc(v, 'NEW') : v, {}));
  });
  const blockT = { ...t, book: dir === 'O2M' ? 'OMNIBUS' : undefined };
  const block = mkRow(bSide, valuesOf(blockT, bSide, { book: dir === 'O2M' }), {});
  const first = project(allocRows[0].values);
  const agg = { ...first, Quantity: allocRows.reduce((s, r) => s + r.values.Quantity, 0), NetCash: r2(allocRows.reduce((s, r) => s + r.values.NetCash, 0)) };
  const blockProj = project(block.values);
  const [oursP, theirsP] = dir === 'M2O' ? [agg, blockProj] : [blockProj, agg];
  const diffs = buildDiffs('allocations', oursP, theirsP);
  addEntry({
    pathId: 'allocations', alignment: 'Matched', composition: [dir === 'M2O' ? 'ManyToOne' : 'OneToMany', ...(cancelledAlloc ? ['LifecycleCollapsed'] : [])],
    outcome: outcomeOf(diffs), ours: oursP, theirs: theirsP, committedBy: pass, trail: committedTrail(pass, dir === 'M2O' ? allocRows : [block], dir === 'M2O' ? [block] : allocRows),
    hints: mode === 'short' ? [{ code: 'AllocationShortfall', detail: 'allocations do not sum to the block' }] : [], diffs,
  }, dir === 'M2O' ? { left: allocRows, right: [block] } : { left: [block], right: allocRows }, linked);
}

// A consolidated multi-leg item: every leg row carries the item's values, only the first leg leads.
function packageEntry({ legs, legSide, mutateTheirs, pass = PASS.P2 }) {
  const t = newTrade({ asset: pick(['Swap', 'FX']), withFees: true });
  const legRows = Array.from({ length: legs }, (_, i) => mkRow(legSide, valuesOf(t, legSide), { itemLead: i === 0 }));
  const Ot = mutateTheirs ? mutateTheirs({ ...t }) : { ...t };
  const oneRow = mkRow(other(legSide), valuesOf(Ot, other(legSide)), {});
  const [lrows, rrows] = legSide === 'left' ? [legRows, [oneRow]] : [[oneRow], legRows];
  const diffs = buildDiffs(pathOf(t.asset), lrows[0].values, rrows[0].values);
  addEntry({
    pathId: pathOf(t.asset), alignment: 'Matched', composition: [legSide === 'left' ? 'ManyToOne' : 'OneToMany', 'Package'],
    outcome: outcomeOf(diffs), ours: project(lrows[0].values), theirs: project(rrows[0].values), committedBy: pass,
    trail: committedTrail(pass, lrows, rrows), hints: [{ code: 'PackageConsolidated', detail: `${legs} legs consolidated into one item` }], diffs,
  }, { left: lrows, right: rrows });
}

// ============================================================================================
// 9. Open entries and rows without an entry
// ============================================================================================
const REFDATA_WARN = ['UnknownParty', 'UnknownSymbol', 'MissingKey'];
function openRow(side, { routed = true, diag = [], patch = {}, trade = {}, trailKind = 0, nearest = null } = {}) {
  const t = newTrade(trade);
  const v = ordered({ ...valuesOf(t, side), ...patch });
  const row = mkRow(side, v, {
    disposition: side === 'left' ? 'LeftOnly' : 'RightOnly', diagnostics: diag, validity: diag.length ? 'ValidWithWarnings' : 'Valid',
  });
  const near = nearest ?? anyRow(other(side));
  const hints = near ? [{ code: 'NearestCandidate', detail: `${near.rowKey} is the closest ${other(side)} row` }] : [];
  const trail = !routed ? [att('route', 'NoCandidate', null, 'Unrouted', [])]
    : trailKind === 0 ? [att(PASS.P2, 'NoCandidate', econKey(v), 'no row with the same key'), att(PASS.P4, 'NoCandidate', null, 'nothing in the 2 day window')]
    : [att(PASS.P2, 'Vetoed', econKey(v), 'veto: quantity differs over 5%', near ? [cand(near)] : []), att(PASS.P4, 'NoCandidate', null, 'all candidates below threshold')];
  addEntry({
    pathId: routed ? pathOf(v.Asset) : '', alignment: side === 'left' ? 'LeftOnly' : 'RightOnly', composition: [], outcome: 'NotCompared',
    ours: side === 'left' ? v : null, theirs: side === 'right' ? v : null, committedBy: routed ? PASS.P4 : null, trail: routed ? trail : trail,
    hints: routed ? hints : [],
  }, { [side]: [row] });
  return row;
}
function nearMissPair() { // a left-only and a right-only entry that nearly match each other
  const tl = newTrade(), tr = { ...tl };
  tr.qty = Math.round((tl.qty * 1.5) / 50) * 50;
  recalc(tr, { refees: true });
  tr.sd = tl.sd ? addBiz(tl.sd, 2) : null;
  const L = mkRow('left', valuesOf(tl, 'left'), { disposition: 'LeftOnly' });
  const R = mkRow('right', valuesOf(tr, 'right'), { disposition: 'RightOnly' });
  for (const [row, opp, side] of [[L, R, 'left'], [R, L, 'right']]) {
    addEntry({
      pathId: pathOf(row.values.Asset), alignment: side === 'left' ? 'LeftOnly' : 'RightOnly', composition: [], outcome: 'NotCompared',
      ours: side === 'left' ? row.values : null, theirs: side === 'right' ? row.values : null, committedBy: PASS.P4,
      trail: [att(PASS.P2, 'Vetoed', econKey(row.values), 'veto: quantity differs over 5%', [cand(opp)]), att(PASS.P4, 'NoCandidate', null, 'best score 0.61 < 0.85', [cand(opp)])],
      hints: [{ code: 'NearestCandidate', detail: `${opp.rowKey}: same key fields, quantity differs 50%` }],
    }, { [side]: [row] });
  }
}
function orphan(side, disposition, { diag = [], patch = {}, invalid = false, trade = {} } = {}) {
  const v = ordered({ ...valuesOf(newTrade(trade), side), ...patch });
  mkRow(side, v, { disposition, diagnostics: diag, validity: invalid ? 'Invalid' : diag.length ? 'ValidWithWarnings' : 'Valid' });
}

function ambiguous({ nL, nR, pass = PASS.P2 }) {
  const t = newTrade({ asset: pick(['Equity', 'Bond']) });
  const lrows = Array.from({ length: nL }, () => mkRow('left', valuesOf(t, 'left'), { disposition: 'Ambiguous' }));
  const rrows = Array.from({ length: nR }, () => mkRow('right', valuesOf(t, 'right'), { disposition: 'Ambiguous' }));
  const all = [...lrows, ...rrows];
  addEntry({
    pathId: pathOf(t.asset), alignment: 'Ambiguous', composition: [], outcome: 'NotCompared', ours: lrows[0].values, theirs: rrows[0].values, committedBy: pass,
    trail: [att(PASS.P1, 'NoKey', null, 'no trade ref'), att(pass, 'Contest', econKey(t.cp ? valuesOf(t, 'left') : {}), `${all.length} rows tie`, uniqCands(all))],
    hints: [{ code: 'TiedCandidates', detail: `${all.map((r) => r.rowKey).join(', ')} share every key field` }],
  }, { left: lrows, right: rrows });
}
function searchIncomplete({ nL, nR }) {
  const t = newTrade({ asset: pick(['Equity', 'Bond']) });
  const lrows = Array.from({ length: nL }, () => mkRow('left', valuesOf(t, 'left'), { disposition: 'SearchIncomplete' }));
  const rrows = Array.from({ length: nR }, (_, i) => mkRow('right', valuesOf({ ...t, sd: i ? addBiz(t.sd ?? t.td, 1) : t.sd }, 'right'), { disposition: 'SearchIncomplete' }));
  addEntry({
    pathId: pathOf(t.asset), alignment: 'SearchIncomplete', composition: [], outcome: 'NotCompared', ours: lrows[0].values, theirs: rrows[0].values, committedBy: PASS.P4,
    trail: [att(PASS.P1, 'NoKey', null, 'no trade ref'), att(PASS.P4, 'SearchIncomplete', null, 'candidate limit of 25 reached', uniqCands([...lrows, ...rrows]))],
    hints: [{ code: 'RaiseSearchLimit', detail: 'raise the candidate limit' }, { code: 'NearestCandidate', detail: `${rrows[0].rowKey} looked closest` }],
  }, { left: lrows, right: rrows });
}

// ============================================================================================
// 10. The story: a plan of scenarios, shuffled with the seeded PRNG so categories interleave
// ============================================================================================
const N = { exactP1: 66, exactP2: 24, exactP4: 5 };
const plans = [];
const add = (fn) => plans.push(fn);
const rep = (n, f) => { for (let i = 0; i < n; i++) add(f(i)); };
const exact = (pass, i, raw, extra = {}) => pairPlan({ name: 'exact', pass, expect: 'Exact', make: same(), raw: i < raw, warn: i % 19 === 7, ...extra });

// Matched, Exact, one-to-one, committed by different passes (the first few carry raw data for BO3).
rep(N.exactP1, (i) => exact(PASS.P1, i, 4));
rep(N.exactP2, (i) => exact(PASS.P2, i, 2));
rep(N.exactP4, (i) => exact(PASS.P4, i, 1));
add(pairPlan({ name: 'exact, no settle on either side', pass: PASS.P1, expect: 'Exact', make: same({ noSettle: true, asset: 'Equity' }) }));
add(pairPlan({ name: 'exact, weekend trade date', pass: PASS.P2, expect: 'Exact', make: same({ td: '2026-03-21', asset: 'FX' }) }));

// Within tolerance: [name, pass, factory, raw?]
const WT = [
  ['price, 97% used, cash follows', PASS.P1, wtPrice(0.97, ['Equity'], { minPx: 100 }), true],
  ['price, 45% used', PASS.P2, wtPrice(0.45, ASSETS_T, { minPx: 90 }), true],
  ['bond price, 70% used', PASS.P1, wtPrice(0.7, ['Bond'])],
  ['price, 99% used, cash unchanged', PASS.P4, wtPrice(0.99, ASSETS_T, { cash: false }), true],
  ['price, 20% used, cash unchanged', PASS.P2, wtPrice(0.2, ASSETS_T, { cash: false })],
  ['fx price, 90% used', PASS.P1, wtPrice(0.9, ['FX'], { cash: false })],
  ['swap price, 50% used', PASS.P2, wtPrice(0.5, ['Swap'], { cash: false })],
  ['fees, 60% used', PASS.P1, wtFees(0.6, ['Equity'])],
  ['fees, 98% used', PASS.P2, wtFees(0.98, ['Equity'])],
  ['bond fees, 30% used', PASS.P1, wtFees(0.3, ['Bond'])],
  ['swap fees, 80% used', PASS.P2, wtFees(0.8, ['Swap'])],
  ['accrued, 95% used', PASS.P1, wtAccrued(0.95, ['Bond'])],
  ['accrued, exactly on the boundary', PASS.P2, wtAccrued(1, ['Bond']), true],
  ['swap accrued, 90% used', PASS.P4, wtAccrued(0.9, ['Swap'])],
  ['net cash, 50% used', PASS.P1, wtNetCash(0.5, ['Equity'])],
  ['net cash, 96% used', PASS.P2, wtNetCash(0.96, ['Bond'])],
  ['fx net cash, 99% used', PASS.P1, wtNetCash(0.99, ['FX'])],
  ['swap net cash, exactly on the boundary', PASS.P2, wtNetCash(1, ['Swap'])],
  ['swap net cash, 30% used', PASS.P4, wtNetCash(0.3, ['Swap'])],
  ['settle date one day apart (no amount)', PASS.P1, wtSettle(['Equity'])],
  ['bond settle date one day apart (no amount)', PASS.P2, wtSettle(['Bond'])],
  ['price and fees both within tolerance', PASS.P1, wtPriceAndFees()],
  ['net cash, negative, 75% used', PASS.P4, wtNetCash(0.75, ['Equity'])],
];
WT.forEach(([name, pass, make, raw]) => add(pairPlan({ name, pass, expect: 'WithinTolerance', make, raw: !!raw })));

// Breaks: [name, pass, factory, expected root fields, raw?]
const rootsOf = (...r) => r;
const BREAKS = [
  ['price (equity)', PASS.P1, bkPrice(['Equity']), rootsOf('Price'), true],
  ['price (bond)', PASS.P2, bkPrice(['Bond']), rootsOf('Price'), true],
  ['price (fx)', PASS.P1, bkPrice(['FX']), rootsOf('Price')],
  ['price (swap)', PASS.P2, bkPrice(['Swap']), rootsOf('Price')],
  ['price (equity, again)', PASS.P4, bkPrice(['Equity']), rootsOf('Price')],
  ['price (bond, again)', PASS.P1, bkPrice(['Bond']), rootsOf('Price')],
  ['quantity (equity)', PASS.P1, bkQty(['Equity']), rootsOf('Quantity')],
  ['quantity (bond)', PASS.P2, bkQty(['Bond']), rootsOf('Quantity')],
  ['quantity (fx)', PASS.P4, bkQty(['FX']), rootsOf('Quantity')],
  ['quantity (equity, again)', PASS.P1, bkQty(['Equity']), rootsOf('Quantity')],
  ['settle date (equity)', PASS.P1, bkSettle(['Equity']), rootsOf('SettleDate')],
  ['settle date (swap)', PASS.P2, bkSettle(['Swap']), rootsOf('SettleDate')],
  ['settle date (fx)', PASS.P1, bkSettle(['FX']), rootsOf('SettleDate')],
  ['settle date (bond)', PASS.P4, bkSettle(['Bond']), rootsOf('SettleDate')],
  ['settle date missing on theirs', PASS.P2, bkSettleNull(), rootsOf('SettleDate')],
  ['fees (equity)', PASS.P1, bkFees(['Equity']), rootsOf('Fees')],
  ['fees (swap)', PASS.P2, bkFees(['Swap']), rootsOf('Fees')],
  ['fees (bond)', PASS.P4, bkFees(['Bond']), rootsOf('Fees')],
  ['accrued (bond)', PASS.P1, bkAccrued(['Bond']), rootsOf('Accrued')],
  ['accrued (swap)', PASS.P2, bkAccrued(['Swap']), rootsOf('Accrued')],
  ['accrued (bond, again)', PASS.P4, bkAccrued(['Bond']), rootsOf('Accrued')],
  ['net cash only (+118.40)', PASS.P1, bkNetCash(['Equity', 'Bond'], 118.4), rootsOf('NetCash')],
  ['net cash only (-57.25, derivative)', PASS.P2, bkNetCash(['FX', 'Swap'], -57.25), rootsOf('NetCash')],
  ['net cash only (+342.10)', PASS.P4, bkNetCash(['Equity'], 342.1), rootsOf('NetCash')],
  ['currency split (usd/eur etc)', PASS.P1, bkCurrency(['Equity']), rootsOf('Currency')],
  ['currency split (bond)', PASS.P2, bkCurrency(['Bond']), rootsOf('Currency')],
  ['currency split (to SEK)', PASS.P1, bkCurrency(['Equity'], 'SEK'), rootsOf('Currency')],
  ['currency split (fx)', PASS.P4, bkCurrency(['FX']), rootsOf('Currency')],
  ['trade date next business day', PASS.P2, bkTradeDate('2026-03-23'), rootsOf('TradeDate')],
  ['trade date lands on a Saturday', PASS.P1, bkTradeDate('2026-03-20', '2026-03-21'), rootsOf('TradeDate')],
  ['maturity date (no role)', PASS.P2, bkMaturity(), rootsOf('MaturityDate')],
  ['price and settle date', PASS.P1, bkPriceAndSettle(), rootsOf('Price', 'SettleDate')],
  ['quantity and settle date', PASS.P2, bkQtyAndSettle(), rootsOf('Quantity', 'SettleDate')],
  ['all breaks attributed', PASS.P1, bkAllAttributed(), rootsOf('Price'), false, (d) => d.every((x) => x.outcome === 'Break' && x.attributedTo.length)],
  ['settle date break plus accrued within tolerance', PASS.P2, bkSettleAndAccruedWT(), rootsOf('SettleDate')],
  ['settle date break plus fees within tolerance', PASS.P1, bkSettleAndFeesWT(), rootsOf('SettleDate')],
];
BREAKS.forEach(([name, pass, make, roots, raw, check]) => add(pairPlan({ name, pass, expect: 'Break', roots, make, raw: !!raw, check })));

// Lifecycle chains
add(() => lifecycleChain({ kind: 'corr', side: 'left', pass: PASS.P1 }));
add(() => lifecycleChain({ kind: 'corr', side: 'left', pass: PASS.P2 }));
add(() => lifecycleChain({ kind: 'corr', side: 'left', pass: PASS.P1, breakIt: true }));
add(() => lifecycleChain({ kind: 'rebook', side: 'left', pass: PASS.P2 }));
add(() => lifecycleChain({ kind: 'rebook', side: 'left', pass: PASS.P1 }));
add(() => lifecycleChain({ kind: 'corr', side: 'right', pass: PASS.P1 }));
add(() => lifecycleChain({ kind: 'rebook', side: 'right', pass: PASS.P2 }));
add(() => lifecycleChain({ kind: 'corr', side: 'left', pass: PASS.P2, both: true }));
for (let i = 0; i < 9; i++) add(() => fullyCancelled('left'));
for (let i = 0; i < 13; i++) add(() => fullyCancelled('right'));

// Blocks and allocations
add(() => allocationBlock({ dir: 'M2O', n: 3, mode: 'equal', asset: 'Equity' }));
add(() => allocationBlock({ dir: 'M2O', n: 4, mode: 'short', asset: 'Equity' }));
add(() => allocationBlock({ dir: 'M2O', n: 3, mode: 'rounding', asset: 'Bond' }));
add(() => allocationBlock({ dir: 'M2O', n: 3, mode: 'equal', asset: 'Equity', cancelledAlloc: true }));
add(() => allocationBlock({ dir: 'O2M', n: 3, mode: 'equal', asset: 'Bond' }));
add(() => allocationBlock({ dir: 'O2M', n: 4, mode: 'short', asset: 'Equity' }));

// Packages (consolidated legs; the engine merges them into one item)
add(() => packageEntry({ legs: 2, legSide: 'left' }));
add(() => packageEntry({ legs: 3, legSide: 'left' }));
add(() => packageEntry({ legs: 2, legSide: 'left', mutateTheirs: (t) => { t.fees = t.fees === undefined ? undefined : r2(t.fees + 1.5); return recalc(t); } }));
add(() => packageEntry({ legs: 2, legSide: 'right', mutateTheirs: (t) => { t.sd = addBiz(t.sd, 2); return t; } }));

// Open entries
rep(6, () => nearMissPair);
rep(8, (i) => () => openRow('left', { trailKind: i % 2, trade: i === 0 ? { noSettle: true } : i === 1 ? { td: '2026-03-21', asset: 'FX' } : {} }));
rep(10, (i) => () => openRow('right', { trailKind: i % 2, trade: i === 0 ? { noSettle: true } : i === 1 ? { noSettle: true, asset: 'Bond' } : {} }));
add(() => openRow('left', { diag: ['UnknownSymbol'], patch: { Instrument: 'XS0987654321' }, trade: { asset: 'Bond' } }));
add(() => openRow('left', { diag: ['UnknownParty'], patch: { Counterparty: null } }));
add(() => openRow('right', { diag: ['UnknownSymbol'], patch: { Instrument: 'US0000000009' }, trade: { asset: 'Equity' } }));
add(() => openRow('right', { diag: ['MissingKey'], patch: { Instrument: null } }));
// Unrouted (no path accepted the row): trail is one attempt with detail "Unrouted"
add(() => openRow('left', { routed: false, diag: ['UnknownSymbol'], patch: { Instrument: 'ZZ-UNLISTED-01' }, trade: { asset: 'Swap' } }));
add(() => openRow('left', { routed: false, diag: ['MissingKey'], patch: { Instrument: null } }));
add(() => openRow('left', { routed: false, diag: ['UnknownParty', 'UnknownSymbol'], patch: { Counterparty: null, Instrument: 'ZZ-UNLISTED-02' } }));
add(() => openRow('left', { routed: false, trade: { asset: 'FX', noSettle: true } }));
add(() => openRow('left', { routed: false }));
add(() => openRow('right', { routed: false, diag: ['UnknownSymbol'], patch: { Instrument: 'XS5550001234' }, trade: { asset: 'Bond' } }));
add(() => openRow('right', { routed: false, diag: ['UnknownSymbol'], patch: { Instrument: 'ZZ-UNLISTED-03' }, trade: { asset: 'Swap' } }));
add(() => openRow('right', { routed: false, diag: ['MissingKey'], patch: { Instrument: null } }));
add(() => openRow('right', { routed: false, diag: ['UnknownParty'], patch: { Counterparty: null } }));
add(() => openRow('right', { routed: false }));
// Ambiguous and search-incomplete
add(() => ambiguous({ nL: 2, nR: 2 })); add(() => ambiguous({ nL: 2, nR: 2 })); add(() => ambiguous({ nL: 2, nR: 2, pass: PASS.P4 }));
add(() => ambiguous({ nL: 2, nR: 1 })); add(() => ambiguous({ nL: 2, nR: 1 })); add(() => ambiguous({ nL: 1, nR: 2 }));
add(() => searchIncomplete({ nL: 1, nR: 2 })); add(() => searchIncomplete({ nL: 1, nR: 2 }));
add(() => searchIncomplete({ nL: 2, nR: 1 })); add(() => searchIncomplete({ nL: 2, nR: 1 }));
// Rows the engine left without an entry: orphans (no route) and quarantined (invalid)
add(() => orphan('left', 'LeftOnly', { diag: ['NoPathMatched'] }));
add(() => orphan('left', 'LeftOnly', { diag: ['NoPathMatched'], trade: { asset: 'Swap', noSettle: true } }));
add(() => orphan('left', 'LeftOnly', { diag: ['NoPathMatched'], trade: { asset: 'Bond' } }));
add(() => orphan('right', 'RightOnly', { diag: ['NoPathMatched'] }));
add(() => orphan('right', 'RightOnly', { diag: ['NoPathMatched'], trade: { asset: 'FX' } }));
add(() => orphan('right', 'RightOnly', { diag: ['NoPathMatched'], trade: { asset: 'Swap', noSettle: true } }));
add(() => orphan('right', 'RightOnly', { diag: ['NoPathMatched'], trade: { asset: 'Bond' } }));
add(() => orphan('left', 'Quarantined', { invalid: true, diag: ['InvalidDate'], patch: { TradeDate: null } }));
add(() => orphan('left', 'Quarantined', { invalid: true, diag: ['NegativeQuantity'], patch: { Quantity: -500 } }));
add(() => orphan('left', 'Quarantined', { invalid: true, diag: ['DuplicateConflict', 'InvalidSide'], patch: { Side: 'XX' } }));
add(() => orphan('right', 'Quarantined', { invalid: true, diag: ['MissingCurrency'], patch: { Currency: null } }));
add(() => orphan('right', 'Quarantined', { invalid: true, diag: ['UnparsablePrice'], patch: { Price: null } }));
add(() => orphan('right', 'Quarantined', { invalid: true, diag: ['NegativeQuantity'], patch: { Quantity: -250 } }));
add(() => orphan('right', 'Quarantined', { invalid: true, diag: ['InvalidDate', 'MissingKey'], patch: { SettleDate: null, Instrument: null } }));

shuffle(plans).forEach((p) => p());

// ============================================================================================
// 11. Assemble and write the document
// ============================================================================================
const doc = {
  run: {
    asOf: AS_OF, profileId: 'trade-recon-demo', title: 'Trade reconciliation demo, 31 March 2026', engineVersion: '1.2.0', roles: ROLES,
  },
  options: {
    currencyOrder: CURRENCY_ORDER, payingSides: PAYING, selectedEntries: null, maxCards: 50, labels: {},
    rawColumns: RAW_COLS, tolerances: TOLERANCES, theme: 'auto',
  },
  passes: PASSES,
  entries: W.entries,
  rows: W.rows,
};
const J = (x) => JSON.stringify(x);
const text = '{\n'
  + ` "run": ${J(doc.run)},\n`
  + ` "options": {\n  "currencyOrder": ${J(doc.options.currencyOrder)},\n  "payingSides": ${J(doc.options.payingSides)},\n  "selectedEntries": null,\n  "maxCards": 50,\n  "labels": {},\n`
  + `  "rawColumns": ${J(doc.options.rawColumns)},\n  "tolerances": [\n${TOLERANCES.map((t) => '   ' + J(t)).join(',\n')}\n  ],\n  "theme": "auto"\n },\n`
  + ` "passes": ${J(doc.passes)},\n`
  + ` "entries": [\n${W.entries.map((e) => '  ' + J(e)).join(',\n')}\n ],\n`
  + ` "rows": [\n${W.rows.map((r) => '  ' + J(r)).join(',\n')}\n ]\n}\n`;
const outPath = join(dirname(fileURLToPath(import.meta.url)), 'sample.json');
writeFileSync(outPath, text, 'utf8');
JSON.parse(text); // must be valid JSON

// ============================================================================================
// 12. Self-checks (fail loudly) and the printed summary
// ============================================================================================
const errors = [];
const bad = (group, msg) => errors.push(`[check ${group}] ${msg}`);
const rowMap = new Map(), entryMap = new Map();
for (const r of W.rows) {
  const k = `${r.source}|${r.rowKey}`;
  if (rowMap.has(k)) bad(1, `duplicate row ${k}`);
  rowMap.set(k, r);
}
for (const e of W.entries) {
  if (!/^[0-9a-f]{32}$/.test(e.id)) bad(1, `entry id not 32 lowercase hex: ${e.id}`);
  if (entryMap.has(e.id)) bad(1, `duplicate entry id ${e.id}`);
  entryMap.set(e.id, e);
}
for (const r of W.rows) if (r.entryId !== null && !entryMap.has(r.entryId)) bad(1, `row ${r.rowKey} points to a missing entry`);
const members = (e, side) => e[side].map((k) => {
  const r = rowMap.get(`${side}|${k}`);
  if (!r) bad(1, `entry ${e.id.slice(0, 8)} lists missing ${side} row ${k}`);
  else if (r.entryId !== e.id) bad(1, `row ${k} does not point back to entry ${e.id.slice(0, 8)}`);
  return r;
}).filter(Boolean);
const eqV = (a, b) => sameVal(a, b) || (typeof a === 'number' && typeof b === 'number' && Math.abs(a - b) < 0.005);
const dayDiff = (a, b) => daysBetween(a, b);

for (const e of W.entries) {
  const L = members(e, 'left'), R = members(e, 'right');
  const tag = e.id.slice(0, 8);
  if (e.alignment === 'Matched') {
    // 2
    if (!L.length || !R.length) bad(2, `${tag} Matched without members on both sides`);
    if (!['Exact', 'WithinTolerance', 'Break'].includes(e.outcome)) bad(2, `${tag} Matched with outcome ${e.outcome}`);
    for (const r of [...L, ...R]) if (r.suppression === null && r.disposition !== 'Matched') bad(2, `${tag} member ${r.rowKey} is ${r.disposition}`);
    const diffs = e.diffs ?? [];
    const exp = !diffs.length ? 'Exact' : diffs.some((d) => d.outcome === 'Break') ? 'Break' : 'WithinTolerance';
    if (exp !== e.outcome) bad(2, `${tag} outcome ${e.outcome} but diffs say ${exp}`);
    // 4: projections
    const side = (mem, proj, name) => {
      if (!mem.length) return;
      const pkg = (e.composition ?? []).includes('Package');
      let want = project(mem[0].values);
      if (mem.length > 1 && !pkg) {
        want.Quantity = mem.reduce((s, r) => s + r.values.Quantity, 0);
        want.NetCash = r2(mem.reduce((s, r) => s + r.values.NetCash, 0));
      }
      const keys = new Set([...Object.keys(want), ...Object.keys(proj ?? {})]);
      for (const k of keys) if (!eqV(want[k], proj?.[k])) bad(4, `${tag} ${name}.${k}: projection ${proj?.[k]} vs rows ${want[k]}`);
    };
    side(L, e.ours, 'ours'); side(R, e.theirs, 'theirs');
    for (const role of DIFF_ROLES) {
      const has = diffs.some((d) => d.role === role);
      if (!sameVal(e.ours[role], e.theirs[role]) !== has) bad(4, `${tag} ${role}: diff presence disagrees with the projections`);
    }
    for (const d of diffs) {
      if (d.role) {
        if (!sameVal(d.ours, e.ours[d.role])) bad(4, `${tag} diff ${d.field} ours ${d.ours} != projection ${e.ours[d.role]}`);
        if (!sameVal(d.theirs, e.theirs[d.role])) bad(4, `${tag} diff ${d.field} theirs ${d.theirs} != projection ${e.theirs[d.role]}`);
      }
      if (d.deltaKind === 'None') { if (d.delta !== null) bad(4, `${tag} ${d.field} delta should be null`); }
      else if (d.deltaKind === 'Days') { if (d.delta !== dayDiff(d.ours, d.theirs)) bad(4, `${tag} ${d.field} days delta wrong`); }
      else if (Math.abs(d.delta - (d.theirs - d.ours)) > 1e-7) bad(4, `${tag} ${d.field} delta wrong`);
      // 5
      if (d.outcome === 'WithinTolerance') {
        const decl = tolOf(e.pathId, d.field);
        if (!decl) bad(5, `${tag} ${d.field} within tolerance without a declaration on path ${e.pathId}`);
        else if (decl.kind === 'Days') { if (Math.abs(d.delta) > 1) bad(5, `${tag} ${d.field} days outside window`); }
        else if (Math.abs(d.delta) > tolAmount(decl, d.ours, d.theirs) + EPS) bad(5, `${tag} ${d.field} |${d.delta}| exceeds tolerance ${tolAmount(decl, d.ours, d.theirs)}`);
      } else if (d.outcome !== 'Break') bad(2, `${tag} diff outcome ${d.outcome}`);
    }
  } else {
    // 3
    if (e.outcome !== 'NotCompared') bad(3, `${tag} ${e.alignment} has outcome ${e.outcome}`);
    if (e.diffs?.length) bad(3, `${tag} ${e.alignment} carries diffs`);
    for (const r of [...L, ...R]) if (r.disposition !== e.alignment) bad(3, `${tag} member ${r.rowKey} is ${r.disposition}, entry is ${e.alignment}`);
    for (const [mem, proj, name] of [[L, e.ours, 'ours'], [R, e.theirs, 'theirs']]) {
      if (!mem.length) { if (proj !== null) bad(3, `${tag} ${name} should be null`); continue; }
      for (const k of new Set([...Object.keys(mem[0].values), ...Object.keys(proj ?? {})])) if (!sameVal(mem[0].values[k], proj?.[k])) bad(3, `${tag} ${name}.${k} differs from the first member`);
    }
  }
  // 7
  const unrouted = e.trail?.length === 1 && e.trail[0].detail === 'Unrouted';
  if (unrouted) { if (e.committedBy !== null || e.pathId !== '') bad(7, `${tag} unrouted entry has committedBy/pathId`); }
  else if (!PASSES.includes(e.committedBy)) bad(7, `${tag} committedBy ${e.committedBy}`);
}
for (const r of W.rows) { // 2/3 row-level sanity and 6
  if ((r.disposition === 'Suppressed') !== (r.suppression != null)) bad(2, `row ${r.rowKey}: Suppressed and suppression disagree`);
  if (r.disposition === 'Quarantined' && (r.entryId !== null || r.validity !== 'Invalid')) bad(3, `row ${r.rowKey}: quarantined row must be Invalid without an entry`);
  for (const [k, v] of Object.entries(r.values)) {
    if (!(k in ROLES)) bad(6, `row ${r.rowKey} uses undeclared role ${k}`);
    else if (DEC.has(k) && v !== null && typeof v !== 'number') bad(6, `row ${r.rowKey} ${k} is not a number`);
    else if (DATE.has(k) && v !== null && !/^\d{4}-\d{2}-\d{2}$/.test(v)) bad(6, `row ${r.rowKey} ${k} is not a date`);
    else if (!DEC.has(k) && !DATE.has(k) && v !== null && typeof v !== 'string') bad(6, `row ${r.rowKey} ${k} is not text`);
  }
}
for (const e of W.entries) for (const side of ['ours', 'theirs']) for (const [k, v] of Object.entries(e[side] ?? {})) {
  if (!(k in ROLES)) bad(6, `entry ${e.id.slice(0, 8)} ${side} uses undeclared role ${k}`);
  if (DEC.has(k) && v !== null && typeof v !== 'number') bad(6, `entry ${e.id.slice(0, 8)} ${side}.${k} is not a number`);
  if (DATE.has(k) && v !== null && !/^\d{4}-\d{2}-\d{2}$/.test(v)) bad(6, `entry ${e.id.slice(0, 8)} ${side}.${k} is not a date`);
}
const size = statSync(outPath).size;
if (size > 400 * 1024) errors.push(`[size] sample.json is ${size} bytes, over 400 KB`);
const leftN = W.rows.filter((r) => r.source === 'left').length, rightN = W.rows.length - leftN;
if (leftN < 250 || leftN > 330 || rightN < 250 || rightN > 330) errors.push(`[size] row counts out of range: left ${leftN}, right ${rightN}`);

const tally = (arr, f) => { const m = new Map(); for (const x of arr) { const k = f(x); if (k === undefined) continue; for (const kk of [].concat(k)) m.set(kk, (m.get(kk) ?? 0) + 1); } return [...m].sort((a, b) => String(a[0]).localeCompare(String(b[0]))).map(([k, v]) => `${k}=${v}`).join('  '); };
const diffsAll = W.entries.flatMap((e) => (e.diffs ?? []).map((d) => ({ e, d })));
const rootBreaks = W.entries.filter((e) => e.outcome === 'Break').flatMap((e) => rootFields(e.diffs));
const settleBucket = (s) => (s === null || s === undefined ? 'missing' : s < AS_OF ? 'before' : s === AS_OF ? 'on' : 'after');
const ccyCounts = tally(W.rows, (r) => r.values.Currency ?? 'none');
const splitCount = W.entries.filter((e) => e.alignment === 'Matched' && e.ours.Currency !== e.theirs.Currency).length;
const out = [];
out.push(`sample.json: ${size} bytes (${(size / 1024).toFixed(1)} KB)   entries ${W.entries.length}   rows ${W.rows.length} (left ${leftN}, right ${rightN})`);
out.push(`alignment   : ${tally(W.entries, (e) => e.alignment)}`);
out.push(`outcome     : ${tally(W.entries, (e) => e.outcome)}`);
out.push(`disposition : ${tally(W.rows, (r) => r.disposition)}`);
out.push(`suppression : ${tally(W.rows, (r) => r.suppression ?? undefined)}`);
out.push(`composition : ${tally(W.entries, (e) => (e.composition?.length ? e.composition : 'None'))}`);
out.push(`currency (rows): ${ccyCounts}   matched pairs with split currencies: ${splitCount}`);
out.push(`committedBy : ${tally(W.entries, (e) => e.committedBy ?? 'null (unrouted)')}`);
out.push(`root breaks : ${tally(rootBreaks, (x) => x)}`);
out.push(`within tol. : ${tally(diffsAll.filter((x) => x.d.outcome === 'WithinTolerance'), (x) => x.d.field + (x.d.attributedTo.length ? '*' : ''))}   (* = consequence)`);
out.push(`entries with raw+trace on members: ${W.entries.filter((e) => [...e.left, ...e.right].some((k) => rowMap.get(`left|${k}`)?.raw || rowMap.get(`right|${k}`)?.raw)).length}   rule ids: ${tally(W.rows.flatMap((r) => r.trace ?? []), (s) => s.ruleId)}`);
out.push(`trail verdicts: ${tally(W.entries.flatMap((e) => e.trail ?? []), (a) => a.verdict)}`);
out.push(`hints       : ${tally(W.entries.flatMap((e) => e.hints ?? []), (h) => h.code)}`);
out.push(`diagnostics : ${tally(W.rows.flatMap((r) => r.diagnostics ?? []), (c) => c)}   validity: ${tally(W.rows, (r) => r.validity ?? 'Valid')}`);
out.push(`settle (left rows): ${tally(W.rows.filter((r) => r.source === 'left'), (r) => settleBucket(r.values.SettleDate))}   weekend trade dates: ${W.rows.filter((r) => r.values.TradeDate && !isBiz(r.values.TradeDate)).length} rows`);
out.push(`lifecycle rows: ${W.rows.filter((r) => r.values.LifecycleAction).length}   unrouted entries: ${W.entries.filter((e) => e.trail?.length === 1 && e.trail[0].detail === 'Unrouted').length}   orphan rows (no entry, not quarantined/suppressed): ${W.rows.filter((r) => !r.entryId && ['LeftOnly', 'RightOnly'].includes(r.disposition)).length}`);
console.log(out.join('\n'));
if (errors.length) {
  console.log(`\nSELF-CHECKS FAILED (${errors.length}):`);
  errors.slice(0, 40).forEach((m) => console.log('  ' + m));
  process.exit(1);
}
console.log('\nSelf-checks passed: 1 keys/uniqueness, 2 matched shape and outcome, 3 open entries, 4 projections and deltas, 5 tolerances, 6 roles and typing, 7 committedBy.');
