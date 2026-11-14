const { h, model, text, label, opt, fmt, int } = RD;
const F = model.fields;

RD.tieOut(document.getElementById('tie'));

const swClass = st => {
  if (st === 'leftOnly') return 'l';
  if (st === 'rightOnly') return 'r';
  if (st === 'tolerance') return 'tol';
  if (st === 'break') return 'brk';
  if (st === 'duplicate') return 'dup';
  return '';
};

const rawKeys = RD.groupNames();
const allKeys = rawKeys.length > 0 ? rawKeys : [...new Set(model.records.map(r => r.group[0] || ''))];
if (!allKeys.length && model.records.length > 0) allKeys.push('');

const groups = allKeys.map(key => {
  const recs = model.records.filter(r => (r.group[0] ?? '') === key || (!key && !r.group.length));
  const info = RD.groupInfo(key);
  const attrs = info?.attrs || {};
  const lines = [];
  recs.forEach(r => {
    const count = r.count ?? 1;
    if (r.duplicate) {
      lines.push({
        left: r.left,
        right: r.right,
        status: r.status,
        breaks: r.breaks ?? [],
        reason: null,
        count
      });
      const isLeft = r.duplicate === 'left';
      lines.push({
        left: isLeft ? r.left : null,
        right: isLeft ? null : r.right,
        status: 'duplicate',
        breaks: [],
        reason: r.reason,
        count
      });
    } else {
      lines.push({
        left: r.left,
        right: r.right,
        status: r.status,
        breaks: r.breaks ?? [],
        reason: r.reason,
        count
      });
    }
  });
  return { key, info, attrs, lines, recs };
}).filter(g => g.lines.length > 0 || model.records.length === 0);

if (opt('sortBy') === 'breaks') {
  groups.sort((a, b) => {
    const aB = a.lines.filter(l => l.status === 'break').length;
    const bB = b.lines.filter(l => l.status === 'break').length;
    return bB - aB;
  });
}

let sel = groups.find(g => g.lines.some(l => l.status === 'break')) ?? groups[0];

const btns = groups.map(g => {
  const bad = g.lines.filter(x => x.status !== 'exact');
  const totalCount = g.lines.reduce((s, l) => s + l.count, 0);
  const badCount = bad.reduce((s, l) => s + l.count, 0);
  const summaryText = badCount > 0
    ? text('linesDiffer').replace('{differ}', int(badCount)).replace('{total}', int(totalCount)).replace('{unit}', model.unit)
    : text('allAgree').replace('{unit}', model.unit);

  const who = g.attrs.patient || g.info?.name || g.attrs.who || '';
  const title = g.key || text('defaultGroup');

  const b = h('button', {
    class: 'claim',
    on: { click: () => { sel = g; show(); } }
  }, [
    h('b', { text: title }),
    who ? h('span', { class: 'who', text: who }) : null,
    h('div', { class: 'strip' }, [
      ...g.lines.map(x => h('i', { class: ('sw ' + swClass(x.status)).trim(), title: label(x.status) })),
      h('span', { text: summaryText })
    ])
  ]);
  return { g, el: b };
});
document.getElementById('list').replaceChildren(...btns.map(b => b.el));

function show() {
  if (!sel) return;
  btns.forEach(b => b.el.setAttribute('aria-current', String(b.g === sel)));
  const g = sel;
  const who = g.attrs.patient || g.info?.name || g.attrs.who || '';
  const title = g.key || text('defaultGroup');
  document.getElementById('dt').textContent = who ? `${title}, ${who}` : title;

  let meta = text('groupMeta');
  if (meta && g.attrs.dos) {
    meta = meta.replace('{dos}', g.attrs.dos);
  } else if (Object.keys(g.attrs).length) {
    meta = Object.entries(g.attrs).map(([k, v]) => `${k}: ${v}`).join(', ');
  } else {
    meta = '';
  }
  document.getElementById('ds').textContent = meta;

  const diffEl = document.getElementById('diff');
  diffEl.style.gridTemplateColumns = `repeat(${F.length}, auto) 44px repeat(${F.length}, auto)`;

  const out = [];
  const hd = () => F.map(f => h('div', { class: 'hd' + (f.type === 'number' ? ' amt' : ''), text: f.label }));
  out.push(...hd(), h('div', { class: 'hd gut' }), ...hd());

  const GLYPHS = { exact: '=', tolerance: '≈', break: '≠', duplicate: '×2' };

  g.lines.forEach(x => {
    const cells = (side, cls) => {
      const data = x[side];
      if (!data) {
        return [h('div', { class: 'gap', style: `grid-column: span ${F.length}`, text: side === 'left' ? text('gapLeft') : text('gapRight') })];
      }
      return F.map(f => {
        const val = data[f.key];
        const textVal = (val === '' || val === null || val === undefined) ? '–' : fmt(f.key, val);
        const differs = x.left && x.right && ((x.breaks && x.breaks.includes(f.key)) || x.left[f.key] !== x.right[f.key]);
        const extraCls = (differs ? ' ' + cls : '') + (f.type === 'number' ? ' amt' : '');
        return h('div', { class: extraCls.trim(), text: textVal });
      });
    };

    const gl = GLYPHS[x.status];
    const gut = h('div', { class: 'gut', title: label(x.status) },
      gl ? gl : [h('i', { class: ('sw ' + swClass(x.status)).trim(), style: 'margin:0' })]
    );

    out.push(...cells('left', 'dl'), gut, ...cells('right', 'dr'));
    if (x.reason) {
      out.push(h('div', { class: 'why', text: x.reason }));
    }
  });

  diffEl.replaceChildren(...out);
}
if (sel) show();
