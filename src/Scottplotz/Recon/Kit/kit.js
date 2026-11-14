const RD = (() => {
  const q = new URLSearchParams(location.search);
  if (q.get('theme')) document.documentElement.dataset.theme = q.get('theme');
  addEventListener('load', () => { const y = +q.get('y'); if (y) scrollTo(0, y); });
  const model = JSON.parse(document.getElementById('rd-model').textContent);
  const money = (n, d = 2) => (n < 0 ? '−' : '') + Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
  const signed = (n, d = 2) => (n > 0 ? '+' : '') + money(n, d);
  const int = n => Math.round(n).toLocaleString('en-US');
  const mk = ns => (tag, attrs = {}, kids = []) => {
    const e = ns ? document.createElementNS(ns, tag) : document.createElement(tag);
    for (const k in attrs) {
      if (k === 'text') e.textContent = attrs[k];
      else if (k === 'on') for (const ev in attrs.on) e.addEventListener(ev, attrs.on[ev]);
      else if (attrs[k] !== null && attrs[k] !== undefined) e.setAttribute(k, attrs[k]);
    }
    [].concat(kids).forEach(c => c && e.append(c));
    return e;
  };
  const h = mk(null), s = mk('http://www.w3.org/2000/svg');
  const tipEl = h('div', { id: 'tip' });
  document.body.append(tipEl);
  const tip = {
    show(ev, title, lines = []) {
      tipEl.replaceChildren(h('b', { text: title }), ...lines.map(l => h('span', { text: l })));
      tipEl.style.display = 'block';
      const w = tipEl.offsetWidth, x = ev.clientX + 14, y = ev.clientY + 14;
      tipEl.style.left = Math.min(x, innerWidth - w - 8) + 'px';
      tipEl.style.top = Math.min(y, innerHeight - tipEl.offsetHeight - 8) + 'px';
    },
    hide() { tipEl.style.display = 'none'; }
  };
  const hover = (el, title, lines) => { el.addEventListener('pointermove', ev => tip.show(ev, title, typeof lines === 'function' ? lines() : lines)); el.addEventListener('pointerleave', tip.hide); return el; };
  // A difference takes the colour of the side that is higher. t in [-1, 1]: negative = left higher.
  const diff = t => { const p = Math.round(Math.min(1, Math.abs(t)) * 100); return t === 0 ? 'var(--mid)' : `color-mix(in oklab, var(--${t < 0 ? 'left' : 'right'}) ${p}%, var(--mid))`; };

  const label = k => model.labels[k] ?? k;
  const text = k => model.text[k] ?? '';
  const opt = k => model.options[k] ?? null;
  const field = k => model.fields.find(f => f.key === k) ?? null;
  const measure = model.measure ? field(model.measure) : null;
  const fmt = (key, v) => { const f = field(key); return v === null || v === undefined ? label('none') : f && f.type === 'number' ? money(v, f.decimals) : String(v); };
  const fmtSigned = (key, v) => v === null || v === undefined ? '' : signed(v, field(key)?.decimals ?? 2);
  const val = (rec, side, key = model.measure) => rec[side] && key ? rec[side][key] ?? null : null;
  const cls = { leftOnly: 'l', exact: 'ex', tolerance: 'tol', break: 'brk', rightOnly: 'r' };
  const statusWord = rec => label(rec.status) + (rec.duplicate ? ', ' + label('duplicate').toLowerCase() : '');
  const clip = (t, n) => t.length > n ? t.slice(0, n - 1) + '…' : t;
  // Group names at one level of the group path, in the order of model.groups first and then first seen.
  const groupNames = (records = model.records, level = 0) => {
    const declared = model.groups.filter(g => g.path.length === level + 1).map(g => g.path[level]);
    return [...new Set([...declared, ...records.map(r => r.group[level]).filter(g => g !== undefined)])];
  };
  const groupInfo = path => { const k = [].concat(path).join('\u0001'); return model.groups.find(g => g.path.join('\u0001') === k) ?? null; };
  // Round tick values from zero to at least max. The last tick is the top of the scale.
  const ticks = (max, n = 5) => {
    if (!(max > 0)) return [0, 1];
    const raw = max / n, p = 10 ** Math.floor(Math.log10(raw)), m = raw / p, step = (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * p, out = [];
    for (let v = 0; v < max + step * .999; v += step) out.push(+v.toPrecision(12));
    return out;
  };
  // o.leftMeta / o.rightMeta replace the count text beside each side; o.net === false hides the net line.
  const tieOut = (el, o = {}) => {
    const c = model.summary.counts, sm = model.summary, u = model.unit;
    const meta = (n, total) => `${int(n)} ${u}` + (total === null || !measure ? '' : `, ${fmt(measure.key, total)}`);
    const side = (k, d, m) => h('div', {}, [h('i', { class: 'sw ' + k }), h('b', { text: (d.name.toLowerCase().includes(d.short.toLowerCase()) ? d.name : d.short) + ' ' }), d.name.toLowerCase().includes(d.short.toLowerCase()) ? null : h('span', { text: d.name }), h('span', { class: 'mt', text: m })]);
    const segs = [['lo', 'leftOnly'], ['ex', 'exact'], ['tol', 'tolerance'], ['brk', 'break'], ['ro', 'rightOnly']].filter(x => c[x[1]] > 0);
    const bar = h('div', { class: 'tiebar', role: 'img', 'aria-label': segs.map(x => `${label(x[1])} ${c[x[1]]}`).join(', ') },
      segs.map(x => hover(h('div', { class: 'seg ' + x[0], style: `flex:${c[x[1]]} 1 0`, text: int(c[x[1]]) }), label(x[1]), [`${int(c[x[1]])} ${u}`])));
    const li = (sw, glyph, k, n) => h('span', {}, [h('i', { class: 'sw ' + sw }), glyph ? h('span', { class: 'gl', text: glyph + ' ' }) : null, label(k) + ' ', h('b', { text: int(n) })]);
    const leg = h('div', { class: 'tie-leg' }, [
      li('l', '', 'leftOnly', c.leftOnly), li('', '=', 'exact', c.exact), li('tol', '≈', 'tolerance', c.tolerance),
      li('brk', '≠', 'break', c.break), li('r', '', 'rightOnly', c.rightOnly), li('dup', '', 'duplicate', sm.duplicates),
      sm.net === null || !measure || o.net === false ? null : h('span', { class: 'net' }, [label('net') + ' ', h('b', { text: fmtSigned(measure.key, sm.net) })])
    ]);
    el.classList.add('panel');
    el.replaceChildren(h('div', { class: 'tie-sides' }, [side('l', model.left, o.leftMeta ?? meta(sm.leftCount, sm.leftTotal)), side('r', model.right, o.rightMeta ?? meta(sm.rightCount, sm.rightTotal))]), bar, leg);
  };
  document.querySelectorAll('[data-text]').forEach(e => { e.textContent = text(e.dataset.text); });
  document.querySelectorAll('[data-label]').forEach(e => { e.textContent = label(e.dataset.label); });
  return { model, money, signed, int, h, s, tip, hover, diff, label, text, opt, field, measure, fmt, fmtSigned, val, cls, statusWord, clip, groupNames, groupInfo, ticks, tieOut };
})();
