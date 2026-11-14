const { h, s, model, label, text, opt, fmt, val, money, int } = RD;
RD.tieOut(document.getElementById('tie'));

const dateFieldKey = opt('dateField') || model.fields.find(f => f.type === 'date')?.key || '';

function toDateKey(v) {
  if (!v) return null;
  if (typeof v === 'string') return v.slice(0, 10);
  if (v instanceof Date) return v.toISOString().slice(0, 10);
  return String(v);
}

function parseDateKey(k) {
  const [y, m, d] = k.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function dayDiff(k1, k2) {
  if (!k1 || !k2) return null;
  return Math.round((parseDateKey(k2) - parseDateKey(k1)) / 86400000);
}

const fmtDay = dt => dt.getDate() + ' ' + dt.toLocaleDateString('en-US', { month: 'short' });
const fmtDayLong = dt => dt.getDate() + ' ' + dt.toLocaleDateString('en-US', { month: 'long' });

const allDateKeys = [];
model.records.forEach(r => {
  const lk = toDateKey(val(r, 'left', dateFieldKey));
  const rk = toDateKey(val(r, 'right', dateFieldKey));
  const dk = toDateKey(r.attrs?.dupDay || r.attrs?.dupDate);
  if (lk) allDateKeys.push(lk);
  if (rk) allDateKeys.push(rk);
  if (dk) allDateKeys.push(dk);
});

if (!allDateKeys.length) {
  document.getElementById('chart').append(h('p', { class: 'sub', text: text('noDates') }));
  return;
}
let minKey = allDateKeys[0];
let maxKey = minKey;
allDateKeys.forEach(k => {
  if (k < minKey) minKey = k;
  if (k > maxKey) maxKey = k;
});

const days = [];
const cur = parseDateKey(minKey);
const end = parseDateKey(maxKey);
while (cur <= end) {
  const y = cur.getFullYear();
  const m = String(cur.getMonth() + 1).padStart(2, '0');
  const d = String(cur.getDate()).padStart(2, '0');
  days.push(`${y}-${m}-${d}`);
  cur.setDate(cur.getDate() + 1);
}
if (days.length === 0) days.push(minKey);

const totalDays = days.length;
const dateIndex = new Map(days.map((k, i) => [k, i]));

const W = 1100, TOP = 150, BOT = 330, H = 470;
const X0 = 96, X1 = W - 24;
const x = i => X0 + (totalDays > 1 ? i / (totalDays - 1) : 0.5) * (X1 - X0);
const dw = totalDays > 1 ? (X1 - X0) / (totalDays - 1) : 40;

const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

const firstBreak = model.records.find(r => r.status === 'break' && toDateKey(val(r, 'left', dateFieldKey)));
const firstWithDate = model.records.find(r => toDateKey(val(r, 'left', dateFieldKey)) || toDateKey(val(r, 'right', dateFieldKey)));
let selKey = firstBreak ? toDateKey(val(firstBreak, 'left', dateFieldKey)) : (firstWithDate ? toDateKey(val(firstWithDate, 'left', dateFieldKey)) || toDateKey(val(firstWithDate, 'right', dateFieldKey)) : days[0]);

const cols = [];
const step = Math.max(1, Math.ceil(totalDays / 25));

days.forEach((dk, i) => {
  const colX = x(i) - dw / 2;
  const c = s('rect', { class: 'day' + (dk === selKey ? ' on' : ''), x: colX, y: 0, width: dw, height: H });
  cols.push(c);
  svg.append(c);
  c.addEventListener('click', () => { selKey = dk; show(); });
  const dt = parseDateKey(dk);
  if (i % step === 0) {
    svg.append(s('text', { class: 'mut', x: x(i), y: 14, 'text-anchor': 'middle', 'pointer-events': 'none', text: fmtDay(dt) }));
  }
});

[[TOP, model.left.short, -8], [BOT, model.right.short, 18]].forEach(([y, t, dy]) => {
  svg.append(
    s('line', { class: 'axl', x1: 30, x2: W - 10, y1: y, y2: y }),
    s('text', { x: 0, y: y + dy, 'font-weight': 600, text: t })
  );
});

const up = {}, down = {}, slot = (m, d) => (m[d] = (m[d] || 0) + 1);
const order = { exact: 0, tolerance: 1, break: 2, leftOnly: 3, rightOnly: 3 };
const marks = [];

[...model.records].sort((a, b) => (order[a.status] ?? 0) - (order[b.status] ?? 0)).forEach(r => {
  const lk = toDateKey(val(r, 'left', dateFieldKey));
  const rk = toDateKey(val(r, 'right', dateFieldKey));
  const dk = toDateKey(r.attrs?.dupDay || r.attrs?.dupDate);

  const lIdx = lk !== null ? dateIndex.get(lk) : undefined;
  const rIdx = rk !== null ? dateIndex.get(rk) : undefined;
  const dIdx = dk !== null ? dateIndex.get(dk) : undefined;

  const ty = lIdx !== undefined ? TOP - 2 - slot(up, lIdx) * 12 + 6 : null;
  const by = rIdx !== undefined ? BOT + 2 - 6 + slot(down, rIdx) * 12 : null;
  const c = RD.cls[r.status] || 'ex';

  if (ty !== null && by !== null) {
    svg.append(s('line', { class: 'ln s-' + c, x1: x(lIdx), y1: ty, x2: x(rIdx), y2: by, 'pointer-events': 'none', opacity: r.status === 'exact' ? 0.55 : 1 }));
  }

  if (dIdx !== undefined && ty !== null) {
    const dy = BOT + 2 - 6 + slot(down, dIdx) * 12;
    svg.append(
      s('line', { class: 'ln s-brk', x1: x(lIdx), y1: ty, x2: x(dIdx), y2: dy, 'pointer-events': 'none' }),
      s('circle', { cx: x(dIdx), cy: dy, r: 8, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' })
    );
    marks.push([x(dIdx), dy, 'brk']);
  } else if (r.duplicate) {
    const cx = rIdx !== undefined ? x(rIdx) : (lIdx !== undefined ? x(lIdx) : null);
    const cy = by !== null ? by : ty;
    if (cx !== null && cy !== null) {
      svg.append(s('circle', { cx, cy, r: 8, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' }));
    }
  }

  if (r.status === 'leftOnly' && ty !== null) {
    svg.append(s('line', { class: 'ln s-l', x1: x(lIdx), y1: ty, x2: x(lIdx) + 14, y2: ty + 40, 'pointer-events': 'none', opacity: 0.5 }));
  }

  if (ty !== null) marks.push([x(lIdx), ty, c]);
  if (by !== null) marks.push([x(rIdx), by, c]);
});

marks.forEach(([cx, cy, c]) => svg.append(s('circle', { class: 'dot f-' + c, cx, cy, r: 4.5, 'pointer-events': 'none' })));
document.getElementById('chart').append(svg);

function show() {
  cols.forEach((c, i) => c.setAttribute('class', days[i] === selKey ? 'day on' : 'day'));
  const rows = model.records.filter(r => {
    const lk = toDateKey(val(r, 'left', dateFieldKey));
    const rk = toDateKey(val(r, 'right', dateFieldKey));
    return lk === selKey || (!lk && rk === selKey);
  });

  const dtObj = parseDateKey(selKey);
  document.getElementById('dt').textContent = text('dayTitle')
    .replace('{unit}', model.unit)
    .replace('{day}', fmtDayLong(dtObj));

  const totalCount = rows.reduce((s, r) => s + (r.count || 1), 0);
  const lateCount = rows.filter(r => r.status === 'break').reduce((s, r) => s + (r.count || 1), 0);

  if (totalCount > 0) {
    const unitWord = totalCount === 1 ? model.unitOne : model.unit;
    const baseSummary = text('daySummary').replace('{count}', int(totalCount)).replace('{unit}', unitWord);
    const lateText = lateCount > 0 ? text('lateSummary').replace('{count}', int(lateCount)) : '';
    document.getElementById('ds').textContent = baseSummary + lateText + '.';
  } else {
    document.getElementById('ds').textContent = text('emptyDay');
  }

  const statusMap = {
    exact: text('statusExact'),
    tolerance: text('statusTolerance'),
    break: text('statusBreak'),
    leftOnly: text('statusLeftOnly').replace('{leftShort}', model.left.short),
    rightOnly: text('statusRightOnly').replace('{rightShort}', model.right.short)
  };

  if (!rows.length) {
    document.getElementById('detail').replaceChildren(h('p', { class: 'sub', text: text('selectAnother') }));
    return;
  }

  const thead = h('thead', {}, h('tr', {}, [
    h('th', { text: text('resultCol') }),
    h('th', { text: text('recordCol').replace('{unitOne}', model.unitOne) }),
    h('th', { text: model.left.short }),
    h('th', { text: model.right.short }),
    h('th', { class: 'n', text: text('daysCol') }),
    h('th', { class: 'n', text: RD.measure ? RD.measure.label : text('amountCol') }),
    h('th', { text: text('noteCol') })
  ]));

  const tbody = h('tbody', {}, rows.map(r => {
    const lk = toDateKey(val(r, 'left', dateFieldKey));
    const rk = toDateKey(val(r, 'right', dateFieldKey));
    const dk = toDateKey(r.attrs?.dupDay || r.attrs?.dupDate);

    const stDesc = (statusMap[r.status] || label(r.status)) + (r.duplicate ? text('duplicateSuffix') : '');
    const lTxt = lk ? fmtDay(parseDateKey(lk)) : label('none');
    let rTxt = rk ? fmtDay(parseDateKey(rk)) : label('none');
    if (dk) rTxt += ' ' + text('and') + ' ' + fmtDay(parseDateKey(dk));

    const diff = dayDiff(lk, rk);
    const diffTxt = diff !== null ? int(diff) : '';

    const amtVal = val(r, 'left') ?? val(r, 'right');
    const amtTxt = amtVal !== null ? (RD.measure ? fmt(RD.measure.key, amtVal) : money(amtVal)) : label('none');

    const noteTxt = r.reason || (r.status === 'break' ? text('defaultBreakReason') : '');

    return h('tr', {}, [
      h('td', { text: stDesc }),
      h('td', { class: lk ? '' : 'none', text: r.name && r.name !== r.key ? `${r.key} ${r.name}` : r.key }),
      h('td', { class: lk ? '' : 'none', text: lTxt }),
      h('td', { class: rk ? '' : 'none', text: rTxt }),
      h('td', { class: 'n', text: diffTxt }),
      h('td', { class: 'n', text: amtTxt }),
      h('td', { text: noteTxt })
    ]);
  }));

  document.getElementById('detail').replaceChildren(h('table', { class: 'tbl' }, [thead, tbody]));
}

show();
