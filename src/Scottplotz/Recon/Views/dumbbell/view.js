const { h, s, model, label, text, opt, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

// Rows: grouped by the first group level, in group order, capped at maxRows.
const sorted = opt('sortBy') === 'difference'
  ? [...model.records].sort((a, b) => Math.abs(b.delta ?? Infinity) - Math.abs(a.delta ?? Infinity))
  : model.records;
const names = RD.groupNames(sorted);
const grouped = names.length ? names.flatMap(g => sorted.filter(r => r.group[0] === g)).concat(sorted.filter(r => r.group[0] === undefined)) : sorted;
const D = grouped.slice(0, opt('maxRows'));
document.getElementById('chartSub').textContent = text('chartSub') +
  (D.length < model.records.length ? ' ' + text('truncated').replace('{shown}', int(D.length)).replace('{total}', int(model.records.length)) : '');

const values = D.flatMap(r => [val(r, 'left'), val(r, 'right')]).filter(v => v !== null);
const T = RD.ticks(Math.max(0, ...values)), MAX = T[T.length - 1], MIN = Math.min(0, ...values);
const W = 780, RH = 30, X0 = 250, X1 = 660, x = v => X0 + (v - MIN) / (MAX - MIN || 1) * (X1 - X0);
let yy = 26, group; const lay = [];
D.forEach(r => { if (r.group[0] !== group && r.group[0] !== undefined) { group = r.group[0]; lay.push({ head: group, y: yy + 8 }); yy += 34; } lay.push({ r, y: yy }); yy += RH; });
const svg = s('svg', { viewBox: `0 0 ${W} ${yy + 6}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });
T.forEach(t => svg.append(s('line', { class: 'gridl', x1: x(t), x2: x(t), y1: 22, y2: yy }), s('text', { class: 'mut', x: x(t), y: 14, 'text-anchor': 'middle', text: int(t) })));
svg.append(s('text', { class: 'mut', x: W - 4, y: 14, 'text-anchor': 'end', text: text('difference') }));

let sel = D.find(r => r.status === 'break') ?? D[0]; const hits = new Map();
lay.forEach(({ head, r, y }) => {
  if (head) { svg.append(s('text', { x: 0, y: y + 14, 'font-weight': 600, text: clip(head, 40) })); return; }
  const cy = y + RH / 2, l = val(r, 'left'), rt = val(r, 'right'), np = { 'pointer-events': 'none' };
  const hit = s('rect', { class: 'row-hit', x: 0, y, width: W, height: RH, rx: 4 });
  hits.set(r, hit); svg.append(hit);
  if (r.name === r.key) svg.append(s('text', { x: 8, y: cy + 4, text: clip(r.name, 34), ...np }));
  else svg.append(s('text', { class: 'sec', x: 8, y: cy + 4, text: clip(r.key, 12), ...np }), s('text', { x: 92, y: cy + 4, text: clip(r.name, 22), ...np }));
  if ((r.status === 'break' || r.status === 'tolerance') && l !== null && rt !== null && l !== rt)
    svg.append(s('line', { class: 'conn ' + (r.status === 'tolerance' ? 's-tol' : rt > l ? 's-r' : 's-l'), x1: x(l), x2: x(rt), y1: cy, y2: cy, ...np }));
  if (r.duplicate) svg.append(s('circle', { cx: x(r.duplicate === 'left' ? l ?? rt : rt ?? l), cy, r: 10, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, ...np }));
  if (l !== null && rt !== null && l === rt) svg.append(s('circle', { class: 'dot f-ex', cx: x(l), cy, r: 6, ...np }));
  else {
    if (l !== null) svg.append(s('circle', { class: 'dot f-l', cx: x(l), cy, r: 6, ...np }));
    if (rt !== null) svg.append(s('circle', { class: 'dot f-r', cx: x(rt), cy, r: 6, ...np }));
  }
  const note = r.status === 'leftOnly' || r.status === 'rightOnly' ? label(r.status) : r.duplicate ? label('duplicate') : r.delta ? fmtSigned(M, r.delta) : '';
  svg.append(s('text', { class: r.delta ? '' : 'sec', x: W - 4, y: cy + 4, 'text-anchor': 'end', text: note, ...np }));
  hit.addEventListener('click', () => { sel = r; show(); });
  RD.hover(hit, r.name === r.key ? r.name : `${r.key}, ${r.name}`, [`${model.left.short} ${fmt(M, l)}`, `${model.right.short} ${fmt(M, rt)}`]);
});
document.getElementById('chart').append(svg);

function show() {
  hits.forEach((e, r) => e.setAttribute('class', r === sel ? 'row-hit row-sel' : 'row-hit'));
  const r = sel;
  document.getElementById('dt').textContent = r.name;
  document.getElementById('ds').textContent = [...r.group, r.name === r.key ? null : r.key].filter(Boolean).join(', ') + (r.group.length || r.name !== r.key ? '. ' : '') + RD.statusWord(r) + '.';
  const cell = (side, f, differs) => { const v = val(r, side, f.key); return h('td', { class: (f.type === 'number' ? 'n' : '') + (differs ? (side === 'left' ? ' dl' : ' dr') : '') + (v === null ? ' none' : ''), text: fmt(f.key, v) }); };
  const row = f => { const a = val(r, 'left', f.key), b = val(r, 'right', f.key), differs = a !== null && b !== null && a !== b; return h('tr', {}, [h('td', { text: f.label }), cell('left', f, differs), cell('right', f, differs)]); };
  document.getElementById('detail').replaceChildren(
    h('div', { class: 'big', text: r.delta === null ? label(r.status) : r.delta === 0 ? label('agrees') : fmtSigned(M, r.delta) }),
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [h('th', { text: text('field') }), h('th', { class: 'n', text: model.left.short }), h('th', { class: 'n', text: model.right.short })])),
      h('tbody', {}, model.fields.map(row))]),
    h('p', { class: 'why', text: r.reason || text('noReason') }));
}
if (sel) show();
