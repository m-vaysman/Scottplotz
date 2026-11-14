const { h, s, model, label, text, opt, fmt, int, clip } = RD;
const M = RD.measure;
RD.tieOut(document.getElementById('tie'));

const STATUSES = ['exact', 'tolerance', 'break', 'leftOnly', 'rightOnly'];
const K = ['ex', 'tol', 'brk', 'l', 'r'];

let groupNames = RD.groupNames(model.records, 0);
if (!groupNames.length && model.records.length) groupNames = [''];

let groups = groupNames.map(name => {
  const recs = model.records.filter(r => (r.group[0] ?? '') === name);
  const info = RD.groupInfo([name]);
  const counts = { exact: 0, tolerance: 0, break: 0, leftOnly: 0, rightOnly: 0 };
  const measureTotals = { exact: 0, tolerance: 0, break: 0, leftOnly: 0, rightOnly: 0 };
  const reasons = {};
  let dupCount = 0;
  let totalCount = 0;
  let totalMeasure = 0;

  recs.forEach(r => {
    const c = r.count || 1;
    const st = r.status || 'exact';
    counts[st] = (counts[st] || 0) + c;
    totalCount += c;
    if (r.duplicate) dupCount += c;

    const val = RD.val(r, 'left') ?? RD.val(r, 'right') ?? 0;
    measureTotals[st] = (measureTotals[st] || 0) + val;
    totalMeasure += val;

    if (r.reason && !reasons[st]) reasons[st] = r.reason;
  });

  const issues = (counts.break || 0) + (counts.leftOnly || 0) + (counts.rightOnly || 0);

  return {
    name: name || label('none'),
    rawName: name,
    info,
    counts,
    measureTotals,
    reasons,
    dupCount,
    count: totalCount,
    measureTotal: totalMeasure,
    issues
  };
});

if (opt('sortBy') === 'count') {
  groups.sort((a, b) => b.count - a.count);
} else if (opt('sortBy') === 'issues') {
  groups.sort((a, b) => b.issues - a.issues);
}

const displayGroups = groups.filter(g => g.count > 0);
const groupsToRender = displayGroups.length ? displayGroups : groups;

const initName = opt('initialGroup');
let sel = initName ? groupsToRender.findIndex(g => g.rawName === initName || g.name === initName) : -1;
if (sel < 0) {
  let maxIssues = -1;
  groupsToRender.forEach((g, i) => {
    if (g.issues > maxIssues) {
      maxIssues = g.issues;
      sel = i;
    }
  });
}
if (sel < 0) sel = 0;

const W = 1100, X0 = 46, X1 = W - 6, Y0 = 10, Y1 = 380, GAP = groupsToRender.length > 1 ? 6 : 0;
const totalVolume = groupsToRender.reduce((sum, g) => sum + g.count, 0) || 1;
const usable = Math.max(10, X1 - X0 - GAP * (groupsToRender.length - 1));

const chartEl = document.getElementById('chart');
chartEl.replaceChildren();

const svg = s('svg', { viewBox: `0 0 ${W} 440`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

[0, 25, 50, 75, 100].forEach(t => {
  svg.append(s('text', { class: 'mut', x: X0 - 8, y: Y1 - t / 100 * (Y1 - Y0) + 4, 'text-anchor': 'end', text: t + '%' }));
});

const frame = s('rect', { class: 'colsel', rx: 4, 'pointer-events': 'none' });
let x0 = X0;

groupsToRender.forEach((p, pi) => {
  const w = (p.count / totalVolume) * usable;
  let y = Y1;
  p.x = x0;
  p.w = w;

  STATUSES.forEach((st, i) => {
    const v = p.counts[st] || 0;
    const hh = p.count > 0 ? (v / p.count) * (Y1 - Y0) : 0;
    if (hh > 0 && w > 0) {
      const r = s('rect', { class: 'seg2 f-' + K[i], x: x0, y: y - hh, width: w, height: hh, rx: 3 });
      r.addEventListener('click', () => { sel = pi; show(); });
      RD.hover(r, `${p.name}: ${label(st).toLowerCase()}`, [
        `${int(v)} ${p.count === 1 ? model.unitOne : model.unit}`,
        `${(p.count > 0 ? (v / p.count * 100) : 0).toFixed(1)}%`
      ]);
      svg.append(r);

      if (hh > 22 && w > 60) {
        svg.append(s('text', {
          class: i === 0 ? 'txt-on-exact' : 'txt-on-color',
          x: x0 + 8,
          y: y - hh + 17,
          'pointer-events': 'none',
          text: (v / p.count * 100).toFixed(0) + '%'
        }));
      }
    }
    y -= hh;
  });

  const labelText = w > 70 ? p.name : clip(p.name.split(' ')[0], Math.max(4, Math.floor(w / 6)));
  const textGroup = s('g', { style: 'cursor:pointer' }, [
    s('text', { x: x0 + w / 2, y: Y1 + 18, 'text-anchor': 'middle', 'font-weight': 600, text: labelText }),
    s('text', { class: 'sec', x: x0 + w / 2, y: Y1 + 34, 'text-anchor': 'middle', text: int(p.count) })
  ]);
  textGroup.addEventListener('click', () => { sel = pi; show(); });
  svg.append(textGroup);

  x0 += w + GAP;
});

svg.append(frame);
chartEl.append(svg);

function show() {
  const p = groupsToRender[sel];
  if (!p) return;

  frame.setAttribute('x', p.x - 3);
  frame.setAttribute('y', Y0 - 3);
  frame.setAttribute('width', Math.max(0, p.w + 6));
  frame.setAttribute('height', Y1 - Y0 + 6);

  const workPct = p.count > 0 ? ((p.issues / p.count) * 100).toFixed(0) : '0';
  document.getElementById('dt').textContent = text('detailTitle')
    .replace('{group}', p.name)
    .replace('{count}', int(p.count))
    .replace('{unit}', p.count === 1 ? model.unitOne : model.unit)
    .replace('{pct}', workPct);

  const note = p.info?.note || '';
  const dupText = p.dupCount > 0
    ? ' ' + text('acceptedTwice').replace('{count}', int(p.dupCount)).replace('{unit}', p.dupCount === 1 ? model.unitOne : model.unit)
    : '';
  document.getElementById('ds').textContent = (note + dupText).trim();

  const hasMeasure = Boolean(M);

  const theadRow = [
    h('th', { text: '' }),
    h('th', { text: text('result') }),
    h('th', { class: 'n', text: model.unit.charAt(0).toUpperCase() + model.unit.slice(1) }),
    h('th', { class: 'n', text: text('share') })
  ];
  if (hasMeasure) theadRow.push(h('th', { class: 'n', text: M.label }));
  theadRow.push(h('th', { text: text('usualCause') }));

  const tbodyRows = STATUSES.map((st, i) => {
    const v = p.counts[st] || 0;
    const share = p.count > 0 ? (v / p.count * 100).toFixed(1) + '%' : '0.0%';
    const mVal = p.measureTotals[st] || 0;
    const cause = p.reasons[st] || '';

    const cells = [
      h('td', {}, h('i', { class: 'sw ' + (K[i] === 'ex' ? '' : K[i]) })),
      h('td', { text: label(st) }),
      h('td', { class: 'n', text: int(v) }),
      h('td', { class: 'n', text: share })
    ];
    if (hasMeasure) cells.push(h('td', { class: 'n', text: RD.fmt(M.key, mVal) }));
    cells.push(h('td', { text: cause }));
    return h('tr', {}, cells);
  });

  const tfootCells = [
    h('td', { colspan: 2, text: text('total') }),
    h('td', { class: 'n', text: int(p.count) }),
    h('td', { class: 'n', text: '100%' })
  ];
  if (hasMeasure) tfootCells.push(h('td', { class: 'n', text: RD.fmt(M.key, p.measureTotal) }));
  tfootCells.push(h('td', {}));

  document.getElementById('detail').replaceChildren(
    h('table', { class: 'tbl', style: 'max-width:900px' }, [
      h('thead', {}, h('tr', {}, theadRow)),
      h('tbody', {}, tbodyRows),
      h('tfoot', {}, h('tr', {}, tfootCells))
    ])
  );
}

if (groupsToRender.length > 0) show();
