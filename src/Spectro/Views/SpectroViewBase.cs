using Windows.UI.Xaml;
using Cimbalino.Toolkit.Controls;

namespace Spectro.Views
{
    public abstract class SpectroViewBase : ExtendedPageBase
    {
        protected Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    }
}
