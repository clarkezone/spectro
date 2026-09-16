using Spectro.Domain;
using Spectro.Presentation;
using Microsoft.UI.Text;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Spectro.WinUI;

[WinRT.GeneratedBindableCustomProperty(
    [nameof(Title), nameof(UnreadCount), nameof(Glyph), nameof(CountText)],
    [])]
public sealed partial class NavigationItemView
{
    internal NavigationItemView(NavigationItem model)
    {
        Model = model;
        Title = model.Title;
        UnreadCount = model.UnreadCount;
    }

    internal NavigationItem Model { get; }

    public string Title { get; set; }

    public int UnreadCount { get; set; }

    public string CountText => Model.Kind == NavigationItemKind.Feed || UnreadCount > 0 ? UnreadCount.ToString() : string.Empty;

    public FontWeight TitleWeight => UnreadCount > 0 ? FontWeights.SemiBold : FontWeights.Normal;

    public string AccessibleName => $"{Title}, {UnreadCount} downloaded unread stories";

    public string Initial => Title.Length == 0 ? "" : Title[..1].ToUpperInvariant();

    public SolidColorBrush BadgeBrush => new((Model.FeedId.GetValueOrDefault() % 5) switch
    {
        0 => Windows.UI.Color.FromArgb(255, 54, 113, 123),
        1 => Windows.UI.Color.FromArgb(255, 164, 75, 37),
        2 => Windows.UI.Color.FromArgb(255, 74, 95, 154),
        3 => Windows.UI.Color.FromArgb(255, 129, 87, 133),
        _ => Windows.UI.Color.FromArgb(255, 88, 104, 89)
    });

    public string Glyph => Model.Kind switch
    {
        NavigationItemKind.Folder => "\uE8B7",
        NavigationItemKind.Feed => "\uE95A",
        _ => Model.Filter switch
        {
            StoryFilter.Unread => "\uE8F1",
            StoryFilter.Saved => "\uE734",
            _ => "\uE8A5"
        }
    };
}

[WinRT.GeneratedBindableCustomProperty(
    [nameof(Title), nameof(Author), nameof(Source), nameof(Summary), nameof(DateLabel), nameof(StateGlyph)],
    [])]
public sealed partial class StoryItemView
{
    internal StoryItemView(Story model, string? source = null, bool dense = false)
    {
        Model = model;
        Title = model.Title;
        Author = model.Author ?? string.Empty;
        Source = source ?? Author;
        _dense = dense;
        Summary = StoryPresentation.PlainText(
            string.IsNullOrWhiteSpace(model.Summary) || model.Summary == model.Title ? model.Content : model.Summary);
    }

    internal Story Model { get; }

    public string Title { get; set; }

    public string Author { get; set; }

    public string Source { get; }
    private readonly bool _dense;
    internal bool IsDense => _dense;

    public double TitleSize => _dense ? 12 : 14;
    public double ImageSize => _dense ? 56 : 80;
    public int SummaryLines => _dense ? 1 : 2;
    public Thickness PreviewPadding => _dense ? new Thickness(8, 6, 8, 6) : new Thickness(12, 9, 12, 9);

    public string Summary { get; }

    public string DateLabel => StoryPresentation.DateLabel(Model.PublishedAt, DateTimeOffset.Now);

    public FontWeight TitleWeight => Model.IsRead ? FontWeights.Normal : FontWeights.SemiBold;

    public string StateGlyph => Model.IsSaved ? "\uE735" : Model.IsRead ? "" : "\uE915";

    public string ReadAction => Model.IsRead ? "Mark as unread" : "Mark as read";

    public string SaveAction => Model.IsSaved ? "Remove from saved" : "Save story";

    public ImageSource? Thumbnail { get; internal set; }

    public Visibility ThumbnailVisibility => Thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
}
