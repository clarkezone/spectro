using Cimbalino.Toolkit.Services;

namespace Spectro.Core.Services
{
    public interface ISpectroNavigationService : INavigationService
    {
        bool NavigateToSettings(object parameter = null);
        bool NavigateToProfile(object parameter = null);
        bool NavigateToNewsFeed(object parameter = null);
    }
}