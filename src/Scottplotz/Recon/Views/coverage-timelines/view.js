const { h, s, model, label, text, opt, fmt, int, clip } = RD;
RD.tieOut(document.getElementById('tie'));

const startKey = opt('startField') || 'start';
const endKey = opt('endField') || 'end';

function parseDate(v) {
  if (v === null || v === undefined || v === '') return null;
  if (typeof v === 'number') return v;
  const parts = String(v).split(/[-/T ]/);
  if (parts.length >= 3) {
    const y = +parts[0], m = +parts[1], d = +parts[2];
    if (!isNaN(y) && !isNaN(m) && !isNaN(d)) return Date.UTC(y, m - 1, d);
  }
  const t = Date.parse(v);
  return isNaN(t) ? null : t;
}

const names = RD.groupNames(model.records);
const sorted = opt('sortBy') === 'status'
  ? [...model.records].sort((a, b) => a.status.localeCompare(b.status))
  : opt('sortBy') === 'name'
  ? [...model.records].sort((a, b) => (a.name || a.key).localeCompare(b.name || b.key))
  : model.records;
const ordered = names.length
  ? names.flatMap(g => sorted.filter(r => r.group[0] === g)).concat(sorted.filter(r => !r.group || r.group[0] === undefined))
  : sorted;
const D = ordered.slice(0, opt('maxRows') || 200);

const totalRecords = model.records.reduce((sum, r) => sum + (r.count || 1), 0);
const shownRecords = D.reduce((sum, r) => sum + (r.count || 1), 0);
document.getElementById('chartSub').textContent = text('chartSub') +
  (D.length < model.records.length ? ' ' + text('truncated').replace('{shown}', int(shownRecords)).replace('{total}', int(totalRecords)) : '');

let minT = Infinity, maxT = -Infinity;
D.forEach(r => {
  ['left', 'right'].forEach(side => {
    if (r[side]) {
      const sVal = parseDate(r[side][startKey]);
      const eVal = parseDate(r[side][endKey]);
      if (sVal !== null) { minT = Math.min(minT, sVal); maxT = Math.max(maxT, sVal); }
      if (eVal !== null) { minT = Math.min(minT, eVal); maxT = Math.max(maxT, eVal); }
    }
  });
});
const hasDates = isFinite(minT) && isFinite(maxT);
if (!hasDates) {
  const yr = model.asOf && !isNaN(new Date(model.asOf).getTime()) ? new Date(model.asOf).getUTCFullYear() : 1970;
  minT = Date.UTC(yr, 0, 1);
  maxT = Date.UTC(yr, 11, 31);
  document.getElementById('chartSub').textContent += ' ' + text('noDates');
}
if (minT === maxT) maxT = minT + 86400000;

const W = 1100, X0 = 170, X1 = W - 150, RH = 36, TOP = 26;
const x = t => X0 + (t - minT) / (maxT - minT || 1) * (X1 - X0);

const svg = s('svg', { viewBox: `0 0 ${W} ${TOP + D.length * RH + 4}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

const MN = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const fmtField = (key, v) => {
  const f = RD.field(key), t = f && f.type === 'date' ? parseDate(v) : null;
  if (t === null) return fmt(key, v);
  const d = new Date(t);
  return d.getUTCDate() + ' ' + MN[d.getUTCMonth()] + (new Date(minT).getUTCFullYear() !== new Date(maxT).getUTCFullYear() ? ' ' + d.getUTCFullYear() : '');
};
const dMin = new Date(minT), dMax = new Date(maxT);
const ticks = [];
let cur = new Date(Date.UTC(dMin.getUTCFullYear(), dMin.getUTCMonth(), 1));
const endT = new Date(Date.UTC(dMax.getUTCFullYear(), dMax.getUTCMonth() + 1, 1));
const multiYear = dMin.getUTCFullYear() !== dMax.getUTCFullYear();
while (cur <= endT) {
  const t = cur.getTime();
  if (t >= minT && t <= maxT) {
    const lbl = MN[cur.getUTCMonth()] + (multiYear ? " '" + String(cur.getUTCFullYear()).slice(2) : '');
    ticks.push({ t, label: lbl });
  }
  cur.setUTCMonth(cur.getUTCMonth() + 1);
}
if (ticks.length < 2) {
  for (let i = 0; i <= 4; i++) {
    const t = minT + i * (maxT - minT) / 4;
    const d = new Date(t);
    ticks.push({ t, label: `${d.getUTCDate()} ${MN[d.getUTCMonth()]}` });
  }
}
ticks.forEach(tk => {
  svg.append(
    s('line', { class: 'gridl', x1: x(tk.t), x2: x(tk.t), y1: TOP - 6, y2: TOP + D.length * RH }),
    s('text', { class: 'mut', x: x(tk.t) + 4, y: 14, text: tk.label })
  );
});

let sel = D.find(r => r.status === 'break') ?? D[0];
const hits = new Map();
const DAY = 86400000;

D.forEach((r, i) => {
  const y = TOP + i * RH, cy = y + RH / 2;
  const hit = s('rect', { class: 'rowhit', x: 0, y, width: W, height: RH, rx: 4 });
  const np = { 'pointer-events': 'none' };
  hits.set(r, hit);
  svg.append(hit);

  const lStart = r.left ? parseDate(r.left[startKey]) : null;
  const lEnd = r.left ? parseDate(r.left[endKey]) : null;
  const rStart = r.right ? parseDate(r.right[startKey]) : null;
  const rEnd = r.right ? parseDate(r.right[endKey]) : null;
  const hasLeft = lStart !== null && lEnd !== null;
  const hasRight = rStart !== null && rEnd !== null;

  hit.addEventListener('click', () => { sel = r; show(); });
  const lHover = hasLeft ? `${fmtField(startKey, r.left[startKey])} – ${fmtField(endKey, r.left[endKey])}` : label('none');
  const rHover = hasRight ? `${fmtField(startKey, r.right[startKey])} – ${fmtField(endKey, r.right[endKey])}` : label('none');
  RD.hover(hit, r.name === r.key ? r.name : `${r.key}, ${r.name}`, [`${model.left.short}: ${lHover}`, `${model.right.short}: ${rHover}`]);

  svg.append(s('text', { x: 6, y: cy + 4, text: clip(r.name || r.key, 22), ...np }));

  const bar = (a, b, cls, yy, hh) => svg.append(s('rect', { class: cls, x: x(a), y: yy, width: Math.max(3, x(b) - x(a)), height: hh, rx: 3, ...np }));
  const otherBreak = Boolean(r.breaks && r.breaks.some(k => k !== startKey && k !== endKey));

  if (hasLeft && hasRight) {
    const a = Math.max(lStart, rStart), b = Math.min(lEnd, rEnd);
    if (a <= b) {
      bar(a, b, otherBreak ? 'f-brk' : 'f-ex', cy - 8, 16);
      if (lStart < a) bar(lStart, a, 'f-l', cy - 8, 7);
      if (lEnd > b) bar(b, lEnd, 'f-l', cy - 8, 7);
      if (rStart < a) bar(rStart, a, 'f-r', cy + 1, 7);
      if (rEnd > b) bar(b, rEnd, 'f-r', cy + 1, 7);
      if (r.duplicate) svg.append(s('rect', { x: x(a) + 3, y: cy - 5, width: Math.max(0, x(b) - x(a) - 6), height: 10, rx: 2, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, ...np }));
    } else {
      bar(lStart, lEnd, 'f-l', cy - 8, 7);
      bar(rStart, rEnd, 'f-r', cy + 1, 7);
    }
  } else if (hasLeft) {
    bar(lStart, lEnd, 'f-l', cy - 8, 7);
    if (r.duplicate) svg.append(s('rect', { x: x(lStart) + 2, y: cy - 7, width: Math.max(0, x(lEnd) - x(lStart) - 4), height: 5, rx: 1.5, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, ...np }));
  } else if (hasRight) {
    bar(rStart, rEnd, 'f-r', cy + 1, 7);
    if (r.duplicate) svg.append(s('rect', { x: x(rStart) + 2, y: cy + 2, width: Math.max(0, x(rEnd) - x(rStart) - 4), height: 5, rx: 1.5, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, ...np }));
  }

  let note = '';
  if (r.duplicate) {
    note = label('duplicate');
  } else if (hasLeft && hasRight) {
    const diffDays = Math.round(Math.abs(lStart - rStart) / DAY) + Math.round(Math.abs(lEnd - rEnd) / DAY);
    if (diffDays === 0) note = otherBreak ? (text('otherBreak') || label('break')) : label('agrees');
    else note = text('daysDiffer').replace('{days}', int(diffDays));
  } else if (hasLeft || hasRight) {
    const durDays = Math.round(Math.abs((lEnd ?? rEnd) - (lStart ?? rStart)) / DAY) + 1;
    note = text('daysDiffer').replace('{days}', int(durDays));
  } else {
    note = label(r.status);
  }

  const isMuted = (r.status === 'exact' && !r.duplicate) || (hasLeft && hasRight && !otherBreak && !r.duplicate && lStart === rStart && lEnd === rEnd);
  svg.append(s('text', { class: isMuted ? 'sec' : '', x: W - 6, y: cy + 4, 'text-anchor': 'end', text: note, ...np }));
});

document.getElementById('chart').append(svg);

function show() {
  hits.forEach((el, r) => el.setAttribute('class', r === sel ? 'rowhit on' : 'rowhit'));
  const r = sel;
  if (!r) return;
  document.getElementById('dt').textContent = r.name || r.key;
  document.getElementById('ds').textContent = [...r.group, r.name === r.key ? null : r.key].filter(Boolean).join(', ') +
    (r.group.length || r.name !== r.key ? '. ' : '') + RD.statusWord(r) + '.';

  const row = f => {
    const a = RD.val(r, 'left', f.key);
    const b = RD.val(r, 'right', f.key);
    const differs = a !== null && b !== null && a !== b;
    return h('tr', {}, [
      h('td', { text: f.label }),
      h('td', { class: (f.type === 'number' ? 'n ' : '') + (differs ? 'dl' : '') + (a === null ? ' none' : ''), text: fmtField(f.key, a) }),
      h('td', { class: (f.type === 'number' ? 'n ' : '') + (differs ? 'dr' : ''), text: fmtField(f.key, b) })
    ]);
  };

  document.getElementById('detail').replaceChildren(
    h('table', { class: 'tbl', style: 'max-width:640px' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') }),
        h('th', { text: model.left.short }),
        h('th', { text: model.right.short })
      ])),
      h('tbody', {}, model.fields.map(row))
    ]),
    h('p', { class: 'why', text: r.reason || (r.status === 'exact' ? text('noReason') : '') })
  );
}
if (sel) show();
