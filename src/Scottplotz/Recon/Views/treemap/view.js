const { h, s, model, text, label, opt, val, int, money, signed, fmt, fmtSigned, clip } = RD;
const M = model.measure, SCALE = Math.max(opt('colourScale') || 12, 0.0001);
// Fill the placeholders that only this page knows; the kit has already filled the rest.
const fill = (key, f) => Object.keys(f).reduce((t, k) => t.replace('{' + k + '}', f[k]), text(key));
RD.tieOut(document.getElementById('tie'));
document.getElementById('s1').style.background = RD.diff(-.5);
document.getElementById('s2').style.background = RD.diff(.5);

// Level 1 and level 2 names of a record. A missing level falls back to the level above, or to the "no group" text.
const l1 = r => r.group[0] ?? text('ungrouped');
const l2 = r => r.group[1] ?? l1(r);
// Area of a record: its measure, or its count when the page has no measure.
const wt = r => M ? Math.abs(val(r, 'left') ?? val(r, 'right') ?? 0) : r.count;
const cnt = rs => rs.reduce((t, r) => t + r.count, 0);
const sumw = rs => rs.reduce((t, r) => t + wt(r), 0);
// Percent difference of the right side over the left, on records that have both sides.
const rel = rs => {
  const pr = rs.filter(r => val(r, 'left') !== null && val(r, 'right') !== null), l = pr.reduce((t, r) => t + val(r, 'left'), 0);
  return l ? (pr.reduce((t, r) => t + val(r, 'right'), 0) - l) / l * 100 : null;
};
const tone = d => RD.diff(d === null ? 0 : Math.max(-1, Math.min(1, d / SCALE)));
const compact = v => v.toLocaleString('en-US', { notation: 'compact', maximumFractionDigits: 1 });
const unpaired = rs => cnt(rs.filter(r => !['exact', 'tolerance', 'break'].includes(r.status)));

const names1 = [...new Set([...RD.groupNames(model.records, 0), ...model.records.map(l1)])];
const lobs = names1.map(name => {
  const recs = model.records.filter(r => l1(r) === name);
  return { name, recs, kids: [...new Set(recs.map(l2))].map(b => ({ name: b, recs: recs.filter(r => l2(r) === b) })) };
}).filter(g => g.recs.length);

// Open on the subgroup with the largest paired difference.
const gap = rs => Math.abs(rs.reduce((t, r) => t + (val(r, 'left') !== null && val(r, 'right') !== null ? val(r, 'right') - val(r, 'left') : 0), 0));
let sel = null, best = -1, zoom = null;
lobs.forEach((g, li) => g.kids.forEach((k, bi) => { const v = gap(k.recs); if (v > best) { best = v; sel = { lob: li, broker: bi, rec: null }; } }));
const W = 1100, H = 440, HD = 24;

function draw() {
  const groups = zoom === null
    ? lobs.map((g, li) => ({ name: g.name, li, kids: g.kids.map((b, bi) => ({ name: b.name, v: sumw(b.recs), d: rel(b.recs), key: { lob: li, broker: bi, rec: null }, extra: unpaired(b.recs), n: cnt(b.recs) })) }))
    : lobs[zoom].kids.map((b, bi) => ({ name: b.name, kids: b.recs.map(r => {
        const l = val(r, 'left'), rt = val(r, 'right');
        return { name: r.key, v: wt(r), d: l !== null && rt !== null && l !== 0 ? (rt - l) / l * 100 : null, st: r.status, extra: 0, n: r.count, key: { lob: zoom, broker: bi, rec: r } };
      }) }));
  groups.forEach(g => { g.kids = g.kids.filter(k => k.v > 0); g.gv = g.kids.reduce((a, k) => a + k.v, 0); });
  const shown = groups.filter(g => g.gv > 0), total = shown.reduce((t, g) => t + g.gv, 0);
  const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });
  let x0 = 0;
  shown.forEach(g => {
    const w = g.gv / total * W; let y0 = HD;
    const hd = s('text', { class: zoom === null ? 'colhd' : '', x: x0 + 4, y: 16, 'font-weight': 600, text: w > 90 ? g.name : g.name.slice(0, Math.max(3, Math.floor(w / 8))) });
    if (zoom === null) hd.addEventListener('click', () => { zoom = g.li; sel = { lob: g.li, broker: 0, rec: null }; draw(); });
    svg.append(hd);
    g.kids.forEach(k => {
      const hh = k.v / g.gv * (H - HD);
      const on = k.key.lob === sel.lob && k.key.broker === sel.broker && (zoom === null || k.key.rec === sel.rec);
      const f = k.st === 'leftOnly' ? 'var(--left)' : k.st === 'rightOnly' ? 'var(--right)' : tone(k.d);
      const t = s('rect', { class: 'tile' + (on ? ' on' : ''), x: x0, y: y0, width: w, height: hh, rx: 3, style: `fill:${f}` });
      t.addEventListener('click', () => { sel = k.key; draw(); });
      const diffLine = k.d !== null ? fill('tipDiff', { pct: signed(k.d, 1) })
        : k.st === 'leftOnly' || k.st === 'rightOnly' ? label(k.st) : text('tipNoPairs');
      RD.hover(t, k.name, [M ? fill('tipValue', { value: money(k.v, RD.field(M).decimals) }) : `${int(k.n)} ${model.unit}`, diffLine]);
      svg.append(t);
      if (hh > 34 && w > 110) svg.append(s('text', { x: x0 + 8, y: y0 + 17, 'pointer-events': 'none', text: clip(k.name, Math.max(6, Math.floor(w / 8))) }),
        s('text', { class: 'sec', x: x0 + 8, y: y0 + 32, 'pointer-events': 'none', text: clip((M ? compact(k.v) : int(k.n)) + (k.d ? ', ' + signed(k.d, 1) + '%' : '') + (k.extra ? ', ' + fill('unpaired', { n: int(k.extra) }) : ''), Math.max(6, Math.floor((w - 16) / 6.5))) }));
      y0 += hh;
    });
    x0 += w;
  });
  document.getElementById('chart').replaceChildren(shown.length ? svg : h('p', { class: 'sub', text: text('noData') }));
  document.getElementById('ct').textContent = zoom === null ? text('chartTitle') : fill('zoomTitle', { group: lobs[zoom].name });
  document.getElementById('crumbs').replaceChildren(zoom === null ? text('allGroups') : h('button', { text: text('back'), on: { click: () => { zoom = null; sel.rec = null; draw(); } } }));
  detail();
}

function detail() {
  const out = document.getElementById('detail');
  if (!sel) { out.replaceChildren(); return; }
  const g = lobs[sel.lob], b = g.kids[sel.broker], rs = b.recs, d = rel(rs);
  const total = M ? fmt(M, rs.reduce((t, r) => t + (val(r, 'left') ?? val(r, 'right') ?? 0), 0)) : '';
  document.getElementById('dt').textContent = fill('detailTitle', { subgroup: b.name, group: g.name });
  document.getElementById('ds').textContent = (M ? fill('detailSub', { n: int(cnt(rs)), total }) : `${int(cnt(rs))} ${model.unit}.`) + (d === null ? '' : ' ' + fill('detailDiff', { pct: signed(d, 1) }));
  const G = { exact: '=', tolerance: '≈', break: '≠' };
  const hasReason = rs.some(r => r.reason);
  out.replaceChildren(h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [h('th', { text: '' }), h('th', { text: text('colRecord') }), h('th', { text: text('colName') }),
      h('th', { class: 'n', text: model.left.short }), h('th', { class: 'n', text: model.right.short }), h('th', { class: 'n', text: text('colDifference') }), hasReason ? h('th', { text: text('colWhy') }) : null])),
    h('tbody', {}, rs.map(r => {
      const l = val(r, 'left'), rt = val(r, 'right'), df = r.status === 'break';
      return h('tr', { class: r === sel.rec ? 'on' : '' }, [
        h('td', { text: (G[r.status] ?? label(r.status).toLowerCase()) + (r.duplicate ? ' ' + label('duplicate').toLowerCase() : '') }),
        h('td', { text: r.key + (r.count > 1 ? ' ×' + int(r.count) : '') }), h('td', { text: r.name === r.key ? '' : r.name }),
        h('td', { class: 'n' + (df ? ' dl' : '') + (l === null ? ' none' : ''), text: M ? fmt(M, l) : '' }),
        h('td', { class: 'n' + (df ? ' dr' : '') + (rt === null ? ' none' : ''), text: M ? fmt(M, rt) : '' }),
        h('td', { class: 'n', text: M && r.delta ? fmtSigned(M, r.delta) : '' }), hasReason ? h('td', { text: r.reason || text('noReason') }) : null]);
    }))]));
}
draw();
