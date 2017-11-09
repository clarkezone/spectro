using Windows.UI.Xaml;
using Spectro.Core.Services;

namespace Spectro.Extensions
{
    public static class SpectroThemeExtensions
    {
        public static ElementTheme ToElementTheme(this SpectroTheme theme) => (ElementTheme) (int)theme;
    }
}
