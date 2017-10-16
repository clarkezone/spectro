using Spectro.Helpers;
using Spectro.Models.UWP;
using Spectro.Services;
using Spectro.Views;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        SettingsViewModel _settingsviewmodel;
        NavigationRootViewModel _navigationRootViewModel;
        NewsFeedListViewModel _newsFeedListViewmodel;
        ProfileViewModel _profileViewModel;

        public ViewModelLocator()
        {

            Singleton<NavigationServiceEx>.Instance.Configure(typeof(NewsFeedListViewModel).FullName, typeof(NewsFeedList));

            Register<NavigationRootViewModel, NavigationRoot>();
            Register<ProfileViewModel, ProfilePage>();
            Register<SettingsViewModel, SettingsPage>();
        }

        public SettingsViewModel SettingsViewModel
        {
            get
            {
                if (_settingsviewmodel == null)
                {
                    _settingsviewmodel = new SettingsViewModel();
                    _navigationRootViewModel = new NavigationRootViewModel();
                    _newsFeedListViewmodel = new NewsFeedListViewModel(new RealmAllNewsFeedsSource());
                    _profileViewModel = new ProfileViewModel();
                }
                return _settingsviewmodel;
            }
        }

        public NavigationRootViewModel NavViewModel => _navigationRootViewModel;

        public NewsFeedListViewModel NewsList => _newsFeedListViewmodel;

        public ProfileViewModel ProfileVM => _profileViewModel;

        public void Register<VM, V>() where VM : class
        {
            //SimpleIoc.Default.Register<VM>();
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
