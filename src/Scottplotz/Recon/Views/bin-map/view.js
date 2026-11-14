const { h, s, model, label, text, opt, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const s1 = document.getElementById('s1'); if (s1) s1.style.background = RD.diff(-.5);
const s2 = document.getElementById('s2'); if (s2) s2.style.background = RD.diff(.5);

const aisles = RD.groupNames(model.records, 0);
if (!aisles.length) aisles.push('');

const facesByAisle = new Map();
aisles.forEach(a => {
  const recs = model.records.filter(r => (r.group[0] ?? '') === a);
  const faces = [...new Set(recs.map(r => r.group[1] ?? '0'))];
  if (!faces.length) faces.push('0');
  facesByAisle.set(a, faces);
});

let maxSlot = 0;
const slotCounts = new Map();
model.records.forEach(r => {
  const a = r.group[0] ?? '';
  const f = r.group[1] ?? '0';
  const k = a + '::' + f;
  const next = (slotCounts.get(k) || 0) + 1;
  slotCounts.set(k, next);
  if (!r.attrs) r.attrs = {};
  if (r.attrs.slot == null) r.attrs.slot = next;
  if (Number(r.attrs.slot) > maxSlot) maxSlot = Number(r.attrs.slot);
});
if (maxSlot < 1) maxSlot = 1;

const CW = Math.max(10, Math.min(33, Math.floor((760 - 44 - 10) / maxSlot) - 2));
const CH = 24, gapX = 2, gapY = 2;
const cellPitchX = CW + gapX, cellPitchY = CH + gapY, X0 = 44;
const W = Math.max(760, X0 + maxSlot * cellPitchX + 10);

const aisleY = new Map();
let curY = 20;
aisles.forEach(a => {
  const faces = facesByAisle.get(a);
  aisleY.set(a, { y: curY, faces });
  curY += faces.length * cellPitchY + 26;
});
const totalH = Math.max(curY, 60);

const svg = s('svg', { viewBox: `0 0 ${W} ${totalH}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

const step = maxSlot > 40 ? 10 : 5;
for (let sNum = 1; sNum <= maxSlot; sNum++) {
  if ((sNum - 1) % step === 0 || sNum === maxSlot) {
    const cx = X0 + (sNum - 1) * cellPitchX + CW / 2;
    svg.append(s('text', { class: 'mut', x: cx, y: 12, 'text-anchor': 'middle', text: String(sNum) }));
  }
}

aisles.forEach(a => {
  if (!a) return;
  const aInfo = aisleY.get(a);
  const aisleRecs = model.records.filter(r => (r.group[0] ?? '') === a);
  const midY = aInfo.y + (aInfo.faces.length * cellPitchY) / 2 + 5;
  const t = s('text', { class: 'aisle', x: 6, y: midY, text: clip(a, 4) });
  t.addEventListener('click', () => {
    sel = aisleRecs.find(q => q.status !== 'exact') || aisleRecs[0] || sel;
    show();
  });
  svg.append(t);
});

let sel;
const cells = new Map();
const scale = opt('colourScale') || 30;

model.records.forEach(rec => {
  const a = rec.group[0] ?? '';
  const aInfo = aisleY.get(a) || { y: 20, faces: ['0'] };
  const f = rec.group[1] ?? '0';
  const fIdx = Math.max(0, aInfo.faces.indexOf(f));
  const slot = Number(rec.attrs?.slot || 1);
  const cx = X0 + (slot - 1) * cellPitchX;
  const cy = aInfo.y + fIdx * cellPitchY;

  let fill;
  if (rec.status === 'leftOnly') fill = 'var(--left)';
  else if (rec.status === 'rightOnly') fill = 'var(--right)';
  else if (rec.status === 'exact') fill = 'var(--wash)';
  else {
    let t = rec.pct !== null && rec.pct !== undefined ? rec.pct / scale : null;
    if (t === null) t = rec.delta ? (rec.delta > 0 ? 0.15 : -0.15) : 0;
    t = Math.max(-1, Math.min(1, t));
    fill = RD.diff(t || (rec.delta > 0 ? 0.15 : -0.15));
  }

  const r = s('rect', { class: 'bin', x: cx, y: cy, width: CW, height: CH, rx: 3, style: 'fill:' + fill });
  r.addEventListener('click', () => { sel = rec; show(); });
  const l = val(rec, 'left');
  const rt = val(rec, 'right');
  RD.hover(r, rec.key, [
    rec.name,
    model.left.short + ' ' + (l === null ? label('none') : fmt(M, l)),
    model.right.short + ' ' + (rt === null ? label('none') : fmt(M, rt))
  ]);
  cells.set(rec, r);
  svg.append(r);

  if (rec.duplicate) {
    const dx = cx + Math.round((CW - 15) / 2);
    const dy = cy + Math.round((CH - 12) / 2);
    svg.append(
      s('rect', { x: dx, y: dy, width: 11, height: 9, rx: 2, fill: 'var(--surface)', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' }),
      s('rect', { x: dx + 4, y: dy + 3, width: 11, height: 9, rx: 2, fill: 'var(--surface)', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' })
    );
  }
});

const chartEl = document.getElementById('chart');
if (chartEl) chartEl.append(svg);

const hasCost = model.fields.some(f => f.key === 'cost' || f.key === 'value') ||
  model.records.some(r => r.attrs?.cost != null);

const costVal = r => {
  const c = r.attrs?.cost ?? val(r, 'left', 'cost') ?? val(r, 'right', 'cost');
  if (c !== null && c !== undefined) {
    const d = r.delta ?? (r.status === 'rightOnly' ? (val(r, 'right') ?? 0) : -(val(r, 'left') ?? 0));
    return Math.round(d * c * 100) / 100;
  }
  return r.delta;
};
{
  const cnt = new Map(); model.records.forEach(r => { if (r.status !== 'exact') cnt.set(r.group[0] ?? '', (cnt.get(r.group[0] ?? '') || 0) + r.count); });
  const top = [...cnt.entries()].sort((p, q) => q[1] - p[1])[0];
  const pool = model.records.filter(r => r.status === 'break' && (!top || (r.group[0] ?? '') === top[0]));
  sel = pool.sort((p, q) => Math.abs(costVal(q) ?? 0) - Math.abs(costVal(p) ?? 0))[0] || model.records.find(r => r.status === 'break') || model.records[0];
}

function show() {
  if (!sel) return;
  cells.forEach((el, r) => {
    const isSel = r === sel;
    const isLinked = !isSel && sel.attrs?.link && r.attrs?.link === sel.attrs.link;
    el.setAttribute('class', isSel ? 'bin on' : isLinked ? 'bin on-link' : 'bin');
  });

  const selAisle = sel.group[0] ?? '';
  const aisleRecords = selAisle
    ? model.records.filter(r => (r.group[0] ?? '') === selAisle)
    : model.records;
  const aisleTotal = aisleRecords.reduce((sum, r) => sum + r.count, 0);
  const differRecords = aisleRecords.filter(r => r.status !== 'exact');
  const differCount = differRecords.reduce((sum, r) => sum + r.count, 0);

  const dtEl = document.getElementById('dt');
  if (dtEl) {
    if (selAisle) {
      dtEl.textContent = text('groupSummary')
        .replace('{group}', selAisle)
        .replace('{differCount}', int(differCount))
        .replace('{totalCount}', int(aisleTotal))
        .replace('{unit}', model.unit);
    } else {
      dtEl.textContent = text('allSummary')
        .replace('{differCount}', int(differCount))
        .replace('{totalCount}', int(aisleTotal))
        .replace('{unit}', model.unit);
    }
  }

  const dsEl = document.getElementById('ds');
  if (dsEl) {
    let sub = sel.reason;
    if (!sub) {
      if (sel.status === 'exact') sub = text('expected').replace('{key}', sel.key);
      else if (sel.status === 'tolerance') sub = text('inTolerance').replace('{key}', sel.key);
      else sub = text('recount').replace('{key}', sel.key);
    }
    dsEl.textContent = sub;
  }

  const displayRecords = differRecords.length ? differRecords : aisleRecords;
  const sorted = [...displayRecords].sort((p, q) => Math.abs(costVal(q) ?? 0) - Math.abs(costVal(p) ?? 0));
  const rows = sorted.slice(0, 14);

  const thCols = [
    h('th', { text: text('bin').charAt(0).toUpperCase() + text('bin').slice(1) }),
    h('th', { text: text('item') }),
    h('th', { class: 'n', text: model.left.short }),
    h('th', { class: 'n', text: model.right.short }),
    h('th', { class: 'n', text: text('units') })
  ];
  if (hasCost) thCols.push(h('th', { class: 'n', text: text('atCost') }));

  const tbody = h('tbody', {}, rows.map(r => {
    const isSel = r === sel;
    const isLinked = !isSel && sel.attrs?.link && r.attrs?.link === sel.attrs.link;
    const df = r.status === 'break';
    const l = val(r, 'left');
    const rt = val(r, 'right');
    const tds = [
      h('td', { text: r.key + (r.duplicate ? ' (' + label('duplicate').toLowerCase() + ')' : '') }),
      h('td', { text: clip(r.name, 28) }),
      h('td', { class: 'n' + (df ? ' dl' : '') + (l === null ? ' none' : ''), text: l === null ? label('none') : fmt(M, l) }),
      h('td', { class: 'n' + (df ? ' dr' : '') + (rt === null ? ' none' : ''), text: rt === null ? label('none') : fmt(M, rt) }),
      h('td', { class: 'n', text: r.delta === null ? '' : fmtSigned(M, r.delta) })
    ];
    if (hasCost) {
      const cv = costVal(r);
      tds.push(h('td', { class: 'n', text: cv === null ? '' : RD.signed(cv) }));
    }
    return h('tr', {
      class: isSel ? 'on' : isLinked ? 'on-link' : '',
      style: 'cursor:pointer',
      on: { click: () => { sel = r; show(); } }
    }, tds);
  }));

  const totalCost = displayRecords.reduce((t, x) => t + (costVal(x) ?? 0), 0);
  const tfoot = h('tfoot', {}, h('tr', {}, [
    h('td', { colspan: hasCost ? 5 : 4, text: text('groupTotal') }),
    h('td', { class: 'n', text: hasCost ? RD.signed(totalCost) : fmtSigned(M, totalCost) })
  ]));

  const detailEl = document.getElementById('detail');
  if (detailEl) {
    detailEl.replaceChildren(h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, thCols)),
      tbody,
      tfoot
    ]));
  }
}

if (sel) show();
