using Spectro.Core.Services;
using Spectro.Helpers;
using Spectro.Models.UWP;
using Spectro.Services;
using Spectro.Views;

namespace Spectro.ViewModels
{
    public class ViewModelLocator
    {
        const string REALMNAME = "NewsBlurStore";
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

                    _newsFeedListViewmodel = new NewsFeedListViewModel(new RealmAllNewsFeedsSource());
                    _profileViewModel = new ProfileViewModel();
                }
                return _settingsviewmodel;
            }
        }

        public NavigationRootViewModel NavViewModel {

            get
            {
                if (_navigationRootViewModel == null)
                {
                    _navigationRootViewModel = new NavigationRootViewModel(new NewsBlurService(REALMNAME));
                }
                return _navigationRootViewModel;
            }
        }

        public NewsFeedListViewModel NewsList
        {
            get
            {
                if (_newsFeedListViewmodel == null)
                {
                    _newsFeedListViewmodel = new NewsFeedListViewModel(new RealmAllNewsFeedsSource());
                }
                return _newsFeedListViewmodel;
            }
        }

        public ProfileViewModel ProfileVM => _profileViewModel;

        public void Register<VM, V>() where VM : class
        {
            //SimpleIoc.Default.Register<VM>();
            Singleton<NavigationServiceEx>.Instance.Configure(typeof(VM).FullName, typeof(V));
        }
    }
}
