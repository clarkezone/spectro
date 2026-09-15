#if DEBUG
using Spectro.Domain;

namespace Spectro.WinUI;

internal static class DemoLibrary
{
    internal static async Task SeedAsync(IContentRepository repository, CancellationToken cancellationToken)
    {
        string[] publications =
        [
            "Fieldnotes", "The Modern Web", "Design Details", "Windows Journal",
            "Architecture Today", "Offscreen", "The Planetary", "Slow Travel",
            "Signal & Noise", "The Workshop", "Open Source Weekly", "Paper & Type",
            "Everyday Science", "The City Observer", "Weekend Edition"
        ];
        var now = DateTimeOffset.UtcNow;
        var articles = new (int Feed, string Title, string Summary, string Art)[]
        {
            (0, "The quiet return of the personal website", "Away from the algorithm, a new generation is building small places on the web that feel unmistakably their own.", "paper"),
            (1, "A faster web starts with doing less", "The most meaningful performance improvements often come from the code we choose not to ship.", "chip"),
            (2, "Good design leaves room for the ordinary", "On useful objects, thoughtful details, and the beauty of things that simply work.", "paper"),
            (4, "A cabin that follows the contours of the land", "On a wooded hillside, a modest timber structure takes its cues from the landscape around it.", "landscape"),
            (6, "A new perspective on our nearest star", "Watching the Sun means learning to see change on a scale that is difficult to imagine.", "orbit"),
            (3, "Building a desktop that feels like home", "Why familiar patterns and a few carefully considered details still matter in the software we use every day.", "chip"),
            (7, "Take the long way to the coast", "An unhurried journey through small towns, open landscapes, and the places between destinations.", "landscape"),
            (11, "In praise of a well-set paragraph", "A little space, a comfortable line length, and the quiet craft that makes reading feel effortless.", "paper"),
            (8, "The tools we keep coming back to", "Not everything needs an upgrade. Sometimes the best tool is the one you already understand.", "chip"),
            (13, "What makes a neighborhood walkable?", "The small-scale decisions that turn a street from a route into a place worth spending time.", "city"),
            (9, "Making something with your hands", "A visit to the workshop, where patient repetition is still the surest path to something lasting.", "paper"),
            (14, "A few good things for the weekend", "A short reading list, a walk without a destination, and permission to leave a little time unplanned.", "landscape")
        };
        await repository.ReplaceFeedCatalogAsync(
            publications.Select((title, i) => new Feed(200 + i, title, $"https://example.test/{i}", null,
                articles.Count(a => a.Feed == i), now, true)).ToArray(),
            [new Folder("design", "Design & technology", 0), new Folder("world", "The world outside", 1)],
            publications.Select((_, i) => new FolderFeed(i < 6 ? "design" : "world", 200 + i, i)).ToArray(),
            cancellationToken);
        for (var i = 0; i < articles.Length; i++)
        {
            var article = articles[i];
            var body = i == 0 ? PersonalWebsiteEssay : $"""
                <p>{article.Summary}</p>
                <p>There is a particular pleasure in paying attention to the things we usually pass by. Step away from the rush for a moment, and the details begin to come into focus: a useful material, a considered decision, an idea that has been given enough time to develop.</p>
                <h2>A different way of looking</h2>
                <p>The interesting question is not always what comes next. Sometimes it is what is already here, waiting to be noticed. The best work grows from that kind of attention, from understanding a place or a problem before reaching for a solution.</p>
                <blockquote>Make room for the details. They are often where the whole story begins.</blockquote>
                <p>That is a useful thought to carry into the week. Not a prescription, just an invitation to slow down, look closely, and see what becomes possible.</p>
                <hr><p><small>Original sample article for the Spectro demonstration library.</small></p>
                """;
            await repository.UpsertStoryAsync(new Story(
                $"{200 + article.Feed}:sample-{i}", 200 + article.Feed, null, null,
                article.Title, $"{publications[article.Feed]} editors", "https://newsblur.com",
                body, article.Summary, $"ms-appx:///Assets/Demo/{article.Art}.png",
                now.AddMinutes(-24 - i * 43), i >= 10, i is 0 or 4 or 7), cancellationToken);
        }
    }

    private const string PersonalWebsiteEssay = """
        <p>There was a time when a personal website felt less like a destination and more like a room. You could arrange it however you liked. Put a few links on the wall. Leave something unfinished. Invite people in.</p>
        <p>That feeling is finding its way back.</p>
        <p>Across the web, people are making small, independent spaces again: reading journals, collections of photographs, notes about things they are learning. Not because a platform suggested it, or because there is a strategy to optimize, but because having a place of your own still means something.</p>
        <h2>A place, not a platform</h2>
        <p>The appeal is not nostalgia. It is ownership of the experience. A personal site can be slow, particular, and a little unusual. It does not need to explain itself to everyone. It can change direction when its author does.</p>
        <blockquote>The best corners of the internet tend to feel like someone has been there before you, making a little room.</blockquote>
        <p>There is something generous about that. A page of carefully chosen links says: these things mattered to me, and perhaps they will matter to you. A short account of an ordinary afternoon can become a small window into another life.</p>
        <h2>Start smaller than you think</h2>
        <p>You do not need a publishing schedule. You do not even need a finished design. Start with a page, a paragraph, and a way for someone to find the next thing. Add an RSS feed so people can return on their own terms.</p>
        <p>The web was built for that kind of connection: quiet, direct, and open. It still works.</p>
        <hr><p><small>Original sample article for the Spectro demonstration library.</small></p>
        """;
}
#endif
