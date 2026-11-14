const { h, s, model, text, opt, fmt, int, clip, val } = RD;
RD.tieOut(document.getElementById('tie'));

const sm = model.summary, c = sm.counts;
const unpaired = [
  [c.leftOnly, 'l', text('leftOnlyDesc')],
  [c.rightOnly, 'r', text('rightOnlyDesc')],
  [sm.duplicates, 'dup', text('duplicateDesc')]
];
document.getElementById('figs').replaceChildren(
  ...unpaired.map(([v, sw, t]) =>
    h('div', { class: 'fig' }, [
      h('b', {}, [
        h('i', { class: 'sw ' + sw, style: 'width:14px;height:14px;margin-right:10px' }),
        int(v)
      ]),
      h('span', { text: t })
    ])
  )
);

const breakRecords = model.records.filter(r => r.breaks && r.breaks.length > 0);
const fieldMap = new Map(model.fields.map(f => [f.key, f]));
const allFieldKeys = [];
model.fields.forEach(f => {
  if (breakRecords.some(r => r.breaks.includes(f.key))) allFieldKeys.push(f.key);
});
breakRecords.forEach(r => {
  r.breaks.forEach(k => {
    if (!allFieldKeys.includes(k)) allFieldKeys.push(k);
  });
});

const comboMap = new Map();
breakRecords.forEach(r => {
  const sortedBreaks = [...r.breaks].sort((a, b) => allFieldKeys.indexOf(a) - allFieldKeys.indexOf(b));
  const key = sortedBreaks.join('|||');
  let entry = comboMap.get(key);
  if (!entry) {
    entry = { key, fields: sortedBreaks, count: 0, records: [] };
    comboMap.set(key, entry);
  }
  entry.count += (r.count || 1);
  entry.records.push(r);
});

const maxCombos = opt('maxCombos') || 9;
const combos = [...comboMap.values()].sort((a, b) => b.count - a.count).slice(0, maxCombos);

function getTopAttr(records, attrKey) {
  if (!attrKey) return null;
  const counts = new Map();
  records.forEach(r => {
    const v = r.attrs && r.attrs[attrKey];
    if (v !== undefined && v !== null && v !== '') {
      counts.set(v, (counts.get(v) || 0) + (r.count || 1));
    }
  });
  let top = null, maxC = -1;
  counts.forEach((cnt, v) => {
    if (cnt > maxC) { maxC = cnt; top = v; }
  });
  return top;
}

if (!combos.length) {
  document.getElementById('chart').replaceChildren(h('p', { class: 'sub', text: text('noBreaks') }));
  document.getElementById('dt').textContent = text('noBreaks');
  document.getElementById('ds').textContent = '';
  document.getElementById('detail').replaceChildren();
} else {
  const F = allFieldKeys
    .filter(fkey => combos.some(k => k.fields.includes(fkey)))
    .map(fkey => fieldMap.get(fkey) || { key: fkey, label: fkey });

  combos.forEach(k => {
    k.fieldIndices = k.fields.map(fkey => F.findIndex(f => f.key === fkey)).filter(i => i >= 0).sort((a, b) => a - b);
  });

  const W = 820, X0 = 250, CW = combos.length ? (W - X0 - 10) / combos.length : 40;
  const BH = 190, RH = 34, MT = BH + 30;
  const tot = F.map(f => combos.filter(k => k.fields.includes(f.key)).reduce((t, k) => t + k.count, 0));
  const maxT = Math.max(...tot, 1);
  const maxComboCount = Math.max(...combos.map(k => k.count), 1);
  const ticks = RD.ticks(maxComboCount, 4);
  const maxTick = ticks[ticks.length - 1] || 1;

  const svg = s('svg', {
    viewBox: `0 0 ${W} ${MT + F.length * RH + 10}`,
    width: '100%',
    role: 'img',
    'aria-label': text('chartTitle')
  });

  ticks.forEach(t => {
    const y = BH + 14 - (t / maxTick) * BH;
    svg.append(
      s('line', { class: t ? 'gridl' : 'axl', x1: X0, x2: W - 10, y1: y, y2: y }),
      s('text', { class: 'mut', x: X0 - 8, y: y + 4, 'text-anchor': 'end', text: int(t) })
    );
  });

  F.forEach((f, fi) => {
    const y = MT + fi * RH + RH / 2;
    const bw = (tot[fi] / maxT) * 100;
    svg.append(
      s('text', { x: 0, y: y + 4, text: clip(f.label, 18) }),
      s('rect', { class: 'f-tol', x: X0 - 34 - bw, y: y - 7, width: bw, height: 14, rx: 3 }),
      s('text', { class: 'sec', x: X0 - 40 - bw, y: y + 4, 'text-anchor': 'end', text: int(tot[fi]) })
    );
    if (fi % 2 === 0) {
      svg.append(s('rect', { x: X0, y: y - RH / 2, width: W - X0 - 10, height: RH, fill: 'var(--wash)', 'pointer-events': 'none' }));
    }
  });

  let sel = 0;
  const hits = [];
  const capAttr = opt('captionAttr');

  combos.forEach((k, ki) => {
    const cx = X0 + ki * CW + CW / 2;
    const bh = (k.count / maxTick) * BH;
    const hit = s('rect', { class: 'colhit', x: cx - CW / 2, y: 0, width: CW, height: MT + F.length * RH + 6, rx: 4 });
    hits.push(hit);
    svg.append(hit);
    hit.addEventListener('click', () => { sel = ki; show(); });

    const topVal = capAttr ? getTopAttr(k.records, capAttr) : null;
    const fNames = k.fieldIndices.map(i => F[i].label).join(` ${text('and') || 'and'} `);
    RD.hover(hit, fNames, [
      `${int(k.count)} ${k.count === 1 ? (model.unitOne || model.unit) : model.unit}`,
      ...(topVal ? [text('mostly').replace('{value}', topVal)] : [])
    ]);

    const np = { 'pointer-events': 'none' };
    const bw = Math.min(22, Math.max(6, CW - 6));
    svg.append(
      s('rect', { class: 'f-brk', x: cx - bw / 2, y: BH + 14 - bh, width: bw, height: bh, rx: 3, ...np }),
      s('text', { x: cx, y: BH + 8 - bh, 'text-anchor': 'middle', text: int(k.count), ...np })
    );

    if (k.fieldIndices.length > 1) {
      svg.append(s('line', {
        x1: cx, x2: cx,
        y1: MT + Math.min(...k.fieldIndices) * RH + RH / 2,
        y2: MT + Math.max(...k.fieldIndices) * RH + RH / 2,
        stroke: 'var(--brk)', 'stroke-width': 3, ...np
      }));
    }

    F.forEach((_, fi) => {
      const isInc = k.fieldIndices.includes(fi);
      svg.append(s('circle', {
        cx, cy: MT + fi * RH + RH / 2,
        r: isInc ? 7 : 4,
        class: isInc ? 'f-brk' : '',
        fill: isInc ? null : 'var(--axis)',
        ...np
      }));
    });
  });

  document.getElementById('chart').replaceChildren(svg);

  function show() {
    hits.forEach((e, i) => e.setAttribute('class', i === sel ? 'colhit on' : 'colhit'));
    const k = combos[sel];
    if (!k) return;

    const names = k.fieldIndices.map(i => F[i].label);
    const titleStr = (text('detailTitle') || '{count} {unit} break on {fields}')
      .replace('{count}', int(k.count))
      .replace('{unit}', k.count === 1 ? (model.unitOne || model.unit) : model.unit)
      .replace('{fields}', names.join(` ${text('and') || 'and'} `).toLowerCase());
    document.getElementById('dt').textContent = titleStr;

    const reason = k.records.find(r => r.reason)?.reason;
    const topVal = capAttr ? getTopAttr(k.records, capAttr) : null;
    const mostlyStr = topVal ? (text('mostly') || 'Mostly {value}.').replace('{value}', topVal) : '';
    const sampleLimit = opt('sampleRows') || 6;
    const shown = k.records.slice(0, sampleLimit);
    const showingStr = (text('showing') || 'Showing {shown}.').replace('{shown}', int(shown.length));
    const subParts = [reason, mostlyStr, showingStr].filter(Boolean);
    document.getElementById('ds').textContent = subParts.join(' ');

    const hasName = k.records.some(r => r.name && r.name !== r.key);
    const headerCols = [
      h('th', { text: text('trade') || 'Record' }),
      ...(hasName ? [h('th', { text: text('name') || 'Name' })] : []),
      ...(capAttr ? [h('th', { text: capAttr.charAt(0).toUpperCase() + capAttr.slice(1) })] : []),
      ...k.fieldIndices.flatMap(fi => [
        h('th', { class: 'n', text: `${F[fi].label}, ${model.left.short}` }),
        h('th', { class: 'n', text: `${F[fi].label}, ${model.right.short}` })
      ])
    ];

    const bodyRows = shown.map(r => {
      const cells = [
        h('td', { text: r.key }),
        ...(hasName ? [h('td', { text: r.name || '' })] : []),
        ...(capAttr ? [h('td', { text: (r.attrs && r.attrs[capAttr]) || '' })] : []),
        ...k.fieldIndices.flatMap(fi => {
          const f = F[fi];
          const lv = val(r, 'left', f.key);
          const rv = val(r, 'right', f.key);
          return [
            h('td', { class: 'n dl' + (lv === null ? ' none' : ''), text: fmt(f.key, lv) }),
            h('td', { class: 'n dr' + (rv === null ? ' none' : ''), text: fmt(f.key, rv) })
          ];
        })
      ];
      return h('tr', {}, cells);
    });

    document.getElementById('detail').replaceChildren(
      h('table', { class: 'tbl' }, [
        h('thead', {}, h('tr', {}, headerCols)),
        h('tbody', {}, bodyRows)
      ])
    );
  }

  show();
}
