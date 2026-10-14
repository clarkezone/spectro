using Spectro.Domain;
using Spectro.Presentation;
using Xunit;

namespace Spectro.ViewModels.Tests;

public sealed class ReaderSafetyTests
{
    [Fact]
    public async Task RepeatedUnclosedDangerousTagsHaveBoundedInertFallback()
    {
        var content = string.Concat(Enumerable.Repeat("<script>", 64000)) + "readable fixture text";
        var html = await ReaderHtmlBuilder.BuildAsync(Story(content), new()).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("readable fixture text", html);
    }

    [Fact]
    public async Task MalformedPreviewTagsDoNotRescanTheEntireRemainder()
    {
        var content = new string('<', 256000);
        var text = await Task.Run(() => StoryPresentation.PlainText(content)).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(content, text);
    }

    [Fact]
    public async Task ValidArticlesLargerThanTwoMiBAreStillRenderedAsHtml()
    {
        var content = string.Concat(Enumerable.Repeat("<p>A complete article paragraph.</p>", 70000));
        var html = await ReaderHtmlBuilder.BuildAsync(Story(content), new()).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(content.Length > 2 * 1024 * 1024);
        Assert.Contains(content, html);
        Assert.DoesNotContain("&lt;p&gt;", html);
    }

    private static Story Story(string content) => new("1:fixture", 1, null, null, "Fixture article", null, null,
        content, "Fixture preview", null, DateTimeOffset.UtcNow, true, false);
}
