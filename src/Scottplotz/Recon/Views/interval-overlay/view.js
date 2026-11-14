const { h, s, model, label, text, opt, fmt, fmtSigned, val, int } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const monthFull = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

function parseDate(str) {
  if (!str) return null;
  const m = String(str).match(/(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/);
  if (!m) return null;
  return { year: m[1], month: +m[2], day: +m[3], hour: m[4] ?? '00', min: m[5] ?? '00' };
}

function formatXLabel(r, i) {
  const p = parseDate(r.date);
  if (p) return `${p.day} ${monthNames[p.month - 1]} ${p.hour}:${p.min}`;
  return r.name || r.key || String(i + 1);
}

function formatTime(r, i) {
  const p = parseDate(r.date);
  if (p) return `${p.day} ${monthFull[p.month - 1]} ${p.year}, ${p.hour}:${p.min}`;
  return r.name || r.key || `${text('interval')} ${i + 1}`;
}

const fillClass = r => {
  if (r.status === 'leftOnly') return 'f-l';
  if (r.status === 'rightOnly') return 'f-r';
  if (r.status === 'tolerance') return 'f-tol';
  if (r.delta !== null && r.delta !== undefined) return r.delta > 0 ? 'f-r' : 'f-l';
  return 'f-brk';
};

const names = RD.groupNames();
const groups = (names.length ? names : ['']).map(name => {
  const gRecs = names.length ? model.records.filter(r => r.group[0] === name) : [...model.records];
  return {
    name: name || model.left.name,
    key: name,
    recs: gRecs.sort((a, b) => (a.date || '').localeCompare(b.date || ''))
  };
});

let best = 0, bestBreaks = -1;
groups.forEach((g, idx) => {
  const brks = g.recs.filter(r => r.status === 'break').length;
  if (brks > bestBreaks) {
    bestBreaks = brks;
    best = idx;
  }
});
let sel = bestBreaks > 0 ? best : 0;

const btns = groups.map((g, gi) => {
  const bad = g.recs.filter(r => r.status !== 'exact').reduce((sum, r) => sum + (r.count || 1), 0);
  const total = g.recs.reduce((sum, r) => sum + (r.count || 1), 0);
  const N = g.recs.length;
  const strip = s('svg', { viewBox: `0 0 ${N || 1} 10`, width: '100%', height: 10, preserveAspectRatio: 'none' }, [
    s('rect', { x: 0, y: 4, width: N || 1, height: 2, fill: 'var(--grid)' })
  ]);
  g.recs.forEach((r, i) => {
    if (r.status !== 'exact') strip.append(s('rect', { class: fillClass(r), x: i, y: 0, width: 1, height: 10 }));
  });
  const subText = bad ? text('diffCount').replace('{count}', int(bad)).replace('{total}', int(total)).replace('{unit}', model.unit) : text('allAgree');
  return h('button', { class: 'meter', on: { click: () => { sel = gi; show(); } } }, [
    h('b', { text: g.name }),
    h('span', { text: subText }),
    strip
  ]);
});
document.getElementById('list').replaceChildren(...btns);

const W = 900, X0 = 44, X1 = W - 10, T0 = 12, T1 = 240, R0 = 278, R1 = 350;

function show() {
  btns.forEach((b, i) => b.setAttribute('aria-current', String(i === sel)));
  const g = groups[sel] || { name: '', key: '', recs: [] };
  const recs = g.recs, N = recs.length;
  const x = i => N > 1 ? X0 + (i / (N - 1)) * (X1 - X0) : (X0 + X1) / 2;

  const allVals = recs.flatMap(r => [val(r, 'left'), val(r, 'right')]).filter(v => v !== null && v !== undefined);
  const rawMax = allVals.length ? Math.max(0, ...allVals) : 1;
  const yTicks = RD.ticks(rawMax > 0 ? rawMax : 1, 4);
  const yMax = yTicks[yTicks.length - 1] || 1;
  const y = v => T1 - (v / yMax) * (T1 - T0);

  const deltas = recs.map(r => r.delta).filter(d => d !== null && d !== undefined);
  const tol = model.tolerance?.absolute ?? 0;
  const maxDelta = Math.max(tol * 1.2, 0.05, ...deltas.map(Math.abs));
  const dm = maxDelta * 1.1 || 1;
  const ry = d => (R0 + R1) / 2 - (d / dm) * ((R1 - R0) / 2);

  const svg = s('svg', { id: 'plot', viewBox: `0 0 ${W} 372`, width: '100%', role: 'img', 'aria-label': text('title') });

  yTicks.forEach(t => {
    svg.append(
      s('line', { class: t === 0 ? 'axl' : 'gridl', x1: X0, x2: X1, y1: y(t), y2: y(t) }),
      s('text', { class: 'mut', x: X0 - 6, y: y(t) + 4, 'text-anchor': 'end', text: fmt(M, t) })
    );
  });

  if (N > 0) {
    const xStep = Math.max(1, Math.round(N / 6));
    for (let i = 0; i < N; i += xStep) {
      svg.append(
        s('line', { class: 'gridl', x1: x(i), x2: x(i), y1: T0, y2: R1 }),
        s('text', { class: 'mut', x: x(i) + 2, y: 368, text: formatXLabel(recs[i], i) })
      );
    }
  }

  if (tol > 0) {
    const yBand = Math.min(ry(tol), ry(-tol));
    const hBand = Math.abs(ry(-tol) - ry(tol));
    svg.append(s('rect', { class: 'band', x: X0, y: yBand, width: X1 - X0, height: hBand }));
  }
  svg.append(s('line', { class: 'axl', x1: X0, x2: X1, y1: ry(0), y2: ry(0) }));
  svg.append(s('text', { class: 'sec', x: X0, y: R0 - 8, text: text('residualTitle') }));
  svg.append(s('text', { class: 'sec', x: X0, y: T0 + 2, text: text('measurePerInterval') }));

  for (let i = 0; i < N - 1; i++) {
    const l1 = val(recs[i], 'left'), r1 = val(recs[i], 'right');
    const l2 = val(recs[i + 1], 'left'), r2 = val(recs[i + 1], 'right');
    if (l1 === null || r1 === null || l2 === null || r2 === null) continue;
    if (l1 === r1 && l2 === r2) continue;
    const d1 = r1 - l1, d2 = r2 - l2;
    if (d1 * d2 < 0) {
      const t = d1 / (d1 - d2);
      const xi = x(i) + t * (x(i + 1) - x(i));
      const yi = y(l1 + t * (l2 - l1));
      svg.append(
        s('polygon', { class: d1 > 0 ? 'f-r' : 'f-l', opacity: 0.3, points: `${x(i)},${y(l1)} ${xi},${yi} ${x(i)},${y(r1)}` }),
        s('polygon', { class: d2 > 0 ? 'f-r' : 'f-l', opacity: 0.3, points: `${xi},${yi} ${x(i + 1)},${y(l2)} ${x(i + 1)},${y(r2)}` })
      );
    } else {
      svg.append(s('polygon', {
        class: (r1 + r2 > l1 + l2) ? 'f-r' : 'f-l',
        opacity: 0.3,
        points: `${x(i)},${y(l1)} ${x(i + 1)},${y(l2)} ${x(i + 1)},${y(r2)} ${x(i)},${y(r1)}`
      }));
    }
  }

  const path = side => {
    let d = '', pen = false;
    recs.forEach((r, i) => {
      const v = val(r, side);
      if (v === null || v === undefined) { pen = false; return; }
      d += `${pen ? 'L' : 'M'}${x(i).toFixed(1)} ${y(v).toFixed(1)}`;
      pen = true;
    });
    return d;
  };
  const pL = path('left'), pR = path('right');
  if (pL) svg.append(s('path', { class: 'ln s-l', d: pL }));
  if (pR) svg.append(s('path', { class: 'ln s-r', d: pR }));

  recs.forEach((r, i) => {
    if (r.status === 'leftOnly' || r.status === 'rightOnly') {
      svg.append(s('rect', { class: r.status === 'leftOnly' ? 'f-l' : 'f-r', x: x(i) - 2, y: R0 + 4, width: 4, height: R1 - R0 - 8, rx: 1, opacity: 0.8 }));
    } else if (r.delta) {
      svg.append(s('rect', { class: fillClass(r), x: x(i) - 1.5, y: Math.min(ry(0), ry(r.delta)), width: 3, height: Math.max(1.5, Math.abs(ry(r.delta) - ry(0))), rx: 1 }));
    }
    if (r.duplicate) {
      const v = val(r, r.duplicate === 'left' ? 'left' : 'right') ?? val(r, 'right') ?? val(r, 'left') ?? 0;
      svg.append(
        s('circle', { cx: x(i), cy: y(v), r: 8, fill: 'none', stroke: 'var(--ink)', 'stroke-width': 1.5 }),
        s('text', { x: x(i) + 12, y: y(v) + 4, text: label('duplicate') })
      );
    }
  });

  const cross = s('line', { class: 'axl', y1: T0, y2: R1, visibility: 'hidden' });
  svg.append(cross);
  svg.addEventListener('pointermove', ev => {
    if (N === 0) return;
    const b = svg.getBoundingClientRect();
    const rawI = N > 1 ? Math.round(((ev.clientX - b.left) * W / b.width - X0) / (X1 - X0) * (N - 1)) : 0;
    const i = Math.max(0, Math.min(N - 1, rawI));
    const r = recs[i];
    cross.setAttribute('x1', x(i));
    cross.setAttribute('x2', x(i));
    cross.setAttribute('visibility', 'visible');
    const lv = val(r, 'left'), rv = val(r, 'right');
    const lines = [
      `${model.left.short} ${fmt(M, lv)}`,
      `${model.right.short} ${fmt(M, rv)}`
    ];
    if (r.delta !== null && r.delta !== undefined) lines.push(`${text('tableDiff')}: ${fmtSigned(M, r.delta)}`);
    if (r.reason) lines.push(r.reason);
    RD.tip.show(ev, formatTime(r, i), lines);
  });
  svg.addEventListener('pointerleave', () => {
    cross.setAttribute('visibility', 'hidden');
    RD.tip.hide();
  });

  document.getElementById('chart').replaceChildren(svg);

  let dateRange = '';
  if (recs.length > 0 && recs[0].date && recs[recs.length - 1].date) {
    const p0 = parseDate(recs[0].date), p1 = parseDate(recs[recs.length - 1].date);
    if (p0 && p1) {
      if (p0.year === p1.year && p0.month === p1.month && p0.day === p1.day) {
        dateRange = `${p0.day} ${monthFull[p0.month - 1]} ${p0.year}`;
      } else {
        dateRange = `${p0.day} ${monthNames[p0.month - 1]} – ${p1.day} ${monthNames[p1.month - 1]} ${p1.year}`;
      }
    }
  }
  document.getElementById('ct').textContent = g.name + (dateRange ? `, ${dateRange}` : '');
  const gInfo = RD.groupInfo([g.key]);
  document.getElementById('cs').textContent = gInfo?.note || recs.find(r => r.reason)?.reason || text('groupAgrees');

  const bad = recs.filter(r => r.status !== 'exact');
  const maxTable = opt('maxTableRows') || 12;
  const badCount = bad.reduce((sum, r) => sum + (r.count || 1), 0);
  document.getElementById('dt').textContent = badCount
    ? text('differTitle').replace('{count}', int(badCount)).replace('{group}', g.name).replace('{unit}', model.unit) + (bad.length > maxTable ? ', ' + text('firstShown').replace('{shown}', int(maxTable)) : '')
    : text('nothingDiffers').replace('{group}', g.name);

  if (bad.length > 0) {
    document.getElementById('detail').replaceChildren(h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('tableTime') }),
        h('th', { text: text('tableResult') }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short }),
        h('th', { class: 'n', text: text('tableDiff') })
      ])),
      h('tbody', {}, bad.slice(0, maxTable).map(r => {
        const idx = recs.indexOf(r);
        const lv = val(r, 'left'), rv = val(r, 'right');
        const isBrk = r.status === 'break';
        return h('tr', {}, [
          h('td', { text: formatTime(r, idx >= 0 ? idx : 0) }),
          h('td', { text: RD.statusWord(r) }),
          h('td', { class: 'n' + (isBrk ? ' dl' : '') + (lv === null ? ' none' : ''), text: fmt(M, lv) }),
          h('td', { class: 'n' + (isBrk ? ' dr' : '') + (rv === null ? ' none' : ''), text: fmt(M, rv) }),
          h('td', { class: 'n', text: r.delta !== null && r.delta !== undefined ? fmtSigned(M, r.delta) : '' })
        ]);
      }))
    ]));
  } else {
    document.getElementById('detail').replaceChildren(h('p', { class: 'sub', text: text('pickGroup') }));
  }
}
show();
