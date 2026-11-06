using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Spectro.Domain;

namespace Spectro.Presentation;

public static partial class ReaderHtmlBuilder
{
    public static Task<string> BuildAsync(Story story, ReaderSettings settings,
        string? imageDataUri = null, string? publication = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => Build(story, settings, imageDataUri, publication), cancellationToken);

    public static string Build(Story story, ReaderSettings settings, string? imageDataUri = null, string? publication = null)
    {
        var content = Sanitize(story.Content);
        var cover = imageDataUri is not null && SafeImageDataUriRegex().IsMatch(imageDataUri)
            ? $"<figure class=\"cover\"><img src=\"{imageDataUri}\" alt=\"\"></figure>"
            : string.Empty;
        var colorScheme = settings.Theme switch
        {
            AppTheme.Light => "light",
            AppTheme.Dark => "dark",
            _ => "light dark"
        };
        var foreground = settings.Theme == AppTheme.Dark ? "#edede7" : "#242725";
        var background = settings.Theme == AppTheme.Dark ? "#1b1e1c" : "#fffefb";
        var muted = settings.Theme == AppTheme.Dark ? "#adb2ab" : "#646863";
        var accent = settings.Theme == AppTheme.Dark ? "#efa77f" : "#a44b25";
        var rule = settings.Theme == AppTheme.Dark ? "#3b403a" : "#e4e3de";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
              <title>{{WebUtility.HtmlEncode(story.Title)}}</title>
              <style>
                :root { color-scheme: {{colorScheme}}; }
                * { box-sizing: border-box; }
                html { background: {{background}}; }
                body { color: {{foreground}}; background: {{background}}; font: {{settings.TextSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}}px/1.8 Georgia, 'Times New Roman', serif; margin: 0 auto; max-width: {{settings.ReadingWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)}}px; padding: 44px 42px 88px; overflow-wrap: anywhere; }
                .publication { font: 600 11px/1.6 system-ui, sans-serif; color: {{accent}}; letter-spacing: .13em; text-transform: uppercase; margin-bottom: 16px; }
                h1 { font-size: clamp(28px, 3.5vw, 40px); font-weight: 400; letter-spacing: -.035em; line-height: 1.14; margin: 0 0 20px; text-wrap: balance; }
                h2 { font-size: 1.25em; line-height: 1.35; margin: 2em 0 .7em; font-weight: 400; }
                p { margin: 0 0 1.3em; }
                a { color: {{accent}}; text-underline-offset: 3px; }
                .byline { font: 12px/1.8 system-ui, sans-serif; color: {{muted}}; padding-bottom: 24px; margin-bottom: 28px; border-bottom: 1px solid {{rule}}; }
                .cover { margin: 0 0 30px; }
                .cover img { display: block; width: 100%; max-height: 280px; object-fit: cover; border-radius: 5px; }
                img, video { max-width: 100%; height: auto; }
                img:not([src]) { display: none; }
                blockquote { margin: 1.8em 0; border-left: 2px solid {{accent}}; padding: .2em 0 .2em 22px; color: {{muted}}; font-style: italic; }
                pre { overflow-x: auto; padding: 16px; border: 1px solid {{rule}}; font: .8em/1.6 Consolas, monospace; }
                table { display: block; max-width: 100%; overflow-x: auto; border-collapse: collapse; }
                td, th { border: 1px solid {{rule}}; padding: 8px; }
                hr { border: 0; border-top: 1px solid {{rule}}; margin: 32px 0; }
                small { font: 11px/1.7 system-ui, sans-serif; color: {{muted}}; }
                :focus-visible { outline: 2px solid {{accent}}; outline-offset: 4px; }
                @media (max-width: 520px) { body { padding: 28px 24px 60px; } }
                @media (forced-colors: active) {
                  html, body { background: Canvas; color: CanvasText; }
                  .publication, .byline, blockquote, small { color: CanvasText; }
                  a { color: LinkText; }
                }
              </style>
            </head>
            <body>
              <article>
                <div class="publication">{{WebUtility.HtmlEncode(publication ?? "From your library")}}</div>
                <h1>{{WebUtility.HtmlEncode(story.Title)}}</h1>
                <div class="byline">{{WebUtility.HtmlEncode(story.Author ?? string.Empty)}}<br>{{WebUtility.HtmlEncode(story.PublishedAt.ToLocalTime().ToString("MMMM d, yyyy"))}} · {{StoryPresentation.ReadingMinutes(story.Content)}} min read</div>
                {{cover}}
                {{content}}
              </article>
            </body>
            </html>
            """;
    }

    [GeneratedRegex(@"\Adata:image/(?:png|jpeg|gif|webp);base64,[A-Za-z0-9+/]+={0,2}\z")]
    private static partial Regex SafeImageDataUriRegex();

    public static string Sanitize(string html)
    {
        var sanitized = RemoveDangerousElements(html ?? string.Empty);
        sanitized = EventAttributeRegex().Replace(sanitized, string.Empty);
        sanitized = StyleAttributeRegex().Replace(sanitized, string.Empty);
        sanitized = SourceAttributeRegex().Replace(sanitized, string.Empty);
        sanitized = JavascriptUrlRegex().Replace(sanitized, "$1=\"#\"");
        return MetaRefreshRegex().Replace(sanitized, string.Empty);
    }

    private static string RemoveDangerousElements(string html)
    {
        var output = new StringBuilder(html.Length);
        var position = 0;
        while (position < html.Length)
        {
            var start = html.IndexOf('<', position);
            if (start < 0) { output.Append(html, position, html.Length - position); break; }
            output.Append(html, position, start - position);
            if (html.AsSpan(start).StartsWith("<!--", StringComparison.Ordinal))
            {
                var commentEnd = html.IndexOf("-->", start + 4, StringComparison.Ordinal);
                if (commentEnd < 0) return InertMarkup(html);
                position = commentEnd + 3;
                output.Append(html, start, position - start);
                continue;
            }
            var nameStart = start + 1;
            while (nameStart < html.Length && char.IsWhiteSpace(html[nameStart])) nameStart++;
            var nameEnd = nameStart;
            while (nameEnd < html.Length && char.IsAsciiLetter(html[nameEnd])) nameEnd++;
            if (nameEnd == nameStart)
            {
                output.Append('<');
                position = start + 1;
                continue;
            }
            var end = TagEnd(html, nameEnd);
            if (end < 0) return InertMarkup(html);
            var name = html.AsSpan(nameStart, nameEnd - nameStart);
            var wholeName = nameEnd == html.Length || !char.IsLetterOrDigit(html[nameEnd]) && html[nameEnd] is not '_' and not '-' and not ':';
            if (!wholeName || !IsDangerousElement(name))
            {
                output.Append(html, start, end - start);
                position = end;
                continue;
            }
            var last = end - 2;
            while (last > nameEnd && char.IsWhiteSpace(html[last])) last--;
            if (html[last] == '/' || name.Equals("input", StringComparison.OrdinalIgnoreCase)
                || name.Equals("embed", StringComparison.OrdinalIgnoreCase) || name.Equals("link", StringComparison.OrdinalIgnoreCase))
            {
                position = end;
                continue;
            }
            var closing = "</" + name.ToString();
            var search = end;
            var closed = false;
            while (search < html.Length)
            {
                var candidate = html.IndexOf(closing, search, StringComparison.OrdinalIgnoreCase);
                if (candidate < 0) break;
                search = candidate + closing.Length;
                while (search < html.Length && char.IsWhiteSpace(html[search])) search++;
                if (search < html.Length && html[search] == '>')
                {
                    position = search + 1;
                    closed = true;
                    break;
                }
            }
            // An unterminated element gets one inert fallback, never another scan from each opener.
            if (!closed) return InertMarkup(html);
        }
        return output.ToString();
    }

    private static int TagEnd(string html, int position)
    {
        var quote = '\0';
        for (; position < html.Length; position++)
        {
            var value = html[position];
            if (quote != '\0') { if (value == quote) quote = '\0'; }
            else if (value is '\'' or '"') quote = value;
            else if (value == '>') return position + 1;
            else if (value == '<') return -1;
        }
        return -1;
    }

    private static bool IsDangerousElement(ReadOnlySpan<char> name) =>
        name.Equals("script", StringComparison.OrdinalIgnoreCase) || name.Equals("iframe", StringComparison.OrdinalIgnoreCase)
        || name.Equals("object", StringComparison.OrdinalIgnoreCase) || name.Equals("embed", StringComparison.OrdinalIgnoreCase)
        || name.Equals("form", StringComparison.OrdinalIgnoreCase) || name.Equals("input", StringComparison.OrdinalIgnoreCase)
        || name.Equals("button", StringComparison.OrdinalIgnoreCase) || name.Equals("textarea", StringComparison.OrdinalIgnoreCase)
        || name.Equals("select", StringComparison.OrdinalIgnoreCase) || name.Equals("style", StringComparison.OrdinalIgnoreCase)
        || name.Equals("link", StringComparison.OrdinalIgnoreCase) || name.Equals("svg", StringComparison.OrdinalIgnoreCase)
        || name.Equals("math", StringComparison.OrdinalIgnoreCase);

    private static string InertMarkup(string html) => "<pre>" + WebUtility.HtmlEncode(html) + "</pre>";

    [GeneratedRegex(@"\s+on[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex EventAttributeRegex();

    [GeneratedRegex(@"\s+style\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex StyleAttributeRegex();

    [GeneratedRegex(@"\s+(?:src|srcset|poster|background|data|action|formaction|xlink:href)\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex SourceAttributeRegex();

    [GeneratedRegex(@"(\s(?:href|src)\s*)=\s*(?:""\s*javascript:[^""]*""|'\s*javascript:[^']*'|javascript:[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex JavascriptUrlRegex();

    [GeneratedRegex(@"<meta\b[^>]*http-equiv\s*=\s*(?:""refresh""|'refresh'|refresh)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex MetaRefreshRegex();
}
