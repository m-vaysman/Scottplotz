const { h, s, model, label, text, opt, fmt, fmtSigned, val, int } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const asOfDate = model.asOf ? new Date(model.asOf + (model.asOf.length === 10 ? 'T00:00:00Z' : '')) : null;

function getAge(r) {
  if (r.attrs && r.attrs.age !== undefined && r.attrs.age !== null) {
    const a = +r.attrs.age;
    return Number.isFinite(a) ? Math.max(0, a) : 0;
  }
  if (r.date && asOfDate) {
    const d = new Date(r.date + (r.date.length === 10 ? 'T00:00:00Z' : ''));
    if (!isNaN(d.getTime())) {
      const diffMs = asOfDate.getTime() - d.getTime();
      return Math.max(0, Math.round(diffMs / 86400000));
    }
  }
  return 0;
}

function getAmt(r) {
  const l = val(r, 'left');
  const rt = val(r, 'right');
  const lAbs = l !== null && l !== undefined ? Math.abs(l) : 0;
  const rtAbs = rt !== null && rt !== undefined ? Math.abs(rt) : 0;
  return Math.max(lAbs, rtAbs);
}

const openItems = model.records.filter(r => r.status !== 'exact' && r.status !== 'tolerance');
openItems.forEach(r => {
  r.age = getAge(r);
  r.amt = getAmt(r);
});

const ageFrom = opt('ageFrom') ?? 30;
const amountFrom = opt('amountFrom') ?? 100000;
const ageCap = opt('ageCap') ?? 120;

const amts = openItems.map(i => i.amt).filter(a => a > 0);
const minAmt = amts.length ? Math.min(...amts) : 100;
const maxAmt = Math.max(...amts, amountFrom, 100);
const logMin = Math.floor(Math.log10(Math.max(1, minAmt)));
const rawMax = Math.log10(Math.max(10, maxAmt));
const logMax = Math.max(logMin + 1.5, rawMax + 0.25);

const W = 860, H = 500, X0 = 64, X1 = W - 16, Y0 = 16, Y1 = H - 40;
const x = a => X0 + Math.min(Math.max(0, a), ageCap) / (ageCap || 1) * (X1 - X0);
const y = v => Y1 - (Math.log10(Math.max(10 ** logMin, v)) - logMin) / (logMax - logMin || 1) * (Y1 - Y0);

const svg = s('svg', {
  id: 'plot',
  viewBox: `0 0 ${W} ${H}`,
  width: '100%',
  role: 'img',
  'aria-label': text('chartTitle')
});

svg.append(
  s('rect', {
    class: 'zone',
    x: x(ageFrom),
    y: Y0,
    width: Math.max(0, X1 - x(ageFrom)),
    height: Math.max(0, y(amountFrom) - Y0)
  }),
  s('text', {
    x: X1 - 8,
    y: Y0 + 18,
    'text-anchor': 'end',
    'font-weight': 600,
    text: text('priorityZone')
  }),
  s('text', {
    class: 'sec',
    x: X0 + 8,
    y: Y1 - 8,
    text: text('clearZone')
  })
);

const yTicks = [];
for (let p = logMin; p < logMax; p++) yTicks.push(10 ** p);
yTicks.forEach(t => {
  const isAxl = Math.abs(t - amountFrom) < 1;
  const tStr = t >= 1e9 ? (t / 1e9) + 'B' : t >= 1e6 ? (t / 1e6) + 'M' : t >= 1e3 ? (t / 1e3) + 'K' : int(t);
  svg.append(
    s('line', { class: isAxl ? 'axl' : 'gridl', x1: X0, x2: X1, y1: y(t), y2: y(t) }),
    s('text', { class: 'mut', x: X0 - 8, y: y(t) + 4, 'text-anchor': 'end', text: tStr })
  );
});
if (!yTicks.some(t => Math.abs(t - amountFrom) < 1) && amountFrom >= 10 ** logMin && amountFrom <= 10 ** logMax) {
  svg.append(s('line', { class: 'axl', x1: X0, x2: X1, y1: y(amountFrom), y2: y(amountFrom) }));
}

const xTicks = [];
const step = ageCap > 0 ? (ageCap % 4 === 0 ? ageCap / 4 : ageCap % 3 === 0 ? ageCap / 3 : ageCap / 5) : 1;
for (let t = 0; t <= ageCap + 0.001; t += step) xTicks.push(Math.round(t));
xTicks.forEach(t => {
  const isAxl = Math.abs(t - ageFrom) < 0.001;
  svg.append(
    s('line', { class: isAxl ? 'axl' : 'gridl', x1: x(t), x2: x(t), y1: Y0, y2: Y1 }),
    s('text', { class: 'mut', x: x(t), y: Y1 + 18, 'text-anchor': 'middle', text: int(t) })
  );
});
if (!xTicks.some(t => Math.abs(t - ageFrom) < 0.001) && ageFrom >= 0 && ageFrom <= ageCap) {
  svg.append(s('line', { class: 'axl', x1: x(ageFrom), x2: x(ageFrom), y1: Y0, y2: Y1 }));
}

svg.append(s('text', {
  class: 'sec',
  x: (X0 + X1) / 2,
  y: H - 4,
  'text-anchor': 'middle',
  text: text('daysOpen')
}));

openItems.forEach(i => {
  i.x = x(i.age);
  i.y = y(i.amt);
  const np = { 'pointer-events': 'none' };
  if (i.duplicate) {
    svg.append(s('circle', { cx: i.x, cy: i.y, r: 10, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, ...np }));
  }
  const isPriority = i.age > ageFrom && i.amt > amountFrom;
  const dotCls = RD.cls[i.status] || 'brk';
  svg.append(s('circle', { class: 'dot f-' + dotCls, cx: i.x, cy: i.y, r: isPriority ? 7 : 5, ...np }));
});

const ring = s('circle', { class: 'selring', r: 11, 'pointer-events': 'none' });
svg.append(ring);

const near = ev => {
  const b = svg.getBoundingClientRect();
  if (!b.width) return null;
  const k = W / b.width, mx = (ev.clientX - b.left) * k, my = (ev.clientY - b.top) * k;
  return openItems.reduce((best, i) => {
    const dd = (i.x - mx) ** 2 + (i.y - my) ** 2;
    return dd < best[1] ? [i, dd] : best;
  }, [null, 900])[0];
};

svg.addEventListener('pointermove', ev => {
  const i = near(ev);
  if (i) {
    const title = i.name && i.name !== i.key ? `${i.key} · ${i.name}` : i.key;
    RD.tip.show(ev, title, [fmt(M, i.amt), text('openDays').replace('{age}', int(i.age))]);
  } else {
    RD.tip.hide();
  }
});
svg.addEventListener('pointerleave', RD.tip.hide);
svg.addEventListener('click', ev => {
  const i = near(ev);
  if (i) show(i);
});

document.getElementById('chart').append(svg);

function show(r) {
  if (!r) return;
  ring.setAttribute('cx', r.x);
  ring.setAttribute('cy', r.y);

  document.getElementById('dt').textContent = r.name && r.name !== r.key ? `${r.key} · ${r.name}` : r.key;
  const grp = r.group && r.group.length ? r.group.join(', ') + '. ' : '';
  document.getElementById('ds').textContent = grp + RD.statusWord(r) + '. ' + text('openDays').replace('{age}', int(r.age)) + '.';

  const cell = (side, f, differs) => {
    const v = val(r, side, f.key);
    return h('td', {
      class: (f.type === 'number' ? 'n' : '') + (differs ? (side === 'left' ? ' dl' : ' dr') : '') + (v === null || v === undefined ? ' none' : ''),
      text: fmt(f.key, v)
    });
  };

  const fields = model.fields.length ? model.fields : (M ? [RD.field(M)] : []);
  const rows = fields.map(f => {
    const a = val(r, 'left', f.key), b = val(r, 'right', f.key);
    const differs = a !== null && b !== null && a !== b;
    return h('tr', {}, [
      h('td', { text: f.label }),
      cell('left', f, differs),
      cell('right', f, differs)
    ]);
  });

  const diffText = r.delta !== null && r.delta !== undefined
    ? fmtSigned(M, r.delta)
    : text('nothingToCompare');

  rows.push(h('tr', {}, [
    h('td', { text: text('difference') }),
    h('td', { class: 'n', colspan: 2, text: diffText })
  ]));

  const isPriority = r.age > ageFrom && r.amt > amountFrom;
  const whyText = (r.reason || '') + (isPriority ? (r.reason ? ' ' : '') + text('escalate') : '');

  document.getElementById('detail').replaceChildren(
    h('div', { class: 'big', text: fmt(M, r.amt) }),
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short })
      ])),
      h('tbody', {}, rows)
    ]),
    h('p', { class: 'why', text: whyText || text('noReason') })
  );
}

const sel = openItems.slice().sort((a, b) => b.amt - a.amt)[0];
if (sel) show(sel);
