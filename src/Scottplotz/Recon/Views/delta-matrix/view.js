const { h, model, label, text, opt, fmt, fmtSigned, val, clip } = RD;
RD.tieOut(document.getElementById('tie'));

const s1 = document.getElementById('s1'), s2 = document.getElementById('s2');
if (s1) s1.style.background = RD.diff(-0.5);
if (s2) s2.style.background = RD.diff(0.5);
document.getElementById('chartSub').textContent = text('chartSub');

const numFields = model.fields.filter(f => f.type === 'number');
const cols = numFields.filter(f => f.compare !== false);
if (!cols.length) cols.push(...numFields);
if (!cols.length) cols.push(...model.fields);

const names = RD.groupNames(model.records);
const groups = names.length
  ? names.map(g => ({ name: g, records: model.records.filter(r => r.group[0] === g) }))
      .concat(model.records.some(r => !r.group.length) ? [{ name: '', records: model.records.filter(r => !r.group.length) }] : [])
  : [{ name: '', records: model.records }];

const rows = groups.flatMap(g => g.records);
const colourScale = opt('colourScale') || 25;

const cells = new Map();
const tbl = h('table', { class: 'mx' });
const headerName = (text('record') || model.unitOne).replace(/^./, c => c.toUpperCase());
tbl.append(h('tr', {}, [h('th', { class: 'nm', text: headerName }), ...cols.map(c => h('th', { text: c.label }))]));

let curRi = 0;
groups.forEach(g => {
  if (g.name) {
    tbl.append(h('tr', { class: 'dept' }, [h('td', { colspan: cols.length + 1, text: g.name })]));
  }
  g.records.forEach(r => {
    const ri = curRi++;
    const isLeftOnly = r.status === 'leftOnly';
    const isRightOnly = r.status === 'rightOnly';
    const isSingle = isLeftOnly || isRightOnly;
    const nmKids = [r.duplicate ? h('i', { class: 'sw dup' }) : null, clip(r.name || r.key, 28)].filter(Boolean);
    const nm = h('td', { class: 'nm' }, nmKids);

    if (isSingle) {
      const bandColor = isLeftOnly ? 'var(--left)' : 'var(--right)';
      const bandText = isLeftOnly ? text('leftOnlyBand') : text('rightOnlyBand');
      const band = h('td', {
        class: 'band',
        colspan: Math.max(1, cols.length),
        tabindex: 0,
        style: `background:${bandColor}`,
        text: bandText,
        on: {
          click: () => { sel = [ri, 0]; show(); },
          keydown: ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); sel = [ri, 0]; show(); } }
        }
      });
      RD.hover(band, r.name || r.key, [bandText]);
      cells.set(ri + '|band', band);
      tbl.append(h('tr', {}, [nm, band]));
      return;
    }

    const tds = cols.map((col, ci) => {
      const l = val(r, 'left', col.key);
      const rt = val(r, 'right', col.key);
      const d = l !== null && rt !== null ? rt - l : null;
      const tol = col.tolerance?.absolute ?? model.tolerance?.absolute ?? 0;
      const isBreak = r.breaks ? r.breaks.includes(col.key) : (d !== null && Math.abs(d) > tol);
      const isTol = !isBreak && d !== null && d !== 0;

      let t = 0;
      if (d !== null && d !== 0) {
        const denom = (l !== null && l !== 0) ? Math.abs(l) : 1;
        const pct = (d / denom) * 100;
        t = Math.max(-1, Math.min(1, pct / colourScale));
      }

      const clsName = 'c' + (isBreak ? ' d' : isTol ? ' tol' : '');
      const bg = isBreak ? `background:${RD.diff(t)}` : '';
      const txt = (d === null || d === 0) ? '' : fmtSigned(col.key, d);

      const td = h('td', {
        class: clsName,
        tabindex: 0,
        style: bg,
        text: txt,
        on: {
          click: () => { sel = [ri, ci]; show(); },
          keydown: ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); sel = [ri, ci]; show(); } }
        }
      });

      RD.hover(td, `${r.name || r.key}, ${col.label}`, [
        `${model.left.short} ${fmt(col.key, l)}`,
        `${model.right.short} ${fmt(col.key, rt)}`
      ]);
      cells.set(ri + '|' + ci, td);
      return td;
    });

    tbl.append(h('tr', {}, [nm, ...tds]));
  });
});

const footTds = cols.map(col => {
  let sum = 0, hasVal = false;
  rows.filter(r => r.status !== 'leftOnly' && r.status !== 'rightOnly').forEach(r => {
    const l = val(r, 'left', col.key);
    const rt = val(r, 'right', col.key);
    if (l !== null || rt !== null) {
      hasVal = true;
      sum += (rt ?? 0) - (l ?? 0);
    }
  });
  return h('td', { text: hasVal ? fmtSigned(col.key, sum) : '' });
});
tbl.append(h('tr', { class: 'tot' }, [
  h('td', { class: 'nm', style: 'text-align:left', text: text('netDifference') }),
  ...footTds
]));
document.getElementById('chart').append(tbl);

let sel = [0, 0];
const brkIdx = rows.findIndex(r => r.status === 'break');
if (brkIdx >= 0) {
  const brk = rows[brkIdx];
  let ci = 0;
  if (brk.attrs?.rootCause) {
    const found = cols.findIndex(c => c.key === brk.attrs.rootCause);
    if (found >= 0) ci = found;
  } else if (brk.breaks?.length) {
    const found = cols.findIndex(c => c.key === brk.breaks[0]);
    if (found >= 0) ci = found;
  }
  sel = [brkIdx, ci];
}

function show() {
  cells.forEach(td => td.classList.remove('on'));
  const [ri, ci] = sel;
  const r = rows[ri];
  if (!r) return;
  const isSingle = r.status === 'leftOnly' || r.status === 'rightOnly';
  const target = isSingle ? cells.get(ri + '|band') : cells.get(ri + '|' + ci);
  if (target) target.classList.add('on');

  document.getElementById('dt').textContent = r.name || r.key;
  const grpText = r.group?.length ? r.group.join(', ') + '. ' : '';
  document.getElementById('ds').textContent = grpText + RD.statusWord(r) + '.';

  const selCol = cols[ci] || cols[0];
  let bigText = '';
  if (isSingle) {
    bigText = r.status === 'leftOnly' ? text('leftOnlyDetail') : text('rightOnlyDetail');
  } else if (selCol) {
    const l = val(r, 'left', selCol.key);
    const rt = val(r, 'right', selCol.key);
    const d = l !== null && rt !== null ? rt - l : null;
    bigText = selCol.label + ': ' + (d === null ? label('none') : d === 0 ? label('agrees') : fmtSigned(selCol.key, d));
  }

  const body = cols.map(c => {
    const l = val(r, 'left', c.key);
    const rt = val(r, 'right', c.key);
    const diff = l !== null && rt !== null ? rt - l : null;
    const differs = diff !== null && diff !== 0;
    const isSelected = !isSingle && c === selCol;
    return h('tr', { style: isSelected ? 'font-weight:600' : '' }, [
      h('td', { text: c.label }),
      h('td', { class: 'n' + (differs ? ' dl' : '') + (l === null ? ' none' : ''), text: fmt(c.key, l) }),
      h('td', { class: 'n' + (differs ? ' dr' : '') + (rt === null ? ' none' : ''), text: fmt(c.key, rt) }),
      h('td', { class: 'n', text: diff === null || diff === 0 ? '' : fmtSigned(c.key, diff) })
    ]);
  });

  let follows = '';
  const rootKey = r.attrs?.rootCause;
  if (!isSingle && rootKey && selCol && selCol.key !== rootKey) {
    const rootCol = RD.field(rootKey) || cols.find(c => c.key === rootKey);
    const l = val(r, 'left', selCol.key);
    const rt = val(r, 'right', selCol.key);
    const diff = l !== null && rt !== null ? rt - l : null;
    if (rootCol && diff !== null && diff !== 0) {
      follows = ' ' + text('followsFrom').replace('{cause}', rootCol.label.toLowerCase());
    }
  }

  const whyText = (r.reason || (r.status === 'break' || r.status === 'tolerance' ? '' : text('allAgree'))) + follows;
  const detailTable = h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [
      h('th', { text: text('component') }),
      h('th', { class: 'n', text: model.left.short }),
      h('th', { class: 'n', text: model.right.short }),
      h('th', { class: 'n', text: text('difference') })
    ])),
    h('tbody', {}, body)
  ]);

  document.getElementById('detail').replaceChildren(
    h('div', { class: 'big', text: bigText }),
    detailTable,
    h('p', { class: 'why', text: whyText })
  );
}

show();
