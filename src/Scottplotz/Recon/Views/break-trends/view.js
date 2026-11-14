const { h, s, model, label, text, opt, int } = RD;

RD.tieOut(document.getElementById('tie'));

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
function fmtDate(str) {
  if (!str) return '';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(str);
  if (!m) return str;
  return `${+m[3]} ${MONTHS[+m[2] - 1]}`;
}

const OPEN = ['leftOnly', 'break', 'rightOnly'];
// Only dated records with an open status make a column; bulk rows (exact, tolerance) have no date and only feed the tie-out.
const dated = model.records.filter(r => r.date && OPEN.includes(r.status));
const dates = [...new Set(dated.map(r => r.date))].sort();
if (!dates.length) {
  document.getElementById('chart').replaceChildren(h('p', { class: 'sub', text: text('needsDates') }));
  document.getElementById('dt').parentElement.hidden = true;
  return;
}
const groupNames = [...new Set([...RD.groupNames().filter(g => dated.some(r => r.group[0] === g) || RD.groupInfo(g)), ...dated.map(r => r.group[0] ?? '')])];

const data = new Map();
groupNames.forEach(g => {
  const byDate = new Map();
  dates.forEach(d => byDate.set(d, { leftOnly: 0, break: 0, rightOnly: 0, open: 0 }));
  data.set(g, byDate);
});

dated.forEach(r => {
  const entry = data.get(r.group[0] ?? '').get(r.date);
  entry[r.status] += r.count;
  entry.open += r.count;
});

let maxOpen = 0;
data.forEach(byDate => {
  byDate.forEach(e => {
    if (e.open > maxOpen) maxOpen = e.open;
  });
});
const max = Math.max(1, maxOpen);

const W = 330, H = 120;
const numDates = Math.max(1, dates.length);
const CW = W / numDates;
const bw = Math.max(2, Math.floor(CW > 8 ? CW - 8 : CW * 0.8));

let sel = groupNames[0] ?? '';
let bestScore = -1;
const latestDate = dates.length ? dates[dates.length - 1] : '';
const earliestDate = dates.length ? dates[0] : '';

groupNames.forEach(g => {
  const byDate = data.get(g);
  const cur = byDate?.get(latestDate);
  const score = (cur?.break ?? 0) * 2 + (cur?.open ?? 0);
  if (score > bestScore) {
    bestScore = score;
    sel = g;
  }
});

const btns = new Map();
const chartEl = document.getElementById('chart');

groupNames.forEach(g => {
  const byDate = data.get(g);
  const now = (latestDate && byDate?.get(latestDate)?.open) ?? 0;
  const was = (earliestDate && byDate?.get(earliestDate)?.open) ?? now;
  const displayName = g || model.unit;

  const svg = s('svg', {
    viewBox: `0 0 ${W} ${H + 4}`,
    width: '100%',
    role: 'img',
    'aria-label': `${displayName}: ${int(now)}`
  }, [s('line', { class: 'axl', x1: 0, x2: W, y1: H, y2: H })]);

  dates.forEach((d, i) => {
    const w = byDate?.get(d) ?? { leftOnly: 0, break: 0, rightOnly: 0 };
    let y = H;
    [['f-l', w.leftOnly], ['f-brk', w.break], ['f-r', w.rightOnly]].forEach(([cls, v]) => {
      const hh = (v / max) * (H - 8);
      if (hh > 0) {
        svg.append(s('rect', {
          class: cls,
          x: Math.round(i * CW + (CW - bw) / 2),
          y: Math.round(y - hh + 1),
          width: Math.round(bw),
          height: Math.max(1, Math.round(hh - 2)),
          rx: 2
        }));
      }
      y -= hh;
    });
  });

  const sub = text('openSummary').replace('{now}', int(now)).replace('{was}', int(was));
  const btn = h('button', {
    class: 'pool',
    on: { click: () => { sel = g; show(); } }
  }, [
    h('b', { text: displayName }),
    h('span', { text: sub }),
    svg
  ]);

  btns.set(g, btn);
  chartEl.append(btn);
});

function show() {
  btns.forEach((b, g) => b.setAttribute('aria-current', String(g === sel)));
  if (!sel && groupNames.length === 0) return;

  const byDate = data.get(sel);
  const now = (latestDate && byDate?.get(latestDate)?.open) ?? 0;
  const gInfo = RD.groupInfo(sel);
  const total = Number.isFinite(+gInfo?.attrs?.total) && gInfo.attrs.total !== '' ? +gInfo.attrs.total : null;
  const displayName = sel || model.unit;

  const dtText = text(total === null ? 'detailTitle' : 'detailTitleTotal')
    .replace('{group}', displayName).replace('{open}', int(now)).replace('{total}', total === null ? '' : int(total));

  document.getElementById('dt').textContent = dtText;
  document.getElementById('ds').textContent = gInfo?.note || text('noNote');

  const recentCount = opt('recentRuns') || 6;
  const sliceStart = Math.max(0, dates.length - recentCount);
  const tableDates = dates.slice(sliceStart);

  const thead = h('thead', {}, h('tr', {}, [
    h('th', { text: text('dateCol') }),
    h('th', { class: 'n', text: label('leftOnly') }),
    h('th', { class: 'n', text: label('break') }),
    h('th', { class: 'n', text: label('rightOnly') }),
    h('th', { class: 'n', text: text('totalCol') }),
    h('th', { class: 'n', text: text('changeCol') })
  ]));

  const rows = tableDates.map((d, idx) => {
    const globalIdx = sliceStart + idx;
    const w = byDate?.get(d) ?? { leftOnly: 0, break: 0, rightOnly: 0, open: 0 };
    const t = w.open;
    const prevDate = globalIdx > 0 ? dates[globalIdx - 1] : null;
    const prevOpen = prevDate ? (byDate?.get(prevDate)?.open ?? 0) : null;
    let chg = '';
    if (prevOpen !== null) {
      const diff = t - prevOpen;
      if (diff !== 0) chg = (diff > 0 ? '+' : '−') + int(Math.abs(diff));
    }
    return h('tr', {}, [
      h('td', { text: fmtDate(d) }),
      h('td', { class: 'n', text: int(w.leftOnly) }),
      h('td', { class: 'n', text: int(w.break) }),
      h('td', { class: 'n', text: int(w.rightOnly) }),
      h('td', { class: 'n', text: int(t) }),
      h('td', { class: 'n', text: chg })
    ]);
  });

  document.getElementById('detail').replaceChildren(
    h('table', { class: 'tbl', style: 'max-width:760px' }, [thead, h('tbody', {}, rows)])
  );
}

if (groupNames.length) show();
