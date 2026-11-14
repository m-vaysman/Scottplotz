using System.Globalization;
using System.Net;
using Scottplotz.Recon;
using Scottplotz.Reports.Model;

namespace Scottplotz.Reports.Rendering;

public sealed record ReportPage(string Html, string ContentSecurityPolicy);

public static class ReportHtml
{
    public const string Policy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'";

    public static ReportPage Render(ReportDocument document, ReconTheme theme) =>
        new(Page(document, theme).Replace("\r\n", "\n"), Policy);

    private static string Page(ReportDocument document, ReconTheme theme)
    {
        var info = document.Info;
        var asOf = document.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var themeAttribute = theme == ReconTheme.Auto ? "" : $" data-theme=\"{ReconWording.ThemeToken(theme)}\"";
        return string.Concat(
            "<!doctype html>\n",
            $"<html lang=\"en\"{themeAttribute}>\n",
            "<head>\n",
            "<meta charset=\"utf-8\">\n",
            $"<meta http-equiv=\"Content-Security-Policy\" content=\"{Policy}\">\n",
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n",
            $"<title>{E(info.Id)} {E(document.Title)}</title>\n",
            $"<style>\n{ReportCss.Text}\n</style>\n",
            "</head>\n",
            "<body>\n",
            $"<div class=\"label\"><strong>{E(info.Id)} · {E(document.Title)}</strong><span class=\"sep\"></span><span>{E(info.Office)}</span><span class=\"num\">As of {asOf}</span></div>\n",
            "<main>\n",
            $"<h1>{E(info.Id)} · {E(document.Title)}</h1>\n",
            $"<p class=\"lede\">{E(info.About)}</p>\n",
            $"<p class=\"meta\"><span><b>Users</b> {E(info.Users)}</span><span><b>Question</b> {E(info.Question)}</span><span><b>Cadence</b> {E(info.Cadence)}</span></p>\n",
            $"<p class=\"run\">As of {asOf} · {E(document.RunLine)}</p>\n",
            string.Concat(document.Blocks.Select(block => Block(block, nested: false))),
            "</main>\n",
            "</body>\n",
            "</html>\n");
    }

    private static string Block(ReportBlock block, bool nested) =>
        block switch
        {
            TableBlock table => Panel(nested, (table.Title is { Length: > 0 } title ? Heading(nested, title) : "") + Table(table)),
            TilesBlock tiles => $"<div class=\"tiles\">{string.Concat(tiles.Tiles.Select(Tile))}</div>\n",
            BarsBlock bars => Panel(nested, Heading(nested, bars.Title) + Bars(bars)),
            NoteBlock note => $"<p class=\"note\">{E(note.Text)}</p>\n",
            LegendBlock legend => $"<p class=\"legend\">{string.Concat(legend.Items.Select(Legend))}</p>\n",
            SectionBlock section => Panel(
                nested,
                Heading(nested, section.Title)
                + (section.Sub is { Length: > 0 } sub ? $"<p class=\"sub\">{E(sub)}</p>\n" : "")
                + string.Concat(section.Blocks.Select(inner => Block(inner, nested: true)))),
            DataBlock data =>
                $"<details class=\"data\"><summary>Data: {E(data.Name)}<span>{data.Table.Rows.Count.ToString("#,##0", CultureInfo.InvariantCulture)} rows</span></summary><div class=\"in\">"
                + (data.About is { Length: > 0 } about ? $"<p class=\"sub\">{E(about)}</p>\n" : "")
                + Table(data.Table)
                + "</div></details>\n",
            _ => ""
        };

    private static string Panel(bool nested, string inner) => $"<section class=\"panel\">\n{inner}</section>\n";

    private static string Heading(bool nested, string title) => nested ? $"<h3>{E(title)}</h3>\n" : $"<h2>{E(title)}</h2>\n";

    private static string Table(TableBlock table) =>
        table.Rows.Count == 0 && table.Empty is { Length: > 0 } empty
            ? $"<p class=\"empty\">{E(empty)}</p>\n"
            : string.Concat(
                "<div class=\"scroll\"><table class=\"tbl\">\n<thead>",
                table.Groups is { Count: > 0 } groups
                    ? $"<tr>{string.Concat(groups.Select(group => $"<th class=\"c\" colspan=\"{group.Span.ToString(CultureInfo.InvariantCulture)}\">{E(group.Title)}</th>"))}</tr>\n"
                    : "",
                "<tr>",
                string.Concat(table.Columns.Select(column => $"<th{Class(Alignment(column.Align), column.Wrap ? "w" : "")}>{E(column.Title)}</th>")),
                "</tr></thead>\n<tbody>\n",
                string.Concat(table.Rows.Select(row => Row(row, table.Columns))),
                "</tbody>\n</table></div>\n",
                table.Note is { Length: > 0 } note ? $"<p class=\"note\">{E(note)}</p>\n" : "");

    private static string Row(ReportRow row, IReadOnlyList<ReportColumn> columns)
    {
        var kind = row.Kind switch { RowKind.Band => "band", RowKind.SubHeader => "subh", RowKind.Total => "total", _ => "" };
        var starts = row.Cells.Aggregate((Index: 0, List: Enumerable.Empty<int>()), (state, cell) => (state.Index + Math.Max(1, cell.Span), state.List.Append(state.Index))).List;
        return $"<tr{Class(kind)}>{string.Concat(row.Cells.Zip(starts, (cell, start) => Cell(cell, start < columns.Count ? columns[start].Align : CellAlign.Auto)))}</tr>\n";
    }

    private static string Cell(ReportCell cell, CellAlign columnAlign)
    {
        var text = E(CellText.Of(cell));
        var align = cell.Align != CellAlign.Auto ? Alignment(cell.Align)
            : columnAlign != CellAlign.Auto ? Alignment(columnAlign)
            : CellText.IsNumeric(cell) && cell.Format != NumberFormat.Date ? "n" : "";
        var heat = cell.Heat is double level ? Math.Clamp(level, 0, 1) : (double?)null;
        var classes = Class(
            align,
            ToneClass(cell.Tone),
            cell.Tone == Tone.Ok && cell.Value is CellValue.Text { Value.Length: > 1 } ? "fill" : "",
            cell.Bold ? "b" : "",
            cell.Italic ? "i" : "",
            cell.Strike ? "strike" : "",
            cell.Wrap || CellText.Of(cell).Length > 34 ? "w" : "",
            cell.Bar is not null && cell.HeatColour is HeatColour.Amber or HeatColour.Orange ? "amber" : "",
            heat is not null ? "heat" : "",
            heat is not null && cell.HeatColour == HeatColour.Orange ? "orange" : "",
            heat is not null && cell.HeatColour == HeatColour.Blue ? "blue" : "",
            heat > 0.55 ? "hot" : "",
            cell.Bar is not null ? "barcell" : "");
        var styles = string.Concat(
            heat is double shade ? $"--h:{Math.Round(shade * 100).ToString(CultureInfo.InvariantCulture)};" : "",
            cell.Indent > 0 ? $"padding-left:{(9 + cell.Indent * 16).ToString(CultureInfo.InvariantCulture)}px;" : "");
        var span = cell.Span > 1 ? $" colspan=\"{cell.Span.ToString(CultureInfo.InvariantCulture)}\"" : "";
        var style = styles.Length > 0 ? $" style=\"{styles}\"" : "";
        var inner = cell.Bar is double bar
            ? $"<i style=\"width:{Math.Round(Math.Clamp(bar, 0, 1) * 100).ToString(CultureInfo.InvariantCulture)}%\"></i><span>{text}</span>"
            : text;
        return $"<td{classes}{span}{style}>{inner}</td>";
    }

    private static string Tile(ReportTile tile) =>
        $"<div class=\"tile\"><div class=\"k\">{E(tile.Label)}</div><div class=\"v {ToneClass(tile.Value.Tone)}{(tile.Value.Tone == Tone.Ok ? " fill" : "")}\">{E(CellText.Of(tile.Value))}</div>"
        + (tile.Sub is { Length: > 0 } sub ? $"<div class=\"s\">{E(sub)}</div>" : "")
        + "</div>";

    private static string Bars(BarsBlock bars)
    {
        var largest = bars.Bars.SelectMany(bar => new[] { Math.Abs(bar.Value), Math.Abs(bar.Second ?? 0m) }).DefaultIfEmpty(0).Max();
        string Width(decimal value) => (largest == 0 ? 0 : Math.Round(Math.Abs(value) / largest * 100, 1)).ToString(CultureInfo.InvariantCulture);
        var key = bars.FirstSeries is { Length: > 0 } first && bars.SecondSeries is { Length: > 0 } second
            ? $"<p class=\"legend\"><span><i class=\"sw\"></i>{E(first)}</span><span><i class=\"sw two\"></i>{E(second)}</span></p>\n"
            : "";
        return string.Concat(
            key,
            "<div class=\"bars\">\n",
            string.Concat(bars.Bars.Select(bar =>
                (bar.Second is { } other
                    ? $"<div>{E(bar.Label)}</div><div class=\"trk two\"><i style=\"left:0;width:{Width(bar.Value)}%\"></i><i class=\"two\" style=\"left:0;width:{Width(other)}%\"></i></div><div class=\"v\">{E(CellText.Number(bar.Value, bars.Format))}<br>{E(CellText.Number(other, bars.Format))}</div>"
                    : $"<div>{E(bar.Label)}</div><div class=\"trk\"><i{Class(ToneClass(bar.Tone))} style=\"left:0;width:{Width(bar.Value)}%\"></i></div><div class=\"v\">{E(CellText.Number(bar.Value, bars.Format))}</div>")
                + (bar.Note is { Length: > 0 } note ? $"<div class=\"nt\">{E(note)}</div>" : "")
                + "\n")),
            "</div>\n",
            bars.AxisTitle is { Length: > 0 } axis ? $"<p class=\"axis\">{E(axis)}</p>\n" : "");
    }

    private static string Legend(LegendItem item) =>
        $"<span><b{Class(ToneClass(item.Tone), "fill")}>{E(item.Label)}</b>{(item.Meaning is { Length: > 0 } meaning ? " " + E(meaning) : "")}</span>";

    private static string Alignment(CellAlign align) =>
        align switch { CellAlign.Left => "l", CellAlign.Center => "c", CellAlign.Right => "r", _ => "" };

    private static string ToneClass(Tone tone) =>
        tone switch
        {
            Tone.Break => "t-break",
            Tone.Tolerance => "t-tolerance",
            Tone.Attributed => "t-attributed",
            Tone.Ok => "t-ok",
            Tone.Fail => "t-fail",
            Tone.Muted => "t-muted",
            Tone.Link => "t-link",
            Tone.Shade => "t-shade",
            Tone.Alert => "t-alert",
            _ => ""
        };

    private static string Class(params string[] names)
    {
        var joined = string.Join(" ", names.Where(name => name.Length > 0));
        return joined.Length > 0 ? $" class=\"{joined}\"" : "";
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);
}
