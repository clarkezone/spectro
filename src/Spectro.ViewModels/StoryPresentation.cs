using System.Net;
using System.Text.RegularExpressions;

namespace Spectro.Presentation;

public static partial class StoryPresentation
{
    public static string PlainText(string? html) =>
        Whitespace().Replace(WebUtility.HtmlDecode(Tags().Replace(html ?? "", " ")), " ").Trim();

    public static string DateLabel(DateTimeOffset published, DateTimeOffset now)
    {
        var age = now - published;
        if (age < TimeSpan.FromMinutes(1)) return "Now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours}h";
        return published.ToLocalTime().ToString("MMM d");
    }

    public static int ReadingMinutes(string content) =>
        Math.Max(1, (int)Math.Ceiling(PlainText(content).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / 220d));

    [GeneratedRegex("<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
