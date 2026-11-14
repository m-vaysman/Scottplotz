const { h, s, model, label, text, opt, fmt, int } = RD;
RD.tieOut(document.getElementById('tie'));

const num = v => typeof v === 'number' && isFinite(v) ? v : null;
const score = r => num(r.attrs.score);
const MATCH = num(opt('matchFrom')) ?? 92, REVIEW = num(opt('reviewFrom')) ?? 70;
const fill = (t, o) => Object.keys(o).reduce((a, k) => a.split('{' + k + '}').join(o[k]), t);
const wordOf = r => r.duplicate ? label('duplicate') : label(r.status);

// Strip: one dot per record sent, weighted by count, stacked per whole score.
document.getElementById('stripSub').textContent = fill(text('stripSub'), { match: MATCH, review: REVIEW, unitOne: model.unitOne });
const scored = model.records.filter(r => score(r) !== null && r.count > 0);
const all = scored.map(score).concat([MATCH, REVIEW]);
const LO = Math.floor(Math.min(...all) / 10) * 10, HI = Math.max(LO + 10, Math.ceil(Math.max(...all) / 10) * 10), SPAN = HI - LO;
const W = 1100, X0 = 30, X1 = W - 20, BASE = 176, CAP = 20, x = v => X0 + (v - LO) / SPAN * (X1 - X0);
if (!scored.length) document.getElementById('chart').append(h('p', { class: 'empty', text: text('noScores') }));
else {
  const svg = s('svg', { viewBox: `0 0 ${W} 216`, width: '100%', role: 'img', 'aria-label': text('stripTitle') });
  svg.append(s('rect', { class: 'zone', x: x(REVIEW), y: 28, width: Math.max(0, x(MATCH) - x(REVIEW)), height: BASE - 28 }), s('line', { class: 'axl', x1: X0, x2: X1, y1: BASE, y2: BASE }));
  [[REVIEW, fill(text('reviewLabel'), { review: REVIEW })], [MATCH, fill(text('matchLabel'), { match: MATCH })]].forEach(([v, t]) =>
    svg.append(s('line', { class: 'thr', x1: x(v), x2: x(v), y1: 28, y2: BASE }), s('text', { x: x(v), y: 20, text: t })));
  const step = SPAN <= 30 ? 5 : SPAN <= 80 ? 10 : 20;
  for (let v = LO; v <= HI; v += step) svg.append(s('text', { class: 'mut', x: x(v), y: BASE + 18, 'text-anchor': 'middle', text: int(v) }));
  svg.append(s('text', { class: 'sec', x: X0, y: 20, text: text('lowLabel') }));
  const stack = new Map();
  scored.forEach(r => { const v = Math.round(score(r)); stack.set(v, (stack.get(v) ?? 0) + r.count); });
  [...stack].sort((a, b) => a[0] - b[0]).forEach(([v, n]) => {
    for (let k = 1; k <= Math.min(n, CAP); k++)
      svg.append(s('circle', { class: 'dot ' + (v >= MATCH ? 'f-ex' : v >= REVIEW ? 'f-tol' : 'f-brk'), cx: x(v), cy: BASE - 5 - (k - 1) * 6.5, r: 3.2 }));
  });
  document.getElementById('chart').append(svg);
}

// Clusters: records that carry lines.
const C = model.records.filter(r => r.leftLines.length + r.rightLines.length > 0);
const lines = r => [...r.leftLines.map(l => ({ side: 'left', l })), ...r.rightLines.map(l => ({ side: 'right', l }))];
const short = d => d.short.toLowerCase();
let sel = 0;
const btns = C.map((c, i) => h('button', { class: 'cl', on: { click: () => { sel = i; show(); } } }, [
  h('b', { text: c.name }),
  h('span', { text: wordOf(c) + (score(c) !== null ? ', ' + fill(text('scoreWord'), { score: int(score(c)) }) : '') }),
  h('div', { class: 'recs' }, [...lines(c).map(x => h('i', { class: 'sw ' + (x.side === 'left' ? 'l' : 'r') })),
    h('em', { text: `${c.leftLines.length} ${short(model.left)}, ${c.rightLines.length} ${short(model.right)}` })])]));
document.getElementById('list').append(...(btns.length ? btns : [h('p', { class: 'empty', text: text('noClusters') })]));

function show() {
  if (!C.length) return;
  btns.forEach((b, i) => b.setAttribute('aria-current', String(i === sel)));
  const c = C[sel], rows = lines(c), base = rows[0].l;
  document.getElementById('dt').textContent = c.name;
  document.getElementById('ds').textContent = wordOf(c) + '.';
  const fields = model.fields.filter(f => rows.some(x => x.l.values[f.key] !== null && x.l.values[f.key] !== undefined));
  const sims = Object.keys(c.attrs).filter(k => k.startsWith('sim_') && num(c.attrs[k]) !== null).map(k => k.slice(4));
  const order = k => k === 'name' ? -1 : model.fields.findIndex(f => f.key === k);
  sims.sort((a, b) => (order(a) < 0 && a !== 'name' ? 1e9 : order(a)) - (order(b) < 0 && b !== 'name' ? 1e9 : order(b)));
  const simLabel = k => k === 'name' ? text('nameColumn') : RD.field(k)?.label ?? k;
  const simGrid = sims.length ? h('div', { class: 'sim' }, sims.flatMap(k => {
    const v = Math.max(0, Math.min(100, c.attrs['sim_' + k]));
    return [h('span', { text: simLabel(k) }), h('div', { class: 'track' }, h('i', { class: v === 100 ? 'full' : v < 50 ? 'low' : '', style: `width:${v}%` })), h('span', { class: 'n', text: int(c.attrs['sim_' + k]) })];
  })) : null;
  const differs = (ri, name, get) => ri > 0 && get(rows[ri].l) !== get(base);
  const cellOf = (ri, side, get, shown) => h('td', { class: differs(ri, side, get) ? (rows[ri].side === 'left' ? 'dl' : 'dr') : '', text: shown });
  document.getElementById('detail').replaceChildren(
    simGrid,
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [text('sideColumn'), text('recordColumn'), text('nameColumn'), ...fields.map(f => f.label)].map(t => h('th', { text: t })))),
      h('tbody', {}, rows.map((x, ri) => h('tr', {}, [
        h('td', {}, [h('i', { class: 'sw ' + (x.side === 'left' ? 'l' : 'r') }), model[x.side].short]),
        h('td', { text: x.l.key ?? '' }),
        cellOf(ri, 'name', l => l.name ?? '', x.l.name ?? ''),
        ...fields.map(f => cellOf(ri, f.key, l => l.values[f.key] ?? null, fmt(f.key, x.l.values[f.key] ?? null)))
      ])))]),
    h('p', { class: 'why', text: c.reason || text('noReason') }));
}
show();
