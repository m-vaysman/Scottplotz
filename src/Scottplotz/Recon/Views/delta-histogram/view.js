const { h, s, model, label, text, opt, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const figsEl = document.getElementById('figs');
const sm = model.summary;
const figData = [
  [sm.counts.leftOnly, 'l', text('leftOnlyDesc')],
  [sm.counts.rightOnly, 'r', text('rightOnlyDesc')],
  [sm.duplicates, 'dup', text('dupDesc')]
];
figsEl.replaceChildren(...figData.map(([v, sw, t]) => h('div', { class: 'fig' }, [
  h('b', {}, [h('i', { class: 'sw ' + sw, style: 'width:14px;height:14px;margin-right:10px' }), int(v)]),
  h('span', { text: t })
])));

const maxBins = Math.max(1, opt('maxBins') || 20);
const tolAbs = model.tolerance?.absolute ?? 0;
const matched = model.records.filter(r => val(r, 'left') !== null && val(r, 'right') !== null && r.delta !== null);

let bw = opt('binWidth');
if (!bw || bw <= 0) {
  if (tolAbs > 0) {
    bw = tolAbs;
  } else {
    const maxDiff = Math.max(0, ...matched.map(r => Math.abs(r.delta ?? 0)));
    const t = RD.ticks(maxDiff || 1, maxBins);
    bw = t.length > 1 ? t[1] - t[0] : 1;
  }
}
if (!bw || bw <= 0) bw = 1;

const beyond = maxBins + 1;
const bins = new Map();
for (let k = -beyond; k <= beyond; k++) {
  bins.set(k, { k, count: 0, records: [] });
}

matched.forEach(r => {
  const cnt = r.count || 1;
  const rawK = Math.round(r.delta / bw);
  const k = rawK > maxBins ? beyond : rawK < -maxBins ? -beyond : rawK;
  const b = bins.get(k);
  b.count += cnt;
  b.records.push(r);
});

const W = 820, H = 380, X0 = 54, X1 = W - 10, Y0 = 14, Y1 = 320;
const numBins = 2 * beyond + 1;
const colW = (X1 - X0) / numBins;
const x = k => X0 + (k + beyond) * colW;

const maxCount = Math.max(0, ...Array.from(bins.values()).map(b => b.count));
const rawTicks = RD.ticks(maxCount, 4);
const topTick = rawTicks[rawTicks.length - 1] || 1;
const yTicks = [0];
if (topTick >= 20) {
  const p1 = 10 ** (Math.floor(Math.log10(topTick)) - 1);
  const low = Math.round((topTick * 0.05) / p1) * p1;
  if (low > 0 && low < topTick * 0.2) yTicks.push(low);
}
rawTicks.forEach(t => { if (t > 0 && !yTicks.includes(t)) yTicks.push(t); });
yTicks.sort((a, b) => a - b);

const y = v => Y1 - Math.sqrt(Math.max(0, v) / topTick) * (Y1 - Y0);

const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

if (tolAbs > 0) {
  const xLeft = Math.max(X0 + colW, X0 + (beyond + 0.5 - (tolAbs / bw + 0.5)) * colW);
  const xRight = Math.min(X1 - colW, X0 + (beyond + 0.5 + (tolAbs / bw + 0.5)) * colW);
  if (xRight > xLeft) {
    svg.append(s('rect', { class: 'band', x: xLeft, y: Y0, width: xRight - xLeft, height: Y1 - Y0 }));
  }
}

yTicks.forEach(t => {
  svg.append(
    s('line', { class: t ? 'gridl' : 'axl', x1: X0, x2: X1, y1: y(t), y2: y(t) }),
    s('text', { class: 'mut', x: X0 - 8, y: y(t) + 4, 'text-anchor': 'end', text: int(t) })
  );
});

const step = maxBins <= 6 ? 1 : maxBins <= 12 ? 2 : maxBins <= 25 ? 5 : maxBins <= 50 ? 10 : 20;
const dec = Number.isInteger(bw) ? 0 : (RD.measure?.decimals ?? 2);
for (let k = -Math.floor(maxBins / step) * step; k <= maxBins; k += step) {
  const valDiff = k * bw;
  const txt = valDiff === 0 ? '0' : (valDiff > 0 ? '+' : '−') + Math.abs(valDiff).toLocaleString('en-US', { minimumFractionDigits: dec, maximumFractionDigits: dec });
  svg.append(s('text', { class: 'mut', x: x(k) + colW / 2, y: Y1 + 18, 'text-anchor': 'middle', text: txt }));
}

svg.append(
  s('text', { class: 'mut', x: x(-beyond) + colW / 2, y: Y1 + 34, 'text-anchor': 'middle', text: text('beyond') }),
  s('text', { class: 'mut', x: x(beyond) + colW / 2, y: Y1 + 34, 'text-anchor': 'middle', text: text('beyond') }),
  s('text', { class: 'sec', x: (X0 + X1) / 2, y: H - 6, 'text-anchor': 'middle', text: text('axisLabel') })
);

let sel = 0, bestCount = -1;
bins.forEach((b, k) => {
  if (k !== 0 && b.count > bestCount) {
    bestCount = b.count;
    sel = k;
  }
});
if (bestCount <= 0 && bins.has(0)) sel = 0;

const bars = new Map();
bins.forEach((bin, k) => {
  if (bin.count <= 0) return;
  let cls;
  if (bin.records.every(r => r.status === 'exact')) {
    cls = 'f-ex';
  } else if (bin.records.every(r => r.status === 'tolerance' || r.status === 'exact')) {
    cls = 'f-tol';
  } else {
    cls = k < 0 ? 'f-l' : k > 0 ? 'f-r' : 'f-brk';
  }
  const top = y(bin.count);
  const b = s('rect', {
    class: 'bar ' + cls,
    x: x(k) + 1,
    y: top,
    width: Math.max(1, colW - 2),
    height: Math.max(2, Y1 - top),
    rx: 2
  });
  b.addEventListener('click', () => { sel = k; show(); });

  let tipTitle;
  const boundStr = fmt(M, maxBins * bw);
  if (Math.abs(k) === beyond) {
    tipTitle = `> ${boundStr} (${k > 0 ? model.right.short : model.left.short} higher)`;
  } else if (k === 0) {
    tipTitle = label('agrees');
  } else {
    tipTitle = fmtSigned(M, k * bw);
  }
  RD.hover(b, tipTitle, [`${int(bin.count)} ${model.unit}`]);

  bars.set(k, b);
  svg.append(b);
});

document.getElementById('chart').append(svg);

const hasGroup = model.records.some(r => r.group && r.group.length > 0);

function show() {
  bars.forEach((b, k) => b.classList.toggle('on', k === sel));
  const bin = bins.get(sel);
  if (!bin || bin.count === 0) {
    document.getElementById('dt').textContent = text('noRecords');
    document.getElementById('ds').textContent = '';
    document.getElementById('detail').replaceChildren();
    return;
  }

  const groupBy = new Map(), reasonBy = new Map();
  let totalWeight = 0;
  bin.records.forEach(r => {
    const cnt = r.count || 1;
    totalWeight += cnt;
    const g = r.group && r.group[0] ? r.group[0] : null;
    if (g) groupBy.set(g, (groupBy.get(g) || 0) + cnt);
    if (r.reason) reasonBy.set(r.reason, (reasonBy.get(r.reason) || 0) + cnt);
  });

  let topGroup = null, topGroupCount = 0;
  groupBy.forEach((cnt, g) => { if (cnt > topGroupCount) { topGroup = g; topGroupCount = cnt; } });

  let topReason = null, topReasonCount = 0;
  reasonBy.forEach((cnt, r) => { if (cnt > topReasonCount) { topReason = r; topReasonCount = cnt; } });

  let dtText;
  if (Math.abs(sel) === beyond) {
    const bound = fmt(M, maxBins * bw);
    const dir = sel > 0 ? text('rightHigher') : text('leftHigher');
    dtText = text('detailTitleBeyond')
      .replace('{count}', int(bin.count))
      .replace('{unit}', bin.count === 1 ? model.unitOne : model.unit)
      .replace('{bound}', bound)
      .replace('{dir}', dir);
  } else if (sel === 0) {
    dtText = text('detailTitleExact')
      .replace('{count}', int(bin.count))
      .replace('{unit}', bin.count === 1 ? model.unitOne : model.unit);
  } else {
    const diff = fmtSigned(M, sel * bw);
    dtText = text('detailTitle')
      .replace('{count}', int(bin.count))
      .replace('{unit}', bin.count === 1 ? model.unitOne : model.unit)
      .replace('{diff}', diff);
  }
  document.getElementById('dt').textContent = dtText;

  const subParts = [];
  if (topGroup) {
    const pct = totalWeight > 0 ? Math.round((topGroupCount / totalWeight) * 100) : 0;
    subParts.push(text('topGroup').replace('{pct}', int(pct)).replace('{group}', topGroup));
  }
  if (topReason) {
    subParts.push(topReason.endsWith('.') ? topReason : topReason + '.');
  }
  if (bin.records.length > 8 || totalWeight > 8) {
    const shown = Math.min(8, bin.records.length);
    subParts.push(text('showingFirst').replace('{shown}', int(shown)).replace('{total}', int(totalWeight)).replace('{unit}', model.unit));
  }
  document.getElementById('ds').textContent = subParts.join(' ');

  const displayRecords = [...bin.records].sort((a, b) => (a.count > 1 ? 1 : 0) - (b.count > 1 ? 1 : 0)).slice(0, 8);
  const rows = displayRecords.map(r => {
    const l = val(r, 'left');
    const rt = val(r, 'right');
    const differs = l !== null && rt !== null && l !== rt;
    return h('tr', {}, [
      h('td', { text: clip(r.name || r.key, 30) }),
      hasGroup ? h('td', { text: clip(r.group[0] || '', 20) }) : null,
      h('td', { class: 'n' + (differs ? ' dl' : '') + (l === null ? ' none' : ''), text: fmt(M, l) }),
      h('td', { class: 'n' + (differs ? ' dr' : ''), text: fmt(M, rt) }),
      h('td', { class: 'n', text: r.delta === null ? '' : fmtSigned(M, r.delta) })
    ]);
  });

  const table = h('table', { class: 'tbl' }, [
    h('thead', {}, h('tr', {}, [
      h('th', { text: text('field') }),
      hasGroup ? h('th', { text: text('group') }) : null,
      h('th', { class: 'n', text: model.left.short }),
      h('th', { class: 'n', text: model.right.short }),
      h('th', { class: 'n', text: text('difference') })
    ])),
    h('tbody', {}, rows)
  ]);

  document.getElementById('detail').replaceChildren(table);
}

show();
