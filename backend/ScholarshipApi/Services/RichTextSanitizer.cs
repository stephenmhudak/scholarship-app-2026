using AngleSharp.Dom;
using Ganss.Xss;

namespace ScholarshipApi.Services;

/// <summary>
/// Sanitizes HTML answers to <c>rich_text</c> questions before they are stored. The allow-list
/// mirrors what the frontend Tiptap editor produces (and frontend/src/utils/richText.js), so
/// legitimate formatting round-trips unchanged while scripts, event handlers, styles and
/// unsafe URLs are stripped.
/// </summary>
public static class RichTextSanitizer
{
    private static readonly HashSet<string> DropWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "template", "iframe", "object", "embed", "svg", "math",
        "textarea", "select", "head", "title",
    };

    private static readonly HtmlSanitizer Sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "strong", "b", "em", "i", "u", "s", "h2", "h3",
                "ul", "ol", "li", "blockquote", "a", "hr",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "target", "rel" },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
            AllowedCssProperties = new HashSet<string>(),
            AllowedAtRules = new HashSet<AngleSharp.Css.Dom.CssRuleType>(),
        })
        {
            // Unwrap disallowed tags (e.g. a pasted <div> or <h1>) instead of dropping their text.
            KeepChildNodes = true,
        };

        // ...except for elements whose content is code or embedded media rather than prose.
        sanitizer.RemovingTag += (_, e) =>
        {
            if (DropWithContent.Contains(e.Tag.LocalName)) e.Tag.InnerHtml = string.Empty;
        };

        // Links always open in a new tab without giving the target page access to ours.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "a" } link && link.HasAttribute("href"))
            {
                link.SetAttribute("target", "_blank");
                link.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };

        return sanitizer;
    }

    /// <summary>Returns safe HTML, or null when nothing visible remains.</summary>
    public static string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var clean = Sanitizer.Sanitize(html).Trim();
        return clean.Length == 0 ? null : clean;
    }
}
