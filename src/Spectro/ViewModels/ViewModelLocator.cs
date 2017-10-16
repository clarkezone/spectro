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

            //SimpleIoc.Default.Register(() => _navigationService);
            //Register<MainViewModel, MainPage>();

            ////TODO: is there a better place to put this as it is a data not a navigation thing?  Eg where the default template initializes it's datasource?
            //SimpleIoc.Default.Register<RealmAllNewsFeedsSource>();

            ////Creating a new NewsFeedListViewModel blows up here.. that needs figuring out before we can use DI successfully
            //SimpleIoc.Default.Register(() => new NewsFeedListViewModel(SimpleIoc.Default.GetInstance<RealmAllNewsFeedsSource>() as IList));
            //SimpleIoc.Default.Register<NewsFeedListViewModel>();
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
