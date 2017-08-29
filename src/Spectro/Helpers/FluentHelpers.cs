using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Spectro.Helpers
{
    internal static class FluentHelpers
    {
        internal static void SetupTitlebar()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    #region Setup Colors
                    //if (!ThemeSelectorService.IsLightThemeEnabled)
                    //{
                    //titleBar.ButtonForegroundColor = Colors.White;
                    //titleBar.ForegroundColor = Colors.White;
                    //}
                    //else
                    //{
                    titleBar.ButtonForegroundColor = Colors.Black;
                    titleBar.ForegroundColor = Colors.Black;
                    //}

                    titleBar.BackgroundColor = Colors.Black;

                    CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
                    coreTitleBar.ExtendViewIntoTitleBar = false;
                    #endregion

                    coreTitleBar.ExtendViewIntoTitleBar = true;
                    titleBar.ButtonBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    titleBar.ButtonInactiveForegroundColor = Colors.LightGray;
                }
            }
        }
    }
}
