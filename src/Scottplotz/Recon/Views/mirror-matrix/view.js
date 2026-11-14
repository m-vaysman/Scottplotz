const { h, model, label, text, opt, fmt, fmtSigned, val, int, clip, hover, diff } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const s1 = document.getElementById('s1');
const s2 = document.getElementById('s2');
if (s1) s1.style.background = diff(-0.5);
if (s2) s2.style.background = diff(0.5);

const declared = model.groups.map(g => g.path[0]).filter(Boolean);
const fromRecs = model.records.flatMap(r => [r.group?.[0], r.group?.[1]]).filter(Boolean);
const entities = [...new Set([...declared, ...fromRecs])];
if (entities.length === 0) {
  entities.push(model.left.short, model.right.short);
} else if (entities.length === 1) {
  entities.push(model.right.short !== entities[0] ? model.right.short : 'Other');
}

const P = new Map();
model.records.forEach(r => {
  const rowName = r.group?.[0] ?? entities[0];
  const colName = r.group?.[1] ?? entities[1];
  const i = entities.indexOf(rowName);
  const j = entities.indexOf(colName);
  if (i >= 0 && j >= 0 && i !== j) {
    const key = `${i}|${j}`;
    if (!P.has(key)) P.set(key, []);
    P.get(key).push(r);
  }
});

const pairSums = it => {
  let left = 0, right = 0;
  for (const r of it) {
    const l = val(r, 'left', M);
    const rt = val(r, 'right', M);
    if (l !== null && l !== undefined) left += l;
    if (rt !== null && rt !== undefined) right += rt;
  }
  return [left, right];
};

let worstKey = null, maxDiff = -1;
P.forEach((it, k) => {
  const [l, r] = pairSums(it);
  const diffAmt = Math.abs(r - l);
  if (diffAmt > maxDiff) {
    maxDiff = diffAmt;
    worstKey = k;
  }
});
let sel = worstKey ?? (P.keys().next().value ?? null);

const div = opt('divisor') || 1;
const dec = RD.field(M)?.decimals ?? 2;
const diffDec = div === 1 ? dec : 1;
const leftDec = div === 1 ? dec : 0;
const cells = new Map();

const tbl = h('table', { class: 'mx' });
tbl.append(h('tr', {}, [
  h('th', { class: 'rh', text: text('rowHeader') }),
  ...entities.map(e => h('th', { text: clip(e, 12) }))
]));

entities.forEach((a, i) => {
  tbl.append(h('tr', {}, [
    h('th', { class: 'rh', text: clip(a, 12) }),
    ...entities.map((b, j) => {
      if (i === j) return h('td', { class: 'self' });
      const key = `${i}|${j}`;
      const it = P.get(key);
      if (!it || it.length === 0) return h('td', { class: 'empty' });
      const [l, r] = pairSums(it);
      const d = r - l;
      const t = Math.abs(d) < 1e-4 ? 0 : Math.sign(d) * Math.min(1, 0.2 + (Math.abs(d) / Math.max(Math.abs(l), 1)) * 2.5);
      const docCount = it.reduce((c, x) => c + (x.count || 1), 0);
      const dText = Math.abs(d) < 1e-4 ? '=' : RD.signed(d / div, diffDec);
      const lText = RD.money(l / div, leftDec);
      const td = h('td', {
        class: 'c',
        tabindex: 0,
        style: t !== 0 ? `background:${diff(t)}` : '',
        on: {
          click: () => { sel = key; show(); },
          keydown: ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); sel = key; show(); } }
        }
      }, [
        dText,
        h('small', { text: lText })
      ]);
      const hoverTitle = text('pairTitle').replace('{row}', a).replace('{col}', b);
      const hoverLines = [
        `${model.left.short} ${fmt(M, l)}`,
        `${model.right.short} ${fmt(M, r)}`,
        `${int(docCount)} ${docCount === 1 ? model.unitOne : model.unit}`
      ];
      hover(td, hoverTitle, hoverLines);
      cells.set(key, td);
      return td;
    })
  ]));
});

document.getElementById('chart').replaceChildren(tbl);

function show() {
  cells.forEach((td, k) => td.classList.toggle('on', k === sel));
  const dtEl = document.getElementById('dt');
  const dsEl = document.getElementById('ds');
  const detailEl = document.getElementById('detail');
  if (!sel || !P.has(sel)) {
    dtEl.textContent = '';
    dsEl.textContent = '';
    detailEl.replaceChildren();
    return;
  }
  const [i, j] = sel.split('|').map(Number);
  const a = entities[i], b = entities[j];
  const it = P.get(sel);
  const [l, r] = pairSums(it);
  const d = r - l;

  dtEl.textContent = text('pairTitle').replace('{row}', a).replace('{col}', b);
  const tolAbs = model.tolerance?.absolute ?? 0;
  const statusStr = Math.abs(d) <= tolAbs ? text('pairAgrees') : text('pairDiff').replace('{diff}', fmtSigned(M, d));
  dsEl.textContent = text('pairSub')
    .replace('{leftShort}', model.left.short)
    .replace('{left}', fmt(M, l))
    .replace('{rightShort}', model.right.short)
    .replace('{right}', fmt(M, r))
    .replace('{status}', statusStr);

  const statusSymbol = rec => {
    if (rec.duplicate) return '×2';
    if (rec.status === 'exact') return '=';
    if (rec.status === 'tolerance') return '≈';
    if (rec.status === 'break') return '≠';
    return label(rec.status);
  };

  const rows = it.flatMap(x => {
    const lx = val(x, 'left', M);
    const rx = val(x, 'right', M);
    const df = x.status === 'break' || !!x.duplicate;
    const lDiffers = df && lx !== null;
    const rDiffers = df && rx !== null;
    const itemRow = h('tr', {}, [
      h('td', { text: statusSymbol(x) }),
      h('td', { text: x.name && x.name !== x.key ? `${x.key} – ${x.name}` : (x.key || x.name || '') }),
      h('td', { class: 'n' + (lDiffers ? ' dl' : '') + (lx === null ? ' none' : ''), text: fmt(M, lx) }),
      h('td', { class: 'n' + (rDiffers ? ' dr' : '') + (rx === null ? ' none' : ''), text: fmt(M, rx) }),
      h('td', { class: 'n', text: (lx !== null && rx !== null && lx !== rx) ? fmtSigned(M, rx - lx) : '' })
    ]);
    const whyRow = x.reason ? h('tr', {}, [
      h('td', { colspan: 5, class: 'none', style: 'padding-top:0', text: x.reason })
    ]) : null;
    return whyRow ? [itemRow, whyRow] : [itemRow];
  });

  const table = h('table', { class: 'tbl' }, [
    h('thead', {}, [
      h('tr', {}, [
        h('th', { text: '' }),
        h('th', { text: text('document') }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short }),
        h('th', { class: 'n', text: text('difference') })
      ])
    ]),
    h('tbody', {}, rows),
    h('tfoot', {}, [
      h('tr', {}, [
        h('td', { colspan: 2, text: text('total') }),
        h('td', { class: 'n', text: fmt(M, l) }),
        h('td', { class: 'n', text: fmt(M, r) }),
        h('td', { class: 'n', text: fmtSigned(M, d) })
      ])
    ])
  ]);

  detailEl.replaceChildren(table);
}

if (sel) show();
