const { h, s, model, label, text, opt, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const values = model.records.flatMap(r => [val(r, 'left'), val(r, 'right')]).filter(v => v !== null && v !== undefined && !isNaN(v));
const maxVal = values.length ? Math.max(0, ...values) : 0;
const tickList = RD.ticks(maxVal, 5);
const MAX = tickList[tickList.length - 1] || 1;

const W = 760, H = 620, XL = 250, XR = W - 250, Y0 = 46, Y1 = H - 20;
const isLinear = opt('scale') === 'linear';
const scaleFn = v => isLinear ? (v / MAX) : Math.sqrt(Math.max(0, v) / MAX);
const y = v => Y1 - scaleFn(v) * (Y1 - Y0);

const pathFor = r => {
  const l = val(r, 'left'), rt = val(r, 'right');
  const yl = l !== null ? y(l) : null, yr = rt !== null ? y(rt) : null;
  return yl !== null && yr !== null ? `M${XL} ${yl}L${XR} ${yr}` : yl !== null ? `M${XL} ${yl}L${XL + 46} ${yl}` : yr !== null ? `M${XR - 46} ${yr}L${XR} ${yr}` : '';
};

const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

svg.append(
  s('text', { x: XL, y: 16, 'text-anchor': 'end', 'font-weight': 600, text: model.left.short || model.left.name }),
  s('text', { x: XR, y: 16, 'font-weight': 600, text: model.right.short || model.right.name }),
  s('line', { class: 'axl', x1: XL, x2: XL, y1: Y0, y2: Y1 }),
  s('line', { class: 'axl', x1: XR, x2: XR, y1: Y0, y2: Y1 })
);

tickList.filter(t => t > 0).forEach(t => {
  const yt = y(t);
  svg.append(
    s('line', { class: 'gridl', x1: XL, x2: XR, y1: yt, y2: yt }),
    s('text', { class: 'mut', x: (XL + XR) / 2, y: yt - 4, 'text-anchor': 'middle', text: int(t) })
  );
});

const selLine = s('path', { class: 'selline' });
svg.append(selLine);

const LAB = { l: [], r: [] };
const labelAll = opt('labelOnly') === 'all';
const sorted = [...model.records].sort((a, b) => (a.status === 'exact') === (b.status === 'exact') ? 0 : a.status === 'exact' ? -1 : 1);

let sel = [...model.records].filter(r => r.status === 'break').sort((a, b) => Math.abs(b.delta ?? 0) - Math.abs(a.delta ?? 0))[0] ?? model.records[0];

sorted.forEach(r => {
  const l = val(r, 'left'), rt = val(r, 'right');
  const yl = l !== null ? y(l) : null, yr = rt !== null ? y(rt) : null;
  const d = pathFor(r);
  if (!d) return;

  const status = r.status;
  const cls = status === 'exact' ? 'ex'
    : status === 'tolerance' ? 'tol'
    : status === 'leftOnly' ? 'l'
    : status === 'rightOnly' ? 'r'
    : (rt !== null && l !== null && rt > l ? 'r' : 'l');

  svg.append(s('path', { class: 'sl s-' + cls, d, opacity: status === 'exact' ? 0.6 : 1 }));
  if (yl !== null) svg.append(s('circle', { class: 'dot f-' + cls, cx: XL, cy: yl, r: 4.5 }));
  if (yr !== null) svg.append(s('circle', { class: 'dot f-' + cls, cx: XR, cy: yr, r: 4.5 }));

  if (r.duplicate) {
    const cx = r.duplicate === 'left' ? XL : (yr !== null ? XR : XL);
    const cy = r.duplicate === 'left' ? yl : (yr !== null ? yr : yl);
    if (cy !== null) svg.append(s('circle', { cx, cy, r: 9, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' }));
  }

  const shouldLabel = labelAll || status !== 'exact' || r.duplicate;
  if (shouldLabel) {
    if (yl !== null) LAB.l.push([yl, `${clip(r.name, 22)}  ${fmt(M, l)}`]);
    if (yr !== null) {
      const dup = r.duplicate ? '  ' + text('duplicateSuffix') : '';
      const rName = l === null ? '  ' + clip(r.name, 22) : '';
      LAB.r.push([yr, `${fmt(M, rt)}${rName}${dup}`]);
    }
  }

  const hit = s('path', { class: 'hit', d });
  hit.addEventListener('click', () => { sel = r; show(); });
  RD.hover(hit, r.name === r.key ? r.name : `${r.key}, ${r.name}`, [
    `${model.left.short} ${fmt(M, l)}`,
    `${model.right.short} ${fmt(M, rt)}`
  ]);
  svg.append(hit);
});

['l', 'r'].forEach(k => {
  const a = LAB[k].sort((p, q) => p[0] - q[0]);
  for (let pass = 0; pass < 3; pass++) {
    for (let i = 1; i < a.length; i++) {
      if (a[i][0] - a[i - 1][0] < 15) {
        const m = (a[i][0] + a[i - 1][0]) / 2;
        a[i - 1][0] = m - 7.5;
        a[i][0] = m + 7.5;
      }
    }
  }
  for (let i = 1; i < a.length; i++) {
    if (a[i][0] - a[i - 1][0] < 15) {
      a[i][0] = a[i - 1][0] + 15;
    }
  }
  a.forEach(([yy, t]) => svg.append(s('text', {
    x: k === 'l' ? XL - 12 : XR + 14,
    y: yy + 4,
    'text-anchor': k === 'l' ? 'end' : 'start',
    'pointer-events': 'none',
    text: t
  })));
});

document.getElementById('chart').append(svg);

function show() {
  if (!sel) {
    document.getElementById('dt').textContent = '';
    document.getElementById('ds').textContent = '';
    document.getElementById('detail').replaceChildren();
    return;
  }
  const r = sel;
  selLine.setAttribute('d', pathFor(r));
  document.getElementById('dt').textContent = r.name;
  const sub = [...r.group, r.name === r.key ? null : r.key].filter(Boolean).join(', ');
  document.getElementById('ds').textContent = (sub ? sub + '. ' : '') + RD.statusWord(r) + (r.count > 1 ? ` (${int(r.count)} ${model.unit})` : '') + '.';

  const cell = (side, f, differs) => {
    const v = val(r, side, f.key);
    return h('td', { class: (f.type === 'number' ? 'n' : '') + (differs ? (side === 'left' ? ' dl' : ' dr') : '') + (v === null ? ' none' : ''), text: fmt(f.key, v) });
  };
  const row = f => {
    const a = val(r, 'left', f.key), b = val(r, 'right', f.key), differs = a !== null && b !== null && a !== b;
    return h('tr', {}, [h('td', { text: f.label }), cell('left', f, differs), cell('right', f, differs)]);
  };

  const deltaText = r.delta === null ? label(r.status) : r.delta === 0 ? label('agrees') : fmtSigned(M, r.delta);
  document.getElementById('detail').replaceChildren(
    h('div', { class: 'big', text: deltaText }),
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [h('th', { text: text('field') }), h('th', { class: 'n', text: model.left.short }), h('th', { class: 'n', text: model.right.short })])),
      h('tbody', {}, model.fields.map(row))
    ]),
    h('p', { class: 'why', text: r.reason || text('noReason') })
  );
}

if (sel) show();
