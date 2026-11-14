const { h, model, label, text, opt, fmt, val, int, signed } = RD;
RD.tieOut(document.getElementById('tie'));

const numFields = model.fields.filter(f => f.type === 'number');
const names = RD.groupNames();
const groupItems = names.length
  ? names.map(name => ({ name, path: [name] }))
  : [{ name: text('allGroups'), path: [] }];

function getGroupData(item) {
  const gInfo = RD.groupInfo(item.path);
  const pKey = item.path.join('\u0001');
  const groupRecs = model.records.filter(r => (item.path.length === 0 ? true : r.group.slice(0, item.path.length).join('\u0001') === pKey));

  const rows = numFields.map(f => {
    let l = null, r = null;
    if (gInfo?.left && typeof gInfo.left === 'object' && gInfo.left[f.key] !== undefined) {
      l = gInfo.left[f.key];
    } else if (gInfo && typeof gInfo.left === 'number' && f.key === model.measure) {
      l = gInfo.left;
    } else {
      let sum = 0, count = 0;
      for (const rec of groupRecs) {
        const v = val(rec, 'left', f.key);
        if (v !== null && v !== undefined) {
          sum += v * (rec.count ?? 1);
          count += (rec.count ?? 1);
        }
      }
      l = count > 0 ? sum : null;
    }

    if (gInfo?.right && typeof gInfo.right === 'object' && gInfo.right[f.key] !== undefined) {
      r = gInfo.right[f.key];
    } else if (gInfo && typeof gInfo.right === 'number' && f.key === model.measure) {
      r = gInfo.right;
    } else {
      let sum = 0, count = 0;
      for (const rec of groupRecs) {
        const v = val(rec, 'right', f.key);
        if (v !== null && v !== undefined) {
          sum += v * (rec.count ?? 1);
          count += (rec.count ?? 1);
        }
      }
      r = count > 0 ? sum : null;
    }

    let tick = 'ex';
    if (l === null && r === null) {
      tick = 'ex';
    } else if (l === null || r === null) {
      tick = 'brk';
    } else if (l === r) {
      tick = 'ex';
    } else {
      const diff = Math.abs(r - l);
      const tol = f.tolerance?.absolute !== undefined ? f.tolerance.absolute : (opt('totalTolerance') ?? 1);
      tick = tol > 0 && diff <= tol ? 'tol' : 'brk';
    }
    return { f, l, r, tick };
  });

  const exceptions = groupRecs.filter(rec => rec.status !== 'exact' || rec.duplicate);
  return { ...item, gInfo, groupRecs, rows, ticks: rows.map(rw => rw.tick), exceptions };
}

const groups = groupItems.map(getGroupData);
let sel = 0, maxBrk = -1;
groups.forEach((g, i) => {
  const brks = g.ticks.filter(t => t === 'brk').length;
  if (brks > maxBrk) { maxBrk = brks; sel = i; }
});

const SYM = { ex: '✓', tol: '≈', brk: '≠' };
const tick = t => h('span', {
  class: 'tk ' + (t === 'ex' ? '' : t),
  title: label(t === 'ex' ? 'exact' : t === 'tol' ? 'tolerance' : 'break'),
  text: SYM[t]
});

const btns = groups.map((g, i) => h('button', {
  class: 'grp',
  on: { click: () => { sel = i; show(); } }
}, [
  h('b', { text: g.name }),
  h('div', { class: 'ticks' }, g.ticks.map(tick))
]));
document.getElementById('list').append(...btns);

function show() {
  btns.forEach((b, i) => b.setAttribute('aria-current', String(i === sel)));
  const g = groups[sel];
  if (!g) return;

  const bad = g.ticks.filter(t => t === 'brk').length;
  const tot = g.rows.length;
  const headingText = bad > 0
    ? text('totalsBreak').replace('{group}', g.name).replace('{bad}', int(bad)).replace('{total}', int(tot))
    : text('totalsFoot').replace('{group}', g.name).replace('{total}', int(tot));
  document.getElementById('ct').textContent = headingText;

  document.getElementById('sheetSub').textContent = text('sheetSub').replace('{tolerance}', RD.money(opt('totalTolerance') ?? 1, 2));

  document.getElementById('sheet').replaceChildren(h('table', { class: 'tbl sheet' }, [
    h('thead', {}, h('tr', {}, [
      h('th', { text: text('controlTotal') }),
      h('th', { class: 'n', text: model.left.short }),
      h('th', { class: 'n', text: model.right.short }),
      h('th', { class: 'n', text: text('difference') }),
      h('th', {})
    ])),
    h('tbody', {}, g.rows.map(rw => {
      const df = rw.tick === 'brk';
      const d = rw.l !== null && rw.r !== null ? rw.r - rw.l : null;
      const diffStr = rw.l === rw.r ? '' : d === null ? (rw.l === null ? label('rightOnly') : label('leftOnly')) : signed(d, rw.f.decimals ?? 2);
      return h('tr', {}, [
        h('td', { text: rw.f.label }),
        h('td', { class: 'n' + (df ? ' dl' : '') + (rw.l === null ? ' none' : ''), text: fmt(rw.f.key, rw.l) }),
        h('td', { class: 'n' + (df ? ' dr' : '') + (rw.r === null ? ' none' : ''), text: fmt(rw.f.key, rw.r) }),
        h('td', { class: 'n', text: diffStr }),
        h('td', { class: 'tc' }, tick(rw.tick))
      ]);
    }))
  ]));

  const exCount = g.exceptions.reduce((sum, r) => sum + (r.count ?? 1), 0);
  const uWord = exCount === 1 ? model.unitOne : model.unit;
  document.getElementById('dt').textContent = exCount > 0
    ? text('exceptionsTitle').replace('{count}', int(exCount)).replace('{unit}', uWord).replace('{group}', g.name)
    : text('noExceptionsTitle').replace('{unit}', model.unit).replace('{group}', g.name);

  const mKey = model.measure || numFields[0]?.key;
  const mF = mKey ? RD.field(mKey) : null;
  const mLabel = mF ? mF.label + ', ' : '';
  const leftColTitle = `${mLabel}${model.left.short}`;
  const rightColTitle = `${mLabel}${model.right.short}`;

  const c = (v, cls) => h('td', { class: 'n' + cls + (v === null ? ' none' : ''), text: v === null ? label('none') : fmt(mKey, v) });

  if (g.exceptions.length > 0) {
    document.getElementById('detail').replaceChildren(h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('result') }),
        h('th', { text: text('record') }),
        h('th', { text: text('name') }),
        h('th', { class: 'n', text: leftColTitle }),
        h('th', { class: 'n', text: rightColTitle }),
        h('th', { text: text('why') })
      ])),
      h('tbody', {}, g.exceptions.map(r => {
        const a = val(r, 'left', mKey);
        const b = val(r, 'right', mKey);
        const df = r.status === 'break';
        const sym = r.duplicate ? '×2 ' : r.status === 'break' ? '≠ ' : r.status === 'tolerance' ? '≈ ' : '';
        const stWord = sym + (r.duplicate && r.status === 'exact' ? label('duplicate') : RD.statusWord(r));
        return h('tr', {}, [
          h('td', { text: stWord }),
          h('td', { text: r.key }),
          h('td', { text: r.name }),
          c(a, df ? ' dl' : ''),
          c(b, df ? ' dr' : ''),
          h('td', { text: r.reason || text('noReason') })
        ]);
      }))
    ]));
  } else {
    document.getElementById('detail').replaceChildren(h('p', { class: 'sub', text: text('pickGroupPrompt') }));
  }
}
show();
