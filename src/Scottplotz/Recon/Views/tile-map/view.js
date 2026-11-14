const { h, s, model, label, text, opt, fmt, fmtSigned, val } = RD;
const M = model.measure ? RD.measure : null;
RD.tieOut(document.getElementById('tie'));

const s1 = document.getElementById('s1'), s2 = document.getElementById('s2');
if (s1) s1.style.background = RD.diff(-0.5);
if (s2) s2.style.background = RD.diff(0.5);

const grid = opt('grid') || {};
const gridEntries = Object.entries(grid);
const T = 56;

let maxCol = 0, maxRow = 0;
gridEntries.forEach(([, [c, r]]) => {
  if (c > maxCol) maxCol = c;
  if (r > maxRow) maxRow = r;
});
const W = Math.max(1, maxCol + 1) * T, H = Math.max(1, maxRow + 1) * T;
const svg = s('svg', { viewBox: '0 0 ' + W + ' ' + H, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

const tileMap = new Map();
model.records.forEach(r => {
  const code = r.group && r.group[0] !== undefined ? r.group[0] : '';
  if (!code) return;
  if (!tileMap.has(code)) tileMap.set(code, []);
  tileMap.get(code).push(r);
});

const tol = model.tolerance ? model.tolerance.absolute : 0;
const tilesData = [];
const tileDomMap = new Map();
let sel = null;

gridEntries.forEach(([code, [cx, cy]]) => {
  const x = cx * T, y = cy * T;
  const recs = tileMap.get(code);
  if (!recs || recs.length === 0) {
    svg.append(
      s('rect', { class: 'notax', x: x + 2, y: y + 2, width: T - 4, height: T - 4, rx: 5 }),
      s('text', { class: 'mut', x: x + T / 2, y: y + T / 2 + 4, 'text-anchor': 'middle', text: code })
    );
    return;
  }
  let sumL = 0, sumR = 0;
  recs.forEach(r => {
    const lv = val(r, 'left');
    const rv = val(r, 'right');
    if (lv !== null) sumL += lv;
    if (rv !== null) sumR += rv;
  });
  const d = sumR - sumL;
  const hasDiff = Math.abs(d) > (tol || 0);
  const full = opt('colourScale') > 0 ? opt('colourScale') : 15;
  const ratio = hasDiff ? Math.sign(d) * Math.min(1, .25 + .75 * (sumL ? Math.abs(d / sumL) * 100 / full : 1)) : 0;
  const fill = hasDiff ? RD.diff(ratio) : 'var(--mid)';

  const tileObj = { code, cx, cy, records: recs, sumLeft: sumL, sumRight: sumR, d, hasDiff };
  tilesData.push(tileObj);

  const rect = s('rect', {
    class: 'tile',
    x: x + 1,
    y: y + 1,
    width: T - 2,
    height: T - 2,
    rx: 5,
    style: 'fill:' + fill
  });
  tileDomMap.set(tileObj, rect);

  rect.addEventListener('click', () => { sel = tileObj; show(); });
  RD.hover(rect, code, [
    `${model.left.short} ${fmt(M ? M.key : null, sumL)}`,
    `${model.right.short} ${fmt(M ? M.key : null, sumR)}`
  ]);

  const np = { 'pointer-events': 'none', 'text-anchor': 'middle' };
  svg.append(rect, s('text', { x: x + T / 2, y: y + T / 2 + (hasDiff ? -2 : 4), 'font-weight': 600, text: code, ...np }));
  if (recs.some(r => r.duplicate)) svg.append(s('rect', { x: x + 5, y: y + 5, width: T - 10, height: T - 10, rx: 3, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' }));
  if (hasDiff) {
    const absD = Math.abs(d);
    const diffText = absD >= 1e6 ? RD.signed(d / 1e6, 1) + 'M' : absD >= 1e3 ? RD.signed(d / 1e3, 1) + 'K' : RD.signed(d, Math.abs(d) % 1 === 0 ? 0 : 1);
    svg.append(s('text', { class: 'subtxt', x: x + T / 2, y: y + T / 2 + 13, text: diffText, ...np }));
  }
});
document.getElementById('chart').append(svg);

let maxWeight = -1;
tilesData.forEach(td => {
  const hasBrk = td.records.some(r => r.status === 'break' || r.duplicate);
  const weight = (hasBrk ? 1e9 : 0) + Math.abs(td.d);
  if (weight > maxWeight) {
    maxWeight = weight;
    sel = td;
  }
});
if (!sel && tilesData.length > 0) sel = tilesData[0];

function show() {
  tileDomMap.forEach((rect, td) => { rect.setAttribute('class', td === sel ? 'tile on' : 'tile'); });
  if (!sel) { document.getElementById('ds').textContent = text('noTiles'); return; }
  const st = sel, l = st.sumLeft, r = st.sumRight, d = r - l;
  const note = RD.groupInfo([st.code])?.note ?? text('agreesNote');
  const dFmt = fmtSigned(M ? M.key : null, d);
  const titleTmpl = d === 0 && text('detailAgrees') ? text('detailAgrees') : text('detailTitle');
  document.getElementById('dt').textContent = titleTmpl
    .replace('{code}', st.code)
    .replace('{diff}', dFmt)
    .replace('{leftShort}', model.left.short)
    .replace('{rightShort}', model.right.short);
  document.getElementById('ds').textContent = note;

  const glyph = rec => {
    if (rec.duplicate) return '×2';
    if (rec.status === 'exact') return '=';
    if (rec.status === 'tolerance') return '≈';
    if (rec.status === 'break') return '≠';
    if (rec.status === 'leftOnly') return label('leftOnly');
    if (rec.status === 'rightOnly') return label('rightOnly');
    return '';
  };

  const rows = st.records.map(rec => {
    const lv = val(rec, 'left'), rv = val(rec, 'right'), delta = rec.delta;
    const isBrk = rec.status === 'break' || rec.duplicate;
    return h('tr', {}, [
      h('td', { text: glyph(rec) }),
      h('td', { text: rec.name || rec.key }),
      h('td', { class: 'n' + (isBrk ? ' dl' : '') + (lv === null ? ' none' : ''), text: fmt(M ? M.key : null, lv) }),
      h('td', { class: 'n' + (isBrk ? ' dr' : '') + (rv === null ? ' none' : ''), text: fmt(M ? M.key : null, rv) }),
      h('td', { class: 'n', text: delta !== null && delta !== 0 ? fmtSigned(M ? M.key : null, delta) : '' })
    ]);
  });

  const foot = h('tfoot', {}, h('tr', {}, [
    h('td', { colspan: 2, text: text('total') }),
    h('td', { class: 'n', text: fmt(M ? M.key : null, l) }),
    h('td', { class: 'n', text: fmt(M ? M.key : null, r) }),
    h('td', { class: 'n', text: fmtSigned(M ? M.key : null, d) })
  ]));

  document.getElementById('detail').replaceChildren(h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [
      h('th', { text: '' }),
      h('th', { text: text('item') }),
      h('th', { class: 'n', text: model.left.short }),
      h('th', { class: 'n', text: model.right.short }),
      h('th', { class: 'n', text: text('difference') })
    ])),
    h('tbody', {}, rows),
    foot
  ]));
}
show();
