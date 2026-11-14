const { h, s, model, label, text, opt, fmt, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const sorted = opt('sortBy') === 'input'
  ? [...model.records]
  : [...model.records].sort((a, b) => {
      const pa = a.pct !== null && isFinite(a.pct) ? Math.abs(a.pct) : -1;
      const pb = b.pct !== null && isFinite(b.pct) ? Math.abs(b.pct) : -1;
      return pb - pa;
    });

const D = sorted.slice(0, opt('maxRows') ?? 200);

const shownCount = D.reduce((sum, r) => sum + (r.count ?? 1), 0);
const totalCount = model.records.reduce((sum, r) => sum + (r.count ?? 1), 0);
document.getElementById('chartSub').textContent = text('chartSub') +
  (D.length < model.records.length ? ' ' + text('truncated').replace('{shown}', int(shownCount)).replace('{total}', int(totalCount)) : '');

const tolPct = Math.abs(model.tolerance?.percent ?? 0);
const validPcts = D.map(r => r.pct).filter(p => p !== null && isFinite(p));
const maxDataPct = validPcts.length ? Math.max(...validPcts.map(Math.abs)) : 0;
const T = RD.ticks(Math.max(tolPct > 0 ? tolPct * 1.5 : 10, maxDataPct, 10), 3);
const maxTick = T[T.length - 1] || 10;
const ticks = [...T.slice(1).reverse().map(v => -v), ...T];

const W = 780, X0 = 270, X1 = W - 120, RH = 34, TOP = 30;
const x = p => (X0 + X1) / 2 + Math.max(-maxTick, Math.min(maxTick, p)) / maxTick * (X1 - X0) / 2;

const svgHeight = TOP + Math.max(1, D.length) * RH + 6;
const svg = s('svg', { viewBox: `0 0 ${W} ${svgHeight}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });

if (tolPct > 0 && D.length > 0) {
  svg.append(s('rect', { class: 'band', x: x(-tolPct), y: TOP - 4, width: x(tolPct) - x(-tolPct), height: D.length * RH + 4 }));
}

ticks.forEach(t => {
  svg.append(
    s('line', { class: t === 0 ? 'axl' : 'gridl', x1: x(t), x2: x(t), y1: TOP - 4, y2: TOP + D.length * RH }),
    s('text', { class: 'mut', x: x(t), y: 14, 'text-anchor': 'middle', text: (t > 0 ? '+' : t < 0 ? '−' : '') + Math.abs(t) + '%' })
  );
});
svg.append(s('text', { class: 'mut', x: W - 4, y: 14, 'text-anchor': 'end', text: text('difference') }));

let sel = D.find(r => r.status === 'break') ?? D[0] ?? null;
const hits = new Map();

D.forEach((r, i) => {
  const y = TOP + i * RH, cy = y + RH / 2;
  const hit = s('rect', { class: 'rowhit', x: 0, y, width: W, height: RH, rx: 4 });
  const np = { 'pointer-events': 'none' };
  hits.set(r, hit);
  svg.append(hit);

  hit.addEventListener('click', () => { sel = r; show(); });
  const l = val(r, 'left'), rt = val(r, 'right');
  RD.hover(hit, r.name === r.key ? r.name : `${r.key}, ${r.name}`, [
    `${model.left.short} ${l === null ? label('none') : fmt(M, l)}`,
    `${model.right.short} ${rt === null ? label('none') : fmt(M, rt)}`
  ]);

  svg.append(s('text', { x: 6, y: cy + 4, text: clip(r.name || r.key, 38), ...np }));

  if (r.pct === null || !isFinite(r.pct)) {
    const isLo = r.status === 'leftOnly';
    svg.append(
      s('rect', { class: isLo ? 'f-l' : 'f-r', x: isLo ? X0 : X1 - 8, y: cy - 8, width: 8, height: 16, rx: 2, ...np }),
      s('text', { class: 'sec', x: W - 4, y: cy + 4, 'text-anchor': 'end', text: label(r.status), ...np })
    );
    return;
  }

  const cls = r.status === 'break' ? (r.pct > 0 ? 'r' : 'l') : r.status === 'tolerance' ? 'tol' : 'ex';
  if (r.status === 'break') {
    svg.append(s('line', { class: 'stem s-' + cls, x1: x(0), x2: x(r.pct), y1: cy, y2: cy, ...np }));
  }
  if (r.duplicate) {
    svg.append(s('circle', { cx: x(r.pct), cy, r: 10, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5, ...np }));
  }
  svg.append(s('circle', { class: 'dot f-' + cls, cx: x(r.pct), cy, r: 6, ...np }));
  const note = r.duplicate ? label('duplicate') : r.pct === 0 ? label('agrees') : (RD.signed(r.pct, 1) + '%');
  svg.append(s('text', { class: r.status === 'break' ? '' : 'sec', x: W - 4, y: cy + 4, 'text-anchor': 'end', text: note, ...np }));
});

document.getElementById('chart').append(svg);

function show() {
  hits.forEach((e, r) => e.setAttribute('class', r === sel ? 'rowhit on' : 'rowhit'));
  const r = sel;
  if (!r) return;

  document.getElementById('dt').textContent = r.name || r.key;
  const subParts = [...(r.group || []), r.name && r.key && r.name !== r.key ? r.key : null].filter(Boolean);
  document.getElementById('ds').textContent = (subParts.length ? subParts.join(', ') + '. ' : '') + RD.statusWord(r) + '.';

  const bigText = r.duplicate && r.pct === 0
    ? label('duplicate')
    : r.pct === null || !isFinite(r.pct)
      ? label(r.status)
      : r.pct === 0
        ? label('agrees')
        : (RD.signed(r.pct, 1) + '%');

  const cell = (side, f, differs) => {
    const v = val(r, side, f.key);
    return h('td', { class: (f.type === 'number' ? 'n' : '') + (differs ? (side === 'left' ? ' dl' : ' dr') : '') + (v === null ? ' none' : ''), text: fmt(f.key, v) });
  };
  const fieldRow = f => {
    const a = val(r, 'left', f.key), b = val(r, 'right', f.key), differs = a !== null && b !== null && a !== b;
    return h('tr', {}, [h('td', { text: f.label }), cell('left', f, differs), cell('right', f, differs)]);
  };

  const kids = [
    h('div', { class: 'big', text: bigText }),
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short })
      ])),
      h('tbody', {}, model.fields.map(fieldRow))
    ])
  ];

  const hasLeftLines = r.leftLines && r.leftLines.length > 0;
  const hasRightLines = r.rightLines && r.rightLines.length > 0;
  if (hasLeftLines || hasRightLines) {
    const linesMap = new Map();
    (r.leftLines || []).forEach(l => {
      const id = l.name || l.key;
      linesMap.set(id, { name: id, left: l.values, right: null });
    });
    (r.rightLines || []).forEach(rL => {
      const id = rL.name || rL.key;
      if (!linesMap.has(id)) linesMap.set(id, { name: id, left: null, right: rL.values });
      else linesMap.get(id).right = rL.values;
    });

    const tolPercent = Math.abs(model.tolerance?.percent ?? 0);
    const lineRows = Array.from(linesMap.values()).map(item => {
      const lv = item.left && M ? item.left[M] : null;
      const rv = item.right && M ? item.right[M] : null;
      const linePct = lv && rv ? (rv - lv) / lv * 100 : null;
      const isBreak = linePct !== null && tolPercent > 0 && Math.abs(linePct) > tolPercent;
      return h('tr', {}, [
        h('td', { text: item.name }),
        h('td', { class: 'n' + (isBreak ? ' dl' : '') + (lv === null ? ' none' : ''), text: lv === null ? label('none') : fmt(M, lv) }),
        h('td', { class: 'n' + (isBreak ? ' dr' : '') + (rv === null ? ' none' : ''), text: rv === null ? label('none') : fmt(M, rv) }),
        h('td', { class: 'n', text: linePct === null || linePct === 0 ? (linePct === 0 ? label('agrees') : '') : (RD.signed(linePct, 1) + '%') })
      ]);
    });

    kids.push(
      h('table', { class: 'tbl lines-tbl' }, [
        h('thead', {}, h('tr', {}, [
          h('th', { text: text('line') }),
          h('th', { class: 'n', text: model.left.short }),
          h('th', { class: 'n', text: model.right.short }),
          h('th', { class: 'n', text: text('difference') })
        ])),
        h('tbody', {}, lineRows)
      ])
    );
  }

  const reasonText = r.reason || (r.pct === 0 ? text('agreesReason') : text('noReason'));
  if (reasonText) {
    kids.push(h('p', { class: 'why', text: reasonText }));
  }

  document.getElementById('detail').replaceChildren(...kids);
}

if (sel) show();
