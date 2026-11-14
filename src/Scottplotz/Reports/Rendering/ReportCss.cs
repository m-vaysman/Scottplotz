namespace Scottplotz.Reports.Rendering;

public static class ReportCss
{
    public const string Text = """
:root{color-scheme:light;--plane:#f9f9f7;--surface:#fcfcfb;--ink:#0b0b0b;--ink2:#52514e;--muted:#898781;--grid:#e1e0d9;--axis:#c3c2b7;--ring:rgba(11,11,11,.10);--head:#1f2937;--on-head:#fff;--sub:#e5e7eb;--band:#eef2f7;--brk-bg:#fde2e1;--brk-ink:#9b1c1c;--tol-bg:#fef3c7;--tol-ink:#92400e;--att-bg:#f3f4f6;--att-ink:#6b7280;--ok-bg:#dcfce7;--ok-ink:#166534;--heat:#6d28d9;--link:#0a7a0a;--bar:#2a78d6;--two:#eb6834}
@media (prefers-color-scheme:dark){:root:not([data-theme=light]){color-scheme:dark;--plane:#0d0d0d;--surface:#1a1a19;--ink:#f0efec;--ink2:#c3c2b7;--muted:#898781;--grid:#2c2c2a;--axis:#383835;--ring:rgba(255,255,255,.10);--head:#2f3a4a;--on-head:#f0efec;--sub:#2a2d33;--band:#20242b;--brk-bg:#4a1d1c;--brk-ink:#fca5a5;--tol-bg:#453209;--tol-ink:#fcd34d;--att-bg:#262626;--att-ink:#a3a3a3;--ok-bg:#12351f;--ok-ink:#86efac;--heat:#8b5cf6;--link:#4ade80;--bar:#3987e5;--two:#d95926}}
:root[data-theme=dark]{color-scheme:dark;--plane:#0d0d0d;--surface:#1a1a19;--ink:#f0efec;--ink2:#c3c2b7;--muted:#898781;--grid:#2c2c2a;--axis:#383835;--ring:rgba(255,255,255,.10);--head:#2f3a4a;--on-head:#f0efec;--sub:#2a2d33;--band:#20242b;--brk-bg:#4a1d1c;--brk-ink:#fca5a5;--tol-bg:#453209;--tol-ink:#fcd34d;--att-bg:#262626;--att-ink:#a3a3a3;--ok-bg:#12351f;--ok-ink:#86efac;--heat:#8b5cf6;--link:#4ade80;--bar:#3987e5;--two:#d95926}
*{box-sizing:border-box}
body{margin:0;background:var(--plane);color:var(--ink);font:14px/1.45 Bahnschrift,"DIN Alternate","Segoe UI",system-ui,sans-serif}
.label{display:flex;gap:14px;align-items:center;padding:9px 28px;border-bottom:1px solid var(--grid);font-size:13px;color:var(--ink2)}
.label strong{color:var(--ink);font-weight:600}
.label .sep{width:1px;height:14px;background:var(--axis)}
.label .num{margin-left:auto;color:var(--muted)}
main{max-width:1900px;margin:0 auto;padding:26px 28px 60px}
h1{font-size:30px;line-height:1.15;font-weight:600;margin:0 0 6px}
.lede{color:var(--ink2);max-width:90ch;margin:0 0 12px}
.meta{display:flex;flex-wrap:wrap;gap:6px 26px;margin:0 0 6px;font-size:13px;color:var(--ink2)}
.meta b{color:var(--ink);font-weight:600}
.run{color:var(--muted);font-size:13px;margin:0 0 4px}
h2{font-size:16px;font-weight:600;margin:0 0 8px}
h3{font-size:14px;font-weight:600;margin:14px 0 6px}
.sub{color:var(--ink2);font-size:13px;margin:-4px 0 10px}
.panel{background:var(--surface);border:1px solid var(--ring);border-radius:6px;padding:16px 18px;margin-top:18px}
.panel .panel{border:0;padding:0;margin-top:12px;background:none}
.scroll{overflow-x:auto}
table.tbl{border-collapse:collapse;width:100%;font-size:13px}
.tbl th{text-align:left;font-weight:600;background:var(--head);color:var(--on-head);padding:7px 9px;border:1px solid var(--head);vertical-align:middle;white-space:nowrap}
.tbl th.w{white-space:normal}
.tbl td{padding:5px 9px;border:1px solid var(--grid);vertical-align:top;white-space:nowrap}
.tbl td.w{white-space:normal;min-width:240px}
.s{text-decoration:line-through}
.tbl .n{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
.tbl .c{text-align:center}.tbl .l{text-align:left}.tbl .r{text-align:right}
.tbl tr.band td{background:var(--band);font-weight:600}
.tbl tr.subh td{background:var(--sub);font-weight:600;text-align:center}
.tbl tr.total td{background:var(--sub);font-weight:600}
.b{font-weight:600}.i{font-style:italic}
.t-break{background:var(--brk-bg);color:var(--brk-ink);font-weight:600}
.t-tolerance{background:var(--tol-bg);color:var(--tol-ink);font-weight:600}
.t-attributed{background:var(--att-bg);color:var(--att-ink);font-style:italic}
.t-ok{color:var(--ok-ink);font-weight:600}
.tbl td.t-ok.fill,.tile .t-ok.fill{background:var(--ok-bg)}
.t-fail{background:var(--brk-bg);color:var(--brk-ink);font-weight:600}
.t-muted{color:var(--muted)}
.t-shade{background:var(--att-bg)}
.t-alert{color:var(--brk-ink);font-weight:600}
.legend{display:flex;flex-wrap:wrap;gap:8px 22px;margin:14px 0 0;font-size:13px;color:var(--ink2)}
.legend b{padding:1px 7px;border-radius:3px}
.legend .sw{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:6px;background:var(--bar)}
.legend .sw.two{background:var(--two)}
.panel>.legend{margin:0 0 10px}
.t-link{color:var(--link)}
.heat{background:color-mix(in oklab,var(--heat) calc(var(--h)*1%),var(--surface))}
.heat.orange{background:color-mix(in oklab,#c2410c calc(var(--h)*1%),var(--surface))}
.heat.blue{background:color-mix(in oklab,var(--bar) calc(var(--h)*1%),var(--surface))}
.heat.hot{color:#fff}
.barcell{position:relative}
.barcell i{position:absolute;left:0;top:3px;bottom:3px;background:color-mix(in oklab,var(--bar) 35%,var(--surface));border-radius:0 3px 3px 0}
.barcell span{position:relative}
.barcell.amber i{background:color-mix(in oklab,#d97706 40%,var(--surface))}
.note{color:var(--muted);font-style:italic;font-size:13px;margin:10px 0 0;max-width:120ch}
.empty{color:var(--muted);margin:0}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:12px;margin-top:18px}
.tile{background:var(--surface);border:1px solid var(--ring);border-radius:6px;padding:12px 14px}
.tile .k{font-size:13px;color:var(--ink2)}
.tile .v{font-size:26px;font-weight:600;line-height:1.2;font-variant-numeric:tabular-nums;display:inline-block;padding:0 4px;margin-left:-4px;border-radius:4px}
.tile .s{font-size:12px;color:var(--muted)}
.bars{display:grid;grid-template-columns:minmax(160px,max-content) 1fr max-content;gap:6px 12px;align-items:center;font-size:13px}
.bars .trk{height:18px;position:relative}
.bars .trk i{position:absolute;top:0;bottom:0;border-radius:3px;background:var(--bar)}
.bars .trk i.t-break{background:var(--brk-ink)}.bars .trk i.t-tolerance{background:var(--tol-ink)}.bars .trk i.t-ok{background:var(--ok-ink)}.bars .trk i.t-muted,.bars .trk i.t-attributed{background:var(--axis)}
.bars .v{font-variant-numeric:tabular-nums;text-align:right}
.bars .trk.two{height:30px}
.bars .trk.two i{bottom:auto;height:13px}
.bars .trk.two i.two{top:16px;background:var(--two)}
.bars .nt{grid-column:1/-1;color:var(--muted);font-size:12px;margin-top:-4px}
.axis{color:var(--muted);font-size:12px;margin:8px 0 0}
details.data{margin-top:14px;border:1px solid var(--ring);border-radius:6px;background:var(--surface)}
details.data summary{cursor:pointer;padding:10px 14px;font-weight:600}
details.data summary span{color:var(--muted);font-weight:400;margin-left:8px}
details.data .in{padding:0 14px 14px}
:focus-visible{outline:2px solid var(--bar);outline-offset:2px}
@media print{.label{display:none}details.data{display:none}.panel{break-inside:avoid}}
""";
}
