using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    public class ImageEx : Image
    {
        public bool IsCacheEnabled
        {
            get => (bool)GetValue(IsCacheEnabledProperty);
            set => SetValue(IsCacheEnabledProperty, value);
        }

        public static readonly DependencyProperty IsCacheEnabledProperty =
            DependencyProperty.Register(nameof(IsCacheEnabled), typeof(bool), typeof(ImageEx), new PropertyMetadata(false));

        public ImageSource PlaceholderSource
        {
            get => (ImageSource)GetValue(PlaceholderSourceProperty);
            set => SetValue(PlaceholderSourceProperty, value);
        }

        public static readonly DependencyProperty PlaceholderSourceProperty =
            DependencyProperty.Register(nameof(PlaceholderSource), typeof(ImageSource), typeof(ImageEx), new PropertyMetadata(null));
    }
}
