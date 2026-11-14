const { h, s, model, label, text, opt, int, cls, groupInfo } = RD;
RD.tieOut(document.getElementById('tie'));

const stages = [].concat(opt('stages') ?? []).filter(x => typeof x === 'string' && x);
const nodeOf = (r, st) => { const v = r.attrs[stages[st]]; return v === undefined || v === null || v === '' ? null : String(v); };
const total = model.records.reduce((t, r) => t + r.count, 0);

// Nodes per stage, in first-seen order. A node's tone is the status most of its records have.
const order = ['leftOnly', 'exact', 'tolerance', 'break', 'rightOnly'];
const nodes = new Map(), cols = stages.map(() => []);
model.records.forEach(r => stages.forEach((_, st) => {
  const name = nodeOf(r, st); if (name === null) return;
  const id = st + '\u0001' + name;
  if (!nodes.has(id)) { const n = { id, name, st, count: 0, dup: 0, by: {}, in: 0, out: 0 }; nodes.set(id, n); cols[st].push(n); }
  const n = nodes.get(id); n.count += r.count; n.by[r.status] = (n.by[r.status] ?? 0) + r.count; if (r.duplicate) n.dup += r.count;
}));
nodes.forEach(n => { n.tone = order.reduce((best, k) => (n.by[k] ?? 0) > (n.by[best] ?? 0) ? k : best, order[0]); n.isDup = n.st === 0 && n.dup * 2 > n.count; });

// Links between consecutive stages, in first-seen order, widths summed from rec.count.
const links = new Map();
model.records.forEach(r => stages.slice(1).forEach((_, i) => {
  const a = nodeOf(r, i), b = nodeOf(r, i + 1); if (a === null || b === null) return;
  const A = nodes.get(i + '\u0001' + a), B = nodes.get(i + 1 + '\u0001' + b), id = A.id + '\u0002' + B.id;
  if (!links.has(id)) links.set(id, { a: A, b: B, v: 0 });
  links.get(id).v += r.count;
}));
links.forEach(l => { l.a.out += l.v; l.b.in += l.v; });

const chart = document.getElementById('chart');
let sel = null;
if (!nodes.size || !(total > 0)) {
  chart.append(h('p', { class: 'none', text: label('none') }));
} else {
  const NW = 16, GAP = 30, TOP = 50, n = stages.length, maxNodes = Math.max(...cols.map(c => c.length));
  const W = 1100, H = Math.max(580, TOP + 6 + maxNodes * GAP + 200);
  const XS = stages.map((_, i) => n === 1 ? (W - NW) / 2 : 260 + i * 600 / (n - 1));
  const sc = (H - TOP - (maxNodes - 1) * GAP - 6) / Math.max(...cols.map(c => c.reduce((t, x) => t + x.count, 0)));
  cols.forEach((col, st) => { let y = TOP; col.forEach(nd => { const hh = Math.max(4, nd.count * sc); Object.assign(nd, { x: XS[st], y, h: hh, outY: y, inY: y }); y += hh + GAP; }); });
  const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });
  stages.forEach((a, i) => svg.append(s('text', {
    x: XS[i] + (n > 1 && i === 0 ? NW : n > 1 && i === n - 1 ? 0 : NW / 2), y: 14,
    'text-anchor': n > 1 && i === 0 ? 'end' : n > 1 && i === n - 1 ? 'start' : 'middle', 'font-weight': 600, text: text('stage' + (i + 1)) || a
  })));
  const flows = [...links.values()].map(l => {
    const A = l.a, B = l.b, hh = Math.max(1.5, l.v * sc), y0 = A.outY, y1 = B.inY, x0 = A.x + NW, x1 = B.x, mx = (x0 + x1) / 2;
    A.outY += hh; B.inY += hh;
    const p = s('path', { class: 'flow f-' + cls[A.tone], d: `M${x0} ${y0}C${mx} ${y0} ${mx} ${y1} ${x1} ${y1}L${x1} ${y1 + hh}C${mx} ${y1 + hh} ${mx} ${y0 + hh} ${x0} ${y0 + hh}Z` });
    RD.hover(p, text('flowTitle').replace('{from}', A.name).replace('{to}', B.name), [`${int(l.v)} ${model.unit}`]);
    p.addEventListener('click', () => { sel = A; show(); });
    svg.append(p); return Object.assign(l, { p });
  });
  [...nodes.values()].forEach(nd => {
    const st = nd.st, last = n > 1 && st === n - 1, first = n > 1 && st === 0;
    const r = s('rect', { class: 'node ' + (nd.isDup ? 'dupnode' : 'f-' + cls[nd.tone]), x: nd.x, y: nd.y, width: NW, height: nd.h, rx: 3 });
    r.addEventListener('click', () => { sel = nd; show(); }); RD.hover(r, nd.name, [`${int(nd.count)} ${model.unit}`]);
    nd.el = r;
    const t = s('text', {
      class: 'lbl', x: first ? nd.x - 8 : last ? nd.x + NW + 8 : nd.x + NW / 2, y: first || last ?nd.y + Math.min(nd.h / 2, 14) + 4 : nd.y - 6,
      'text-anchor': first ? 'end' : last ? 'start' : 'middle', text: `${nd.name}, ${int(nd.count)}`
    });
    t.addEventListener('click', () => { sel = nd; show(); });
    svg.append(r, t);
  });
  chart.append(svg);

  const all = [...nodes.values()];
  const pick = opt('selected');
  sel = all.find(x => pick && x.name === pick) ?? all.reduce((best, x) => (x.by.break ?? 0) > (best.by.break ?? 0) ? x : best, all[0]);

  function show() {
    all.forEach(x => x.el.classList.toggle('on', x === sel));
    flows.forEach(f => f.p.setAttribute('class', 'flow f-' + cls[f.a.tone] + (f.a === sel || f.b === sel ? ' hot' : ' dim')));
    const ins = flows.filter(f => f.b === sel), outs = flows.filter(f => f.a === sel), v = sel.count;
    document.getElementById('dt').textContent = text('detailTitle').replace('{node}', sel.name).replace('{count}', int(v)).replace('{share}', (v / total * 100).toFixed(1));
    document.getElementById('ds').textContent = groupInfo([sel.name])?.note ?? '';
    const row = (dir, nd, c) => h('tr', {}, [h('td', { text: dir }), h('td', { text: nd.name }), h('td', { class: 'n', text: int(c) }), h('td', { class: 'n', text: (v > 0 ? c / v * 100 : 0).toFixed(0) + '%' })]);
    document.getElementById('detail').replaceChildren(h('table', { class: 'tbl flows' }, [
      h('thead', {}, h('tr', {}, [h('th'), h('th', { text: text('blockHead') }), h('th', { class: 'n', text: text('countHead') }), h('th', { class: 'n', text: text('shareHead') })])),
      h('tbody', {}, [...ins.map(f => row(text('cameFrom'), f.a, f.v)), ...outs.map(f => row(text('wentTo'), f.b, f.v))])]));
  }
  show();
}
