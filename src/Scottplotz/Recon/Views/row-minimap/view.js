const { h, model, label, text, opt, fmt, val, int, clip, statusWord } = RD;
RD.tieOut(document.getElementById('tie'));

// Every record is a span of rows; a record with a count stands for that many consecutive rows.
const recs = model.records;
const starts = [];
let N = 0;
recs.forEach(r => { starts.push(N); N += Math.max(1, r.count || 1); });
const endOf = i => i + 1 < recs.length ? starts[i + 1] : N;
const recAt = pos => {
  let lo = 0, hi = recs.length - 1;
  while (lo < hi) { const mid = (lo + hi + 1) >> 1; if (starts[mid] <= pos) lo = mid; else hi = mid - 1; }
  return lo;
};
const isBad = r => r.status !== 'exact' || !!r.duplicate;

const cv = document.getElementById('cv'), lens = document.getElementById('lens'), map = document.getElementById('map');
cv.setAttribute('role', 'img');
cv.setAttribute('aria-label', text('mapLabel'));
const detail = document.getElementById('detail'), dt = document.getElementById('dt'), ds = document.getElementById('ds');

if (!N) {
  map.style.display = 'none';
  dt.textContent = text('empty');
} else {
  // Layout: S rows are stacked in each pixel column, so S rows share one column.
  const CW_PX = cv.width, CH = cv.height, S = N > 6000 ? Math.ceil(N / 1200) : 5;
  const cols = Math.ceil(N / S), cw = CW_PX / cols, rh = CH / S;
  const ctx = cv.getContext('2d'), css = getComputedStyle(document.documentElement), col = v => css.getPropertyValue(v).trim();
  const C = { exact: col('--exact'), tolerance: col('--tol'), break: col('--brk'), leftOnly: col('--left'), rightOnly: col('--right') };
  ctx.fillStyle = col('--surface'); ctx.fillRect(0, 0, CW_PX, CH);
  const cell = (c0, c1, r0, r1) => { // columns c0..c1 and sub-rows r0..r1, inclusive
    const x0 = Math.floor(c0 * cw), x1 = Math.ceil((c1 + 1) * cw);
    ctx.fillRect(x0, r0 * rh + 1, Math.max(1, x1 - x0), (r1 - r0 + 1) * rh - 1.2);
  };
  recs.forEach((r, i) => {
    const a = starts[i], b = endOf(i) - 1, c0 = Math.floor(a / S), c1 = Math.floor(b / S);
    ctx.fillStyle = r.duplicate ? C.break : C[r.status] || C.exact;
    ctx.globalAlpha = r.status === 'exact' && !r.duplicate ? .45 : 1;
    const rowsOf = (c, lo, hi) => { for (let k = lo; k <= hi; k++) cell(c, c, k, k); };
    if (c0 === c1) { rowsOf(c0, a - c0 * S, b - c0 * S); return; }
    rowsOf(c0, a - c0 * S, S - 1);
    rowsOf(c1, 0, b - c1 * S);
    if (c1 - c0 > 1) for (let k = 0; k < S; k++) cell(c0 + 1, c1 - 1, k, k);
  });
  ctx.globalAlpha = 1;

  // Ruler: the key at five evenly spaced positions.
  const ruler = document.getElementById('ruler');
  [0, .25, .5, .75, 1].map(f => recs[recAt(Math.round(f * (N - 1)))].key)
    .filter((k, i, all) => i === 0 || k !== all[i - 1])
    .forEach(k => ruler.append(h('span', { text: k })));

  // Column profile: records that differ beyond tolerance in each field, and those that differ within it.
  const prof = model.fields.map(f => {
    let brk = 0, tol = 0, why = null, whyTol = null;
    recs.forEach(r => {
      const n = Math.max(1, r.count || 1), a = val(r, 'left', f.key), b = val(r, 'right', f.key);
      if (r.breaks.includes(f.key)) { brk += n; why = why ?? r.reason; }
      else if (f.compare !== false && a !== null && b !== null && a !== b) { tol += n; whyTol = whyTol ?? r.reason; }
    });
    return { f, brk, tol, why: why || whyTol };
  }).sort((p, q) => (q.brk + q.tol) - (p.brk + p.tol));
  const maxc = Math.max(0, ...prof.map(p => p.brk + p.tol));
  const profEl = document.getElementById('prof');
  prof.forEach(p => {
    const t = p.brk + p.tol, bar = h('i', { style: `width:${t && maxc ? Math.max(2, t / maxc * 100) : 0}%` }, [
      p.brk ? h('b', { style: `flex:${p.brk} 1 0` }) : null, p.tol ? h('b', { class: 't', style: `flex:${p.tol} 1 0` }) : null]);
    if (t) RD.hover(bar, p.f.label, [p.brk ? `${label('break')} ${int(p.brk)}` : '', p.tol ? `${label('tolerance')} ${int(p.tol)}` : ''].filter(Boolean));
    profEl.append(h('div', {}, [h('b', { style: 'font-weight:600', text: p.f.label }), h('div', { class: 'd', text: t ? clip(p.why || "", 44) : text('profNone') })]), bar, h('span', { class: 'n', text: int(t) }));
  });

  // Lens: a window of L rows, moved by selecting or dragging on the strip.
  const L = Math.max(1, Math.min(N, Math.round(+opt('lensRows') || 1)));
  const badPre = [0]; recs.forEach(r => badPre.push(badPre[badPre.length - 1] + (isBad(r) ? 1 : 0)));
  // Start where the most differing records sit under the lens.
  let at = 0, best = -1;
  for (let p = 0, step = Math.max(1, Math.floor(N / 800)); p <= N - L; p += step) {
    const c = badPre[recAt(p + L - 1) + 1] - badPre[recAt(p)];
    if (c > best) { best = c; at = p; }
  }
  const place = clientX => {
    const b = cv.getBoundingClientRect();
    at = Math.max(0, Math.min(N - L, Math.round((clientX - b.left) / (b.width || 1) * N / S) * S - Math.floor(L / 2)));
    show();
  };
  let drag = false;
  map.addEventListener('pointerdown', ev => { drag = true; try { map.setPointerCapture(ev.pointerId); } catch (e) { /* not capturable */ } place(ev.clientX); });
  map.addEventListener('pointermove', ev => { if (drag) place(ev.clientX); });
  const stop = () => { drag = false; };
  map.addEventListener('pointerup', stop); map.addEventListener('pointercancel', stop);

  const GLYPH = { exact: '=', tolerance: '≈', break: '≠' };
  function show() {
    lens.style.left = at / N * 100 + '%'; lens.style.width = L / N * 100 + '%';
    const i0 = recAt(at), i1 = recAt(at + L - 1), under = recs.slice(i0, i1 + 1);
    const bad = under.filter(isBad);
    const badRows = bad.reduce((sum, r) => { const i = recs.indexOf(r); return sum + Math.min(endOf(i), at + L) - Math.max(starts[i], at); }, 0);
    const shown = (bad.length ? bad : under).slice(0, 10), reasons = [...new Set(bad.map(r => r.reason).filter(Boolean))];
    dt.textContent = text('lensTitle').replace('{from}', recs[i0].key).replace('{to}', recs[i1].key).replace('{bad}', int(badRows)).replace('{total}', int(L)).replace('{unit}', L === 1 ? model.unitOne : model.unit);
    ds.textContent = bad.length ? reasons.join(' ') : text('lensAllAgree').replace('{shown}', int(shown.length));
    const F = model.fields, mark = r => r.duplicate ? '×2' : GLYPH[r.status] ?? label(r.status);
    const rows = shown.flatMap(r => {
      if (r.left === null && r.right === null)
        return [h('tr', {}, [h('td', { text: mark(r) }), h('td', { text: r.key }), h('td', { class: 'none', colspan: F.length + 1, text: statusWord(r) + (r.count > 1 ? ', ' + int(r.count) + ' ' + model.unit : '') })])];
      return [['left', 'dl'], ['right', 'dr']].map(([side, cls], si) => h('tr', {}, [
        h('td', { text: si ? '' : mark(r) }), h('td', { text: si ? '' : r.key }), h('td', { class: 'none', text: model[side].short }),
        ...(r[side] ? F.map(f => {
          const a = val(r, 'left', f.key), b = val(r, 'right', f.key), v = side === 'left' ? a : b;
          return h('td', { class: 'mono' + (r.left && r.right && a !== b ? ' ' + cls : ''), text: fmt(f.key, v) });
        }) : [h('td', { colspan: F.length, class: 'none', text: text('noRow') })])]));
    });
    detail.replaceChildren(h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [h('th', { text: '' }), h('th', { text: text('keyHead') }), h('th', { text: text('sideHead') }), ...F.map(f => h('th', { text: f.label }))])),
      h('tbody', {}, rows)]));
  }
  show();
}
