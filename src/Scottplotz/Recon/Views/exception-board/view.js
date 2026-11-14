const { h, model, label, text, opt, fmt, fmtSigned, val, int, clip } = RD;
const M = model.measure;
RD.tieOut(document.getElementById('tie'));

const maxCards = opt('maxCards') || 30;

const COLS = [
  { key: 'exact', cls: 'ex', title: text('colExact') || label('exact'), note: text('colExactNote') },
  { key: 'tolerance', cls: 'tol', title: text('colTolerance') || label('tolerance'), note: text('colToleranceNote') },
  { key: 'break', cls: 'brk', title: text('colBreak') || label('break'), note: text('colBreakNote') },
  { key: 'leftOnly', cls: 'lo', title: text('colLeftOnly') || label('leftOnly'), note: text('colLeftOnlyNote') },
  { key: 'rightOnly', cls: 'ro', title: text('colRightOnly') || label('rightOnly'), note: text('colRightOnlyNote') },
  { key: 'duplicate', cls: 'dup', title: text('colDuplicate') || label('duplicate'), note: text('colDuplicateNote') }
];

const isDup = r => Boolean(r.duplicate);
const colMap = new Map();
COLS.forEach(c => colMap.set(c.key, []));

model.records.forEach(r => {
  if (isDup(r)) {
    colMap.get('duplicate').push(r);
  } else if (colMap.has(r.status)) {
    colMap.get(r.status).push(r);
  } else {
    colMap.get('break').push(r);
  }
});

const getAge = r => (r.attrs && typeof r.attrs.age === 'number' ? r.attrs.age : -1);

let sel = model.records.find(r => r.status === 'break' && (r.count || 1) === 1) ||
          model.records.find(r => (r.count || 1) === 1) ||
          model.records[0];

const cards = new Map();
const board = document.getElementById('board');

COLS.forEach(c => {
  const items = colMap.get(c.key);
  items.sort((a, b) => getAge(b) - getAge(a));

  const totalCount = items.reduce((sum, r) => sum + (r.count || 1), 0);
  let stake = 0;
  let hasStake = false;
  items.forEach(r => {
    let v = null;
    if (c.key === 'break' && r.delta !== null) {
      v = Math.abs(r.delta);
    } else {
      v = val(r, 'left') ?? val(r, 'right');
    }
    if (v !== null && v !== undefined && !isNaN(v)) {
      stake += v * (r.count || 1);
      hasStake = true;
    }
  });

  const unitStr = totalCount === 1 ? model.unitOne : model.unit;
  const metaKids = [h('b', { text: int(totalCount) }), ' ' + unitStr];
  if (hasStake && M) {
    metaKids.push(', ', h('b', { text: fmt(M, stake) }), (RD.measure ? ' ' + RD.measure.label.toLowerCase() : '') + (c.note ? '. ' + c.note : ''));
  } else if (c.note) {
    metaKids.push('. ' + c.note);
  }

  const colEl = h('div', { class: 'col' }, [
    h('h3', { text: c.title }),
    h('div', { class: 'meta' }, metaKids)
  ]);

  const visible = items.slice(0, maxCards);
  visible.forEach(r => {
    if (c.key === 'exact' && r.count > 1) {
      colEl.append(h('div', { class: 'card quiet' }, [
        h('b', { text: `${int(r.count)} ${model.unit}` }),
        h('div', { class: 'row' }, [
          h('span', { text: r.reason || r.name || text('exactSummary') })
        ])
      ]));
      return;
    }

    const mVal = val(r, 'left') ?? val(r, 'right');
    const age = r.attrs && r.attrs.age != null ? `${r.attrs.age} ${text('ageUnit')}` : '';
    const diffText = (c.key === 'break' && r.delta !== null && r.delta !== 0) ? fmtSigned(M, r.delta) : '';

    const btn = h('button', {
      class: 'card ' + c.cls + (r === sel ? ' on' : ''),
      on: { click: () => { sel = r; show(); } }
    }, [
      h('b', { text: clip(r.name || r.key, 28) }),
      h('div', { class: 'row' }, [
        h('span', { text: clip(r.key, 18) }),
        mVal !== null && M ? h('span', { text: fmt(M, mVal) }) : null
      ]),
      (age || diffText) ? h('div', { class: 'row' }, [
        h('span', { text: age }),
        diffText ? h('span', { text: diffText }) : null
      ]) : null
    ]);
    cards.set(r, btn);
    colEl.append(btn);
  });

  board.append(colEl);
});

function show() {
  if (!sel) return;
  cards.forEach((btn, r) => btn.classList.toggle('on', r === sel));
  const r = sel;
  const name = r.name || r.key;
  const keyPart = r.name && r.name !== r.key ? `, ${r.key}` : '';
  document.getElementById('dt').textContent = `${name}${keyPart}`;
  document.getElementById('ds').textContent = r.reason || RD.statusWord(r);

  const cell = (side, f, differs) => {
    const v = val(r, side, f.key);
    return h('td', {
      class: (f.type === 'number' ? 'n' : '') +
             (differs ? (side === 'left' ? ' dl' : ' dr') : '') +
             (v === null ? ' none' : ''),
      text: fmt(f.key, v)
    });
  };
  const row = f => {
    const a = val(r, 'left', f.key);
    const b = val(r, 'right', f.key);
    const differs = a !== null && b !== null && a !== b;
    return h('tr', {}, [
      h('td', { text: f.label }),
      cell('left', f, differs),
      cell('right', f, differs)
    ]);
  };

  const children = [
    h('table', { class: 'tbl' }, [
      h('thead', {}, h('tr', {}, [
        h('th', { text: text('field') }),
        h('th', { class: 'n', text: model.left.short }),
        h('th', { class: 'n', text: model.right.short })
      ])),
      h('tbody', {}, model.fields.map(row))
    ])
  ];

  if (r.attrs && r.attrs.action) {
    children.push(h('p', { class: 'act' }, [
      h('b', { text: text('nextStep') + ' ' }),
      r.attrs.action
    ]));
  }

  document.getElementById('detail').replaceChildren(...children);
}

if (sel) show();
