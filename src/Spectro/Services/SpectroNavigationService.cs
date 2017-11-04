using Cimbalino.Toolkit.Services;
using Spectro.Core.Interfaces;
using Spectro.Core.Services;
using Spectro.Views;

namespace Spectro.Services
{
    public class SpectroNavigationService : NavigationService, ISpectroNavigationService
    {
        public bool NavigateToSettings(object parameter = null) => Navigate<SettingsPage>(parameter);
        public bool NavigateToProfile(object parameter = null) => Navigate<ProfilePage>(parameter);
        public bool NavigateToNewsFeed(object parameter = null) => Navigate<NewsFeedList>(parameter);
    }
}
