const { h, s, model, label, text, opt, fmt, fmtSigned, val, signed, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));
document.getElementById('chartSub').textContent = text('chartSub');

const recVal = r => {
  const l = val(r, 'left'), rt = val(r, 'right');
  return l !== null && l !== undefined ? l : (rt !== null && rt !== undefined ? rt : null);
};

const plottable = model.records.filter(r => recVal(r) !== null);
const sizes = plottable.map(r => Math.abs(recVal(r))).filter(v => v > 0);
const minVal = sizes.length ? Math.min(...sizes) : 1;
const maxVal = sizes.length ? Math.max(...sizes) : 10;
let minLog = Math.floor(Math.log10(minVal || 1));
let maxLog = Math.ceil(Math.log10(maxVal || 10));
if (minLog >= maxLog) { minLog -= 1; maxLog += 1; }

const W = 860, H = 500, X0 = 56, X1 = W - 14, Y0 = 16, Y1 = 380;
const x = v => X0 + (Math.log10(Math.max(v, 1e-9)) - minLog) / (maxLog - minLog) * (X1 - X0);

const yCap = Math.max(1, opt('yCap') ?? 10);
const midY = (Y0 + Y1) / 2;
const halfH = (Y1 - Y0) / 2;
const y = d => midY - Math.sign(d) * Math.sqrt(Math.min(Math.abs(d), yCap) / yCap) * halfH;
const railY = { leftOnly: 426, rightOnly: 466 };

const svg = s('svg', { id: 'plot', viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

const tolPct = model.tolerance?.percent ?? 0;
if (tolPct > 0) {
  const topY = y(Math.min(tolPct, yCap));
  const botY = y(-Math.min(tolPct, yCap));
  svg.append(s('rect', { class: 'band', x: X0, y: topY, width: X1 - X0, height: Math.max(1, botY - topY) }));
}

const cand = [yCap, yCap * 0.5, yCap * 0.2];
if (tolPct > 0 && tolPct < yCap) cand.push(tolPct);
const yPos = [...new Set(cand.map(v => +v.toFixed(2)))].sort((a, b) => a - b);
const yTicks = [...yPos.slice().reverse().map(v => -v), 0, ...yPos];
yTicks.forEach(t => {
  const yt = y(t);
  svg.append(
    s('line', { class: t === 0 ? 'axl' : 'gridl', x1: X0, x2: X1, y1: yt, y2: yt }),
    s('text', { class: 'mut', x: X0 - 8, y: yt + 4, 'text-anchor': 'end', text: (t > 0 ? '+' : t < 0 ? '−' : '') + Math.abs(t) + '%' })
  );
});

const xStep = maxLog - minLog > 6 ? 2 : 1;
const xTicks = [];
for (let p = Math.ceil(minLog); p <= maxLog; p += xStep) xTicks.push(10 ** p);
const fmtTick = t => (t >= 1e9 ? (t / 1e9) + 'B' : t >= 1e6 ? (t / 1e6) + 'M' : t >= 1e3 ? (t / 1e3) + 'K' : String(t));
xTicks.forEach(t => {
  const xt = x(t);
  svg.append(
    s('text', { class: 'mut', x: xt, y: Y1 + 18, 'text-anchor': 'middle', text: fmtTick(t) }),
    s('line', { class: 'gridl', x1: xt, x2: xt, y1: Y0, y2: Y1 })
  );
});

svg.append(
  s('text', { class: 'sec', x: X0 + 6, y: Y0 + 12, text: text('rightHigher') }),
  s('text', { class: 'sec', x: X0 + 6, y: Y1 - 6, text: text('leftHigher') })
);

[
  ['leftOnly', text('leftOnlyRail')],
  ['rightOnly', text('rightOnlyRail')]
].forEach(([k, t]) => {
  svg.append(
    s('line', { class: 'axl', x1: X0, x2: X1, y1: railY[k], y2: railY[k] }),
    s('text', { class: 'sec', x: X0, y: railY[k] - 12, text: t })
  );
});

plottable.forEach(p => {
  const v = Math.abs(recVal(p));
  p._x = x(v);
  if (p.status === 'leftOnly' || p.status === 'rightOnly') {
    p._y = railY[p.status];
  } else {
    const pctVal = p.pct ?? (val(p, 'left') && p.delta !== null ? (p.delta / Math.abs(val(p, 'left'))) * 100 : 0);
    p._y = y(pctVal);
  }
  const isBrk = p.status === 'break';
  const isTol = p.status === 'tolerance';
  const isEx = p.status === 'exact';
  const clsName = p.status === 'leftOnly' ? 'f-l'
    : p.status === 'rightOnly' ? 'f-r'
    : isBrk ? ((p.pct ?? p.delta ?? 0) > 0 ? 'f-r' : 'f-l')
    : isTol ? 'f-tol' : 'f-ex';
  p._rad = isBrk ? 7 : (isEx || isTol ? 4 : 6);

  if (p.duplicate) {
    svg.append(s('circle', { cx: p._x, cy: p._y, r: 9, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, 'pointer-events': 'none' }));
  }
  svg.append(s('circle', { class: 'dot ' + clsName, cx: p._x, cy: p._y, r: p._rad, 'pointer-events': 'none' }));
});

const ring = s('circle', { class: 'selring', r: 11, 'pointer-events': 'none', style: 'display:none' });
svg.append(ring);

const topCount = Math.max(0, opt('labelTop') ?? 3);
const breaks = plottable.filter(r => r.status === 'break')
  .sort((a, b) => Math.abs(b.pct ?? b.delta ?? 0) - Math.abs(a.pct ?? a.delta ?? 0));
breaks.slice(0, topCount).forEach(p => {
  const flip = p._x > W - 180;
  svg.append(s('text', {
    x: p._x + (flip ? -14 : 12),
    y: p._y + 4,
    'text-anchor': flip ? 'end' : 'start',
    'pointer-events': 'none',
    text: clip(p.name, 28)
  }));
});

const near = ev => {
  if (!plottable.length) return null;
  const b = svg.getBoundingClientRect();
  const k = W / (b.width || 1);
  const mx = (ev.clientX - b.left) * k;
  const my = (ev.clientY - b.top) * k;
  return plottable.reduce((best, p) => {
    const dd = (p._x - mx) ** 2 + (p._y - my) ** 2 - (p.status === 'exact' ? 0 : 150);
    return dd < best[1] ? [p, dd] : best;
  }, [null, 900])[0];
};

const pctStr = p => {
  if (p.status === 'leftOnly' || p.status === 'rightOnly' || val(p, 'left') === null || val(p, 'right') === null) {
    return text('noPartner');
  }
  if (p.pct !== null && p.pct !== undefined) {
    return signed(p.pct, 2) + '%';
  }
  return label(p.status);
};

svg.addEventListener('pointermove', ev => {
  const p = near(ev);
  if (p) {
    const lVal = val(p, 'left');
    const rVal = val(p, 'right');
    RD.tip.show(ev, p.name === p.key ? p.name : `${p.name} (${p.key})`, [
      pctStr(p),
      `${model.left.short} ${lVal === null ? label('none') : fmt(M, lVal)}`,
      `${model.right.short} ${rVal === null ? label('none') : fmt(M, rVal)}`
    ]);
  } else {
    RD.tip.hide();
  }
});
svg.addEventListener('pointerleave', RD.tip.hide);
svg.addEventListener('click', ev => {
  const p = near(ev);
  if (p) show(p);
});
document.getElementById('chart').append(svg);

function show(p) {
  if (!p) {
    ring.style.display = 'none';
    return;
  }
  ring.style.display = '';
  ring.setAttribute('cx', p._x);
  ring.setAttribute('cy', p._y);

  document.getElementById('dt').textContent = p.name;
  const subParts = [...(p.group || []), p.name === p.key ? null : p.key].filter(Boolean);
  document.getElementById('ds').textContent = (subParts.length ? subParts.join(', ') + '. ' : '') + RD.statusWord(p) + '.';

  const lVal = val(p, 'left');
  const rVal = val(p, 'right');
  const hasBoth = lVal !== null && rVal !== null;
  const differ = hasBoth && p.status === 'break';

  const cell = (side, f) => {
    const v = val(p, side, f.key);
    const isNum = f.type === 'number';
    const isDiff = differ && (p.breaks && p.breaks.length ? p.breaks.includes(f.key) : f.key === M);
    return h('td', {
      class: (isNum ? 'n' : '') + (isDiff ? (side === 'left' ? ' dl' : ' dr') : '') + (v === null ? ' none' : ''),
      text: fmt(f.key, v)
    });
  };

  const row = f => h('tr', {}, [
    h('td', { text: f.label }),
    cell('left', f),
    cell('right', f)
  ]);

  const deltaSub = hasBoth && p.delta !== null
    ? `${fmtSigned(M, p.delta)} ${text('netDiff')}`
    : text('noCompare');

  const defaultWhy = p.status === 'tolerance'
    ? text('defaultReasonAgrees')
    : (p.status === 'exact' ? text('defaultReasonAgrees') : text('defaultReasonBreak'));

  document.getElementById('detail').replaceChildren(
    h('div', { class: 'big', text: pctStr(p) }),
    h('div', { class: 'sub', text: deltaSub }),
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short })
      ])),
      h('tbody', {}, model.fields.map(row))
    ]),
    h('p', { class: 'cause', text: p.reason || defaultWhy })
  );
}

const initial = plottable.find(r => r.name === 'Tessel Semiconductors') ?? breaks[0] ?? plottable[0];
if (initial) show(initial);
