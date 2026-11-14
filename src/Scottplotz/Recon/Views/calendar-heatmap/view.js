const { h, model, label, text, opt, fmt, val, signed, money, clip, diff } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const s1 = document.getElementById('s1'), s2 = document.getElementById('s2');
if (s1) s1.style.background = diff(-0.5);
if (s2) s2.style.background = diff(0.5);

let rowNames = RD.groupNames(model.records, 0);
if (!rowNames.length) {
  rowNames = [...new Set(model.records.map(r => r.group[0] || r.name || ''))].filter(Boolean);
  if (!rowNames.length) rowNames = [''];
}

const rowNet = name => model.records.filter(r => (r.group[0] || r.name || '') === name)
  .reduce((t, r) => t + ((val(r, 'right') ?? 0) - (val(r, 'left') ?? 0)), 0);

if (opt('sortBy') === 'name') {
  rowNames.sort((a, b) => a.localeCompare(b));
} else if (opt('sortBy') === 'net') {
  rowNames.sort((a, b) => Math.abs(rowNet(b)) - Math.abs(rowNet(a)));
}

const dateStrings = [...new Set(model.records.map(r => r.date ? r.date.slice(0, 10) : null).filter(Boolean))].sort();
const allDates = [];
if (dateStrings.length > 0) {
  const d0 = new Date(dateStrings[0] + 'T00:00:00');
  const d1 = new Date(dateStrings[dateStrings.length - 1] + 'T00:00:00');
  const span = Math.round((d1 - d0) / 86400000) + 1;
  if (span >= 1 && span <= 45) {
    for (let cur = new Date(d0); cur <= d1; cur.setDate(cur.getDate() + 1)) {
      const y = cur.getFullYear(), m = String(cur.getMonth() + 1).padStart(2, '0'), d = String(cur.getDate()).padStart(2, '0');
      allDates.push(`${y}-${m}-${d}`);
    }
  } else {
    allDates.push(...dateStrings);
  }
}

const cellMap = new Map();
model.records.forEach(r => {
  if (r.date) {
    const k = (r.group[0] || r.name || '') + '|' + r.date.slice(0, 10);
    cellMap.set(k, r);
  }
});

const WD = 'SMTWTFS';
const tbl = h('table', { class: 'mx' });
const headerCells = [h('th', { class: 'nm', text: '' })];

allDates.forEach(ds => {
  const dt = new Date(ds + 'T00:00:00');
  const wd = isNaN(dt.getTime()) ? 0 : dt.getDay();
  const dayNum = isNaN(dt.getTime()) ? ds.slice(-2) : String(dt.getDate());
  headerCells.push(h('th', { class: wd === 0 || wd === 6 ? 'we' : '' }, [WD[wd] ?? '', h('br'), dayNum]));
});
headerCells.push(h('th', { text: text('total') || 'Total' }));
tbl.append(h('tr', {}, headerCells));

const cells = new Map();
let selRec = [...model.records].filter(r => r.status === 'break' && r.date).sort((p, q) => Math.abs(q.delta ?? 0) - Math.abs(p.delta ?? 0))[0] ?? model.records.find(r => r.delta !== 0) ?? model.records[0] ?? null;
let selKey = selRec ? ((selRec.group[0] || selRec.name || '') + '|' + (selRec.date ? selRec.date.slice(0, 10) : '')) : '';

rowNames.forEach(gName => {
  const rowRecs = model.records.filter(r => (r.group[0] || r.name || '') === gName);
  const rowNetVal = rowRecs.reduce((t, r) => t + ((val(r, 'right') ?? 0) - (val(r, 'left') ?? 0)), 0);
  const rowCells = [h('td', { class: 'nm', text: clip(gName, 24) })];

  allDates.forEach(ds => {
    const k = gName + '|' + ds;
    const r = cellMap.get(k);
    if (!r) {
      rowCells.push(h('td', {}));
      return;
    }
    const lVal = val(r, 'left'), rVal = val(r, 'right');
    let fill = '';
    if (r.status === 'leftOnly') {
      fill = 'var(--left)';
    } else if (r.status === 'rightOnly') {
      fill = 'var(--right)';
    } else if (r.status === 'break' && r.delta !== null) {
      const denom = (lVal !== null && lVal !== 0) ? Math.abs(lVal) : 1;
      const t = Math.sign(r.delta) * Math.min(1, 0.3 + (Math.abs(r.delta) / denom) * 4);
      fill = diff(t);
    }
    const isTol = r.status === 'tolerance';
    const isDup = !!r.duplicate;
    const td = h('td', {
      class: 'c' + (isTol ? ' tol' : '') + (isDup ? ' dup' : ''),
      tabindex: 0,
      style: fill ? `background:${fill}` : '',
      on: {
        click: () => { selRec = r; selKey = k; show(); },
        keydown: ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); selRec = r; selKey = k; show(); } }
      }
    });
    cells.set(k, td);
    const dt = new Date(ds + 'T00:00:00');
    const dayLabel = isNaN(dt.getTime()) ? ds : `${dt.getDate()} ${dt.toLocaleString('en-US', { month: 'short' })}`;
    RD.hover(td, `${gName}, ${dayLabel}`, [
      `${model.left.short} ${lVal === null ? label('none') : fmt(M, lVal)}`,
      `${model.right.short} ${rVal === null ? label('none') : fmt(M, rVal)}`
    ]);
    rowCells.push(td);
  });

  rowCells.push(h('td', { class: 'tot', text: signed(rowNetVal, 0) }));
  tbl.append(h('tr', {}, rowCells));
});

const chartEl = document.getElementById('chart');
if (chartEl) chartEl.append(allDates.length ? tbl : h('p', { class: 'sub', text: text('noDates') }));

function show() {
  cells.forEach((td, k) => td.classList.toggle('on', k === selKey));
  if (!selRec) return;
  const r = selRec;
  const gName = r.group[0] || r.name || '';
  const ds = r.date ? r.date.slice(0, 10) : '';
  const dt = ds ? new Date(ds + 'T00:00:00') : null;
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  const dateStr = dt && !isNaN(dt.getTime()) ? `${days[dt.getDay()]} ${dt.getDate()} ${months[dt.getMonth()]}` : ds;

  document.getElementById('dt').textContent = [gName, dateStr].filter(Boolean).join(', ');
  document.getElementById('ds').textContent = r.reason || (r.delta === 0 ? text('agreed') || label('agrees') : RD.statusWord(r));

  const otherFields = model.fields.filter(f => f.key !== M);
  const measureField = model.fields.find(f => f.key === M);
  const displayFields = otherFields.length > 0 ? otherFields : model.fields;

  const mkCell = (v, cls, key) => h('td', { class: 'n' + cls + (v === null ? ' none' : ''), text: v === null ? label('none') : fmt(key, v) });

  const rows = displayFields.map(f => {
    const l = val(r, 'left', f.key);
    const right = val(r, 'right', f.key);
    const d = l !== null && right !== null ? right - l : null;
    const tol = f.tolerance ?? model.tolerance.absolute ?? 0;
    const df = d !== null && Math.abs(d) > tol;
    return h('tr', {}, [
      h('td', { text: f.label }),
      mkCell(l, df ? ' dl' : '', f.key),
      mkCell(right, df ? ' dr' : '', f.key),
      h('td', { class: 'n', text: d !== null && d !== 0 ? RD.fmtSigned(f.key, d) : '' })
    ]);
  });

  let tfoot = null;
  if (otherFields.length > 0 && measureField) {
    const tl = val(r, 'left', M);
    const tr = val(r, 'right', M);
    const td = tl !== null && tr !== null ? tr - tl : null;
    tfoot = h('tfoot', {}, h('tr', {}, [
      h('td', { text: measureField.label }),
      mkCell(tl, '', M),
      mkCell(tr, '', M),
      h('td', { class: 'n', text: td !== null && td !== 0 ? RD.fmtSigned(M, td) : '' })
    ]));
  }

  const detailEl = document.getElementById('detail');
  if (detailEl) {
    detailEl.replaceChildren(h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') || 'Field' }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short }),
        h('th', { class: 'n', text: text('difference') || 'Difference' })
      ])),
      h('tbody', {}, rows),
      tfoot
    ].filter(Boolean)));
  }
}
show();
