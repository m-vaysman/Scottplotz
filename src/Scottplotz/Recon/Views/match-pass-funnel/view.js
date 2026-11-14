const { h, s, model, label, text, opt, int } = RD;
RD.tieOut(document.getElementById('tie'));

const passNames = model.groups.length > 0
  ? model.groups.map(g => g.path[0] || g.name)
  : [...new Set(model.records.map(r => r.attrs?.pass).filter(Boolean))];

let passList = passNames.map(passName => {
  const g = model.groups.find(x => (x.path[0] || x.name) === passName);
  const passRecs = model.records.filter(r => r.attrs?.pass === passName);
  let l = 0, r = 0;
  passRecs.forEach(rec => {
    l += rec.attrs?.leftCount != null ? Number(rec.attrs.leftCount) : (rec.status === 'rightOnly' ? 0 : (rec.count || 1));
    r += rec.attrs?.rightCount != null ? Number(rec.attrs.rightCount) : (rec.status === 'leftOnly' ? 0 : (rec.count || 1));
  });
  return {
    name: passName,
    rule: g?.note || '',
    l,
    r,
    records: passRecs,
    isUnmatched: false
  };
});

if (passList.length === 0) {
  const matchedRecs = model.records.filter(r => r.status !== 'leftOnly' && r.status !== 'rightOnly');
  if (matchedRecs.length > 0) {
    let l = 0, r = 0;
    matchedRecs.forEach(rec => {
      l += rec.attrs?.leftCount != null ? Number(rec.attrs.leftCount) : (rec.count || 1);
      r += rec.attrs?.rightCount != null ? Number(rec.attrs.rightCount) : (rec.count || 1);
    });
    passList = [{
      name: text('defaultPassName') || 'Matched',
      rule: '',
      l,
      r,
      records: matchedRecs,
      isUnmatched: false
    }];
  }
}

const unmatchedRecs = model.records.filter(r =>
  (!r.attrs?.pass && (r.status === 'leftOnly' || r.status === 'rightOnly')) ||
  r.attrs?.pass === 'unmatched' ||
  (passNames.length === 0 && (r.status === 'leftOnly' || r.status === 'rightOnly'))
);

let unmatchedLeft = 0, unmatchedRight = 0;
unmatchedRecs.forEach(rec => {
  if (rec.status === 'leftOnly') unmatchedLeft += rec.count || 1;
  else if (rec.status === 'rightOnly') unmatchedRight += rec.count || 1;
  else {
    if (rec.left !== null) unmatchedLeft += rec.count || 1;
    if (rec.right !== null) unmatchedRight += rec.count || 1;
  }
});

const unmatchedRow = {
  name: text('unmatchedName'),
  rule: text('unmatchedRule'),
  l: 0,
  r: 0,
  records: unmatchedRecs,
  isUnmatched: true
};

const rows = opt('showUnmatchedRow') !== false ? [...passList, unmatchedRow] : passList;
if (rows.length === 0) return;

const sumPassL = passList.reduce((t, p) => t + p.l, 0);
const sumPassR = passList.reduce((t, p) => t + p.r, 0);
const L0 = sumPassL + unmatchedLeft;
const R0 = sumPassR + unmatchedRight;

const W = 1100, MID = W / 2, GAP = 150, RH = 62, TOP = 30;
const MAXW = MID - GAP - 70;
const maxVal = Math.max(L0, R0, 1);
const sc = MAXW / maxVal;

const svg = s('svg', {
  viewBox: `0 0 ${W} ${TOP + rows.length * RH + 4}`,
  width: '100%',
  role: 'img',
  'aria-label': text('chartTitle')
});

svg.append(
  s('text', { x: MID - GAP, y: 16, 'text-anchor': 'end', 'font-weight': 600, text: text('headerLeft') }),
  s('text', { x: MID + GAP, y: 16, 'font-weight': 600, text: text('headerRight') })
);

let sel = rows.findIndex(p => !p.isUnmatched && p.l !== p.r);
if (sel < 0) sel = rows.length > 1 ? rows.length - 2 : 0;

let lp = L0, rp = R0;
const hits = [];

rows.forEach((p, i) => {
  const y = TOP + i * RH, cy = y + RH / 2;
  const hit = s('rect', { class: 'rowhit', x: 0, y: y + 2, width: W, height: RH - 4, rx: 4 });
  hits.push(hit);
  svg.append(hit);

  hit.addEventListener('click', () => { sel = i; show(); });

  const np = { 'pointer-events': 'none' };
  const lw = lp * sc, rw = rp * sc;
  const lm = p.l * sc, rm = p.r * sc;

  const parts = p.name.split(/:\s*(.*)/);
  if (parts.length > 1 && parts[1]) {
    svg.append(
      s('text', { x: MID, y: cy - 3, 'text-anchor': 'middle', 'font-weight': 600, text: parts[0], ...np }),
      s('text', { class: 'sec', x: MID, y: cy + 13, 'text-anchor': 'middle', text: parts[1], ...np })
    );
  } else {
    svg.append(s('text', { x: MID, y: cy + 4, 'text-anchor': 'middle', 'font-weight': 600, text: p.name, ...np }));
  }

  if (lm > 0) svg.append(s('rect', { class: 'f-ex', x: MID - GAP - lm, y: cy - 12, width: lm, height: 24, rx: 3, ...np }));
  if (lw - lm > 0) svg.append(s('rect', { class: 'f-l', x: MID - GAP - lw, y: cy - 12, width: Math.max(2, lw - lm - (lm ? 2 : 0)), height: 24, rx: 3, ...np }));
  if (rm > 0) svg.append(s('rect', { class: 'f-ex', x: MID + GAP, y: cy - 12, width: rm, height: 24, rx: 3, ...np }));
  if (rw - rm > 0) svg.append(s('rect', { class: 'f-r', x: MID + GAP + rm + (rm ? 2 : 0), y: cy - 12, width: Math.max(2, rw - rm - (rm ? 2 : 0)), height: 24, rx: 3, ...np }));

  svg.append(
    s('text', { x: MID - GAP - lw - 8, y: cy + 4, 'text-anchor': 'end', text: int(lp), ...np }),
    s('text', { x: MID + GAP + rw + 8, y: cy + 4, text: int(rp), ...np })
  );

  p.lp = lp;
  p.rp = rp;
  lp -= p.l;
  rp -= p.r;

  RD.hover(hit, p.name, () => p.l || p.r
    ? [`${model.left.short}: ${int(p.lp)} in, ${int(p.l)} paired`, `${model.right.short}: ${int(p.rp)} in, ${int(p.r)} paired`]
    : [`${model.left.short}: ${int(p.lp)} unmatched`, `${model.right.short}: ${int(p.rp)} unmatched`]
  );
});

document.getElementById('chart').append(svg);

function show() {
  hits.forEach((e, i) => e.setAttribute('class', i === sel ? 'rowhit on' : 'rowhit'));
  const p = rows[sel];
  if (!p) return;

  const dtText = p.isUnmatched
    ? text('unmatchedDetail')
        .replace('{leftCount}', int(p.lp))
        .replace('{rightCount}', int(p.rp))
        .replace('{leftShort}', model.left.short)
        .replace('{rightShort}', model.right.short)
        .replace('{unit}', model.unit)
    : text('passDetail')
        .replace('{name}', p.name)
        .replace('{leftCount}', int(p.l))
        .replace('{rightCount}', int(p.r))
        .replace('{leftShort}', model.left.short)
        .replace('{rightShort}', model.right.short)
        .replace('{unit}', model.unit);

  document.getElementById('dt').textContent = dtText;
  document.getElementById('ds').textContent = p.rule;

  const examples = (p.records.filter(r => r.reason || r.attrs?.example).length > 0
    ? p.records.filter(r => r.reason || r.attrs?.example)
    : p.records
  ).slice(0, 15);

  const thLeft = text('colLeft') || (model.left.short + ' ' + model.unitOne);
  const thRight = text('colRight') || (model.right.short + ' ' + model.unitOne);
  const thWhy = p.isUnmatched ? text('whyUnmatched') : text('whyPaired');
  const isNone = v => v === 'none' || v === label('none');

  const rowsHtml = examples.map(r => {
    const lRef = r.attrs?.leftRef ?? (r.status === 'rightOnly' ? label('none') : r.key);
    const rRef = r.attrs?.rightRef ?? (r.status === 'leftOnly' ? label('none') : (r.name && r.name !== r.key ? r.name : r.key));
    return h('tr', {}, [
      h('td', { class: isNone(lRef) ? 'none' : '', text: lRef }),
      h('td', { class: isNone(rRef) ? 'none' : '', text: rRef }),
      h('td', { text: r.reason || '' })
    ]);
  });

  document.getElementById('detail').replaceChildren(
    h('table', { class: 'tbl', style: 'max-width:900px' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: thLeft }),
        h('th', { text: thRight }),
        h('th', { text: thWhy })
      ])),
      h('tbody', {}, rowsHtml)
    ])
  );
}

show();
