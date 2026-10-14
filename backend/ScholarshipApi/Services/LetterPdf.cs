using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using IElement = AngleSharp.Dom.IElement;

namespace ScholarshipApi.Services;

/// <summary>
/// Renders merged award letters (sanitized HTML from the Tiptap editor) as a US Letter PDF, one letter per page.
/// Handles the tags RichTextSanitizer allows: p, br, strong/b, em/i, u, s, h2, h3, ul, ol, li, blockquote, a, hr.
/// QuestPDF is used under its Community license (free for organizations with under $1M annual revenue).
/// </summary>
public static class LetterPdf
{
    private const string Ink = "#1B2559";

    static LetterPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(IEnumerable<string> letterHtmlPages)
    {
        var parser = new HtmlParser();
        var pages = letterHtmlPages.Select(html => parser.ParseDocument($"<body>{html}</body>").Body!).ToList();

        return QuestPDF.Fluent.Document.Create(document =>
        {
            foreach (var body in pages)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(1, Unit.Inch);
                    page.DefaultTextStyle(style => style.FontSize(11).LineHeight(1.35f).FontColor(Ink));
                    page.Content().Column(column =>
                    {
                        column.Spacing(8);
                        RenderBlocks(column, body.ChildNodes);
                    });
                });
            }
        }).GeneratePdf();
    }

    private record Inline(bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false, string? Href = null);

    private static void RenderBlocks(ColumnDescriptor column, INodeList nodes)
    {
        // Loose inline content between blocks (shouldn't happen with Tiptap HTML) is gathered into a paragraph
        var loose = new List<INode>();
        void FlushLoose()
        {
            if (loose.Count == 0 || loose.All(n => string.IsNullOrWhiteSpace(n.TextContent))) { loose.Clear(); return; }
            var items = loose.ToList();
            column.Item().Text(text => RenderInlines(text, items, new Inline()));
            loose.Clear();
        }

        foreach (var node in nodes)
        {
            if (node is not IElement element) { loose.Add(node); continue; }
            switch (element.LocalName)
            {
                case "p":
                    FlushLoose();
                    column.Item().Text(text => RenderInlines(text, element.ChildNodes, new Inline()));
                    break;
                case "h2":
                case "h3":
                    FlushLoose();
                    var size = element.LocalName == "h2" ? 15 : 13;
                    column.Item().PaddingTop(4).Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(size).Bold());
                        RenderInlines(text, element.ChildNodes, new Inline());
                    });
                    break;
                case "ul":
                case "ol":
                    FlushLoose();
                    RenderList(column, element, ordered: element.LocalName == "ol");
                    break;
                case "blockquote":
                    FlushLoose();
                    column.Item().BorderLeft(2).BorderColor(Colors.Grey.Lighten1).PaddingLeft(10)
                        .Column(inner => { inner.Spacing(6); RenderBlocks(inner, element.ChildNodes); });
                    break;
                case "hr":
                    FlushLoose();
                    column.Item().PaddingVertical(4).LineHorizontal(0.75f).LineColor(Colors.Grey.Lighten1);
                    break;
                default:
                    loose.Add(element);
                    break;
            }
        }
        FlushLoose();
    }

    private static void RenderList(ColumnDescriptor column, IElement list, bool ordered)
    {
        var number = 1;
        foreach (var item in list.Children.Where(c => c.LocalName == "li"))
        {
            var marker = ordered ? $"{number++}." : "•";
            column.Item().Row(row =>
            {
                row.ConstantItem(18).Text(marker);
                row.RelativeItem().Column(inner =>
                {
                    inner.Spacing(4);
                    // List items hold paragraphs (and maybe nested lists) in Tiptap's HTML
                    if (item.Children.Any(c => c.LocalName is "p" or "ul" or "ol")) RenderBlocks(inner, item.ChildNodes);
                    else inner.Item().Text(text => RenderInlines(text, item.ChildNodes, new Inline()));
                });
            });
        }
    }

    private static void RenderInlines(TextDescriptor text, IEnumerable<INode> nodes, Inline style)
    {
        foreach (var node in nodes)
        {
            if (node is IText textNode)
            {
                if (textNode.Data.Length == 0) continue;
                var span = style.Href is not null ? text.Hyperlink(textNode.Data, style.Href) : text.Span(textNode.Data);
                if (style.Bold) span.Bold();
                if (style.Italic) span.Italic();
                if (style.Underline || style.Href is not null) span.Underline();
                if (style.Strike) span.Strikethrough();
                continue;
            }
            if (node is not IElement element) continue;
            switch (element.LocalName)
            {
                case "br": text.Span("\n"); break;
                case "strong" or "b": RenderInlines(text, element.ChildNodes, style with { Bold = true }); break;
                case "em" or "i": RenderInlines(text, element.ChildNodes, style with { Italic = true }); break;
                case "u": RenderInlines(text, element.ChildNodes, style with { Underline = true }); break;
                case "s": RenderInlines(text, element.ChildNodes, style with { Strike = true }); break;
                case "a": RenderInlines(text, element.ChildNodes, style with { Href = element.GetAttribute("href") }); break;
                default: RenderInlines(text, element.ChildNodes, style); break;
            }
        }
    }
}
