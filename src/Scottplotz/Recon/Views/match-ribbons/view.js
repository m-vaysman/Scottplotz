const { h, s, model, text, opt, label, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const sumLines = ls => ls.reduce((t, l) => t + (l.values[M] ?? 0), 0);
// A side's cards come from leftLines / rightLines. A side sent without lines is drawn as one card
// carrying the record's own key, name and measure.
const sideLines = (r, side) => {
  const lines = r[side + 'Lines'];
  if (lines.length) return lines;
  if (!r[side]) return [];
  return [{ key: r.key, name: r.name, values: { [M]: val(r, side) }, whole: true }];
};
const G = model.records.map(r => {
  const L = sideLines(r, 'left'), R = sideLines(r, 'right');
  const dupSide = !r.duplicate ? null : r.duplicate === 'left' ? 'left' : r.duplicate === 'right' ? 'right' : L.length > 1 || !R.length ? 'left' : 'right';
  const dl = dupSide === 'left' ? L : R;
  return { r, L, R, dupSide, dupIdx: dupSide && dl.length > 1 ?dl.length - 1 : -1, n: Math.max(L.length, R.length, 1),
    nL: L.length ? (L[0].whole ? r.count : L.length) : 0, nR: R.length ? (R[0].whole ? r.count : R.length) : 0 };
});

const W = 1100, RH = 46, CW = 340, LX = 0, RX = W - CW, top = 26, mid = (LX + CW + RX) / 2;
let rows = 0;
G.forEach(g => {
  g.base = rows;
  g.ly = g.L.map((_, i) => g.base + i + (g.n - g.L.length) / 2);
  g.ry = g.R.map((_, i) => g.base + i + (g.n - g.R.length) / 2);
  rows += g.n;
});
const cy = v => top + v * RH + RH / 2;
const svg = s('svg', { viewBox: `0 0 ${W} ${top + rows * RH + 4}`, width: '100%', role: 'img', 'aria-label': text('chartLabel') });
svg.append(s('text', { x: LX, y: 14, 'font-weight': 600, text: text('leftTitle') }), s('text', { x: W, y: 14, 'text-anchor': 'end', 'font-weight': 600, text: text('rightTitle') }));

const ribbon = (a, b) => `M${LX + CW} ${a} C${mid} ${a} ${mid} ${b} ${RX} ${b}`;
const names = l => l.whole && l.name === l.key ? [clip(l.name, 34)] : [clip(l.key, 12), clip(l.name, 22)];
let sel = Math.max(0, G.findIndex(g => g.r.status === 'break')), only = opt('attentionOnly') === true;
const els = G.map((g, gi) => {
  const r = g.r, el = s('g', { class: 'g', tabindex: 0 });
  g.L.forEach((_, i) => g.R.forEach((__, j) => {
    if ((g.dupSide === 'left' && i === g.dupIdx) || (g.dupSide === 'right' && j === g.dupIdx)) return;
    el.append(s('path', { class: 'link s-' + RD.cls[r.status], d: ribbon(cy(g.ly[i]), cy(g.ry[j])) }));
  }));
  if (g.dupIdx >= 0 && g.L.length && g.R.length) {
    const a = cy(g.dupSide === 'left' ? g.ly[g.dupIdx] : g.ly[0]), b = cy(g.dupSide === 'right' ? g.ry[g.dupIdx] : g.ry[0]), d = ribbon(a, b);
    el.append(s('path', { class: 'link s-brk', d, style: 'stroke-width:6px' }), s('path', { class: 'link', d, style: 'stroke:var(--surface);stroke-width:2px' }));
  }
  const card = (l, x, yv, side) => {
    const y = top + yv * RH + 5, t = names(l);
    el.append(s('rect', { class: 'card', x, y, width: CW, height: RH - 10, rx: 4 }));
    if (r.status === 'leftOnly' || r.status === 'rightOnly') el.append(s('rect', { class: side === 'l' ? 'f-l' : 'f-r', x: side === 'l' ? x : x + CW - 5, y, width: 5, height: RH - 10, rx: 2 }));
    el.append(s('text', { x: x + 14, y: y + 22, 'font-weight': 600, text: t[0] }));
    if (t[1]) el.append(s('text', { class: 'sec', x: x + 86, y: y + 22, text: t[1] }));
    if (M) el.append(s('text', { x: x + CW - 14, y: y + 22, 'text-anchor': 'end', text: fmt(M, l.values[M] ?? null) }));
  };
  g.L.forEach((l, i) => card(l, LX, g.ly[i], 'l')); g.R.forEach((l, j) => card(l, RX, g.ry[j], 'r'));
  const d = M ? (val(r, 'left') ?? 0) - (val(r, 'right') ?? 0) : 0;
  const lab = r.duplicate ? '×2 ' + text('duplicateNote')
    : r.status === 'break' || r.status === 'tolerance' ? (r.status === 'break' ? '≠ ' : '≈ ') + (M ? fmtSigned(M, d) : label(r.status))
    : r.status === 'leftOnly' ? text('leftOnlyNote') : r.status === 'rightOnly' ? text('rightOnlyNote') : '';
  if (!g.L.length && !g.R.length) {
    const my = cy(g.base), w = 240;
    el.append(s('rect', { class: 'dlab', x: W / 2 - w / 2, y: my - 11, width: w, height: 22, rx: 11 }),
      s('text', { x: W / 2, y: my + 4, 'text-anchor': 'middle', text: `${int(r.count)} ${r.count === 1 ? model.unitOne : model.unit}, ${label(r.status)}` }));
  } else if (lab) {
    const my = cy(((g.ly[g.ly.length - 1] ?? g.ry[0]) + (g.ry[0] ?? g.ly[0])) / 2 + (g.dupIdx >= 0 && g.L.length && g.R.length ? .25 : 0)), w = lab.length * 7 + 30;
    el.append(s('rect', { class: 'dlab', x: W / 2 - w / 2, y: my - 11, width: w, height: 22, rx: 11 }),
      s('text', { x: W / 2, y: my + 4, 'text-anchor': 'middle', text: lab }));
  }
  el.addEventListener('click', () => { sel = gi; draw(); });
  el.addEventListener('keydown', ev => { if (ev.key === 'Enter') { sel = gi; draw(); } });
  RD.hover(el, RD.statusWord(r), r.reason ? [r.reason] : []);
  svg.append(el);
  return el;
});
document.getElementById('chart').append(svg);
const fb = [[text('showAll'), false], [text('showAttention'), true]].map(([t, v]) => h('button', { text: t, on: { click: () => { only = v; draw(); } } }));
document.getElementById('filters').append(...fb);

const agrees = r => (r.status === 'exact' || r.status === 'tolerance') && !r.duplicate;
function draw() {
  fb.forEach((b, i) => b.setAttribute('aria-pressed', String(only === !!i)));
  els.forEach((el, i) => el.setAttribute('class', 'g' + (i === sel ? ' sel' : '') + (only && agrees(G[i].r) ? ' dim' : '')));
  const g = G[sel]; if (!g) return;
  const noun = (n, k) => text(k + (n === 1 ? 'One' : 'Many'));
  document.getElementById('dt').textContent = text('detailTitle').replace('{status}', RD.statusWord(g.r))
    .replace('{leftN}', int(g.nL)).replace('{leftNoun}', noun(g.nL, 'left')).replace('{rightN}', int(g.nR)).replace('{rightNoun}', noun(g.nR, 'right'));
  const tbl = (lines, head, total) => h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [h('th', { text: head }), h('th', { text: text('nameHead') }), M ? h('th', { class: 'n', text: RD.measure.label || text('amountHead') }) : null])),
    h('tbody', {}, lines.length ? lines.map(l => h('tr', {}, [h('td', { text: l.key }), h('td', { text: l.name }), M ? h('td', { class: 'n', text: fmt(M, l.values[M] ?? null) }) : null]))
      : h('tr', {}, h('td', { colspan: M ? 3 : 2, class: 'none', text: text('nothing') }))),
    M ? h('tfoot', {}, h('tr', {}, [h('td', { colspan: 2, text: text('total') }), h('td', { class: 'n', text: fmt(M, total) })])) : null]);
  const l = M ? val(g.r, 'left') ?? sumLines(g.L) : 0, r = M ? val(g.r, 'right') ?? sumLines(g.R) : 0;
  document.getElementById('detail').replaceChildren(
    h('div', { class: 'pair' }, [tbl(g.L, text('leftHead'), l), tbl(g.R, text('rightHead'), r)]),
    h('p', { class: 'verdict' }, [M ? h('b', { text: `${text('verdict')}: ${fmtSigned(M, l - r)}. ` }) : null, g.r.reason || text('noReason')]));
}
draw();
