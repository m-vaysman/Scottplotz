const { h, s, model, label, text, opt, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;

RD.tieOut(document.getElementById('tie'));
const s1 = document.getElementById('s1'), s2 = document.getElementById('s2');
if (s1) s1.style.background = RD.diff(-0.5);
if (s2) s2.style.background = RD.diff(0.5);

const maxDepth = model.records.reduce((m, r) => Math.max(m, r.group.length), 0);
const numCols = Math.max(1, maxDepth + 1);
const W = 1100, H = 520, gap = 8;
const colX = [], colW = [];
if (numCols === 1) {
  colX.push(0);
  colW.push(W);
} else if (numCols === 2) {
  const w0 = 360;
  colX.push(0, w0 + gap);
  colW.push(w0, W - w0 - gap);
} else if (numCols === 3) {
  colX.push(0, 230, 520);
  colW.push(222, 282, 580);
} else {
  const lastW = Math.max(300, Math.round(W * 0.45));
  const rem = W - gap * (numCols - 1) - lastW;
  const gw = rem / (numCols - 1);
  let curX = 0;
  for (let i = 0; i < numCols - 1; i++) {
    const w = Math.round(gw);
    colX.push(curX);
    colW.push(w);
    curX += w + gap;
  }
  colX.push(curX);
  colW.push(W - curX);
}

const sz = r => Math.max(0, val(r, 'left') ?? val(r, 'right') ?? 0);
const hasNonZero = model.records.some(r => sz(r) > 0);
model.records.forEach(r => {
  r._size = hasNonZero ? sz(r) : 1;
  r._isRecord = true;
});

const root = { name: '', level: -1, children: [], records: [] };
function addRecord(node, rec, depth) {
  node.records.push(rec);
  if (depth >= maxDepth) return;
  const gName = rec.group[depth] || '—';
  let child = node.children.find(c => c.name === gName);
  if (!child) {
    child = { name: gName, level: depth, parent: node, children: [], records: [] };
    node.children.push(child);
  }
  addRecord(child, rec, depth + 1);
}
model.records.forEach(r => addRecord(root, r, 0));

function nodeDiff(n) {
  const recs = n._isRecord ? [n] : n.records;
  if (!recs || !recs.length) return 0;
  const l = recs.reduce((t, r) => t + (val(r, 'left') ?? 0), 0);
  const r = recs.reduce((t, r) => t + (val(r, 'right') ?? 0), 0);
  return Math.abs(r - l);
}

function sortNode(node) {
  if (opt('sortBy') === 'difference') {
    if (node.children.length) {
      node.children.sort((a, b) => nodeDiff(b) - nodeDiff(a));
    }
    if (node.records.length) {
      node.records.sort((a, b) => {
        const da = Math.abs(a.delta ?? ((val(a, 'right') ?? 0) - (val(a, 'left') ?? 0)));
        const db = Math.abs(b.delta ?? ((val(b, 'right') ?? 0) - (val(b, 'left') ?? 0)));
        return db - da;
      });
    }
  }
  node.children.forEach(sortNode);
}
sortNode(root);

function calcNodeSizes(node) {
  if (node.children.length) {
    node.children.forEach(calcNodeSizes);
    node._size = node.children.reduce((t, c) => t + c._size, 0);
  } else {
    node._size = node.records.reduce((t, r) => t + r._size, 0);
  }
}
calcNodeSizes(root);
const totalSize = root._size || 1;

const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: '100%', role: 'img', 'aria-label': text('chartTitle') });
const blocks = new Map();

const block = (node, level, y, hh, blockTitle, single) => {
  const aw = single ? [single] : node.records;
  const l = aw.reduce((t, a) => t + (val(a, 'left') ?? 0), 0);
  const r = aw.reduce((t, a) => t + (val(a, 'right') ?? 0), 0);
  const d = r - l;
  const key = single || node;
  if (single) single._groupNode = node;

  const tol = model.tolerance?.absolute ?? 0;
  let fill;
  if (single) {
    if (single.status === 'leftOnly') fill = 'var(--left)';
    else if (single.status === 'rightOnly') fill = 'var(--right)';
    else if (single.status === 'exact' || single.status === 'tolerance' || Math.abs(d) <= tol) fill = 'var(--wash)';
    else fill = RD.diff(Math.sign(d) * Math.min(1, 0.2 + (l ? Math.abs(d) / Math.abs(l) * 3 : 1)));
  } else {
    fill = Math.abs(d) <= tol ? 'var(--wash)' : RD.diff(Math.sign(d) * Math.min(1, 0.2 + (l ? Math.abs(d) / Math.abs(l) * 3 : 1)));
  }

  const b = s('rect', {
    class: 'blk',
    x: colX[level],
    y,
    width: colW[level],
    height: Math.max(2, hh),
    rx: 3,
    style: `fill:${fill}`
  });
  b.addEventListener('click', () => { sel = key; show(); });

  const hoverLines = [`${model.left.short} ${fmt(M, l)}`, `${model.right.short} ${fmt(M, r)}`];
  if (single?.reason) hoverLines.push(single.reason);
  RD.hover(b, blockTitle, hoverLines);

  blocks.set(key, b);
  svg.append(b);

  if (hh > 17) {
    const diffNote = Math.abs(d) > tol ? `  ${fmtSigned(M, d)}` : '';
    const maxChars = Math.max(8, Math.floor((colW[level] - 16) / 7.5));
    const txt = clip(blockTitle + diffNote, maxChars);
    svg.append(s('text', {
      x: colX[level] + 8,
      y: y + Math.min(hh / 2 + 4, 17),
      'pointer-events': 'none',
      'font-weight': level < numCols - 1 ? 600 : 400,
      text: txt
    }));
  }
};

function renderNode(node, y, h) {
  if (node !== root) {
    block(node, node.level, y, h, node.name, null);
  }
  if (node.level === maxDepth - 1) {
    let ry = y;
    node.records.forEach(r => {
      const rh = (r._size / totalSize) * H;
      const title = (r.key && r.key !== 'none' && r.key !== r.name ? `${r.key}, ` : '') + r.name;
      block(node, maxDepth, ry, rh, title, r);
      ry += rh;
    });
  } else {
    let cy = y;
    node.children.forEach(child => {
      const ch = (child._size / totalSize) * H;
      renderNode(child, cy, ch);
      cy += ch;
    });
  }
}
renderNode(root, 0, H);
document.getElementById('chart').append(svg);

function getNodesAtLevel(node, targetLevel) {
  if (node.level === targetLevel) return [node];
  return node.children.flatMap(c => getNodesAtLevel(c, targetLevel));
}
const targetLevel = Math.min(1, Math.max(0, maxDepth - 1));
const candidates = getNodesAtLevel(root, targetLevel);
let sel = candidates.length ? candidates.reduce((best, cur) => nodeDiff(cur) > nodeDiff(best) ? cur : best, candidates[0]) : (model.records[0] ?? null);

function show() {
  if (!sel) return;
  blocks.forEach((b, k) => b.setAttribute('class', k === sel ? 'blk on' : 'blk'));
  const one = sel._isRecord ? sel : null;
  const node = one ? (one._groupNode || root) : sel;
  const aw = node.records || [node];
  const l = aw.reduce((t, a) => t + (val(a, 'left') ?? 0), 0);
  const r = aw.reduce((t, a) => t + (val(a, 'right') ?? 0), 0);
  const d = r - l;
  const recCount = aw.reduce((t, a) => t + (a.count || 1), 0);
  const tol = model.tolerance?.absolute ?? 0;
  const diffCount = aw.filter(a => a.status === 'break' || a.status === 'leftOnly' || a.status === 'rightOnly' || a.duplicate || (a.delta !== null && Math.abs(a.delta) > tol)).reduce((t, a) => t + (a.count || 1), 0);

  const nodeTitle = node.name || text('all');
  const dStr = fmtSigned(M, d);
  document.getElementById('dt').textContent = `${nodeTitle}: ${model.right.short} ${dStr} against ${model.left.short}`;
  const unitStr = recCount === 1 ? model.unitOne : model.unit;
  document.getElementById('ds').textContent = `${int(recCount)} ${unitStr}, ` + text('differ').replace('{count}', int(diffCount)) + '.';

  const glyph = rec => rec.duplicate ? '×2' : ({ exact: '=', tolerance: '≈', break: '≠', leftOnly: label('leftOnly'), rightOnly: label('rightOnly') })[rec.status] || '';

  const trs = aw.map(a => {
    const isDiff = a.status === 'break' || a.duplicate || a.status === 'leftOnly' || a.status === 'rightOnly';
    const vl = val(a, 'left'), vr = val(a, 'right');
    const dlClass = (isDiff && vl !== null) ? ' dl' : '';
    const drClass = (isDiff && vr !== null) ? ' dr' : '';
    const diffValStr = (vl !== null && vr !== null && vl !== vr) ? fmtSigned(M, vr - vl) : (a.delta !== null && a.delta !== 0 ? fmtSigned(M, a.delta) : '');
    const keyClass = (a.key === 'none' || !a.key) ? 'none' : '';
    const keyText = (a.key === 'none' || !a.key) ? label('none') : a.key;

    return h('tr', { class: a === one ? 'on' : '', on: { click: () => { sel = a; show(); } } }, [
      h('td', { text: glyph(a) }),
      h('td', { class: keyClass, text: keyText }),
      h('td', { text: a.name }),
      h('td', { class: 'n' + dlClass + (vl === null ? ' none' : ''), text: fmt(M, vl) }),
      h('td', { class: 'n' + drClass + (vr === null ? ' none' : ''), text: fmt(M, vr) }),
      h('td', { class: 'n', text: diffValStr }),
      h('td', { text: a.reason || '' })
    ]);
  });

  const tfoot = h('tfoot', {}, h('tr', {}, [
    h('td', { colspan: 3, text: text('total') }),
    h('td', { class: 'n', text: fmt(M, l) }),
    h('td', { class: 'n', text: fmt(M, r) }),
    h('td', { class: 'n', text: fmtSigned(M, d) }),
    h('td', {})
  ]));

  const thead = h('thead', {}, h('tr', {}, [
    h('th', { text: '' }),
    h('th', { text: text('item') }),
    h('th', { text: text('name') }),
    h('th', { class: 'n', text: model.left.short }),
    h('th', { class: 'n', text: model.right.short }),
    h('th', { class: 'n', text: text('difference') }),
    h('th', { text: text('reason') })
  ]));

  document.getElementById('detail').replaceChildren(
    h('table', { class: 'tbl' }, [thead, h('tbody', {}, trs), tfoot])
  );
}
show();
