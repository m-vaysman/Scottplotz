const { h, s, model, label, text, opt, fmt, val, int, money } = RD;
RD.tieOut(document.getElementById('tie'));
document.getElementById('cs').textContent = model.asOf ? text('chartSub') : text('chartSubUndated');

const asOfDate = model.asOf ? new Date(model.asOf) : null;
function getAge(rec) {
  if (rec.attrs && rec.attrs.age !== undefined && rec.attrs.age !== null) {
    const a = +rec.attrs.age;
    if (!Number.isNaN(a)) return a;
  }
  if (rec.date && asOfDate) {
    const d = new Date(rec.date);
    if (!Number.isNaN(d.getTime())) {
      const diff = Math.round((asOfDate - d) / 86400000);
      return Math.max(0, diff);
    }
  }
  return 0;
}

const rawBuckets = opt('buckets') || [7, 30, 60, 90];
const bounds = rawBuckets.filter(n => typeof n === 'number' && !Number.isNaN(n)).sort((a, b) => a - b);
if (!bounds.length) bounds.push(7, 30, 60, 90);

const BK = [];
for (let i = 0; i <= bounds.length; i++) {
  if (i === 0) {
    BK.push({ index: i, min: 0, max: bounds[0], label: text('bucketFirst').replace('{max}', int(bounds[0])) });
  } else if (i < bounds.length) {
    const min = bounds[i - 1] + 1, max = bounds[i];
    BK.push({ index: i, min, max, label: text('bucketRange').replace('{min}', int(min)).replace('{max}', int(max)) });
  } else {
    const min = bounds[bounds.length - 1] + 1;
    BK.push({ index: i, min, max: Infinity, label: text('bucketLast').replace('{min}', int(bounds[bounds.length - 1])) });
  }
}

function getBucketIndex(age) {
  for (let i = 0; i < bounds.length; i++) {
    if (age <= bounds[i]) return i;
  }
  return bounds.length;
}

const items = model.records.filter(r => r.status === 'leftOnly' || r.status === 'rightOnly').map(r => ({
  rec: r,
  side: r.status,
  age: getAge(r),
  bucket: getBucketIndex(getAge(r)),
  count: r.count || 1,
  amount: Math.abs(val(r, r.status === 'leftOnly' ? 'left' : 'right') ?? 0)
}));

let sel = null;
const candB = BK.length > 3 ? 3 : BK.length - 1;
if (items.some(x => x.side === 'rightOnly' && x.bucket === candB)) {
  sel = `rightOnly|${candB}`;
} else {
  let maxC = -1, bestKey = 'rightOnly|0';
  BK.forEach((_, b) => {
    ['rightOnly', 'leftOnly'].forEach(side => {
      const c = items.filter(x => x.side === side && x.bucket === b).reduce((t, x) => t + x.count, 0);
      if (c > maxC) { maxC = c; bestKey = `${side}|${b}`; }
    });
  });
  sel = bestKey;
}

let mode = opt('mode') === 'amount' && model.measure ? 'amount' : 'count';
const filterModes = model.measure ? [
  ['count', text('modeCount')],
  ['amount', text('modeAmount')]
] : [
  ['count', text('modeCount')]
];
const fb = filterModes.map(([m, t]) => h('button', {
  text: t,
  on: { click: () => { mode = m; draw(); } }
}));
document.getElementById('filters').replaceChildren(...fb);

const W = 1100, MID = W / 2, GAP = 70, RH = 52, TOP = 34;

function draw() {
  fb.forEach((b, i) => b.setAttribute('aria-pressed', String(filterModes[i][0] === mode)));

  const v = (side, b) => {
    const it = items.filter(x => x.side === side && x.bucket === b);
    return it.reduce((t, x) => t + (mode === 'count' ? x.count : x.amount), 0);
  };

  const allVals = BK.flatMap((_, b) => [v('leftOnly', b), v('rightOnly', b)]);
  const max = Math.max(1, ...allVals);
  const w = val => (val / max) * (MID - GAP - 90);
  const f = val => mode === 'count' ? int(val) : (val > 0 && val < 1 ? money(val, 2) : money(val, 0));

  const svg = s('svg', { viewBox: `0 0 ${W} ${TOP + BK.length * RH + 6}`, width: '100%', role: 'img', 'aria-label': text('chartAria') });
  svg.append(
    s('text', { x: MID - GAP, y: 16, 'text-anchor': 'end', 'font-weight': 600, text: text('leftHeader') }),
    s('text', { x: MID + GAP, y: 16, 'font-weight': 600, text: text('rightHeader') })
  );

  BK.forEach((bk, b) => {
    const y = TOP + b * RH;
    svg.append(s('text', { class: 'sec', x: MID, y: y + RH / 2 + 4, 'text-anchor': 'middle', text: bk.label }));
    [['leftOnly', -1, 'f-l'], ['rightOnly', 1, 'f-r']].forEach(([side, dir, cls]) => {
      const val = v(side, b), bw = Math.max(2, w(val)), x0 = dir < 0 ? MID - GAP - bw : MID + GAP, key = side + '|' + b;
      const r = s('rect', { class: `bar ${cls}${key === sel ? ' on' : ''}`, x: x0, y: y + RH / 2 - 12, width: bw, height: 24, rx: 4 });
      r.addEventListener('click', () => { sel = key; draw(); });

      const it = items.filter(q => q.side === side && q.bucket === b);
      const tipCount = it.reduce((t, q) => t + q.count, 0);
      const tipAmt = it.reduce((t, q) => t + q.amount, 0);
      const tipSide = side === 'leftOnly' ? model.left.short : model.right.short;
      RD.hover(r, `${bk.label}, ${tipSide}`, [
        `${int(tipCount)} ${tipCount === 1 ? model.unitOne : model.unit}`,
        model.measure ? `${fmt(model.measure, tipAmt)} ${RD.measure?.label || ''}`.trim() : money(tipAmt)
      ]);

      svg.append(r, s('text', { x: dir < 0 ? x0 - 8 : x0 + bw + 8, y: y + RH / 2 + 4, 'text-anchor': dir < 0 ? 'end' : 'start', text: f(val) }));
    });
  });

  document.getElementById('chart').replaceChildren(svg);
  document.getElementById('ct').textContent = mode === 'count' ? text('chartTitleCount') : text('chartTitleAmount');

  const [side, bStr] = (sel || 'rightOnly|0').split('|');
  const b = +bStr;
  const bk = BK[b] || BK[0];
  const rows = items.filter(x => x.side === side && x.bucket === b).sort((p, q) => q.age - p.age);
  const totalCount = rows.reduce((t, x) => t + x.count, 0);
  const totalAmt = rows.reduce((t, x) => t + x.amount, 0);

  const sideLabel = label(side);
  document.getElementById('dt').textContent = text('detailTitle')
    .replace('{count}', int(totalCount))
    .replace('{unit}', totalCount === 1 ? model.unitOne : model.unit)
    .replace('{bucket}', bk.label.toLowerCase())
    .replace('{side}', sideLabel);

  const note = text(side === 'leftOnly' ? 'leftNote' : 'rightNote');
  const amtFormatted = model.measure ? fmt(model.measure, totalAmt) : money(totalAmt, 2);
  document.getElementById('ds').textContent = text('detailSub')
    .replace('{total}', amtFormatted)
    .replace('{note}', note);

  const maxRows = opt('maxDetailRows') || 100;
  const shownRows = rows.slice(0, maxRows);

  document.getElementById('detail').replaceChildren(h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [
      h('th', { text: text('colReference') }),
      h('th', { text: text('colName') }),
      h('th', { class: 'n', text: text('colAge') }),
      h('th', { class: 'n', text: model.left.short }),
      h('th', { class: 'n', text: model.right.short }),
      h('th', { text: text('colReason') })
    ])),
    h('tbody', {}, shownRows.map(x => {
      const dup = x.rec.duplicate ? ' (' + label('duplicate').toLowerCase() + ')' : '';
      const isLo = side === 'leftOnly';
      const isRo = side === 'rightOnly';
      const lAmt = val(x.rec, 'left');
      const rAmt = val(x.rec, 'right');
      return h('tr', {}, [
        h('td', { text: x.rec.key + dup }),
        h('td', { text: x.rec.name || '' }),
        h('td', { class: 'n', text: int(x.age) }),
        h('td', { class: 'n' + (isLo ? '' : ' none'), text: isLo ? (model.measure ? fmt(model.measure, lAmt) : money(x.amount)) : label('none') }),
        h('td', { class: 'n' + (isRo ? '' : ' none'), text: isRo ? (model.measure ? fmt(model.measure, rAmt) : money(x.amount)) : label('none') }),
        h('td', { text: x.rec.reason || text('noReason') })
      ]);
    }))
  ]));
}

draw();
