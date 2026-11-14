const { h, s, model, label, text, opt, fmt, fmtSigned, val, int } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const leftTotal = model.summary.leftTotal ?? 0;
const rightTotal = model.summary.rightTotal ?? 0;

const groupNames = RD.groupNames();
const stepNames = groupNames.filter(g => model.records.some(r => r.group && r.group[0] === g));
const ungrouped = model.records.filter(r => (!r.group || !r.group.length) && r.status !== 'exact' && (val(r, 'left') !== null || val(r, 'right') !== null));

const steps = [];
stepNames.forEach(name => {
  const recs = model.records.filter(r => r.group && r.group[0] === name);
  if (!recs.length) return;
  const gInfo = RD.groupInfo([name]);
  const effect = recs.reduce((sum, r) => sum + ((val(r, 'right') ?? 0) - (val(r, 'left') ?? 0)), 0);

  let cls = 'tol';
  if (gInfo?.attrs?.cls) {
    cls = gInfo.attrs.cls;
  } else if (recs.some(r => r.duplicate)) {
    cls = 'dup';
  } else if (recs.every(r => r.status === recs[0].status)) {
    cls = RD.cls[recs[0].status] ?? 'tol';
  } else if (recs.some(r => r.status === 'break')) {
    cls = 'brk';
  } else if (recs.some(r => r.status === 'rightOnly')) {
    cls = 'r';
  } else if (recs.some(r => r.status === 'leftOnly')) {
    cls = 'l';
  }

  steps.push({
    name,
    cls,
    note: gInfo?.note || '',
    records: recs,
    effect
  });
});

if (ungrouped.length) {
  const effect = ungrouped.reduce((sum, r) => sum + ((val(r, 'right') ?? 0) - (val(r, 'left') ?? 0)), 0);
  steps.push({
    name: text('other') || 'Other',
    cls: 'tol',
    note: '',
    records: ungrouped,
    effect
  });
}

const explained = leftTotal + steps.reduce((sum, st) => sum + st.effect, 0);
const unexplained = rightTotal - explained;
if ((opt('showUnexplained') ?? true) && Math.abs(unexplained) >= 0.005) {
  steps.push({
    name: text('unexplained'),
    cls: 'tol',
    note: text('unexplainedNote'),
    records: [],
    effect: unexplained
  });
}

function niceTicks(min, max, n = 5) {
  const span = max - min;
  if (!(span > 0)) return [min];
  const raw = span / n;
  const p = 10 ** Math.floor(Math.log10(raw));
  const m = raw / p;
  const step = (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * p;
  const start = Math.ceil(min / step) * step;
  const out = [];
  for (let v = start; v <= max + step * 0.001; v += step) {
    out.push(+v.toPrecision(12));
  }
  return out.length ? out : [min];
}

function tickFmt(v) {
  if (Math.abs(v) >= 1e6 && v % 1e5 === 0) return (v / 1e6).toLocaleString('en-US', { maximumFractionDigits: 2 }) + 'M';
  if (Math.abs(v) >= 1e3 && v % 1e2 === 0) return (v / 1e3).toLocaleString('en-US', { maximumFractionDigits: 2 }) + 'K';
  return M ? fmt(M.key, v) : int(v);
}

const W = 1100, RH = 44, X0 = 250, X1 = 930, top = 34;
const rows = [
  { name: text('leftEnding'), mark: leftTotal },
  ...steps,
  { name: text('rightEnding'), mark: rightTotal }
];

let run = leftTotal;
const pts = [leftTotal, rightTotal];
steps.forEach(st => {
  st.from = run;
  run += st.effect;
  st.to = run;
  pts.push(run);
});

const lo = Math.min(...pts);
const hi = Math.max(...pts);
const span = (hi - lo) || (Math.abs(lo) * 0.1) || 1;
const pad = span * 0.06;
const minVal = lo - pad;
const maxVal = hi + pad;
const x = v => X0 + (v - minVal) / (maxVal - minVal || 1) * (X1 - X0);

const ticks = niceTicks(minVal, maxVal, 5);
const H = top + rows.length * RH + 6;
const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

ticks.forEach(t => {
  svg.append(
    s('line', { class: 'gridl', x1: x(t), x2: x(t), y1: top - 6, y2: top + rows.length * RH }),
    s('text', { class: 'mut', x: x(t), y: 14, 'text-anchor': 'middle', text: tickFmt(t) })
  );
});

let sel = rows.findIndex(r => !('mark' in r));
if (sel === -1) sel = 0;
const hits = [];

rows.forEach((r, i) => {
  const y = top + i * RH, cy = y + RH / 2;
  const hit = s('rect', { class: 'row-hit', x: 0, y, width: W, height: RH, rx: 4 });
  svg.append(hit);
  hits.push(hit);
  svg.append(s('text', { x: 6, y: cy + 4, text: r.name, 'font-weight': 'mark' in r ? 600 : 400, 'pointer-events': 'none' }));

  if ('mark' in r) {
    svg.append(s('line', { x1: x(r.mark), x2: x(r.mark), y1: cy - 13, y2: cy + 13, stroke: 'var(--ink)', 'stroke-width': 3, 'stroke-linecap': 'round', 'pointer-events': 'none' }));
    svg.append(s('text', { x: W - 6, y: cy + 4, 'text-anchor': 'end', 'font-weight': 600, text: fmt(M?.key, r.mark), 'pointer-events': 'none' }));
  } else {
    const a = x(Math.min(r.from, r.to)), b = x(Math.max(r.from, r.to));
    svg.append(s('rect', { class: r.cls === 'dup' ? 'dupbar' : 'f-' + r.cls, x: a, y: cy - 10, width: Math.max(3, b - a), height: 20, rx: 3, 'pointer-events': 'none' }));
    svg.append(s('text', { x: W - 6, y: cy + 4, 'text-anchor': 'end', text: fmtSigned(M?.key, r.effect), 'pointer-events': 'none' }));
    const count = r.records.reduce((sum, rec) => sum + (rec.count || 1), 0);
    const countText = count ? `${int(count)} ${count === 1 ? model.unitOne : model.unit}` : '';
    svg.append(s('text', { class: 'mut', x: b + 8, y: cy + 4, text: countText, 'pointer-events': 'none' }));
    hit.addEventListener('click', () => { sel = i; show(); });
    RD.hover(hit, r.name, [fmtSigned(M?.key, r.effect), r.note]);
  }

  const next = 'mark' in r ? r.mark : r.to;
  if (i < rows.length - 1) {
    svg.append(s('line', { class: 'axl', x1: x(next), x2: x(next), y1: cy + ('mark' in r ? 13 : 10), y2: cy + RH - 10, 'pointer-events': 'none' }));
  }
});

document.getElementById('chart').append(svg);

function show() {
  hits.forEach((e, i) => e.setAttribute('class', i === sel ? 'row-hit row-sel' : 'row-hit'));
  const st = rows[sel];
  if (!st) return;
  if ('mark' in st) {
    document.getElementById('dt').textContent = `${st.name}: ${fmt(M?.key, st.mark)}`;
    document.getElementById('ds').textContent = '';
    document.getElementById('detail').replaceChildren();
    return;
  }
  document.getElementById('dt').textContent = `${st.name}: ${fmtSigned(M?.key, st.effect)}`;
  document.getElementById('ds').textContent = st.note || '';
  const cell = v => h('td', { class: 'n' + (v === null || v === undefined ? ' none' : ''), text: v === null || v === undefined ? label('none') : fmt(M?.key, v) });
  const body = st.records.map(r => {
    const l = val(r, 'left');
    const rt = val(r, 'right');
    const eff = (rt || 0) - (l || 0);
    return h('tr', {}, [
      h('td', { text: r.date || '' }),
      h('td', { text: r.key || '' }),
      h('td', { text: r.name || '' }),
      cell(l),
      cell(rt),
      h('td', { class: 'n', text: fmtSigned(M?.key, eff) })
    ]);
  });
  const table = h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [
      h('th', { text: text('date') }),
      h('th', { text: text('reference') }),
      h('th', { text: text('description') }),
      h('th', { class: 'n', text: model.left.short }),
      h('th', { class: 'n', text: model.right.short }),
      h('th', { class: 'n', text: text('effect') })
    ])),
    h('tbody', {}, body),
    h('tfoot', {}, h('tr', {}, [
      h('td', { colspan: 5, text: text('stepTotal') }),
      h('td', { class: 'n', text: fmtSigned(M?.key, st.effect) })
    ]))
  ]);
  document.getElementById('detail').replaceChildren(
    st.records.length ? table : h('p', { class: 'sub', text: text('noItems') })
  );
}
if (rows.length) show();
