using Windows.UI.Xaml;

namespace Spectro.Views
{
    public abstract class SpectroViewBase : ExtendedPageBase
    {
        protected Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

        protected Visibility InverseBooleanToVisibility(bool value) => !value ? Visibility.Visible : Visibility.Collapsed;
    }
}
