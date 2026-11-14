const { h, model, label, text, opt, fmt, val, int } = RD;
RD.tieOut(document.getElementById('tie'));

const statusRank = r => {
  if (r.duplicate) return 3;
  switch (r.status) {
    case 'exact': return 0;
    case 'tolerance': return 1;
    case 'break': return 2;
    case 'rightOnly': return 4;
    case 'leftOnly': return 5;
    default: return 6;
  }
};

const names = RD.groupNames();
const groupList = names.length > 0 ? names : [null];
const chart = document.getElementById('chart');
const allButtons = [];
let budget = opt('maxSquares') ?? 600;
let totalDrawn = 0;
const totalRecords = model.records.reduce((sum, r) => sum + (r.count || 1), 0);

groupList.forEach(g => {
  const recs = g === null ? model.records : model.records.filter(r => r.group[0] === g);
  if (recs.length === 0) return;
  const sorted = [...recs].sort((a, b) => statusRank(a) - statusRank(b));
  const totalCount = sorted.reduce((sum, r) => sum + (r.count || 1), 0);
  const openCount = sorted.reduce((sum, r) => sum + ((r.status !== 'exact' && r.status !== 'tolerance') || r.duplicate ? (r.count || 1) : 0), 0);
  const kids = [];
  if (g !== null) {
    const gInfo = RD.groupInfo([g]);
    kids.push(h('h3', { text: gInfo?.name || g }));
  }
  kids.push(h('p', {
    text: text('groupSub')
      .replace('{count}', int(totalCount))
      .replace('{unit}', totalCount === 1 ? model.unitOne : model.unit)
      .replace('{open}', int(openCount))
  }));
  const wf = h('div', { class: 'wf' });
  kids.push(wf);

  sorted.forEach(r => {
    const count = r.count || 1;
    const toDraw = Math.min(count, Math.max(0, budget));
    budget -= toDraw;
    totalDrawn += toDraw;
    for (let i = 0; i < toDraw; i++) {
      const b = h('button', {
        class: 'u ' + (r.duplicate ? 'dup' : r.status === 'exact' ? '' : (RD.cls[r.status] || r.status)),
        'aria-label': `${r.name || r.key}, ${RD.statusWord(r)}`,
        on: { click: () => { sel = r; show(); } }
      });
      RD.hover(b, r.name === r.key ? r.key : `${r.key}, ${r.name}`, [
        RD.statusWord(r) + (count > 1 ? ` (${int(count)} ${model.unit})` : '')
      ]);
      allButtons.push({ el: b, rec: r });
      wf.append(b);
    }
  });

  chart.append(h('div', { class: 'site' }, kids));
});

const subText = text('chartSub');
const truncText = totalDrawn < totalRecords
  ? ' ' + text('truncated').replace('{shown}', int(totalDrawn)).replace('{total}', int(totalRecords)).replace('{unit}', model.unit)
  : '';
document.getElementById('chartSub').textContent = (subText ? subText : '') + truncText;

let sel = model.records.find(r => r.status === 'leftOnly' && (r.group[0] || '').includes('204'))
  || model.records.find(r => r.status === 'break')
  || model.records.find(r => r.status === 'leftOnly')
  || model.records[0]
  || null;

function show() {
  allButtons.forEach(({ el, rec }) => el.classList.toggle('on', rec === sel));
  if (!sel) {
    document.getElementById('dt').textContent = '';
    document.getElementById('ds').textContent = '';
    document.getElementById('detail').replaceChildren();
    return;
  }
  const r = sel;
  document.getElementById('dt').textContent = (r.name && r.name !== r.key ? `${r.key}, ${r.name}` : r.key) + (r.group.length ? `, ${r.group.join(', ')}` : '');
  document.getElementById('ds').textContent = RD.statusWord(r) + '.';
  const cell = (side, f, differs) => {
    const v = val(r, side, f.key);
    return h('td', {
      class: (f.type === 'number' ? 'n' : '') + (differs ? (side === 'left' ? ' dl' : ' dr') : '') + (v === null || v === undefined ? ' none' : ''),
      text: fmt(f.key, v)
    });
  };
  const row = f => {
    const a = val(r, 'left', f.key), b = val(r, 'right', f.key);
    const differs = r.breaks ? r.breaks.includes(f.key) : (a !== null && b !== null && a !== b);
    return h('tr', {}, [
      h('td', { text: f.label || f.key }),
      cell('left', f, differs),
      cell('right', f, differs)
    ]);
  };
  document.getElementById('detail').replaceChildren(
    h('div', { class: 'big', text: r.name || r.key }),
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') }),
        h('th', { text: model.left.short || model.left.name }),
        h('th', { text: model.right.short || model.right.name })
      ])),
      h('tbody', {}, model.fields.map(row))
    ]),
    h('p', { class: 'why', text: r.reason || text('allAgree') })
  );
}

if (sel) show();
